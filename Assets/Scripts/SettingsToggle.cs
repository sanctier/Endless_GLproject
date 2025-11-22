using UnityEngine;

/// <summary>
/// Simple helper that can toggle a settings panel even if that panel is initially disabled.
/// Attach this to an always-active manager object (for example a GameManager or the EventSystem).
/// - Assign `settingsPanel` in the inspector (it may be inactive in the scene). 
/// - Optionally assign a `SettingsMenu` component to use its pause/resume helpers.
/// Press Escape to toggle the panel.
/// </summary>
public class SettingsToggle : MonoBehaviour
{
    [Tooltip("The settings panel GameObject to toggle. Can be inactive in the scene.")]
    public GameObject settingsPanel;

    [Tooltip("Optional: a SettingsMenu component to call PauseGame/ResumeGame on.")]
    public SettingsMenu settingsMenu;

    [Tooltip("Keyboard key used to toggle the settings panel (default 'P')")]
    public KeyCode toggleKey = KeyCode.P;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    /// <summary>
    /// Toggle visibility of settingsPanel and pause/resume the game.
    /// </summary>
    public void Toggle()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("SettingsToggle: settingsPanel is not assigned.");
            return;
        }

        // If no explicit SettingsMenu reference was provided, try to find one on the panel (works even if panel is inactive)
        if (settingsMenu == null && settingsPanel != null)
        {
            settingsMenu = settingsPanel.GetComponent<SettingsMenu>();
        }

        bool active = settingsPanel.activeSelf;

        if (active)
        {
            // hide
            settingsPanel.SetActive(false);
            if (settingsMenu != null)
                settingsMenu.ResumeGame();
            else
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }
        else
        {
            // show
            settingsPanel.SetActive(true);
            if (settingsMenu != null)
                settingsMenu.PauseGame();
            else
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
