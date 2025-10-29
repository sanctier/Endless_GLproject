using UnityEngine;
using TMPro;
using System.Collections;

public class WaveUIManager : MonoBehaviour
{
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI enemyText;

    private Coroutine fadeCoroutine;
    private bool isWaveTextVisible = false;

    void Start()
    {
        // Subscribe to wave events
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted += OnWaveCompleted;
        }

        // Initially hide wave text
        SetWaveTextAlpha(0f);
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
        }
    }

    void Update()
    {
        if (WaveManager.Instance != null && WaveManager.Instance.IsWaveInProgress())
        {
            timeText.text = "Time Left: " + Mathf.Max(0, Mathf.CeilToInt(WaveManager.Instance.GetWaveTimeLeft())) + "s";
            enemyText.text = "Enemies: " + WaveManager.Instance.GetEnemiesRemaining();
        }
    }

    private void OnWaveStarted(int waveNumber)
    {
        // Update wave text
        waveText.text = "Wave " + waveNumber;

        // Show wave text and start fade out coroutine
        ShowWaveTextTemporarily();
    }

    private void OnWaveCompleted(int waveNumber)
    {
        // Optional: You can show "Wave Complete!" or similar here
        // waveText.text = "Wave " + waveNumber + " Complete!";
        // ShowWaveTextTemporarily();
    }

    private void ShowWaveTextTemporarily()
    {
        // Stop any existing fade coroutine
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // Start new fade coroutine
        fadeCoroutine = StartCoroutine(FadeWaveTextRoutine());
    }

    private IEnumerator FadeWaveTextRoutine()
    {
        // Fade in quickly (0.5 seconds)
        float fadeInDuration = 0.5f;
        float fadeInTimer = 0f;
        
        while (fadeInTimer < fadeInDuration)
        {
            fadeInTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, fadeInTimer / fadeInDuration);
            SetWaveTextAlpha(alpha);
            yield return null;
        }

        SetWaveTextAlpha(1f);
        isWaveTextVisible = true;

        // Wait for 2 seconds at full opacity
        yield return new WaitForSeconds(2f);

        // Fade out slowly (1 second)
        float fadeOutDuration = 1f;
        float fadeOutTimer = 0f;
        
        while (fadeOutTimer < fadeOutDuration)
        {
            fadeOutTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeOutTimer / fadeOutDuration);
            SetWaveTextAlpha(alpha);
            yield return null;
        }

        SetWaveTextAlpha(0f);
        isWaveTextVisible = false;
        fadeCoroutine = null;
    }

    private void SetWaveTextAlpha(float alpha)
    {
        Color color = waveText.color;
        color.a = alpha;
        waveText.color = color;
    }

    // Optional: Public method to manually show wave text
    public void ShowWaveText(string message, float displayTime = 2f)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        waveText.text = message;
        fadeCoroutine = StartCoroutine(FadeWaveTextRoutine());
    }

    // Optional: Force hide wave text
    public void HideWaveText()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        SetWaveTextAlpha(0f);
        isWaveTextVisible = false;
    }

    // Optional: Force show wave text
    public void ShowWaveTextPermanently(string message)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        waveText.text = message;
        SetWaveTextAlpha(1f);
        isWaveTextVisible = true;
    }
}