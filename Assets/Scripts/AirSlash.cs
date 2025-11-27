using UnityEngine;

// Attach this to the player GameObject. On left click it will spawn an AirSlash prefab
// at the `airSlashSpawn` position and orient it to the player's facing reliably.
public class AirSlash : MonoBehaviour
{
    [Header("References")]
    public GameObject airSlashPrefab;
    public Transform airSlashSpawn;
    [Tooltip("Set to true if the prefab's primary art faces right by default.")]
    public bool prefabFacesRight = true;
    [Tooltip("Enable verbose orientation debug logs when spawning")]
    public bool debugOrientation = false;

    [Header("Timing")]
    public float cooldown = 0.25f;

    float lastAttackTime = -Mathf.Infinity;

    void Awake()
    {
        // If the spawn transform was disabled in the editor, make sure it stays disabled at runtime.
        if (airSlashSpawn != null && airSlashSpawn.gameObject.activeSelf)
        {
            airSlashSpawn.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        // Animation clips can re-enable objects on start — re-assert disabled at end of frame.
        if (airSlashSpawn != null)
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.StartCoroutine(EnsureSpawnDisabledNextFrame());
            else
                StartCoroutine(EnsureSpawnDisabledNextFrame());
        }
    }

    System.Collections.IEnumerator EnsureSpawnDisabledNextFrame()
    {
        yield return new WaitForEndOfFrame();
        if (airSlashSpawn != null && airSlashSpawn.gameObject.activeSelf)
            airSlashSpawn.gameObject.SetActive(false);
    }

    // Spawning is driven by animation events (call `SpawnAirSlashEvent()` from the Animator).
    // Input-based spawning was removed to avoid accidental spawns.

    // Public method intended to be called from an Animation Event (no parameters).
    // Place an Animation Event on the slashing animation and set the function to `SpawnAirSlashEvent`.
    public void SpawnAirSlashEvent()
    {
        // Prevent spawning unless the shop reports this upgrade purchased.
        if (ShopManager.Instance != null)
        {
            if (!ShopManager.Instance.IsPurchased(ShopItem.UpgradeType.AirSlash))
            {
                if (debugOrientation) Debug.Log("[AirSlash] Spawn attempted but AirSlash not purchased yet.");
                return;
            }
        }
        else
        {
            // If ShopManager is not present, fall back to component enabled state.
            if (!this.enabled)
            {
                if (debugOrientation) Debug.Log("[AirSlash] Spawn attempted but AirSlash component is disabled (probably not purchased).");
                return;
            }
        }

        if (Time.time < lastAttackTime + cooldown) return;
        SpawnAirSlashInternal();
        lastAttackTime = Time.time;
    }

    // Internal spawn routine used by both input and animation events.
    void SpawnAirSlashInternal()
    {
        if (airSlashPrefab == null || airSlashSpawn == null)
        {
            Debug.LogWarning("AirSlash: missing prefab or spawn transform.");
            return;
        }

        GameObject spawned = Instantiate(airSlashPrefab, airSlashSpawn.position, airSlashPrefab.transform.rotation);

        // Determine player's facing using PlayerController when available
        bool playerFacingRight = true;
        if (PlayerController.Instance != null)
            playerFacingRight = PlayerController.Instance.IsFacingRight;
        else
        {
            SpriteRenderer playerSr = GetComponentInChildren<SpriteRenderer>();
            if (playerSr != null) playerFacingRight = !playerSr.flipX;
            else playerFacingRight = transform.localScale.x >= 0f;
        }

        bool desiredFacingRight = playerFacingRight;

        // Tell projectile its direction early so it can set velocity correctly
        AirSlashProjectile asp = spawned.GetComponent<AirSlashProjectile>();
        if (asp != null) asp.SetDirection(desiredFacingRight ? 1 : -1);

        // Flip child sprites to match the desired facing relative to prefab art
        SpriteRenderer[] spawnedSRs = spawned.GetComponentsInChildren<SpriteRenderer>();
        bool shouldFlip = (desiredFacingRight != prefabFacesRight);
        if (spawnedSRs != null && spawnedSRs.Length > 0)
        {
            if (debugOrientation)
            {
                Debug.Log($"[AirSlash] Spawned '{spawned.name}' playerFacingRight={playerFacingRight} prefabFacesRight={prefabFacesRight} desiredFacingRight={desiredFacingRight} shouldFlip={shouldFlip}");
                for (int i = 0; i < spawnedSRs.Length; i++) Debug.Log($"[AirSlash]  child #{i} '{spawnedSRs[i].name}' flipX_before={spawnedSRs[i].flipX}");
            }

            for (int i = 0; i < spawnedSRs.Length; i++) spawnedSRs[i].flipX = shouldFlip;

            if (debugOrientation)
            {
                for (int i = 0; i < spawnedSRs.Length; i++) Debug.Log($"[AirSlash]  child #{i} '{spawnedSRs[i].name}' flipX_after={spawnedSRs[i].flipX}");
                Debug.Log($"[AirSlash]  prefab_root_localScale_before={spawned.transform.localScale.x}");
            }
        }

        // Parent under a container so we can flip the whole thing reliably
        GameObject container = new GameObject(spawned.name + "_Container");
        Transform containerT = container.transform;
        containerT.position = spawned.transform.position;
        containerT.rotation = spawned.transform.rotation;
        spawned.transform.SetParent(containerT, true);

        Vector3 containerScale = containerT.localScale;
        containerScale.x = Mathf.Abs(containerScale.x) * (desiredFacingRight ? 1f : -1f);
        containerT.localScale = containerScale;

        // Re-apply at end of frame in case Animators override during Start/OnEnable
        if (PlayerController.Instance != null)
            PlayerController.Instance.StartCoroutine(ForceOrientationNextFrame(containerT.gameObject, desiredFacingRight));
        else
            StartCoroutine(ForceOrientationNextFrame(containerT.gameObject, desiredFacingRight));
    }

    System.Collections.IEnumerator ForceOrientationNextFrame(GameObject spawnedContainer, bool desiredFacingRight)
    {
        yield return new WaitForEndOfFrame();
        if (spawnedContainer == null) yield break;

        if (debugOrientation) Debug.Log($"[AirSlash] ForceOrientationNextFrame for '{spawnedContainer.name}' desiredFacingRight={desiredFacingRight} prefabFacesRight={prefabFacesRight}");

        Transform prefabT = spawnedContainer.transform.childCount > 0 ? spawnedContainer.transform.GetChild(0) : spawnedContainer.transform;

        AirSlashProjectile asp = prefabT.GetComponent<AirSlashProjectile>();
        if (asp != null) asp.SetDirection(desiredFacingRight ? 1 : -1);

        SpriteRenderer[] spawnedSRs = prefabT.GetComponentsInChildren<SpriteRenderer>();
        bool shouldFlip = (desiredFacingRight != prefabFacesRight);
        if (spawnedSRs != null && spawnedSRs.Length > 0)
        {
            if (debugOrientation)
            {
                for (int i = 0; i < spawnedSRs.Length; i++) Debug.Log($"[AirSlash]  child #{i} '{spawnedSRs[i].name}' flipX_before_fix={spawnedSRs[i].flipX}");
            }

            for (int i = 0; i < spawnedSRs.Length; i++) spawnedSRs[i].flipX = shouldFlip;

            if (debugOrientation)
            {
                for (int i = 0; i < spawnedSRs.Length; i++) Debug.Log($"[AirSlash]  child #{i} '{spawnedSRs[i].name}' flipX_after_fix={spawnedSRs[i].flipX}");
            }
        }

        Vector3 rootScale = spawnedContainer.transform.localScale;
        rootScale.x = Mathf.Abs(rootScale.x) * (desiredFacingRight ? 1f : -1f);
        spawnedContainer.transform.localScale = rootScale;

        if (debugOrientation) Debug.Log($"[AirSlash]  rootScale_after_fix={spawnedContainer.transform.localScale.x}");
    }
}
