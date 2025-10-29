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



    private List<GameObject> activeUpgrades = new List<GameObject>();
    private int fireballCount = 0;

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
                        PlayerController.Instance.Heal(5);
                    break;
            }
        }
        else
        {
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

                case ShopItem.UpgradeType.HealthBoost:

                    if (PlayerController.Instance != null)
                    {
                        PlayerController.Instance.IncreaseMaxHealth((int)item.upgradeValue);
                        PlayerController.Instance.Heal((int)item.upgradeValue);
                    }
                    break;
                case ShopItem.UpgradeType.DamageBoost:
                    PlayerController.Instance.AddPermanentDamageBoost(item.upgradeValue);
                    break;

                case ShopItem.UpgradeType.SpeedBoost:
                    PlayerController.Instance.AddSpeedBoost(item.upgradeValue);
                    break;
            }
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
        if (PlayerController.Instance != null && periodicSwordPrefab != null)
        {
            GameObject sword = Instantiate(periodicSwordPrefab, PlayerController.Instance.transform);
            sword.transform.localPosition = Vector3.zero;
            activeUpgrades.Add(sword);
        }
    }


    public bool TryBuyItem(ShopItem item)
    {
        if (!item.consumable && item.upgradeLevel >= item.maxUpgradeLevel)
        {
            Debug.Log("Already at max upgrade level!");
            return false;
        }

        if (CurrencyManager.Instance.SpendCurrency(item.currentCost))
        {
            // Upgrade the item level
            item.Upgrade();
            ApplyItemEffect(item);
            SavePurchasedItems();

            Debug.Log($"Purchased: {item.itemName} (Level {item.upgradeLevel})");
            UpdateAllShopItemButtons();
            return true;
        }

        Debug.Log("Not enough currency!");
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
                    // Apply all upgrades up to the current level
                    for (int i = 1; i <= item.upgradeLevel; i++)
                    {
                        ApplyItemEffect(item);
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



}
