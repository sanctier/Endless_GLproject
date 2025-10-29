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
            takeDamageMethod?.Invoke(enemy, new object[] { damage });
        }
    }
}
