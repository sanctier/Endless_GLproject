using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Simple settings menu helper: pause/resume and quit the game.
/// - Assign a `settingsPanel` GameObject (UI panel) to toggle visibility.
/// - Optionally assign `resumeButton` and `quitButton` to wire automatic listeners.
/// - Call `ToggleSettings()` from your input handling to open/close the menu.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject settingsPanel; // panel to show/hide when settings open
    public Button resumeButton;      // optional: button that resumes the game
    public Button quitButton;        // optional: button that quits the game
    public Slider masterVolumeSlider; // optional: master volume slider (0-1)

    [Header("Behavior")]
    public bool pauseOnOpen = true;  // pause when settings open

    private bool isPaused = false;
    private const string MasterVolumePrefKey = "MasterVolume";

    void Start()
    {
        // start with panel hidden
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // wire up buttons if provided
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // load saved master volume (fallback to current AudioListener.volume)
        float savedVol = PlayerPrefs.GetFloat(MasterVolumePrefKey, AudioListener.volume);
        AudioListener.volume = Mathf.Clamp01(savedVol);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = AudioListener.volume;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }
    }

    void Update()
    {
        // Toggle settings panel with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettings();
        }
    }

    /// <summary>
    /// Toggle the settings menu (open if closed, close if open).
    /// </summary>
    public void ToggleSettings()
    {
        if (isPaused)
            ResumeGame();
        else
            OpenSettings();
    }

    /// <summary>
    /// Open the settings UI and optionally pause the game.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (pauseOnOpen)
            PauseGame();

        // refresh slider to reflect current volume
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = AudioListener.volume;
    }

    /// <summary>
    /// Pause the game by freezing time and pausing audio.
    /// </summary>
    public void PauseGame()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;
        isPaused = true;

        // show cursor in case it was hidden/locked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Resume gameplay and hide settings UI.
    /// </summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>
    /// Quit the game. In the editor this stops play mode.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // cleanup listeners to avoid memory leaks
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
    }

    /// <summary>
    /// Called by the UI slider to change the master volume.
    /// </summary>
    public void OnMasterVolumeChanged(float value)
    {
        float v = Mathf.Clamp01(value);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(MasterVolumePrefKey, v);
        PlayerPrefs.Save();
    }
}
