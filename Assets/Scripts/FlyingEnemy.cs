using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FlyingEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float detectionRange = 6f; // how far it can "see" the player
    public float stopDistance = 0.2f; // distance considered "reached" the last player location

    [Header("Attack")]
    public float attackCooldown = 1.5f;
    public float attackRange = 1f; // immediate attack if player gets this close

    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;
    [Header("Combat")]
    public LayerMask damageLayer; // set this to the same layer(s) player attack colliders use

    // last known player location that the flyer will move to and attack
    // (flyer no longer chases player) use trigger contact to start attacks
    private bool playerInContact = false;
    private float attackTimer = 0f;
    private bool isBusy = false;
    public float attackFallbackDuration = 1.0f; // safety fallback to clear attack
    [Header("Stun")]
    public float stunDuration = 1.5f; // how long to be stunned instead of dying
    private bool isStunned = false;
    // track the running stun coroutine so we can stop/restart it reliably
    private Coroutine stunCoroutine = null;

    [Header("Wander")]
    public Vector2 minBounds = new Vector2(-10f, -5f);
    public Vector2 maxBounds = new Vector2(10f, 5f);
    public float wanderIntervalMin = 1f;
    public float wanderIntervalMax = 3f;
    private Vector2 wanderTarget;
    private float wanderTimer = 0f;
    private Coroutine fallbackCoroutine = null;

    private bool facingRight = true;
    [Header("Animator")]
    // Make these configurable so they match whatever the Animator actually uses
    public string flightStateName = "Flight";
    public string[] attackStateNames = new string[] { "Attack1", "Attack2" };

    // cached hashes
    private int flightHash;
    private int[] attackHashes;
    [Header("VFX")]
    // optional prefab to show above the enemy while stunned (TextMeshPro or TextMesh prefab).
    // If left null a simple TextMesh will be created at runtime.
    public GameObject stunnedLabelPrefab;
    public Vector3 stunLabelOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("Optional anchor Transform. If assigned the stunned label will appear at this object's position instead of using the default offset on the enemy.")]
    public Transform stunLabelAnchor;
    private GameObject stunnedLabelInstance;
    private RectTransform stunnedLabelRect = null;
    private Canvas stunnedLabelCanvas = null;
    private bool stunnedLabelIsUI = false;
    // label flash settings
    [Tooltip("Full flash cycles per second for the stunned label (on->off->on).")]
    public float stunFlashRate = 2f;
    private Coroutine stunnedLabelFlashCoroutine = null;
    [Header("Damage")]
    public float contactDamage = 10f; // damage dealt to player on contact
    public float contactDamageCooldown = 1f; // seconds between applying contact damage
    private float lastContactDamageTime = -999f;

    [Header("Audio")]
    public AudioClip batBiteClip;
    [Range(0f,1f)] public float batBiteVolume = 1f;
    private AudioSource audioSource;
    public AudioClip batStunnedClip;
    [Range(0f,1f)] public float batStunnedVolume = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        wanderTarget = rb != null ? rb.position : (Vector2)transform.position;
        wanderTimer = Random.Range(wanderIntervalMin, wanderIntervalMax);

        // warn if required components are missing so the user can fix prefabs
        if (rb == null)
            Debug.LogWarning(name + ": Rigidbody2D not found on FlyingEnemy. Movement will fall back to Transform.");
        if (animator == null)
            Debug.LogWarning(name + ": Animator not found on FlyingEnemy. Animation control will be skipped.");

        // compute animator state hashes for faster and more robust comparisons
        flightHash = Animator.StringToHash(flightStateName);
        attackHashes = new int[attackStateNames.Length];
        for (int i = 0; i < attackStateNames.Length; i++)
            attackHashes[i] = Animator.StringToHash(attackStateNames[i]);

        // ensure an AudioSource exists for playing attack sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // spatialized 3D sound
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Player contact used for the flyer to attack the player
        if (collision.CompareTag("Player"))
        {
            // Deal contact damage if not stunned and cooldown elapsed
            if (!isStunned && Time.time - lastContactDamageTime >= contactDamageCooldown)
            {
                var pc = collision.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(contactDamage);
                    lastContactDamageTime = Time.time;
                }
                else if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.TakeDamage(contactDamage);
                    lastContactDamageTime = Time.time;
                }
            }

            playerInContact = true;
            // attack immediately on touch
            if (attackTimer <= 0f && !isBusy)
                TryAttack();

            // If the player body hits this trigger while in their Attack animation, treat it as a hit.
            if (IsColliderPlayerAttacking(collision))
            {
                ApplyStun();
                return;
            }
        }

        // Accept hits from configured damage layer (matches GoblinEnemy behavior)
        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }

        // Also respond to Slash tagged objects (e.g., after-image / slash prefabs)
        if (collision.CompareTag("Slash"))
        {
            ApplyStun();
            return;
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInContact = false;
        }
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        // keep contact state accurate while player stays in trigger
        if (collision.CompareTag("Player"))
        {
            playerInContact = true;

            // attempt repeated attacks while player remains inside trigger
            if (attackTimer <= 0f && !isBusy)
                TryAttack();

            // detect player attacking while inside the trigger
            if (IsColliderPlayerAttacking(collision))
            {
                ApplyStun();
                return;
            }
        }

        // Accept hits from configured damage layer while staying in the trigger
        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }

        // Also respond to Slash tagged objects while staying in trigger
        if (collision.CompareTag("Slash"))
        {
            ApplyStun();
            return;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Accept hits from configured damage layer
        if (((1 << collision.collider.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }

        // If player's body collides and their animator is in Attack state, treat it as a hit
        if (collision.collider.CompareTag("Player"))
        {
            // Deal contact damage if not stunned and cooldown elapsed
            if (!isStunned && Time.time - lastContactDamageTime >= contactDamageCooldown)
            {
                var pc = collision.collider.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(contactDamage);
                    lastContactDamageTime = Time.time;
                }
                else if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.TakeDamage(contactDamage);
                    lastContactDamageTime = Time.time;
                }
            }

            if (IsColliderPlayerAttacking(collision.collider))
                ApplyStun();

            // If hit by a Slash object (physics collision), stun as well
            if (collision.collider.CompareTag("Slash"))
                ApplyStun();
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Accept hits from configured damage layer while colliding
        if (((1 << collision.collider.gameObject.layer) & damageLayer) != 0)
        {
            ApplyStun();
            return;
        }

        // If player's body is colliding and their animator is in Attack state, treat it as a hit
        if (collision.collider.CompareTag("Player"))
        {
            if (IsColliderPlayerAttacking(collision.collider))
                ApplyStun();
            // If staying in contact with a Slash object, stun as well
            if (collision.collider.CompareTag("Slash"))
                ApplyStun();
        }
    }

    bool IsColliderPlayerAttacking(Collider2D col)
    {
        // Look for a PlayerController or Animator on the collider or its parents
        PlayerController pc = col.GetComponent<PlayerController>();
        Animator pAnim = null;
        if (pc != null)
            pAnim = pc.GetComponent<Animator>();
        else
        {
            pAnim = col.GetComponent<Animator>();
            if (pAnim == null && col.transform.parent != null)
                pAnim = col.transform.parent.GetComponent<Animator>();
        }

        if (pAnim != null)
        {
            var state = pAnim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Attack") || state.IsName("Attack1") || state.IsName("Attack2"))
                return true;
        }

        return false;
    }

    void Update()
    {
        if (player == null) return;

        // No chasing behaviour: attack only when player touches (trigger). We still allow
        // immediate attack if player is inside the contact trigger (playerInContact).

        // movement and behavior
        bool isMoving = false;

        // Flyer wanders when not busy
        if (!isBusy)
        {
            // use rb.position if available, otherwise transform.position as fallback
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;

            // wander behavior when no target
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f || Vector2.Distance(currentPos, wanderTarget) <= stopDistance)
            {
                // pick a new wander target inside bounds
                wanderTarget = new Vector2(
                    Random.Range(minBounds.x, maxBounds.x),
                    Random.Range(minBounds.y, maxBounds.y)
                );
                wanderTimer = Random.Range(wanderIntervalMin, wanderIntervalMax);
            }

            Vector2 toWander = (wanderTarget - currentPos);
            if (toWander.magnitude > stopDistance)
            {
                Vector2 dir2 = toWander.normalized;
                if (rb != null)
                {
                    rb.MovePosition(currentPos + dir2 * moveSpeed * Time.deltaTime);
                }
                else
                {
                    // fallback to transform-based movement so missing Rigidbody doesn't break the enemy
                    transform.position = (Vector3)(currentPos + dir2 * moveSpeed * Time.deltaTime);
                }
                isMoving = true;

                if (dir2.x < 0 && facingRight) Flip();
                else if (dir2.x > 0 && !facingRight) Flip();
            }
        }

        // animator
        if (animator != null) animator.SetBool("isFlying", isMoving);

        // cooldowns
        attackTimer -= Time.deltaTime;

        // If currently marked busy because of an attack, try to auto-clear busy when the animator
        // has left the attack states. This helps when animation events are missing and prevents
        // the enemy from getting stuck. HOWEVER, if the enemy is stunned, we must NOT auto-clear
        // busy — stun should keep the enemy idle until StunRoutine recovers it.
        if (isBusy && animator != null)
        {
            if (isStunned)
            {
                // make sure physics motion stops immediately while stunned
                if (rb != null)
                {
                    try { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; } catch { }
                }
                // keep busy until stun routine clears it
            }
            else
            {
                var s = animator.GetCurrentAnimatorStateInfo(0);
                int currentHash = s.shortNameHash;

                bool inAttackState = false;
                for (int i = 0; i < attackHashes.Length; i++)
                {
                    if (currentHash == attackHashes[i])
                    {
                        inAttackState = true;
                        break;
                    }
                }

                // If animator has left the defined attack states, clear busy.
                if (!inAttackState)
                {
                    isBusy = false;
                    StopFallback();
                }

                // Extra safety: if the player is far away (e.g. left scene) force recovery
                if (player != null)
                {
                    float d = Vector2.Distance(transform.position, player.position);
                    if (d > detectionRange * 2f)
                    {
                        ForceRecover();
                    }
                }
            }
        }
    }

    void TryAttack()
    {
        if (attackTimer > 0f) return;
        // start attack: mark busy so movement pauses
        isBusy = true;
        if (animator != null) animator.SetBool("isFlying", false);

        // pick random attack trigger like other enemies
        int attackType = Random.Range(0, 2);
        if (animator != null)
        {
            if (attackType == 0) animator.SetTrigger("Attack1");
            else animator.SetTrigger("Attack2");
        }

        // play bite audio on attack
        if (batBiteClip != null)
        {
            PlayOneShotExclusive(batBiteClip, batBiteVolume);
        }

        // safety fallback in case animation event EndAttack doesn't fire
        StopFallback();
        fallbackCoroutine = StartCoroutine(FallbackClearBusy(attackFallbackDuration));

        attackTimer = attackCooldown;
    }

    // Make the flyer unkillable: when hit, don't die — get stunned for stunDuration seconds
    // Provide a few common method signatures so various player attack implementations will call one of them.
    public void TakeDamage(float amount)
    {
        ApplyStun();
    }

    public void TakeDamage(int amount)
    {
        ApplyStun();
    }

    // For compatibility with SendMessage "TakeHit" calls
    public void TakeHit()
    {
        ApplyStun();
    }

    void ApplyStun()
    {
        Debug.Log(name + ": ApplyStun called");
        // if already stunned, refresh duration
        if (isStunned)
        {
            // restart stun coroutine by stopping the tracked coroutine and starting a fresh one
            if (stunCoroutine != null)
            {
                try { StopCoroutine(stunCoroutine); } catch { }
                stunCoroutine = null;
            }
            ShowStunLabel();
            // play stunned audio on refresh (exclusive)
            if (batStunnedClip != null)
                PlayOneShotExclusive(batStunnedClip, batStunnedVolume);
            stunCoroutine = StartCoroutine(StunRoutine());
            return;
        }

        // clear any pending attack/fallback and mark busy so it won't move or attack
        StopFallback();
        isBusy = true;
        isStunned = true;

        if (animator != null)
        {
            // Clear any attack triggers so we don't stay in an attacking animation
            for (int i = 0; i < attackStateNames.Length; i++)
                animator.ResetTrigger(attackStateNames[i]);
            animator.ResetTrigger("TakeHit");

            // Force the animator to the flight state and freeze it so the bat appears
            // immobilised on the flight pose instead of showing the current attack.
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
            // Do NOT freeze the animator; we want the Flight animation to keep playing
        }

        ShowStunLabel();
        // play stunned audio on initial stun (exclusive)
        if (batStunnedClip != null)
            PlayOneShotExclusive(batStunnedClip, batStunnedVolume);
        stunCoroutine = StartCoroutine(StunRoutine());
    }

    System.Collections.IEnumerator StunRoutine()
    {
        float t = stunDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            // update label while stunned
                if (stunnedLabelInstance != null)
                {
                    Vector3 worldPos = stunLabelAnchor != null ? stunLabelAnchor.position : (transform.position + stunLabelOffset);
                    if (stunnedLabelIsUI)
                    {
                        // update UI anchored position to follow world position
                        if (stunnedLabelCanvas != null && stunnedLabelRect != null && Camera.main != null)
                        {
                            Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
                            RectTransform canvasRect = stunnedLabelCanvas.GetComponent<RectTransform>();
                            Vector2 localPoint;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, stunnedLabelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main, out localPoint);
                            stunnedLabelRect.anchoredPosition = localPoint;
                        }
                    }
                    else
                    {
                        if (Camera.main != null)
                            stunnedLabelInstance.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
                        // if using a world label parented to the anchor, ensure it stays at the anchor position
                        if (stunLabelAnchor != null)
                        {
                            stunnedLabelInstance.transform.position = worldPos;
                        }
                    }
                }
            yield return null;
        }

        // recover
        isStunned = false;
        isBusy = false;
        HideStunLabel();
        if (animator != null)
        {
            // restore animator playback so normal states animate again
            animator.speed = 1f;
            // clear takehit trigger (in case) and return to flight
            animator.ResetTrigger("TakeHit");
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
        // clear tracked coroutine reference
        stunCoroutine = null;
    }

    void StopCoroutineIfRunning(string name)
    {
        // helper, attempt to stop by name; it's OK if nothing is running
        try { StopCoroutine(name); } catch { }
    }

    // Additional overloads commonly used by other code to ensure we react to damage
    public void TakeDamage(float amount, Vector2 hitPoint)
    {
        Debug.Log(name + ": TakeDamage(float, Vector2) received — applying stun.");
        ApplyStun();
    }

    public void TakeDamage(float amount, GameObject source)
    {
        Debug.Log(name + ": TakeDamage(float, GameObject) received from " + (source ? source.name : "null") + " — applying stun.");
        ApplyStun();
    }

    // Animation event called at end of attack
    public void EndAttack()
    {
        // If we're stunned, ignore attack-end events — keep the enemy stunned/idle until recovery
        if (isStunned)
        {
            StopFallback();
            return;
        }

        isBusy = false;
        StopFallback();
        // return to flying animation immediately
        if (animator != null)
        {
            // clear attack triggers and return to flying
            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.SetBool("isFlying", true);
            // attempt to play the configured Flight state directly if it exists to avoid getting stuck
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
    }

    void StopFallback()
    {
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
            fallbackCoroutine = null;
        }
    }

    IEnumerator FallbackClearBusy(float duration)
    {
        yield return new WaitForSeconds(duration);
        isBusy = false;
        // ensure animator returns to flying state
        if (animator != null)
        {
            animator.ResetTrigger("Attack1");
            animator.ResetTrigger("Attack2");
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
        fallbackCoroutine = null;
    }

    void ForceRecover()
    {
        // aggressive recovery: clear triggers, stop fallback coroutine, clear busy and force the flight state
        StopFallback();
        isBusy = false;
        if (animator != null)
        {
            for (int i = 0; i < attackStateNames.Length; i++)
            {
                animator.ResetTrigger(attackStateNames[i]);
            }
            animator.SetBool("isFlying", true);
            if (animator.HasState(0, flightHash))
                animator.Play(flightHash, 0, 0f);
        }
    }

    void OnDisable()
    {
        StopFallback();
        // stop any running stun coroutine when disabled
        if (stunCoroutine != null)
        {
            try { StopCoroutine(stunCoroutine); } catch { }
            stunCoroutine = null;
        }
        // ensure animator speed restored if inspector disables GameObject while frozen
        if (animator != null)
        {
            try { animator.speed = 1f; } catch { }
        }
        HideStunLabel();
    }


    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // Create and show the stunned label above the enemy. If a prefab is assigned it will be used;
    // otherwise a simple TextMesh is created at runtime and reused.
    void ShowStunLabel()
    {
        if (stunnedLabelInstance == null)
        {
            if (stunnedLabelPrefab != null)
            {
                // If the prefab contains UI components (RectTransform) we must parent it to a Canvas
                var prefabRect = stunnedLabelPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    // find existing Canvas
                    Canvas canvas = FindObjectOfType<Canvas>();
                    if (canvas == null)
                    {
                        // create a simple overlay canvas so UI prefabs will be visible
                        var canvasGO = new GameObject("StunCanvas");
                        canvas = canvasGO.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasGO.AddComponent<CanvasScaler>();
                        canvasGO.AddComponent<GraphicRaycaster>();
                    }

                    stunnedLabelCanvas = canvas;
                    stunnedLabelInstance = Instantiate(stunnedLabelPrefab, canvas.transform, false);
                    stunnedLabelRect = stunnedLabelInstance.GetComponent<RectTransform>();
                    stunnedLabelIsUI = true;
                    // position now; StunRoutine will update each frame. Use anchor if provided.
                    Vector3 worldPos = stunLabelAnchor != null ? stunLabelAnchor.position : (transform.position + stunLabelOffset);
                    Vector2 screenPos = Camera.main != null ? (Vector2)Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;
                    RectTransform canvasRect = stunnedLabelCanvas.GetComponent<RectTransform>();
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, stunnedLabelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main, out localPoint);
                    stunnedLabelRect.anchoredPosition = localPoint;
                }
                else
                {
                    // World-space prefab: parent to the anchor (if provided) or to the enemy so it moves with it
                    if (stunLabelAnchor != null)
                    {
                        stunnedLabelInstance = Instantiate(stunnedLabelPrefab, stunLabelAnchor);
                        stunnedLabelInstance.transform.localPosition = Vector3.zero;
                    }
                    else
                    {
                        stunnedLabelInstance = Instantiate(stunnedLabelPrefab, transform);
                        stunnedLabelInstance.transform.localPosition = stunLabelOffset;
                    }
                    stunnedLabelIsUI = false;
                }
            }
            else
            {
                var go = new GameObject("StunLabel");
                if (stunLabelAnchor != null)
                {
                    go.transform.SetParent(stunLabelAnchor, false);
                    go.transform.localPosition = Vector3.zero;
                }
                else
                {
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = stunLabelOffset;
                }
                var tm = go.AddComponent<TextMesh>();
                tm.text = "Stunned";
                tm.characterSize = 0.12f;
                tm.fontSize = 64;
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.cyan;
                stunnedLabelInstance = go;
                stunnedLabelIsUI = false;
            }
        }
        stunnedLabelInstance.SetActive(true);
        // start flashing
        StopStunnedLabelFlash();
        if (stunFlashRate > 0f)
            stunnedLabelFlashCoroutine = StartCoroutine(StunnedLabelFlashRoutine());
    }

    void HideStunLabel()
    {
        if (stunnedLabelInstance != null)
        {
            StopStunnedLabelFlash();
            stunnedLabelInstance.SetActive(false);
        }
    }

    void StopStunnedLabelFlash()
    {
        if (stunnedLabelFlashCoroutine != null)
        {
            try { StopCoroutine(stunnedLabelFlashCoroutine); } catch { }
            stunnedLabelFlashCoroutine = null;
        }
        // ensure label is visible when stopping flash so Hide/Show behaviour remains predictable
        if (stunnedLabelInstance != null)
            stunnedLabelInstance.SetActive(true);
    }

    IEnumerator StunnedLabelFlashRoutine()
    {
        // flashRate is full cycles per second; toggle interval is half the cycle
        float interval = 1f / (stunFlashRate * 2f);
        while (true)
        {
            if (stunnedLabelInstance != null)
                stunnedLabelInstance.SetActive(!stunnedLabelInstance.activeSelf);
            yield return new WaitForSeconds(interval);
        }
    }

    // Play a clip but ensure it is exclusive — stop any currently playing audio on the
    // main AudioSource so clips do not overlap. This keeps sound effects from stacking.
    void PlayOneShotExclusive(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        try { audioSource.Stop(); } catch { }
        audioSource.PlayOneShot(clip, volume);
    }

    // (Removed SafeCompareTag) We now use damageLayer checks like GoblinEnemy
}
