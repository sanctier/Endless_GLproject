using UnityEngine;

public class SpinningFireball2D : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 180f;
    public float radius = 2f;

    [Header("Follow Settings")]
    public float followSmoothness = 5f;
    public float maxFollowDistance = 10f;

    public float verticalOffset = 0.5f;

    [Header("Fireball Indexing")]
    public int fireballIndex = 0;
    public int totalFireballs = 1;

    [Header("Combat")]
    public int damage = 10;
    public LayerMask enemyLayer;
    [Header("Audio")]
    public AudioClip fireballHitClip;
    [Range(0f,1f)] public float fireballHitVolume = 1f;

    // Static provider so other scripts can play the fireball's hit audio without
    // holding a direct reference to a fireball instance.
    private static SpinningFireball2D s_provider;

    void OnEnable()
    {
        // register first available provider
        if (s_provider == null) s_provider = this;
    }

    void OnDisable()
    {
        if (s_provider == this) s_provider = null;
    }

    // Public static helper — other scripts (enemies, slash zones) can call this
    // to play the configured fireball hit clip at a world position.
    public static void PlayHitAt(Vector3 worldPos)
    {
        AudioClip clip = null;
        float volume = 1f;

        // Prefer registered provider
        if (s_provider != null && s_provider.fireballHitClip != null)
        {
            clip = s_provider.fireballHitClip;
            volume = s_provider.fireballHitVolume;
        }
        else
        {
            // Try to find any fireball in the scene with an assigned clip as a fallback
            var all = Object.FindObjectsOfType<SpinningFireball2D>();
            foreach (var f in all)
            {
                if (f != null && f.fireballHitClip != null)
                {
                    clip = f.fireballHitClip;
                    volume = f.fireballHitVolume;
                    s_provider = f; // cache for future calls
                    break;
                }
            }
        }

        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, worldPos, volume);
    }

    // Returns true when a provider exists and it has a hit clip assigned.
    // Other systems can call this to avoid playing duplicate local sounds.
    public static bool HasProviderClip()
    {
        return s_provider != null && s_provider.fireballHitClip != null;
    }

    private Transform playerTransform;
    private Vector2 currentVelocity;
    private float baseAngleOffset = 0f;

    void Start()
    {
        playerTransform = FindPlayer();
        if (playerTransform == null) return;
        CalculateBaseOffset();
    }

    void Update()
    {
        if (playerTransform == null) playerTransform = FindPlayer();
        if (playerTransform == null) return;

        UpdateTargetPosition();

        transform.position = Vector2.SmoothDamp(
            transform.position,
            playerTransform.position + Vector3.up * verticalOffset + (Vector3)currentVelocity, 
            ref currentVelocity,
            followSmoothness * Time.deltaTime
        );

        UpdateRotation();
    }

    void CalculateBaseOffset()
    {
        baseAngleOffset = (360f / totalFireballs) * fireballIndex;
    }

    void UpdateTargetPosition()
    {
        float phase = (Time.time * rotationSpeed) % 360f;
        float angleInRadians = (phase + baseAngleOffset) * Mathf.Deg2Rad;
        float x = Mathf.Cos(angleInRadians) * radius;
        float y = Mathf.Sin(angleInRadians) * radius;
        currentVelocity = new Vector2(x, y);
    }

    void UpdateRotation()
    {
        float phase = (Time.time * rotationSpeed) % 360f;
        float angleInRadians = (phase + baseAngleOffset) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(-Mathf.Sin(angleInRadians), Mathf.Cos(angleInRadians));
        float rotationZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    public void SetFireballIndex(int index, int total)
    {
        fireballIndex = index;
        totalFireballs = Mathf.Max(1, total);
        CalculateBaseOffset();
    }

    Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player?.transform;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            var enemy = collision.GetComponent<MonoBehaviour>();
            var takeDamageMethod = enemy?.GetType().GetMethod("TakeDamage");
            try
            {
                // Play local fireball clip first so audio isn't prevented by the damage call
                // (which may destroy the enemy or otherwise affect audio state).
                if (fireballHitClip != null)
                {
                    AudioSource.PlayClipAtPoint(fireballHitClip, collision.transform.position, fireballHitVolume);
                }
                else
                {
                    PlayHitAt(collision.transform.position);
                }

                // Now apply damage to the enemy
                takeDamageMethod?.Invoke(enemy, new object[] { damage });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(name + ": Exception invoking TakeDamage on " + (enemy ? enemy.name : "null") + " - " + ex.Message);
            }
        }
    }
}

