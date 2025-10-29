using UnityEngine;

public class MushroomEnemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 30;
    public int goldOnDeath = 8;
    public float moveSpeed = 5f; // Very fast movement
    public float explosionDamage = 25f;
    public float explosionRadius = 3f;

    [Header("Explosion Settings")]
    public GameObject explosionPrefab;
    public float timeUntilExplosion = 2f; // Time before exploding
    public float flashInterval = 0.2f; // How fast it flashes
    public float explosionTriggerDistance = 1.5f; // Distance to start countdown
    public float spawnDelay = 1f; // Delay after instantiation

    [Header("Combat")]
    public LayerMask damageLayer;

    private int currentHealth;
    private bool isDead = false;
    private bool isExploding = false;
    private bool countdownStarted = false;
    private bool isActive = false;
    private bool killedByPlayer = false; // Added: Track if player killed it

    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private bool facingRight = true;

    private Collider2D myCollider;
    private float explosionTimer;
    private float flashTimer;
    private bool isVisible = true;
    private bool isMoving = false;
    private float spawnTimer;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Set interpolation for smooth movement
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        // Initialize timers
        explosionTimer = timeUntilExplosion;
        spawnTimer = spawnDelay;

        // Disable initially
        SetActiveState(false);
    }

    void Update()
    {
        // Handle spawn delay
        if (!isActive)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                isActive = true;
                SetActiveState(true);
            }
            return; // Don't process anything else until active
        }

        if (isDead) return;

        // Only start countdown when player is close (and countdown hasn't started yet)
        if (player != null && !isExploding && !countdownStarted)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // Start countdown only when player is within the trigger distance
            if (distanceToPlayer <= explosionTriggerDistance)
            {
                countdownStarted = true; // Countdown cannot be reverted once started
            }
        }

        // Continue countdown if it has started
        if (countdownStarted && !isExploding)
        {
            explosionTimer -= Time.deltaTime;

            if (explosionTimer <= 0f)
            {
                Explode();
                return;
            }

            // Flash effect when close to exploding (last 1.5 seconds)
            if (explosionTimer <= 1.5f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    ToggleVisibility();
                    flashTimer = flashInterval;
                }

                // Switch to idle animation when flashing starts
                if (isMoving)
                {
                    isMoving = false;
                    UpdateAnimator();
                }
            }
        }

        // Move towards player if not exploding, not flashing, and countdown hasn't started
        if (!isExploding && player != null && !countdownStarted)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);

            // Update moving state for animator
            if (!isMoving)
            {
                isMoving = true;
                UpdateAnimator();
            }

            // Update facing direction
            if (player.position.x < transform.position.x && facingRight)
            {
                Flip();
            }
            else if (player.position.x > transform.position.x && !facingRight)
            {
                Flip();
            }
        }
        else if (isMoving && (countdownStarted || isExploding))
        {
            // Stop moving if countdown has started or exploding
            isMoving = false;
            UpdateAnimator();
        }
    }

    void SetActiveState(bool active)
    {
        // Enable/disable components during spawn delay
        if (rb != null) rb.simulated = active;
        if (myCollider != null) myCollider.enabled = active;

        // Optional: Visual indication of spawn delay (like fading in)
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = active ? 1f : 0.5f; // Semi-transparent during delay
            spriteRenderer.color = color;
        }
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);
        }
    }

    void Explode()
    {
        if (isDead) return; // Prevent explosion if already dead

        isExploding = true;

        // Stop movement
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Create explosion prefab
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // Set explosion damage and radius if the prefab has a component for it
            ExplosionEffect explosionEffect = explosion.GetComponent<ExplosionEffect>();
            if (explosionEffect != null)
            {
                explosionEffect.damage = explosionDamage;
                explosionEffect.explosionRadius = explosionRadius;
            }

            Destroy(explosion, 2f); // Clean up explosion after 2 seconds
        }

        // Deal damage to player if in range
        DealExplosionDamage();

        // Destroy the mushroom (but don't award gold for self-destruction)
        Die();
    }

    void DealExplosionDamage()
    {
        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= explosionRadius)
            {
                player.GetComponent<PlayerController>()?.TakeDamage((int)explosionDamage);
            }
        }
    }

    void ToggleVisibility()
    {
        isVisible = !isVisible;
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = isVisible ? 1f : 0.3f; // Flash between full and partial visibility
            spriteRenderer.color = color;
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
        if (isDead || isExploding || !isActive) return; // Can't take damage during spawn delay

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            killedByPlayer = true; // Mark as killed by player
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        // Make sure sprite is fully opaque when dying
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f; // Set to full opaque
            spriteRenderer.color = color;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // Stop movement
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Disable collider
        if (myCollider != null) myCollider.enabled = false;

        // Award gold ONLY if killed by player (not self-destruction)
        if (killedByPlayer)
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddCurrency(goldOnDeath);
        }

        if (WaveManager.Instance != null)
            WaveManager.Instance.EnemyDefeated();

        // Destroy after a short delay
        Destroy(gameObject, 2f);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead || isExploding || !isActive) return; // Ignore collisions during spawn delay

        // Take damage from player attacks
        if (((1 << collision.gameObject.layer) & damageLayer) != 0)
        {
            int damage = 10; // Adjust based on your damage system
            TakeDamage(damage);
        }
    }

    // Visualize explosion radius in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        // Also draw the trigger distance for easier debugging
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionTriggerDistance);
    }
}