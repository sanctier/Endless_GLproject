using UnityEngine;
using System;
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
    public bool IsFacingRight { get { return facingRight; } }
    private float currentHealth;
    public float maxHealth = 100f;
    private bool isDead = false;
    [Header("Abilities")]
    [Tooltip("Optional reference to the AirSlash helper on the player. If null it will be auto-resolved.")]
    public AirSlash airSlash;

    [Header("Audio")]
    public AudioClip attackClip;
    public AudioSource audioSource;
    [Range(0f,1f)] public float attackVolume = 1f;
    public AudioClip hurtClip;
    public AudioClip hurtClip2;
    [Range(0f,1f)] public float hurtVolume = 1f;
    public AudioClip deathClip;
    [Range(0f,1f)] public float deathVolume = 1f;

    // timestamps to prevent overlapping hurt/death sounds
    private float lastHurtPlayTime = -Mathf.Infinity;
    private float lastDeathPlayTime = -Mathf.Infinity;
    // flip toggle to alternate between hurt clips
    private bool useAlternateHurt = false;

    private int wavesSurvived = 0;
    // allow external systems (shop, cutscenes) to disable player attacks
    private bool canAttack = true;
    // prevent spamming attacks: lock input until current attack finishes
    private bool isAttacking = false;
    public float attackLockDuration = 0.5f; // seconds to lock attack (match your animation)
    private Coroutine attackLockCoroutine = null;

    // Event invoked when an attack finishes (attack lock expires)
    public Action OnAttackComplete;

    public void SetCanAttack(bool allowed)
    {
        canAttack = allowed;
        // stop any pending/playing attack audio when disabling attacks
        if (!allowed && audioSource != null && audioSource.isPlaying)
        {
            try { audioSource.Stop(); } catch { }
        }
    }

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
        // Resolve AirSlash reference if not assigned in inspector
        if (airSlash == null)
            airSlash = GetComponent<AirSlash>();
        // If AirSlash component exists, disable it by default unless already purchased
        if (airSlash != null)
        {
            bool purchased = false;
            if (ShopManager.Instance != null)
                purchased = ShopManager.Instance.IsPurchased(ShopItem.UpgradeType.AirSlash);
            airSlash.enabled = purchased;
            if (purchased)
                PlayerController.Instance.airSlash = airSlash;
        }
        // ensure an AudioSource exists for playing attack sounds
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // make it 2D by default
        }
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
            // prevent attacks while disabled (e.g., shop open) or while already attacking
            if (!canAttack || isAttacking) return;

            // trigger attack and lock further attacks until lock expires
            if (animator != null)
                animator.SetTrigger("Attack");

            // play attack sound if assigned
            if (attackClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(attackClip, attackVolume);
            }

            // AirSlash is spawned by animation events. Do not spawn here.

            // start lock
            isAttacking = true;
            if (attackLockCoroutine != null) StopCoroutine(attackLockCoroutine);
            attackLockCoroutine = StartCoroutine(AttackLockCoroutine());
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
        // Only increase the maximum health. Do not automatically change currentHealth
        // so that upgrades modify the max without instantly replenishing the player's
        // current health unless an explicit heal is requested.
        maxHealth += amount;
        // Ensure current health does not exceed the new max
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        Debug.Log($"PlayerController.TakeDamage called. damage={damage} currentHealth(before)={currentHealth}");

        currentHealth -= damage;
        if (healthBar != null)
            healthBar.SetHealth(currentHealth, maxHealth);

        Debug.Log($"PlayerController.TakeDamage applied. currentHealth(after)={currentHealth}");

        // Trigger Hurt animation
        if (animator != null)
            animator.SetTrigger("Hurt");

        // Play hurt sound if assigned and player is still alive, but avoid overlapping
        if (audioSource != null && (hurtClip != null || hurtClip2 != null) && currentHealth > 0f)
        {
            // choose which clip to play (alternate between two if both assigned)
            AudioClip clipToPlay = null;
            if (useAlternateHurt && hurtClip2 != null)
                clipToPlay = hurtClip2;
            else
                clipToPlay = hurtClip != null ? hurtClip : hurtClip2; // fallback if one is missing

            if (clipToPlay != null)
            {
                float now = Time.unscaledTime;
                float hurtDuration = Mathf.Max(0.05f, clipToPlay.length);
                if (now - lastHurtPlayTime >= hurtDuration)
                {
                    audioSource.PlayOneShot(clipToPlay, hurtVolume);
                    lastHurtPlayTime = now;
                    // flip for next hit so we alternate
                    useAlternateHurt = !useAlternateHurt;
                }
            }
        }

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

        // cancel any attack lock when dying
        if (attackLockCoroutine != null)
        {
            try { StopCoroutine(attackLockCoroutine); } catch { }
            attackLockCoroutine = null;
        }
        isAttacking = false;

        // Trigger Death animation
        if (animator != null)
            animator.SetTrigger("Death");

        // Play death sound if assigned (play once, avoid overlapping)
        if (audioSource != null && deathClip != null)
        {
            float now = Time.unscaledTime;
            float deathDuration = Mathf.Max(0.05f, deathClip.length);
            if (now - lastDeathPlayTime >= deathDuration)
            {
                audioSource.PlayOneShot(deathClip, deathVolume);
                lastDeathPlayTime = now;
            }
        }

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

    // Animation Event helper: call this from the Player's animation event list
    // (Animation window shows methods on the GameObject with the Animator, so
    // it's convenient to call the AirSlash spawn through the PlayerController).
    public void SpawnAirSlashFromAnimation()
    {
        try
        {
            // Ensure the player has purchased the AirSlash before forwarding the animation event.
            if (ShopManager.Instance != null && !ShopManager.Instance.IsPurchased(ShopItem.UpgradeType.AirSlash))
                return;

            if (airSlash == null)
                airSlash = GetComponent<AirSlash>();
            if (airSlash != null)
                airSlash.SpawnAirSlashEvent();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
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

    // Damage from enemies is handled by their animation events (they call
    // PlayerController.TakeDamage directly) or by dedicated attack hitboxes.
    // Removing tag-based trigger handlers avoids runtime warnings when tags
    // like `BossAttack` are not defined in the project.

    IEnumerator AttackLockCoroutine()
    {
        float t = 0f;
        // Use unscaled time so pause doesn't affect attack unlocking
        while (t < attackLockDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        isAttacking = false;
        attackLockCoroutine = null;

        try { OnAttackComplete?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
    }
}