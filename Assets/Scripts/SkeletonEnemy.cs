using UnityEngine;

public class SkeletonEnemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 50;
    public float moveSpeed = 2f;
    public float contactDamage = 10f;
    public float attackCooldown = 1.5f;

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
    // separate flag for the (trigger) attack box so Update doesn't overwrite it
    private bool playerInTrigger = false;
    // internal busy flag to avoid relying on Animator state name checks
    private bool isBusy = false;

    // Shield state
    private bool isShielding = false;
    public float shieldDuration = 0.7f;
    private float shieldTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Set initial facing
        if (player != null && player.position.x < transform.position.x && facingRight) Flip();
        else if (player != null && player.position.x > transform.position.x && !facingRight) Flip();
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Prefer a runtime distance check for attack range but respect the trigger collider too
        // If the skeleton's box collider (trigger) contains the player we want to allow attacks even
        // if the distance math is slightly off. Combine both checks.
        bool inRange = distance <= attackRange || playerInTrigger;
        // keep the public/used flag in sync
        playerInRange = inRange;

        // --- Handle shield timer ---
        if (isShielding)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
            {
                // Ensure shield ends via the same path as the animation event
                EndShield();
            }
        }

        bool isRunning = false;

        // --- Movement ---
        // Move while player is out of range
        if (!inRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
            isRunning = true;
        }

        // Animator now uses 'isRunning'
        animator.SetBool("isRunning", isRunning);

        // --- Flipping ---
        if (player.position.x < transform.position.x && facingRight) Flip();
        else if (player.position.x > transform.position.x && !facingRight) Flip();

        // --- Attack / Shield ---
        // Use distance-based inRange primarily so child-collider noise doesn't cancel attacks
        if (inRange && attackTimer <= 0f && !isShielding)
        {
            // don't start a new attack while busy (attack/shield/hit/etc.)
            bool canStartAttack = !isBusy;
            if (!canStartAttack)
            {
                // skip starting an attack this frame; allow Update to continue so timers decrement
            }
            else if (Random.value < shieldChance)
             {
                 // Start shielding
                 isShielding = true;
                 shieldTimer = shieldDuration;
                 // Animator uses 'isBlocking' as a Trigger now
                 animator.SetTrigger("isBlocking");
                // mark busy until EndShield is called by animation event
                isBusy = true;
                  // Make sure running is disabled while blocking
                  animator.SetBool("isRunning", false);
             }
             else
             {
                 // Attack
                 int attackType = Random.Range(0, 2);
                 // Trigger attack animations (Attack1/Attack2 are Triggers)
                 animator.SetBool("isRunning", false);
                 if (attackType == 0)
                 {
                     animator.SetTrigger("Attack1");
                 }
                 else
                 {
                    // Animator parameter uses 'Attack 2' (with space)
                    animator.SetTrigger("Attack2");
                 }
                // mark busy until EndAttack animation event clears it
                isBusy = true;
              }

             // only reset cooldown when an action actually started (shield or attack)
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

        // 50% chance to block if shield is active
        // Prefer the internal shield state for logic; Animator 'isBlocking' is now a trigger so rely on isShielding
        if (isShielding)
        {
            return;
        }

        currentHealth -= damage;
        // Hit is now a trigger named 'TakeHit'
        animator.SetTrigger("TakeHit");
        isBusy = true; // prevent other actions while hit animation plays

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        // Death is now a trigger named 'Die'
        animator.SetTrigger("Die");

        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (myCollider != null) myCollider.enabled = false;

        Destroy(gameObject, 4f); // adjust to match death animation length
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        if (collision.CompareTag("Player"))
            playerInTrigger = true;

        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            TakeDamage(10); // replace with dynamic damage if needed
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (isDead) return;

        if (collision.CompareTag("Player"))
            playerInTrigger = false;
    }

    // Animation event called at end of Attack1/2
    public void EndAttack()
    {
        // attack finished
        isBusy = false;
    }

    // Animation event called at end of shield
    public void EndShield()
    {
        isShielding = false;
        // Clear the blocking trigger in case it's still set (triggers are one-shot but resetting is safe)
        animator.ResetTrigger("isBlocking");
        isBusy = false;
    }
    // Animation event called at end of TakeHit
    public void EndHit()
    {
        // allow attacking again after hit animation
        isBusy = false;
    }

    // Animation event called when dealing damage to player (set in attack animations)
    public void DealDamageToPlayer()
    {
        if (player != null && playerInRange && !isDead)
        {
            player.GetComponent<PlayerController>()?.TakeDamage((int)contactDamage);
        }
    }
}
