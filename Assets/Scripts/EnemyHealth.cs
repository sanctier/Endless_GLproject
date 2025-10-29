using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    [HideInInspector]
    public float currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        // Optionally add effects, rewards, etc.
        Destroy(gameObject);
        // Inform the WaveManager that an enemy has died
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.EnemyDefeated();
        }
    }
}
