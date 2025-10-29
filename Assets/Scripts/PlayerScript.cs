using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement; 
using TMPro;
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance; 

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    private Vector2 movement;

    [Header("Boundaries")]
    public Vector2 minBounds; 
    public Vector2 maxBounds; 

    [Header("Combat Settings")]
    public float baseDamage = 10f;
    public float damageMultiplier = 1f;
    public float temporaryDamageBoost = 0f;
    public float temporaryBoostDuration = 0f;

    public PlayerHealthBar healthBar;
    public GameObject gameOverCanvas;
    public TextMeshProUGUI wavesSurvivedText;
    private Rigidbody2D rb;
    private Animator animator;
    private bool facingRight = true;
    private float currentHealth;
    public float maxHealth = 100f;
    private bool isDead = false;

    private int wavesSurvived = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (ShopManager.Instance != null)
            ShopManager.Instance.ResetAllUpgrades();
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.ResetCurrency();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);

        // Ensure GameOverCanvas is hidden at start
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);

        // Subscribe to wave events
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted += OnWaveCompleted;
        }
    }

    void Update()
    {
        if (isDead) return; // No input if dead

        // Movement input
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Flip character based on direction
        if (movement.x < 0 && !facingRight)
        {
            Flip();
        }
        else if (movement.x > 0 && facingRight)
        {
            Flip();
        }

        // Animation parameters
        if (animator != null)
            animator.SetFloat("Speed", movement.sqrMagnitude);

        // Attack input
        if (Input.GetMouseButtonDown(0))
        {
            if (animator != null)
                animator.SetTrigger("Attack");
            // You might want to add actual attack logic here
        }

        // Update temporary damage boost timer
        if (temporaryDamageBoost > 0)
        {
            temporaryBoostDuration -= Time.deltaTime;
            if (temporaryBoostDuration <= 0)
            {
                temporaryDamageBoost = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return; // No movement if dead

        Vector2 newPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
        // Clamp to min/max bounds
        newPosition.x = Mathf.Clamp(newPosition.x, minBounds.x, maxBounds.x);
        newPosition.y = Mathf.Clamp(newPosition.y, minBounds.y, maxBounds.y);

        rb.MovePosition(newPosition);
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // Health methods for ShopManager
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);
    }

    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth += amount;
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);

        // Trigger Hurt animation
        if (animator != null)
            animator.SetTrigger("Hurt");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void OnWaveCompleted(int waveNumber)
    {
        wavesSurvived = waveNumber;
        Debug.Log($"Player survived wave: {wavesSurvived}");
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Trigger Death animation
        if (animator != null)
            animator.SetTrigger("Death");

        // Update waves survived text
        if (wavesSurvivedText != null)
        {
            wavesSurvivedText.text = $"You Survived Wave {wavesSurvived}.";
        }

        // Reset upgrades and currency
        if (ShopManager.Instance != null)
            ShopManager.Instance.ResetAllUpgrades();
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.ResetCurrency();

        // Show Game Over Canvas
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(true);

        // Optionally: disable the player's collider or set velocity to zero
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // Pause the game
        Time.timeScale = 0f;
    }

    // Combat methods for ShopManager
    public void AddPermanentDamageBoost(float boost)
    {
        damageMultiplier += boost / 100f;
    }

    public void AddTemporaryDamageBoost(float boost, float duration)
    {
        temporaryDamageBoost = boost;
        temporaryBoostDuration = duration;
    }

    public void AddSpeedBoost(float boost)
    {
        moveSpeed += boost;
    }

    public float GetDamage()
    {
        return (baseDamage * damageMultiplier) + temporaryDamageBoost;
    }

    // ===== DEATH CANVAS BUTTON FUNCTIONS =====

    public void RestartGame()
    {
        // Resume time scale
        Time.timeScale = 1f;

        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // Hide the game over canvas
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);
    }

    public void QuitGame()
    {
        // If we're in the Unity Editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // If we're in a built game
        Application.Quit();
#endif
    }

    // Optional: Add these to handle button clicks from the UI
    public void OnRestartButtonClicked()
    {
        RestartGame();
    }

    public void OnQuitButtonClicked()
    {
        QuitGame();
    }

    void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
        }
    }
}