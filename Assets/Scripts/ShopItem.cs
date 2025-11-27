using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ShopItem
{
    public string itemName;
    public int baseCost;
    public int currentCost;
    public int upgradeLevel = 0;
    public int maxUpgradeLevel = 1; // Default to 1, set individually per item
    public Sprite icon;
    public string description;
    public bool consumable = false;
    // If true, this item cannot be purchased until the boss bandit has been defeated
    public bool requiresBossDefeated = false;

    public enum UpgradeType { SpinningFireball, PeriodicSword, HealthBoost, DamageBoost, SpeedBoost, AirSlash, BigHealthBoost }
    public UpgradeType upgradeType;
    public float upgradeValue;

    // Arrays to store different costs, names, and descriptions for each upgrade level
    public int[] upgradeCosts;
    public string[] upgradeNames;
    public string[] upgradeDescriptions;
    public float[] upgradeValues;

    public bool disableAutoUpgrade = false;


    // Method to upgrade and get next cost
    public int Upgrade()
    {
        upgradeLevel++;

        // If we have predefined upgrade arrays, use them
        if (upgradeCosts != null && upgradeLevel - 1 < upgradeCosts.Length)
        {
            currentCost = upgradeCosts[upgradeLevel - 1];
        }
        else
        {
            if (!disableAutoUpgrade)

                // Default behavior for items without predefined upgrade paths
                switch (upgradeType)
                {
                    case UpgradeType.SpinningFireball:
                        if (upgradeLevel == 1)
                        {
                            currentCost = 200;
                            itemName = "Firespirit Level 2";
                            description = "Adds a second rotating firespirit";
                        }
                        else if (upgradeLevel >= maxUpgradeLevel)
                        {
                            currentCost = 0;
                            itemName = "Firespirit Max Level";
                            description = "Maximum Firespirit Upgrade";
                        }
                        break;

                    case UpgradeType.PeriodicSword:
                        if (upgradeLevel == 1)
                        {
                            currentCost = 150;
                            itemName = "Lingering Maxed";
                            description = "Maximum after-image Upgrade";
                            upgradeValue = 15f; // Example: increased damage
                        }
                        break;
                      

                    case UpgradeType.DamageBoost:
                        currentCost = baseCost + (upgradeLevel * 50); // Increasing cost
                        itemName = $"Damage Boost Level {upgradeLevel + 1}";
                        description = $"Permanently increases damage by {5 + (upgradeLevel * 2)}";
                        upgradeValue = 5 + (upgradeLevel * 2);
                        break;

                    // Health boost should increase by small increments (5 per purchase)
                    case UpgradeType.HealthBoost:
                        currentCost = baseCost + (upgradeLevel * 0);
                        upgradeValue = 2.5f; // +5 HP per purchase
                        itemName = $"Health Boost Level {upgradeLevel}";
                        description = $"Heals player by {upgradeValue}";
                        break;

                    case UpgradeType.BigHealthBoost:
                        currentCost = baseCost + (upgradeLevel * 0);
                        upgradeValue = 25f; // Big boost: +50 HP
                        itemName = $"Big Health Boost Level {upgradeLevel}";
                        description = $"Heals player by {upgradeValue}";
                        break;

                    case UpgradeType.SpeedBoost:
                        currentCost = baseCost + (upgradeLevel * 30);
                        itemName = $"Speed Boost Level {upgradeLevel + 1}";
                        description = $"Permanently increases speed by {0.5f + (upgradeLevel * 0.2f)}";
                        upgradeValue = 0.5f + (upgradeLevel * 0.2f);
                        break;
                }
            }

        

        // If we have predefined names and descriptions, use them
        if (upgradeNames != null && upgradeLevel - 1 < upgradeNames.Length)
        {
            itemName = upgradeNames[upgradeLevel - 1];
        }
        if (upgradeDescriptions != null && upgradeLevel - 1 < upgradeDescriptions.Length)
        {
            description = upgradeDescriptions[upgradeLevel - 1];
        }
        if (upgradeValues != null && upgradeLevel - 1 < upgradeValues.Length)
        {
            upgradeValue = upgradeValues[upgradeLevel - 1];
        }

        // Ensure sensible defaults for health-type upgrades in case no
        // upgradeValues were provided (common when using upgradeCosts arrays
        // or inspector-configured data). This guarantees HealthBoost always
        // yields +5 and BigHealthBoost yields +50 when purchased.
        if (upgradeValue == 0f)
        {
            switch (upgradeType)
            {
                case UpgradeType.HealthBoost:
                    upgradeValue = 5f;
                    break;
                case UpgradeType.BigHealthBoost:
                    upgradeValue = 50f;
                    break;
            }
        }

        return currentCost;
    }

    // Helper method to initialize upgrade arrays (can be called in inspector or code)
    public void InitializeUpgradePath(int[] costs, string[] names, string[] descriptions, float[] values)
    {
        upgradeCosts = costs;
        upgradeNames = names;
        upgradeDescriptions = descriptions;
        upgradeValues = values;
        maxUpgradeLevel = costs.Length;
    }
}