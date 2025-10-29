using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
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
        ShopManager.Instance.TryBuyItem(currentItem);
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


}
