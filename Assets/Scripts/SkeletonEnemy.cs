using UnityEngine;
using System.Collections;

public class SkeletonEnemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 60;
    public float moveSpeed = 2f;
    public float contactDamage = 8f;
    public float attackCooldown = 1.5f;
    public int goldOnDeath = 10;

    [Header("Combat")]
    public LayerMask damageLayer;
    public float attackRange = 1.5f;
    [Range(0f,1f)] public float shieldChance = 0.5f; // 50% chance to block

    private int currentHealth;
    private bool isDead = false;
    private float attackTimer = 0f;
    private bool playerInRange = false;

    private Animator animator;
    private Rigidbody2D rb;
    private Transform player;
    private bool facingRight = true;

    private Collider2D myCollider;
    private bool playerInTrigger = false;
    private bool isBusy = false;
    // fallback flag to avoid double-applying damage when animation event and fallback both fire
    private bool attackHitApplied = false;

    // Shield state
    private bool isShielding = false;
    public float shieldDuration = 0.7f;
    private float shieldTimer = 0f;

    // Fallback durations (in seconds) in case animation events are not set
    public float fallbackAttackDuration = 1.0f;
    public float fallbackShieldDuration = 1.0f;
    public float fallbackHitDuration = 0.6f;

    [Header("Audio")]
    public AudioClip attackClip;
    [Range(0f,1f)] public float attackVolume = 1f;
    public AudioClip blockClip;
    [Range(0f,1f)] public float blockVolume = 1f;

    private AudioSource audioSource;
    // timestamps to prevent duplicate/overlapping sounds
    private float lastAttackPlayTime = -Mathf.Infinity;
    private float lastBlockPlayTime = -Mathf.Infinity;

    // store currently running fallback coroutine so we can cancel it when the animation event fires
    private Coroutine fallbackCoroutine = null;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // ensure an AudioSource exists for playing sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // spatialized
        }

        // Set initial facing
        if (player != null && player.position.x < transform.position.x && facingRight) Flip();
        else if (player != null && player.position.x > transform.position.x && !facingRight) Flip();
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        bool inRange = distance <= attackRange || playerInTrigger;
        playerInRange = inRange;

        // Shield timer
        if (isShielding)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
            {
                // Call EndShield via same path as animation event
                EndShield();
            }
        }

        bool isRunning = false;

        // Movement: move while player is out of range
        if (!inRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
            isRunning = true;
        }

        // Animator - ensure parameter exists in your animator (case-sensitive)
        if (animator != null) animator.SetBool("isRunning", isRunning);

        // Flipping
        if (player.position.x < transform.position.x && facingRight) Flip();
        else if (player.position.x > transform.position.x && !facingRight) Flip();

        // Attack / Shield decision
        if (inRange && attackTimer <= 0f && !isShielding)
        {
            bool canStartAttack = !isBusy;
            if (!canStartAttack)
            {
                // Busy: waiting for animation to finish (or fallback)
            }
            else if (Random.value < shieldChance)
            {
                // Start shielding
                isShielding = true;
                shieldTimer = shieldDuration;
                if (animator != null) animator.SetTrigger("isBlocking");
                isBusy = true;
                if (animator != null) animator.SetBool("isRunning", false);

                // play block start sound once (avoid duplicates)
                if (blockClip != null && audioSource != null)
                {
                    float nowBlock = Time.unscaledTime;
                    float durBlock = Mathf.Max(0.05f, blockClip.length);
                    if (nowBlock - lastBlockPlayTime >= durBlock)
                    {
                        audioSource.PlayOneShot(blockClip, blockVolume);
                        lastBlockPlayTime = nowBlock;
                    }
                }

                // start fallback in case animation event EndShield is missing
                StartFallback(FallbackType.Shield);
            }
                else
                {
                // Attack
                int attackType = Random.Range(0, 2);
                if (animator != null) animator.SetBool("isRunning", false);
                if (attackType == 0)
                    if (animator != null) animator.SetTrigger("Attack1");
                    else Debug.LogWarning("Animator missing or Attack1 trigger not found.");
                else
                    if (animator != null) animator.SetTrigger("Attack2");
                    else Debug.LogWarning("Animator missing or Attack2 trigger not found.");

                // play attack sound once (avoid duplicates)
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

                isBusy = true;

                // start fallback in case EndAttack animation event is missing
                    StartFallback(FallbackType.Attack);
                    // start a timed fallback to apply damage in case the animation event (DealDamageToPlayer)
                    // is not present or missed; this will be cancelled if the animation event fires first.
                    attackHitApplied = false;
                    StartCoroutine(FallbackAttackHit(fallbackAttackDuration * 0.45f));
            }


            if (canStartAttack)
                attackTimer = attackCooldown;
        }

        attackTimer -= Time.deltaTime;
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

        // If shielding, consider it blocked (still log it)
        if (isShielding)
        {
            Debug.Log($"{name} blocked damage while shielding.");
            return;
        }

        currentHealth -= damage;
        Debug.Log($"{name} took {damage} damage. HP: {currentHealth}/{maxHealth}");
        if (animator != null) animator.SetTrigger("TakeHit");

        // mark busy and start fallback so hit animation doesn't block forever
        isBusy = true;
        StartFallback(FallbackType.Hit);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        if (animator != null) animator.SetTrigger("Die");

        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (myCollider != null) myCollider.enabled = false;

        // award gold and notify wave manager (if present)
        if (goldOnDeath > 0)
        {
            var cm = FindObjectOfType<CurrencyManager>();
            if (cm != null)
                cm.AddCurrency(goldOnDeath);
            else if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddCurrency(goldOnDeath);
        }

        var wm = FindObjectOfType<WaveManager>();
        if (wm != null)
            wm.EnemyDefeated();
        else if (WaveManager.Instance != null)
            WaveManager.Instance.EnemyDefeated();

        Destroy(gameObject, 4f);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        if (collision.CompareTag("Player"))
        {
            playerInTrigger = true;
            Debug.Log($"{name} player entered trigger.");
        }

        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            Debug.Log($"{name} hit by damage layer object: {collision.gameObject.name} (layer {LayerMask.LayerToName(collision.gameObject.layer)})");
            TakeDamage(10); // adapt if you have dynamic damage
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (isDead) return;

        if (collision.CompareTag("Player"))
        {
            playerInTrigger = false;
            Debug.Log($"{name} player exited trigger.");
        }
    }

    // Animation event called at end of Attack1/2
    public void EndAttack()
    {
        Debug.Log($"{name} EndAttack called (animation event).");
        isBusy = false;
        StopFallback();
    }

    // Animation event called at end of shield
    public void EndShield()
    {
        Debug.Log($"{name} EndShield called (animation event).");
        isShielding = false;
        if (animator != null) animator.ResetTrigger("isBlocking");
        isBusy = false;
        StopFallback();
    }

    // Animation event called at end of TakeHit
    public void EndHit()
    {
        Debug.Log($"{name} EndHit called (animation event).");
        isBusy = false;
        StopFallback();
    }

    // Animation event: apply damage to player at the hit frame
    public void DealDamageToPlayer()
    {
        Debug.Log($"{name} DealDamageToPlayer called. playerInRange={playerInRange}, isDead={isDead}");
        if (player != null && playerInRange && !isDead)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Ensure we don't apply the same hit twice (animation event + fallback)
                if (!attackHitApplied)
                {
                    pc.TakeDamage(contactDamage);
                    Debug.Log($"{name} dealt {contactDamage} to player.");
                    attackHitApplied = true;
                }
            }
            else
            {
                Debug.LogWarning("PlayerController not found on player GameObject.");
            }
        }
    }

    // Fallback attack hit: called if animation event doesn't fire
    IEnumerator FallbackAttackHit(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isBusy && !attackHitApplied)
        {
            // try to apply damage via same method (respects playerInRange)
            DealDamageToPlayer();
        }
    }

    // --- Fallback helper (in case animation events are missing) ---
    enum FallbackType { Attack, Shield, Hit }

    void StartFallback(FallbackType type)
    {
        // cancel any existing fallback coroutine
        StopFallback();

        float dur = fallbackAttackDuration;
        if (type == FallbackType.Shield) dur = fallbackShieldDuration;
        else if (type == FallbackType.Hit) dur = fallbackHitDuration;

        fallbackCoroutine = StartCoroutine(FallbackClearBusy(dur));
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
        isShielding = false;
        fallbackCoroutine = null;
        Debug.Log($"{name} fallback cleared isBusy after {duration} seconds.");
    }
}
