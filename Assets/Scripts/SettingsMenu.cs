using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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
    [Tooltip("Keyboard key to toggle settings (Escape by default)")]
    public KeyCode toggleKey = KeyCode.Escape;
    [Tooltip("Scene name to return to when 'Back to Start' is invoked")]
    public string startSceneName = "StartScene";

    private bool isPaused = false;
    private const string MasterVolumePrefKey = "MasterVolume";
    // keep a single active SettingsMenu instance to avoid multiple components responding to input
    private static SettingsMenu ActiveInstance;
    private bool wired = false;

    void Start()
    {
        // detect duplicates
        int found = FindObjectsOfType<SettingsMenu>().Length;
        Debug.Log($"SettingsMenu: Start called on '{gameObject.name}'. Instances in scene={found}. GameObject active={gameObject.activeInHierarchy}, enabled={enabled}");

        if (settingsPanel == null)
        {
            Debug.LogWarning("SettingsMenu: settingsPanel is not assigned in inspector. Toggle will do nothing.");
        }

        // Choose ActiveInstance: prefer an instance that has a valid settingsPanel assigned
        if (ActiveInstance == null)
        {
            if (this.settingsPanel != null)
            {
                ActiveInstance = this;
            }
            else
            {
                // try to find any existing SettingsMenu in scene with settingsPanel assigned
                var all = FindObjectsOfType<SettingsMenu>();
                foreach (var sm in all)
                {
                    if (sm != this && sm.settingsPanel != null)
                    {
                        ActiveInstance = sm;
                        break;
                    }
                }
                if (ActiveInstance == null)
                    ActiveInstance = this;
            }
        }

        if (ActiveInstance != this)
        {
            Debug.LogWarning($"SettingsMenu: Another active SettingsMenu ('{ActiveInstance.gameObject.name}') exists. Disabling toggle behavior on '{gameObject.name}'.");
            enabled = false; // disable this component so it won't respond to input
            return;
        }

        // start with panel hidden
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // wire up buttons if provided
        EnsureWired(settingsPanel);

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

    /// <summary>
    /// Ensure the UI references (buttons / slider) are assigned and wired. Accepts the panel GameObject
    /// even if it's inactive. Safe to call multiple times.
    /// </summary>
    public void EnsureWired(GameObject panel)
    {
        if (wired) return;
        // If fields are not assigned in inspector, try to find them under the provided panel
        if (panel != null)
        {
            if (resumeButton == null)
            {
                // try to find by common names first
                var btns = panel.GetComponentsInChildren<Button>(true);
                foreach (var b in btns)
                {
                    var name = b.gameObject.name.ToLower();
                    if (name.Contains("resume") || name.Contains("continue") || name.Contains("close"))
                    {
                        resumeButton = b; break;
                    }
                }
                if (resumeButton == null && btns.Length > 0) resumeButton = btns[0];
            }

            if (quitButton == null)
            {
                var btns = panel.GetComponentsInChildren<Button>(true);
                foreach (var b in btns)
                {
                    var name = b.gameObject.name.ToLower();
                    if (name.Contains("quit") || name.Contains("exit") || name.Contains("back"))
                    {
                        quitButton = b; break;
                    }
                }
                // if quit not found, try last button
                if (quitButton == null && btns.Length > 1) quitButton = btns[btns.Length - 1];
            }

            if (masterVolumeSlider == null)
            {
                var sliders = panel.GetComponentsInChildren<Slider>(true);
                foreach (var s in sliders)
                {
                    var name = s.gameObject.name.ToLower();
                    if (name.Contains("volume") || name.Contains("master") || name.Contains("sound"))
                    {
                        masterVolumeSlider = s; break;
                    }
                }
                if (masterVolumeSlider == null && sliders.Length > 0) masterVolumeSlider = sliders[0];
            }
        }

        // Wire listeners (avoid duplicate wiring)
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = AudioListener.volume;
        }

        wired = true;
    }

    void Update()
    {
        // Toggle settings panel with configured key. Also support legacy Input "Cancel" button.
        bool pressed = false;
        if (Input.GetKeyDown(toggleKey)) pressed = true;
        try
        {
            if (!pressed && Input.GetButtonDown("Cancel")) pressed = true;
        }
        catch { /* Input button may not be defined — ignore */ }

        if (pressed)
        {
            Debug.Log("SettingsMenu: toggle key pressed");
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

    /// <summary>
    /// Resume and load the Start scene. Useful for a "Back to Start" button in the settings.
    /// </summary>
    public void BackToStart()
    {
        // ensure game is resumed so timeScale and audio are restored
        ResumeGame();

        if (string.IsNullOrEmpty(startSceneName))
        {
            Debug.LogError("SettingsMenu: startSceneName is empty. Cannot load Start scene.");
            return;
        }

        SceneManager.LoadScene(startSceneName);
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
