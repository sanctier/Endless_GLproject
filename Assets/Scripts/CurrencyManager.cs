using UnityEngine;
using TMPro;
using System.Collections;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    private int currentCurrency;
    public TextMeshProUGUI currencyText;

    public delegate void CurrencyChanged();
    public event CurrencyChanged OnCurrencyChanged;

    [Header("Currency Effects")]
    public GameObject currencyGainEffect;
    public AudioClip currencySound;

    [Header("Text Animation")]
    public float popAnimationDuration = 0.3f;
    public float popScaleAmount = 1.3f;
    private Vector3 originalTextScale;
    private Coroutine popAnimationCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Auto-find currency text if not assigned
        if (currencyText == null)
        {
            GameObject textObj = GameObject.Find("CurrencyText");
            if (textObj != null)
            {
                currencyText = textObj.GetComponent<TextMeshProUGUI>();
                if (currencyText == null)
                {
                    Debug.LogError("CurrencyText GameObject doesn't have TextMeshProUGUI component!");
                }
            }
            else
            {
                Debug.LogError("Could not find CurrencyText GameObject in scene!");
            }
        }

        // Store original scale for animation
        if (currencyText != null)
        {
            originalTextScale = currencyText.transform.localScale;
        }

        // Load saved currency
        currentCurrency = PlayerPrefs.GetInt("PlayerCurrency", 0);
        UpdateCurrencyDisplay();
    }

    public void AddCurrency(int amount)
    {
        currentCurrency += amount;
        PlayerPrefs.SetInt("PlayerCurrency", currentCurrency);
        PlayerPrefs.Save();
        UpdateCurrencyDisplay();
        OnCurrencyChanged?.Invoke();

        // Play pop animation
        PlayCurrencyPopAnimation();

        // Play visual and sound effects
        ShowCurrencyEffect(amount);
        PlayCurrencySound();

        // Update shop buttons if shop is open
        if (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null
            && ShopManager.Instance.shopPanel.activeSelf)
        {
            ShopManager.Instance.UpdateAllShopItemButtons();
        }

        Debug.Log($"[AddCurrency/SpendCurrency] Gold is now: {currentCurrency}");
    }

    public bool SpendCurrency(int amount)
    {
        if (currentCurrency >= amount)
        {
            currentCurrency -= amount;
            PlayerPrefs.SetInt("PlayerCurrency", currentCurrency);
            PlayerPrefs.Save();
            UpdateCurrencyDisplay();
            OnCurrencyChanged?.Invoke();

            // Play pop animation for spending too
            PlayCurrencyPopAnimation();

            // Update shop buttons if shop is open
            if (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null
                && ShopManager.Instance.shopPanel.activeSelf)
            {
                ShopManager.Instance.UpdateAllShopItemButtons();
            }

            Debug.Log($"[AddCurrency/SpendCurrency] Gold is now: {currentCurrency}");
            return true;
        }
        Debug.Log($"[AddCurrency/SpendCurrency] Gold is now: {currentCurrency}");
        return false;
    }

    private void PlayCurrencyPopAnimation()
    {
        if (currencyText == null) return;

        // Stop any existing animation
        if (popAnimationCoroutine != null)
        {
            StopCoroutine(popAnimationCoroutine);
        }

        // Start new animation
        popAnimationCoroutine = StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        float timer = 0f;
        Vector3 startScale = originalTextScale;
        Vector3 targetScale = originalTextScale * popScaleAmount;

        // Grow to target scale
        while (timer < popAnimationDuration / 3f)
        {
            timer += Time.deltaTime;
            float progress = timer / (popAnimationDuration / 3f);
            currencyText.transform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        // Bounce back with overshoot
        timer = 0f;
        Vector3 overshootScale = targetScale * 1.1f;
        while (timer < popAnimationDuration / 3f)
        {
            timer += Time.deltaTime;
            float progress = timer / (popAnimationDuration / 3f);
            currencyText.transform.localScale = Vector3.Lerp(targetScale, overshootScale, progress);
            yield return null;
        }

        // Return to original scale - SLOWER (twice as long as other phases)
        timer = 0f;
        while (timer < popAnimationDuration * 0.66f) // 2/3 of total time
        {
            timer += Time.deltaTime;
            float progress = timer / (popAnimationDuration * 0.66f);
            currencyText.transform.localScale = Vector3.Lerp(overshootScale, originalTextScale, progress);
            yield return null;
        }

        currencyText.transform.localScale = originalTextScale;
        popAnimationCoroutine = null;
    }

    public bool CanAfford(int amount)
    {
        return currentCurrency >= amount;
    }

    public int GetCurrentCurrency()
    {
        return currentCurrency;
    }

    public void ResetCurrency()
    {
        currentCurrency = 0;
        PlayerPrefs.SetInt("PlayerCurrency", 0);
        PlayerPrefs.Save();
        UpdateCurrencyDisplay();
    }

    void UpdateCurrencyDisplay()
    {
        if (currencyText != null)
        {
            currencyText.text = $"<color=#FFD700>Gold: {currentCurrency}</color>";

            // Optional: Add animation when currency changes
            if (currencyText.GetComponent<Animator>() != null)
            {
                currencyText.GetComponent<Animator>().Play("CurrencyPulse");
            }
        }
    }

    void ShowCurrencyEffect(int amount)
    {
        if (currencyGainEffect != null && currencyText != null)
        {
            GameObject effect = Instantiate(currencyGainEffect, currencyText.transform);
            TextMeshProUGUI effectText = effect.GetComponentInChildren<TextMeshProUGUI>();

            if (effectText != null)
            {
                effectText.text = $"+{amount}";
            }

            Destroy(effect, 2f);
        }
    }

    void PlayCurrencySound()
    {
        if (currencySound != null)
        {
            AudioSource.PlayClipAtPoint(currencySound, Camera.main.transform.position, 0.5f);
        }
    }

    public void SaveCurrency()
    {
        PlayerPrefs.SetInt("PlayerCurrency", currentCurrency);
        PlayerPrefs.Save();
    }

    public void LoadCurrency()
    {
        currentCurrency = PlayerPrefs.GetInt("PlayerCurrency", 0);
        UpdateCurrencyDisplay();
    }

    public void OnGameOver()
    {
        currentCurrency = Mathf.FloorToInt(currentCurrency * 0.5f);
        SaveCurrency();
        UpdateCurrencyDisplay();
    }
}