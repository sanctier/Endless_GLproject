using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    public float damage = 25f;
    public float explosionRadius = 3f;
    public LayerMask damageLayers;

    void Start()
    {
        // You can add explosion sound, particles, etc. here
        DealDamageInRadius();
    }

    void DealDamageInRadius()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, damageLayers);
        
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                hit.GetComponent<PlayerController>()?.TakeDamage((int)damage);
            }
            else if (hit.CompareTag("Enemy"))
            {
                // Optional: Damage other enemies in explosion radius
                hit.GetComponent<MushroomEnemy>()?.TakeDamage((int)damage);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}