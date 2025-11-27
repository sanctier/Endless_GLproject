using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Shop Settings")]
    public List<ShopItem> shopItems;
    public GameObject shopItemUIPrefab;
    public Transform shopItemsContainer;
    public GameObject shopPanel;

    [Header("Permanent Upgrades")]
    public GameObject spinningFireballPrefab;
    public GameObject periodicSwordPrefab;

    [Header("Audio")]
    public AudioClip uiClickClip;
    public AudioClip purchaseClip;
    public AudioClip purchaseFailClip;
    public AudioClip shopOpenClip;
    public AudioClip shopCloseClip;
    [Range(0f,1f)] public float uiClickVolume = 1f;
    [Range(0f,1f)] public float purchaseVolume = 1f;
    [Range(0f,1f)] public float shopOpenVolume = 1f;
    [Range(0f,1f)] public float shopCloseVolume = 1f;
    private AudioSource audioSource;



    private List<GameObject> activeUpgrades = new List<GameObject>();
    private int fireballCount = 0;
    // Whether the boss has been defeated (unlocks certain shop items)
    public bool bossDefeated = false;

    // Called by enemies (e.g., BossBandit) when the boss is killed
    public void NotifyBossDefeated()
    {
        if (bossDefeated) return;
        bossDefeated = true;
        Debug.Log("ShopManager: Boss defeated — unlocking boss-locked shop items.");
        UpdateAllShopItemButtons();
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
            //Destroy(gameObject);
        }
        PlayerPrefs.DeleteAll();

        // ensure AudioSource available for UI sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // UI sounds as 2D
        }

    }

    void Start()
    {
        if (shopPanel == null)
        {
            shopPanel = FindShopPanel();
            if (shopPanel == null) Debug.LogError("Could not find ShopPanel GameObject at Start!");
        }

        if (shopItemsContainer == null && shopPanel != null)
        {
            shopItemsContainer = shopPanel.transform;
        }

        if (shopItemUIPrefab == null)
        {
            shopItemUIPrefab = Resources.Load<GameObject>("ShopItemUI");
            if (shopItemUIPrefab == null) Debug.LogError("Could not load ShopItemUI prefab from Resources!");
        }

        InitializeShop();
        LoadPurchasedItems();

        // if (WaveManager.Instance != null)
        // {
        //     WaveManager.Instance.OnWaveCompleted += OpenShop;
        //     WaveManager.Instance.OnWaveStarted += CloseShop;
        // }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("B key pressed - attempting to toggle shop");
            ToggleShop();
        }
    }

    GameObject FindShopPanel()
    {
        // Try direct find
        var foundPanel = GameObject.Find("ShopPanel");
        if (foundPanel != null)
            return foundPanel;

        // Try find under Canvas (handles DontDestroyOnLoad Canvas)
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            var shopPanelTransform = canvas.transform.Find("ShopPanel");
            if (shopPanelTransform != null)
                return shopPanelTransform.gameObject;
        }

        // Try ANY active ShopPanel in the scene
        var panels = GameObject.FindObjectsOfType<Canvas>(true);
        foreach (var canv in panels)
        {
            var sp = canv.transform.Find("ShopPanel");
            if (sp != null)
                return sp.gameObject;
        }

        return null;
    }

    public void UpdateAllShopItemButtons()
    {
        if (shopItemsContainer == null) return;
        foreach (Transform child in shopItemsContainer)
        {
            ShopItemUI ui = child.GetComponent<ShopItemUI>();
            if (ui != null)
                ui.UpdateButtonState();
        }
    }

    // Return true if the given upgrade type has been purchased (upgradeLevel > 0)
    public bool IsPurchased(ShopItem.UpgradeType type)
    {
        if (shopItems == null) return false;
        foreach (var si in shopItems)
        {
            if (!si.consumable && si.upgradeType == type)
                return si.upgradeLevel > 0;
        }
        return false;
    }


    void InitializeShop()
    {
        if (shopItemsContainer == null && shopPanel != null)
        {
            shopItemsContainer = shopPanel.transform;
        }

        if (shopItemsContainer == null)
        {
            Debug.LogError("ShopItemsContainer is not assigned!");
            return;
        }

        if (shopItemUIPrefab == null)
        {
            Debug.LogError("ShopItemUIPrefab is not assigned!");
            return;
        }

        // Clear existing items
        foreach (Transform child in shopItemsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create shop items
        // Ensure AirSlash items are boss-locked by default
        foreach (ShopItem si in shopItems)
        {
            if (!si.consumable && si.upgradeType == ShopItem.UpgradeType.AirSlash)
                si.requiresBossDefeated = true;
        }

        foreach (ShopItem item in shopItems)
        {
            GameObject itemUI = Instantiate(shopItemUIPrefab, shopItemsContainer);
            ShopItemUI shopItemUI = itemUI.GetComponent<ShopItemUI>();
            if (shopItemUI != null)
            {
                shopItemUI.Setup(item);
            }
            else
            {
                Debug.LogError("ShopItemUI prefab doesn't have ShopItemUI component!");
            }
        }
    }

    void ApplyItemEffect(ShopItem item)
    {
        Debug.Log($"ApplyItemEffect called for item='{item.itemName}' type={item.upgradeType} consumable={item.consumable} upgradeValue={item.upgradeValue} upgradeLevel={item.upgradeLevel}");

        // Consumable items: handle by itemName first, then fallback to upgradeType
        if (item.consumable)
        {
            switch (item.itemName)
            {
                case "Health Potion":
                    if (PlayerController.Instance != null)
                        PlayerController.Instance.Heal(50);
                    break;

                case "Temporary Damage Boost":
                    if (PlayerController.Instance != null)
                        PlayerController.Instance.AddTemporaryDamageBoost(10, 30f);
                    break;

                case "Health Boost":
                    if (PlayerController.Instance != null)
                    {
                        float consumableHeal = (item.upgradeValue > 0f) ? item.upgradeValue : 5f;
                        Debug.Log($"ShopManager: Applying consumable Health Boost heal={consumableHeal}");
                        PlayerController.Instance.Heal(consumableHeal);
                    }
                    break;

                // other consumable names fall through
            }

            // Fallback by upgradeType for consumable health items
            // Determine heal amount with a name-based fallback for misconfigured items
            string nameLower = (item.itemName != null) ? item.itemName.ToLower() : "";
            bool nameIndicatesBig = nameLower.Contains("mega") || nameLower.Contains("big") || nameLower.Contains("mega health") || nameLower.Contains("mega boost");

            if (item.upgradeType == ShopItem.UpgradeType.BigHealthBoost || (item.upgradeType == ShopItem.UpgradeType.HealthBoost && nameIndicatesBig))
            {
                float fallbackHeal = (item.upgradeValue > 0f) ? item.upgradeValue : 50f; // use inspector value when present
                Debug.Log($"ShopManager: Fallback consumable BigHealthBoost heal={fallbackHeal} (nameIndicatesBig={nameIndicatesBig})");
                if (PlayerController.Instance != null) PlayerController.Instance.Heal(fallbackHeal);
            }
            else if (item.upgradeType == ShopItem.UpgradeType.HealthBoost)
            {
                float fallbackHeal = (item.upgradeValue > 0f) ? item.upgradeValue : 5f;
                Debug.Log($"ShopManager: Fallback consumable HealthBoost heal={fallbackHeal}");
                if (PlayerController.Instance != null) PlayerController.Instance.Heal(fallbackHeal);
            }

            return;
        }

        // Non-consumable (permanent) items: require a valid player instance
        if (PlayerController.Instance == null)
        {
            Debug.LogError("PlayerController instance not found!");
            return;
        }

        switch (item.upgradeType)
        {
            case ShopItem.UpgradeType.SpinningFireball:
                UpgradeFireball(item.upgradeLevel);
                break;

            case ShopItem.UpgradeType.PeriodicSword:
                ActivatePeriodicSword();
                break;

            case ShopItem.UpgradeType.AirSlash:
                ActivateAirSlash();
                break;

            case ShopItem.UpgradeType.HealthBoost:
                // Treat permanent HealthBoost purchase as an immediate small heal.
                // Prefer inspector `upgradeValue` when present (supports fractional heals).
                PlayerController.Instance.Heal((item.upgradeValue > 0f) ? item.upgradeValue : 5f);
                break;

            case ShopItem.UpgradeType.BigHealthBoost:
                // Treat BigHealthBoost as a larger heal (prefer inspector value).
                PlayerController.Instance.Heal((item.upgradeValue > 0f) ? item.upgradeValue : 50f);
                break;

            case ShopItem.UpgradeType.DamageBoost:
                PlayerController.Instance.AddPermanentDamageBoost(item.upgradeValue);
                break;

            case ShopItem.UpgradeType.SpeedBoost:
                PlayerController.Instance.AddSpeedBoost(item.upgradeValue);
                break;
        }
    }

    void UpgradeFireball(int level)
    {
        if (level == 1) // First purchase
        {
            ActivateSpinningFireball();
        }
        else if (level == 2) // Second purchase
        {
            ActivateSecondFireball();
        }
    }

    void UpdateAllFireballsCount()
    {
        int count = 0;
        foreach (GameObject upgrade in activeUpgrades)
        {
            if (upgrade != null && upgrade.GetComponent<SpinningFireball2D>() != null)
            {
                count++;
            }
        }

        int index = 0;
        foreach (GameObject upgrade in activeUpgrades)
        {
            var fireball = upgrade?.GetComponent<SpinningFireball2D>();
            if (fireball != null)
            {
                fireball.SetFireballIndex(index, count);
                index++;
            }
        }
    }



    void ActivateSpinningFireball()
    {
        if (PlayerController.Instance != null && spinningFireballPrefab != null)
        {
            GameObject fireball = Instantiate(spinningFireballPrefab);
            SpinningFireball2D fireballScript = fireball.GetComponent<SpinningFireball2D>();
            if (fireballScript != null)
            {
                // No SetAsFirstFireball() call needed
                activeUpgrades.Add(fireball);
                UpdateAllFireballsCount();
            }
        }
    }

    void ActivateSecondFireball()
    {
        if (PlayerController.Instance != null && spinningFireballPrefab != null)
        {
            GameObject fireball = Instantiate(spinningFireballPrefab);
            SpinningFireball2D fireballScript = fireball.GetComponent<SpinningFireball2D>();
            if (fireballScript != null)
            {
                // No SetAsSecondFireball() call needed
                activeUpgrades.Add(fireball);
                UpdateAllFireballsCount();
            }
        }
    }


    void ActivatePeriodicSword()
    {
        if (PlayerController.Instance == null || periodicSwordPrefab == null) return;

        // If an instance already exists on the player, activate it; otherwise instantiate and activate.
        var existing = PlayerController.Instance.GetComponentInChildren<PeriodicSword>();
        if (existing != null)
        {
            Debug.Log("ActivatePeriodicSword: Activating existing PeriodicSword on player.");
            existing.Activate();
            return;
        }

        GameObject sword = Instantiate(periodicSwordPrefab, PlayerController.Instance.transform);
        sword.transform.localPosition = Vector3.zero;
        var controller = sword.GetComponent<PeriodicSword>();
        if (controller != null)
        {
            controller.Activate();
        }
        activeUpgrades.Add(sword);
    }

    void ActivateAirSlash()
    {
        if (PlayerController.Instance == null) return;

        var existing = PlayerController.Instance.GetComponent<AirSlash>();
        if (existing != null)
        {
            existing.enabled = true;
            PlayerController.Instance.airSlash = existing;
            Debug.Log("ActivateAirSlash: Enabled AirSlash on player.");
            return;
        }

        Debug.LogWarning("ActivateAirSlash: AirSlash component not found on player. Please add AirSlash to the player GameObject.");
    }


    public bool TryBuyItem(ShopItem item)
    {
        Debug.Log($"TryBuyItem: attempting purchase '{item.itemName}' cost={item.currentCost}");
        Debug.Log($"TryBuyItem DETAILS: consumable={item.consumable} upgradeType={item.upgradeType} upgradeValue={item.upgradeValue} upgradeLevel={item.upgradeLevel}");

        if (!item.consumable && item.upgradeLevel >= item.maxUpgradeLevel)
        {
            Debug.Log("TryBuyItem: Already at max upgrade level!");
            return false;
        }

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("TryBuyItem: CurrencyManager.Instance is null");
            return false;
        }

        int currentCurrency = CurrencyManager.Instance.GetCurrentCurrency();
        Debug.Log($"TryBuyItem: currentCurrency={currentCurrency}, cost={item.currentCost}");

        bool spent = false;
        try
        {
            spent = CurrencyManager.Instance.SpendCurrency(item.currentCost);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            spent = false;
        }

        if (spent)
        {
            // Upgrade the item level
            item.Upgrade();
            ApplyItemEffect(item);
            SavePurchasedItems();

            Debug.Log($"Purchased: {item.itemName} (Level {item.upgradeLevel})");
            UpdateAllShopItemButtons();
            // play purchase sound
            if (purchaseClip != null && audioSource != null)
            {
                PlayOneShotExclusive(purchaseClip, purchaseVolume);
            }
            return true;
        }

        Debug.Log($"TryBuyItem: Not enough currency or SpendCurrency returned false. spent={spent}");
        // play fail sound
        if (purchaseFailClip != null && audioSource != null)
        {
            PlayOneShotExclusive(purchaseFailClip, purchaseVolume);
        }
        return false;
    }



    void SavePurchasedItems()
    {
        foreach (ShopItem item in shopItems)
        {
            if (!item.consumable)
            {
                PlayerPrefs.SetInt($"SHOP_ITEM_{item.upgradeType}_LEVEL", item.upgradeLevel);
                PlayerPrefs.SetInt($"SHOP_ITEM_{item.upgradeType}_COST", item.currentCost);
            }
        }
        PlayerPrefs.Save();
    }

    void LoadPurchasedItems()
    {
        foreach (ShopItem item in shopItems)
        {
            if (!item.consumable)
            {
                item.upgradeLevel = PlayerPrefs.GetInt($"SHOP_ITEM_{item.upgradeType}_LEVEL", 0);
                item.currentCost = PlayerPrefs.GetInt($"SHOP_ITEM_{item.upgradeType}_COST", item.baseCost);

                if (item.upgradeLevel > 0)
                {
                    // Apply permanent upgrades on load EXCEPT health boosts.
                    // Health boosts are intended to act as heals at purchase time
                    // and should NOT re-heal the player when loading saved data.
                    if (item.upgradeType != ShopItem.UpgradeType.HealthBoost && item.upgradeType != ShopItem.UpgradeType.BigHealthBoost)
                    {
                        for (int i = 1; i <= item.upgradeLevel; i++)
                        {
                            ApplyItemEffect(item);
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// Call this when the player dies or starts a new game to clear all purchased upgrades.
    /// </summary>
    public void ResetAllUpgrades()
    {
        // Remove any active upgrades from the player
        foreach (GameObject upgrade in activeUpgrades)
        {
            if (upgrade != null) Destroy(upgrade);
        }
        activeUpgrades.Clear();

        // Reset the purchased state and PlayerPrefs for each upgrade
        foreach (ShopItem item in shopItems)
        {
            if (!item.consumable)
            {

                PlayerPrefs.DeleteKey("SHOP_ITEM_" + item.itemName);
            }
        }
        PlayerPrefs.Save();

        InitializeShop(); // Optional: update UI to reflect reset
    }

    public void ToggleShop()
    {
        if (shopPanel == null)
            shopPanel = FindShopPanel();

        if (shopPanel != null)
        {
            bool newState = !shopPanel.activeSelf;
            Debug.Log($"Toggling shop panel from {shopPanel.activeSelf} to {newState}");
            shopPanel.SetActive(newState);
            Time.timeScale = newState ? 0f : 1f;

            // play open/close audio
            if (newState)
            {
                if (shopOpenClip != null && audioSource != null)
                    PlayOneShotExclusive(shopOpenClip, shopOpenVolume);
            }
            else
            {
                if (shopCloseClip != null && audioSource != null)
                    PlayOneShotExclusive(shopCloseClip, shopCloseVolume);
            }

            // Disable player attacks while shop is open
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetCanAttack(!newState);

            // ADD THIS LINE - Update button states when opening shop
            if (newState) UpdateAllShopItemButtons();
        }
        else
        {
            Debug.LogError("ToggleShop: shopPanel is still null after FindShopPanel!");
        }
    }

    public void OpenShop(int waveNumber)
    {
        if (shopPanel == null)
            shopPanel = FindShopPanel();

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            Time.timeScale = 0f;
            InitializeShop();
            UpdateAllShopItemButtons(); // ADD THIS LINE
            // Disable player attacks while shop is open
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetCanAttack(false);

            // play open sound
            if (shopOpenClip != null && audioSource != null)
                PlayOneShotExclusive(shopOpenClip, shopOpenVolume);
        }
        else
        {
            Debug.LogError("OpenShop: shopPanel is still null after FindShopPanel!");
        }
    }
    public void CloseShop(int waveNumber)
    {
        if (shopPanel == null)
            shopPanel = FindShopPanel();

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
            Time.timeScale = 1f;
            // Re-enable player attacks when closing shop
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetCanAttack(true);

            // play close sound
            if (shopCloseClip != null && audioSource != null)
                PlayOneShotExclusive(shopCloseClip, shopCloseVolume);
        }
        else
        {
            Debug.LogError("CloseShop: shopPanel is still null after FindShopPanel!");
        }
    }


    public void SetShopReferences(GameObject panel, Transform container, GameObject prefab)
    {
        shopPanel = panel;
        shopItemsContainer = container;
        shopItemUIPrefab = prefab;
    }

    // Stop any current sound and play a UI clip so sounds don't overlap.
    void PlayOneShotExclusive(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        try { audioSource.Stop(); } catch { }
        audioSource.PlayOneShot(clip, volume);
    }

    // Called by UI when buy button is pressed to play the click sound immediately.
    public void PlayUIClick()
    {
        if (uiClickClip != null && audioSource != null)
            PlayOneShotExclusive(uiClickClip, uiClickVolume);
    }



}
