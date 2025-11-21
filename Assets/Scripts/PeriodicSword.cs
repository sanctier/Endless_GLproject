using UnityEngine;
using System.Collections;

/// <summary>
/// Minimal PeriodicSword: listens for the player's attack completion and
/// spawns a single `slashPrefab` in front of the player (based on facing).
/// Keep it small: activation gating, single instance, and a simple linger.
/// </summary>
public class PeriodicSword : MonoBehaviour
{
    [Tooltip("Prefab to spawn as the after-image (required)")]
    public GameObject slashPrefab;
    [Tooltip("Local X offset in front of the player (positive = right)")]
    public float offsetX = 1.2f;
    [Tooltip("How long the after-image should linger (seconds)")]
    public float lingerDuration = 5f;
    [Tooltip("If true, parent this controller to the player on Start (may move object to player).")]
    public bool parentToPlayer = false;

    private Transform playerTransform;
    private GameObject currentAfterImage;
    private bool isActivated = false;

    void Start()
    {
        if (PlayerController.Instance == null)
        {
            Debug.LogError("PeriodicSword: PlayerController.Instance not found. Destroying self.");
            Destroy(gameObject);
            return;
        }

        playerTransform = PlayerController.Instance.transform;

        // Respect existing scene position by default: only parent if requested.
        if (parentToPlayer)
        {
            transform.SetParent(playerTransform, false);
            transform.localPosition = Vector3.zero;
        }

        // hide any sprite renderer on the controller itself
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    void OnDestroy()
    {
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnAttackComplete -= OnPlayerAttackComplete;
    }

    /// <summary>
    /// Activate so this controller responds to player attacks.
    /// Call from ShopManager when purchased.
    /// </summary>
    public void Activate()
    {
        if (isActivated) return;
        isActivated = true;
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnAttackComplete += OnPlayerAttackComplete;
    }

    private void OnPlayerAttackComplete()
    {
        if (!isActivated) return;
        if (currentAfterImage != null) return; // only one at a time
        SpawnOnce();
    }

    private void SpawnOnce()
    {
        if (slashPrefab == null)
        {
            Debug.LogWarning("PeriodicSword: no slashPrefab assigned.");
            return;
        }

        bool facingRight = PlayerController.Instance != null && PlayerController.Instance.IsFacingRight;
        float dir = facingRight ? 1f : -1f;

        // If this controller was left in the scene (not parented), honor its own transform position
        // so you can place a `SlashSpawn` GameObject manually. If `parentToPlayer` is true
        // we compute from the player's position as before.
        Vector3 origin = parentToPlayer ? playerTransform.position : transform.position;
        Vector3 spawnPos = origin + new Vector3(offsetX * dir, 0f, 0f);

        currentAfterImage = Instantiate(slashPrefab, spawnPos, Quaternion.identity);

        // optional: flip X scale to mirror prefab based on facing
        Vector3 s = currentAfterImage.transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        currentAfterImage.transform.localScale = s;

        StartCoroutine(LingerCoroutine(currentAfterImage));
    }

    IEnumerator LingerCoroutine(GameObject go)
    {
        yield return new WaitForSeconds(lingerDuration);
        if (go != null) Destroy(go);
        if (currentAfterImage == go) currentAfterImage = null;
    }
}