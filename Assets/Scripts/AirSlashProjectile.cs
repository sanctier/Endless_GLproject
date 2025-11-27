using UnityEngine;

// Simple movement + lifetime for the AirSlash prefab instance.
// Attach this to the AirSlash prefab (or let the spawner add it at runtime).
public class AirSlashProjectile : MonoBehaviour
{
    [Tooltip("Movement speed in units per second")]
    public float speed = 6f;
    [Tooltip("Seconds before the projectile is destroyed")]
    public float lifeTime = 10f;
    [Tooltip("Damage applied to enemies on hit")]
    public float damage = 15f;

    [Header("Audio")]
    public AudioClip hitClip;
    [Range(0f,1f)] public float hitVolume = 1f;

    float timer = 0f;
    int direction = 1; // 1 = right, -1 = left
    Rigidbody2D rb;
    bool directionInitialized = false;
    // track colliders we've already hit so we don't apply damage repeatedly
    private System.Collections.Generic.HashSet<Collider2D> hitSet = new System.Collections.Generic.HashSet<Collider2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // Determine initial direction from localScale.x if not already set by spawner.
        if (!directionInitialized)
        {
            direction = transform.localScale.x >= 0f ? 1 : -1;
        }

        // If there's a Rigidbody2D, set its velocity for physics-driven motion.
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(-direction * speed, rb.linearVelocity.y);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // If we don't have a Rigidbody2D, move via transform.
        if (rb == null)
        {
            // Move in world X direction so parent scale/flipping doesn't invert movement.
            transform.position += Vector3.right * (-direction) * speed * Time.deltaTime;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (hitSet.Contains(other)) return;

        if (IsEnemyCollider(other))
        {
            ApplyDamageToCollider(other, damage);
            hitSet.Add(other);
        }
    }

    bool IsEnemyCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Enemy")) return true;
        if (col.GetComponentInParent<EnemyHealth>() != null) return true;
        if (col.GetComponentInParent<GoblinEnemy>() != null) return true;
        if (col.GetComponentInParent<MushroomEnemy>() != null) return true;
        if (col.GetComponentInParent<SkeletonEnemy>() != null) return true;
        if (col.GetComponentInParent<BossBandit>() != null) return true;
        if (col.GetComponentInParent<FlyingEnemy>() != null) return true;
        return false;
    }

    void ApplyDamageToCollider(Collider2D col, float dmg)
    {
        if (col == null) return;

        var eh = col.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            eh.TakeDamage(dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var gob = col.GetComponentInParent<GoblinEnemy>();
        if (gob != null)
        {
            gob.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var mush = col.GetComponentInParent<MushroomEnemy>();
        if (mush != null)
        {
            mush.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var skel = col.GetComponentInParent<SkeletonEnemy>();
        if (skel != null)
        {
            skel.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var boss = col.GetComponentInParent<BossBandit>();
        if (boss != null)
        {
            boss.TakeDamage((int)dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        var fly = col.GetComponentInParent<FlyingEnemy>();
        if (fly != null)
        {
            fly.TakeDamage(dmg);
            PlayHitSoundAt(col.transform.position);
            return;
        }

        try
        {
            col.gameObject.SendMessageUpwards("TakeDamage", (int)dmg, SendMessageOptions.DontRequireReceiver);
            PlayHitSoundAt(col.transform.position);
        }
        catch { }
    }

    void PlayHitSoundAt(Vector3 pos)
    {
        if (hitClip == null) return;
        AudioSource.PlayClipAtPoint(hitClip, pos, hitVolume);
    }

    // Allow external code (spawner) to set direction so movement matches
    // whatever flip method the game uses (flipX or scale). This also
    // updates Rigidbody velocity and transform scale immediately.
    public void SetDirection(int dir)
    {
        direction = dir >= 0 ? 1 : -1;
        directionInitialized = true;

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(-direction * speed, rb.linearVelocity.y);
        }

        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (direction >= 0 ? 1f : -1f);
        transform.localScale = s;
    }
}
