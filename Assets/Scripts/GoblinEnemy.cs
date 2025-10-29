using UnityEngine;

public class GoblinEnemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 50;
    public int goldOnDeath = 5;
    public float moveSpeed = 2f;
    public float contactDamage = 10f;
    public float attackCooldown = 1.5f;

    [Header("Combat")]
    public LayerMask damageLayer;

    [Header("Death Effects")]
    public GameObject bloodSplatterPrefab; // Assign your blood splatter prefab in inspector
    public Vector2 bloodSplatterOffset = new Vector2(0, 0.5f); // Adjust based on your sprite

    private int currentHealth;
    private bool isDead = false;
    private float attackTimer = 0f;
    private bool playerInRange = false;

    private Animator animator;
    private Rigidbody2D rb;
    private Transform player;
    private bool facingRight = true;

    private Collider2D myCollider;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        // Make sure the initial facing is correct
        if (player != null && player.position.x < transform.position.x)
        {
            // Face left if player is to the left at spawn
            if (facingRight) Flip();
        }
        else if (player != null && player.position.x > transform.position.x)
        {
            // Face right if player is to the right at spawn
            if (!facingRight) Flip();
        }
    }

    void Update()
    {
        if (isDead) return;

        bool isRunning = false;

        // Move towards player if not in attack range
        if (!playerInRange && player != null)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
            isRunning = true;
        }

        animator.SetBool("IsRunning", isRunning);

        // --- Flipping logic ---
        if (player != null)
        {
            if (player.position.x < transform.position.x && facingRight)
            {
                Flip();
            }
            else if (player.position.x > transform.position.x && !facingRight)
            {
                Flip();
            }
        }

        // Attack logic with cooldown (when player in trigger)
        if (playerInRange && attackTimer <= 0f)
        {
            int attackType = Random.Range(0, 2);
            if (attackType == 0)
                animator.SetTrigger("Attack1");
            else
                animator.SetTrigger("Attack2");
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
        currentHealth -= damage;
        animator.SetTrigger("TakeHit");
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        animator.SetTrigger("Die");
        
        // Stop movement instantly
        if (rb != null) rb.linearVelocity = Vector2.zero;
        
        // Disable collider to stop further triggers/collisions
        if (myCollider != null) myCollider.enabled = false;
        
        // Spawn blood splatter effect
        SpawnBloodSplatter();
        
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCurrency(goldOnDeath);
        if (WaveManager.Instance != null)
            WaveManager.Instance.EnemyDefeated();
        
        // Wait for the death animation before destroying
        Destroy(gameObject, 4f); // Adjust delay for your death animation length
    }

    void SpawnBloodSplatter()
    {
        if (bloodSplatterPrefab != null)
        {
            // Calculate spawn position with offset
            Vector3 spawnPosition = transform.position + (Vector3)bloodSplatterOffset;
            
            // Instantiate blood splatter
            GameObject bloodSplatter = Instantiate(bloodSplatterPrefab, spawnPosition, Quaternion.identity);
            
            // Optional: Randomize rotation for variety
            bloodSplatter.transform.Rotate(0, 0, Random.Range(0, 360));
            
            // Optional: Flip based on enemy facing direction
            if (!facingRight)
            {
                Vector3 scale = bloodSplatter.transform.localScale;
                scale.x *= -1;
                bloodSplatter.transform.localScale = scale;
            }

            // Destroy blood splatter after 2 seconds
            Destroy(bloodSplatter, 2f);
        }
        else
        {
            Debug.LogWarning("Blood splatter prefab is not assigned on " + gameObject.name);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return; // Prevent triggers after death

        // Player entered attack range
        if (collision.CompareTag("Player"))
            playerInRange = true;

        // Only take damage from objects on the damageLayer (e.g., player/projectile)
        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            int damage = 10; // Replace with your logic if needed
            TakeDamage(damage);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (isDead) return; // Prevent triggers after death

        // Player left attack range
        if (collision.CompareTag("Player"))
            playerInRange = false;
    }

    // Animation event (set in Attack1/2 at hit frame)
    public void DealDamageToPlayer()
    {
        if (player != null && playerInRange && !isDead)
        {
            player.GetComponent<PlayerController>()?.TakeDamage((int)contactDamage);
        }
    }
}