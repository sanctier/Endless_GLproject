using UnityEngine;
using System.Collections;
// using TMPro; (using built-in TextMesh for runtime safety)

public class BossBandit : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 200;
    public int goldOnDeath = 50;
    public float moveSpeed = 3.5f;
    public float attackRange = 1.6f;
    public float contactDamage = 25f;
    public float attackCooldown = 1.5f;

    [Header("Combat")]
    public LayerMask damageLayer;

    [Header("Death Effects")]
    public GameObject bloodSplatterPrefab;
    public Vector2 bloodSplatterOffset = new Vector2(0, 0.5f);

    [Header("Audio")]
    public AudioClip attackClip;
    [Range(0f,1f)] public float attackVolume = 1f;
    public AudioClip deathClip;
    [Range(0f,1f)] public float deathVolume = 1f;

    private AudioSource audioSource;
    private float lastAttackPlayTime = -Mathf.Infinity;

    private int currentHealth;
    private bool isDead = false;
    private float attackTimer = 0f;
    private bool playerInRange = false;

    private Animator animator;
    private Rigidbody2D rb;
    private Transform player;
    private bool facingRight = true;
    private Collider2D myCollider;

    [Header("UI")]
    public Vector3 hpTextOffset = new Vector3(0f, 1.2f, 0f);
    public float hpTextSize = 3f;
    private TextMesh hpText;

    [Header("Attack Settings")]
    public Transform attackPoint; // optional point to check in front of the boss
    public Vector2 attackOffset = new Vector2(0.9f, 0f); // used if attackPoint is not assigned
    public LayerMask targetMask = ~0; // who can be hit (default: everything)
    public float attackHitDelay = 0.35f; // fallback delay to call DealDamageToPlayer after trigger
    // internal: coroutine handle for scheduled (timed) hit
    private Coroutine scheduledHitCoroutine;
    // Sequence id incremented each time we start an attack; used to dedupe damage calls
    private int attackSequence = 0;
    // Track which attackSequence has already applied damage so we don't double-hit
    private int lastDamageForSequence = -1;
    // Desired normalized time (0..1) inside the attack clip when the hit should apply
    public float hitNormalizedTime = 0.45f;
    // How long to wait while searching for the attack clip before falling back
    public float attackStateTimeout = 1.2f;
    // How long the boss is stunned when taking damage (prevents immediate counter-attack)
    public float stunDuration = 0.6f;
    private float stunTimer = 0f;
    // second phase flag
    private bool secondPhase = false;

    [Header("Phase 2 Settings")]
    [Tooltip("How long (seconds) the Recover transition lasts and during which the boss won't move.")]
    public float recoverDuration = 2f;
    [Tooltip("Temporary animator.speed while Recover plays. 1 = normal speed, <1 = slower.")]
    public float recoverAnimSpeed = 0.6f;
    // (phase-2 dash behaviour removed; phase 2 uses increased moveSpeed)
    // true while playing Recover so movement is suppressed
    private bool isRecovering = false;
    // position to lock to during Recover so animations can't move the boss
    private Vector3 recoverLockedPosition;
    
    [Header("Boss Music")]
    [Tooltip("Music clip to play while this boss is active (will stop background music).")]
    public AudioClip bossMusicClip;
    [Range(0f,1f)] public float bossMusicVolume = 1f;
    // runtime-only AudioSource for boss music so SFX source isn't repurposed
    private AudioSource bossMusicSource;
    [Tooltip("Music clip to play during the short Recover transition (non-looping).")]
    public AudioClip recoverMusicClip;
    [Range(0f,1f)] public float recoverMusicVolume = 1f;
    // runtime-only temporary source for the recover audio
    private AudioSource recoverMusicSource;

    [Tooltip("Looping music clip to play after Recover completes (phase 2 theme).")]
    public AudioClip secondPhaseMusicClip;
    [Range(0f,1f)] public float secondPhaseMusicVolume = 1f;
    // Optional: names used by the animator for attack triggers. Kept flexible to match animator setup.
    private readonly string[] attackTriggerCandidates = new string[] { "Attack1", "Attack2", "Attack" };

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        // When the boss spawns, stop global background music and play boss music (if assigned).
        if (BGMManager.Instance != null)
        {
            try { BGMManager.Instance.Stop(); } catch { }
        }

        if (bossMusicClip != null)
        {
            bossMusicSource = gameObject.AddComponent<AudioSource>();
            bossMusicSource.playOnAwake = false;
            bossMusicSource.loop = true;
            bossMusicSource.spatialBlend = 0f; // treat as 2D music
            bossMusicSource.volume = Mathf.Clamp01(bossMusicVolume);
            bossMusicSource.clip = bossMusicClip;
            bossMusicSource.Play();
        }

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        // Fallback to the PlayerController singleton if tagging wasn't set up
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        // Ensure damageLayer default to 11 if empty
        if (damageLayer == 0)
            damageLayer = 1 << 11;

        // Initialize facing from the sprite's current scale so we match art orientation
        facingRight = transform.localScale.x > 0f;

        // Ensure we face the player on spawn
        if (player != null)
        {
            if (player.position.x < transform.position.x && facingRight) Flip();
            else if (player.position.x > transform.position.x && !facingRight) Flip();
        }

        // Create a world-space HP text above the boss using legacy TextMesh
        var go = new GameObject("HPText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = hpTextOffset;
        hpText = go.AddComponent<TextMesh>();
        if (hpText != null)
        {
            hpText.text = currentHealth.ToString();
            // characterSize scales the mesh — tune with hpTextSize
            hpText.characterSize = 0.12f * Mathf.Max(0.1f, hpTextSize);
            hpText.anchor = TextAnchor.MiddleCenter;
            hpText.alignment = TextAlignment.Center;
            hpText.color = Color.white;
            // Use a reasonable font size and disable receiving shadows
            var renderer = hpText.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hpText.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (isDead) return;

        // Update HP text each frame so it follows and reflects current health/color
        UpdateHpText();

        // If stunned, skip movement and attacking until stunTimer expires.
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            // keep animator idle while stunned
            animator?.SetFloat("Speed", 0f);
            return;
        }

        bool isRunning = false;

        if (!playerInRange && player != null && !isRecovering)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            if (rb != null)
                rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
            isRunning = true;
        }

        // Use the animator's `Speed` float (per inspector) so the controller switches
        // between Idle/Run states based on movement speed. Use 0/1 to avoid flicker.
        animator?.SetFloat("Speed", isRunning ? 1f : 0f);

        if (playerInRange && attackTimer <= 0f)
        {
            // Use the boss animator's `Attack` trigger (matches your animator setup)
            if (animator != null)
                animator.SetTrigger("Attack");

            if (attackClip != null && audioSource != null)
            {
                float now = Time.unscaledTime;
                float dur = Mathf.Max(0.05f, attackClip.length);
                if (now - lastAttackPlayTime >= dur)
                {
                    audioSource.PlayOneShot(attackClip, attackVolume);
                    lastAttackPlayTime = now;
                }
            }

            // Schedule a timed hit that waits for the attack animation clip
            attackSequence++;
            if (scheduledHitCoroutine != null) StopCoroutine(scheduledHitCoroutine);
            scheduledHitCoroutine = StartCoroutine(AttackTimingCoroutine(attackSequence));

            attackTimer = attackCooldown;
        }

        attackTimer -= Time.deltaTime;

        // no dash behaviour in phase 2; boss will use increased moveSpeed instead

        // Flip to face player
        if (player != null)
        {
            if (player.position.x < transform.position.x && facingRight)
                Flip();
            else if (player.position.x > transform.position.x && !facingRight)
                Flip();
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;
        // Boss animator uses 'Hurt'
        animator?.SetTrigger("Hurt");
        // apply stun so boss can't immediately counter-attack
        stunTimer = Mathf.Max(stunTimer, stunDuration);
        // ensure attack cooldown at least matches stun so attack doesn't fire right after stun
        attackTimer = Mathf.Max(attackTimer, stunDuration);
        // cancel any scheduled hit while stunned
        if (scheduledHitCoroutine != null)
        {
            try { StopCoroutine(scheduledHitCoroutine); } catch { }
            scheduledHitCoroutine = null;
        }
        
        // If this hit drops the boss below half-health, enter second phase (once)
        if (!secondPhase && currentHealth > 0 && currentHealth <= (maxHealth / 2))
        {
            secondPhase = true;
            // entering second phase; no dash behaviour — phase 2 uses increased moveSpeed
            if (scheduledHitCoroutine != null)
            {
                try { StopCoroutine(scheduledHitCoroutine); } catch { }
                scheduledHitCoroutine = null;
            }
            StartCoroutine(EnterSecondPhase());
        }
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // update text immediately on damage
            UpdateHpText();
        }
    }

    void Die()
    {
        isDead = true;
        // Boss animator uses 'Death'
        animator?.SetTrigger("Death");

        // Ensure HP reads 0 in the UI immediately when dying
        currentHealth = 0;
        UpdateHpText();

        // cancel any scheduled fallback hit
        if (scheduledHitCoroutine != null)
        {
            try { StopCoroutine(scheduledHitCoroutine); } catch { }
            scheduledHitCoroutine = null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (myCollider != null) myCollider.enabled = false;

        SpawnBloodSplatter();

        if (deathClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathClip, deathVolume);
        }

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCurrency(goldOnDeath);
        if (ShopManager.Instance != null)
        {
            try { ShopManager.Instance.NotifyBossDefeated(); } catch { }
        }
        if (WaveManager.Instance != null)
            WaveManager.Instance.EnemyDefeated();

        // stop boss music and resume global BGM if available
        if (bossMusicSource != null)
        {
            try { bossMusicSource.Stop(); } catch { }
            Destroy(bossMusicSource);
            bossMusicSource = null;
        }

        if (BGMManager.Instance != null)
        {
            try { BGMManager.Instance.Play(); } catch { }
        }

        Destroy(gameObject, 4f);
    }

    void SpawnBloodSplatter()
    {
        if (bloodSplatterPrefab != null)
        {
            Vector3 spawnPosition = transform.position + (Vector3)bloodSplatterOffset;
            GameObject bloodSplatter = Instantiate(bloodSplatterPrefab, spawnPosition, Quaternion.identity);
            bloodSplatter.transform.Rotate(0, 0, Random.Range(0, 360));
            if (!facingRight)
            {
                Vector3 s = bloodSplatter.transform.localScale;
                s.x *= -1;
                bloodSplatter.transform.localScale = s;
            }
            Destroy(bloodSplatter, 2f);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        // Set playerInRange if the collider belongs to the PlayerController
        if (collision.GetComponent<PlayerController>() != null)
        {
            playerInRange = true;
            // ensure we have a reference to the player transform if tag wasn't set
            if (player == null) player = collision.transform;
        }

        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            int damage = 10;
            TakeDamage(damage);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (isDead) return;
        if (collision.GetComponent<PlayerController>() != null)
        {
            playerInRange = false;
        }
    }

    // Some animator setups call an event named `Attack` — provide a matching method
    // so animation events that call `Attack` won't throw a missing method error.
    public void Attack()
    {
        // Forward to the damage method so the animation event can be generic
        DealDamageToPlayer();
    }

    // Utility: check if an animator parameter exists (avoids silent animator mismatches)
    private bool HasAnimatorParameter(string paramName)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.name == paramName) return true;
        }
        return false;
    }

    // Animation event (set in Attack at hit frame)
    // This tries multiple methods to reliably apply contact damage:
    // 1) If we have a `player` reference and they're within `attackRange`, damage them.
    // 2) Otherwise do an OverlapCircleAll and damage the first collider that has a `PlayerController`.
    // 3) This avoids depending on tags and also works if triggers were missed.
    public void DealDamageToPlayer()
    {
        if (isDead) return;

        // Prevent applying damage more than once per attackSequence
        if (lastDamageForSequence == attackSequence)
        {
            return;
        }

        // don't apply damage while stunned or during second-phase transition
        if (stunTimer > 0f) return;

        // compute center of attack check: prefer `attackPoint` if assigned, otherwise use offset in front
        Vector2 center;
        if (attackPoint != null)
            center = attackPoint.position;
        else
            center = (Vector2)transform.position + new Vector2(attackOffset.x * (facingRight ? 1f : -1f), attackOffset.y);

        

        // 1) Direct player reference distance check (using center)
        if (player != null)
        {
            float dist = Vector2.Distance(player.position, center);
            if (dist <= attackRange)
            {
                var pcDirect = player.GetComponent<PlayerController>();
                if (pcDirect != null)
                {
                    pcDirect.TakeDamage(contactDamage);
                    lastDamageForSequence = attackSequence;
                }
                return;
            }
        }

        // 2) Fallback overlap search for PlayerController component but limit by targetMask for safety
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, attackRange, targetMask);
        float eps = 0.05f;
        foreach (var c in hits)
        {
            if (c == null) continue;
            var pc = c.GetComponent<PlayerController>() ?? c.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                float pdist = Vector2.Distance(pc.transform.position, center);
                if (pdist <= attackRange + eps)
                {
                    pc.TakeDamage(contactDamage);
                    lastDamageForSequence = attackSequence;
                    return;
                }
                else
                {
                }
            }
        }
    }

    // Coroutine that waits for the attack animation clip and triggers the hit
    // at the configured normalized time. If it can't find an attack clip within
    // `attackStateTimeout`, it falls back to a short delay before applying damage.
    private IEnumerator AttackTimingCoroutine(int seq)
    {
        float elapsed = 0f;
        bool foundAttackClip = false;

        while (elapsed < attackStateTimeout)
        {
            if (isDead) yield break;
            if (animator != null)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                foreach (var ci in clips)
                {
                    if (ci.clip != null && ci.clip.name.ToLower().Contains("attack"))
                    {
                        foundAttackClip = true;
                        break;
                    }
                }
                if (foundAttackClip) break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead) yield break;

        if (seq != attackSequence)
        {
            scheduledHitCoroutine = null;
            yield break;
        }

        if (foundAttackClip && animator != null)
        {
            // Wait until the current attack clip normalized time reaches hitNormalizedTime
            while (true)
            {
                if (isDead) yield break;
                if (seq != attackSequence) break; // new attack started

                var state = animator.GetCurrentAnimatorStateInfo(0);
                float norm = state.normalizedTime % 1f;
                if (norm >= hitNormalizedTime) break;
                yield return null;
            }

            if (seq == attackSequence)
            {
                DealDamageToPlayer();
            }

            scheduledHitCoroutine = null;
            yield break;
        }

        // Fallback if no clear attack clip detected
        yield return new WaitForSeconds(attackHitDelay);
        if (isDead) yield break;
        if (seq == attackSequence)
        {
            DealDamageToPlayer();
        }

        scheduledHitCoroutine = null;
    }

    // Dash behaviour removed; phase 2 uses a higher moveSpeed instead.

    // Enter the second phase: play Death animation for 2 seconds, then Recover,
    // then buff stats (damage and move speed).
    private IEnumerator EnterSecondPhase()
    {
        // Use configurable recoverDuration to block movement/attacks while Recover plays
        float dur = Mathf.Max(0.01f, recoverDuration);
        stunTimer = Mathf.Max(stunTimer, dur);
        attackTimer = Mathf.Max(attackTimer, dur);

        // Play Death animation as the 'stagger' visual (kept for visual effect)
        animator?.SetTrigger("Death");
        // Immediately start Recover and slow the animator for the duration so the Recover
        // animation appears slower. Movement is blocked by stunTimer above.

        // mark recovering so other behaviors (dash) don't run
        isRecovering = true;

        // ensure rigidbody not moving
        if (rb != null)
        {
            try { rb.linearVelocity = Vector2.zero; } catch { }
        }

        // disable root motion during recover so the animation cannot reposition the boss
        if (animator != null)
        {
            try { animator.applyRootMotion = false; } catch { }
        }

        // stop any currently-playing boss music for the recover moment
        if (bossMusicSource != null && bossMusicSource.isPlaying)
        {
            try { bossMusicSource.Stop(); } catch { }
        }

        // play recover-phase audio (one-shot) if assigned
        if (recoverMusicClip != null)
        {
            recoverMusicSource = gameObject.AddComponent<AudioSource>();
            recoverMusicSource.playOnAwake = false;
            recoverMusicSource.loop = false;
            recoverMusicSource.spatialBlend = 0f;
            recoverMusicSource.volume = Mathf.Clamp01(recoverMusicVolume);
            recoverMusicSource.clip = recoverMusicClip;
            recoverMusicSource.Play();
        }

        if (animator != null)
        {
            float originalSpeed = animator.speed;
            animator.speed = recoverAnimSpeed;
            animator.SetTrigger("Recover");

            // lock position every frame so the recover animation cannot move the boss
            recoverLockedPosition = transform.position;
            float t = 0f;
            while (t < dur)
            {
                if (isDead) break;
                if (rb != null)
                {
                    rb.MovePosition((Vector2)recoverLockedPosition);
                    rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    transform.position = recoverLockedPosition;
                }

                t += Time.deltaTime;
                yield return null;
            }

            // restore animator speed
            animator.speed = originalSpeed;
        }
        else
        {
            // fallback wait while keeping position locked
            recoverLockedPosition = transform.position;
            float t = 0f;
            while (t < dur)
            {
                if (isDead) break;
                if (rb != null) { rb.MovePosition((Vector2)recoverLockedPosition); rb.linearVelocity = Vector2.zero; }
                else transform.position = recoverLockedPosition;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // stop and cleanup recover audio
        if (recoverMusicSource != null)
        {
            try { recoverMusicSource.Stop(); } catch { }
            Destroy(recoverMusicSource);
            recoverMusicSource = null;
        }

        // keep root motion disabled to prevent animation repositioning
        if (animator != null)
        {
            try { animator.applyRootMotion = false; } catch { }
        }

        // no longer recovering
        isRecovering = false;

        // start second-phase looping music if assigned
        if (secondPhaseMusicClip != null)
        {
            if (bossMusicSource == null)
                bossMusicSource = gameObject.AddComponent<AudioSource>();
            bossMusicSource.playOnAwake = false;
            bossMusicSource.loop = true;
            bossMusicSource.spatialBlend = 0f;
            bossMusicSource.clip = secondPhaseMusicClip;
            bossMusicSource.volume = Mathf.Clamp01(secondPhaseMusicVolume);
            bossMusicSource.Play();
        }
        else
        {
            // if no phase-2 music provided, resume previous bossMusicClip if present
            if (bossMusicClip != null)
            {
                if (bossMusicSource == null) bossMusicSource = gameObject.AddComponent<AudioSource>();
                bossMusicSource.playOnAwake = false;
                bossMusicSource.loop = true;
                bossMusicSource.spatialBlend = 0f;
                bossMusicSource.clip = bossMusicClip;
                bossMusicSource.volume = Mathf.Clamp01(bossMusicVolume);
                bossMusicSource.Play();
            }
        }

        // Apply phase-two stat changes after Recover completes
        contactDamage = 11f;
        moveSpeed = 20f;

        // no dash behaviour — phase 2 uses increased moveSpeed

        yield break;
    }

    // Visualize the attack range in the editor for tuning
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red * 0.6f;
        Vector2 center = (attackPoint != null) ? (Vector2)attackPoint.position : (Vector2)transform.position + new Vector2(attackOffset.x * (transform.localScale.x > 0f ? 1f : -1f), attackOffset.y);
        Gizmos.DrawWireSphere(center, attackRange);
    }

    private void UpdateHpText()
    {
        if (hpText == null) return;
        // ensure non-negative display
        int disp = Mathf.Max(0, currentHealth);
        hpText.text = disp.ToString();
        // color red when below half
        if (currentHealth <= (maxHealth / 2))
            hpText.color = Color.red;
        else
            hpText.color = Color.white;

        // keep text at the configured offset
        hpText.transform.localPosition = hpTextOffset;

        // face the main camera if present, but keep text upright.
        if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            Vector3 lookDir = camPos - hpText.transform.position; // from text to camera
            lookDir.y = 0f; // keep text upright
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(lookDir);
                hpText.transform.rotation = rot;

                // Ensure the text is not mirrored: if the text's forward faces away from the camera,
                // flip its X scale so it reads correctly.
                Vector3 s = hpText.transform.localScale;
                s.x = Mathf.Abs(s.x);
                hpText.transform.localScale = s;

                Vector3 toCamera = (camPos - hpText.transform.position).normalized;
                float forwardDot = Vector3.Dot(hpText.transform.forward, toCamera);
                if (forwardDot < 0f)
                {
                    s.x = -s.x;
                    hpText.transform.localScale = s;
                }
            }
        }
    }
}
