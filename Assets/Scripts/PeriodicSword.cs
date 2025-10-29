using UnityEngine;
using System.Collections;

public class PeriodicSword : MonoBehaviour
{
    [Header("Attack Settings")]
    public float damage = 25f;
    public float attackInterval = 5f;
    public float attackRange = 3f;
    public LayerMask enemyLayer;
    
    [Header("Visual Settings")]
    public float swingDuration = 0.7f;
    public float returnDuration = 0.3f;
    public Vector3 startPosition = new Vector3(0.5f, 0, 0);
    public Vector3 attackPosition = new Vector3(2f, 0, 0);
    
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Collider2D swordCollider;
    
    void Start()
    {
        player = PlayerController.Instance.transform;
        transform.parent = player;
        transform.localPosition = startPosition;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        swordCollider = GetComponent<Collider2D>();
        
        // Make sure the sword is initially visible but not attacking
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (swordCollider != null) swordCollider.enabled = false;
    }
    
    void Update()
    {
        attackTimer += Time.deltaTime;
        
        // Check if it's time to attack and if enemies are in range
        if (!isAttacking && attackTimer >= attackInterval && EnemiesInRange())
        {
            StartCoroutine(PerformAttack());
        }
    }
    
    bool EnemiesInRange()
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(player.position, attackRange, enemyLayer);
        return enemies.Length > 0;
    }
    
    IEnumerator PerformAttack()
    {
        isAttacking = true;
        attackTimer = 0f;
        
        // Enable collision
        if (swordCollider != null) swordCollider.enabled = true;
        
        // Swing forward
        float timer = 0f;
        Vector3 initialPos = transform.localPosition;
        
        while (timer < swingDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / swingDuration;
            transform.localPosition = Vector3.Lerp(initialPos, attackPosition, progress);
            yield return null;
        }
        
        // Damage all enemies in range at the peak of the swing
        DamageEnemiesInRange();
        
        // Return to position
        timer = 0f;
        initialPos = transform.localPosition;
        
        while (timer < returnDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / returnDuration;
            transform.localPosition = Vector3.Lerp(initialPos, startPosition, progress);
            yield return null;
        }
        
        // Ensure we're back at the start position
        transform.localPosition = startPosition;
        
        // Disable collision
        if (swordCollider != null) swordCollider.enabled = false;
        
        isAttacking = false;
    }
    
    void DamageEnemiesInRange()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, attackRange / 2f, enemyLayer);
        
        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                
                // Visual feedback
                StartCoroutine(FlashEnemy(enemy));
            }
        }
        
        if (hitEnemies.Length > 0)
        {
            // Play sound effect
            AudioManager.Instance.Play("SwordSwing");
        }
    }
    
    IEnumerator FlashEnemy(Collider2D enemy)
    {
        SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            Color originalColor = enemyRenderer.color;
            enemyRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            enemyRenderer.color = originalColor;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.yellow;
        if (player != null)
        {
            Gizmos.DrawWireSphere(player.position, attackRange);
        }
        
        // Draw damage range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange / 2f);
    }
}