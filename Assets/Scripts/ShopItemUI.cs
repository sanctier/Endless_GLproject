using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopItemUI : MonoBehaviour, IPointerClickHandler
{
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI descriptionText;
    public Button buyButton;
    public TextMeshProUGUI buttonText;

    private ShopItem currentItem;

    public void Setup(ShopItem item)
    {
        currentItem = item;
        RefreshTexts();
        itemIcon.sprite = item.icon;
        itemNameText.text = item.itemName;
        costText.text = item.currentCost.ToString();
        descriptionText.text = item.description;

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyButtonClick);

        UpdateButtonState();
    }


    public void UpdateButtonState()
    {
        RefreshTexts();
        Debug.Log($"[UpdateButtonState] {currentItem.itemName} | Level: {currentItem.upgradeLevel}/{currentItem.maxUpgradeLevel} | Cost: {currentItem.currentCost}");

        if (!currentItem.consumable && currentItem.upgradeLevel >= currentItem.maxUpgradeLevel)
        {
            buyButton.interactable = false;
            buttonText.text = "Maxed";
            costText.text = "";
        }
        else if (!currentItem.consumable && currentItem.upgradeLevel > 0)
        {
            bool canAfford = CurrencyManager.Instance.GetCurrentCurrency() >= currentItem.currentCost;
            buyButton.interactable = canAfford;
            buttonText.text = "Upgrade";
            costText.text = currentItem.currentCost.ToString();
        }
        else
        {
            bool canAfford = CurrencyManager.Instance.GetCurrentCurrency() >= currentItem.currentCost;
            buyButton.interactable = canAfford;
            buttonText.text = "Buy";
            costText.text = currentItem.currentCost.ToString();
        }
    }


    void OnBuyButtonClick()
    {
        // play UI click sound immediately
        if (ShopManager.Instance != null)
            ShopManager.Instance.PlayUIClick();

        // Detailed debug logging to trace buy attempts
        Debug.Log($"OnBuyButtonClick: Trying to buy '{currentItem.itemName}' cost={currentItem.currentCost}");
        if (CurrencyManager.Instance != null)
            Debug.Log($"Current currency={CurrencyManager.Instance.GetCurrentCurrency()}");
        else
            Debug.Log("CurrencyManager.Instance is null");

        if (ShopManager.Instance == null)
        {
            Debug.LogError("ShopManager.Instance is null when trying to buy");
        }

        bool result = false;
        try
        {
            result = ShopManager.Instance.TryBuyItem(currentItem);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }

        Debug.Log($"TryBuyItem returned: {result}");
        UpdateButtonState();
    }

    void OnEnable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += UpdateButtonState;
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= UpdateButtonState;
    }

    void RefreshTexts()
    {
        itemIcon.sprite = currentItem.icon;
        itemNameText.text = currentItem.itemName;          // <— refresh name
        descriptionText.text = currentItem.description;    // <— refresh desc
        costText.text = currentItem.currentCost > 0 ? currentItem.currentCost.ToString() : "";
    }

    // Allow buying by left-clicking anywhere on the item UI
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnBuyButtonClick();
        }
    }


}
