using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Minimal helper for the Catalogue screen. Provides a method to return
/// to the Start scene and a utility to load by build index.
/// Wire `BackToStart()` to the Catalogue's Back button.
/// </summary>
public class CatalogueScene : MonoBehaviour
{
    [Tooltip("Name of the Start scene to return to")]
    public string startSceneName = "StartScene";

    public void BackToStart()
    {
        if (string.IsNullOrEmpty(startSceneName))
        {
            Debug.LogError("CatalogueScene: startSceneName is empty. Cannot load Start scene.");
            return;
        }
        SceneManager.LoadScene(startSceneName);
    }

    /// <summary>
    /// Optional helper: load a scene by build index.
    /// </summary>
    public void LoadSceneByIndex(int index)
    {
        int max = SceneManager.sceneCountInBuildSettings;
        if (index < 0 || index >= max)
        {
            Debug.LogError($"CatalogueScene: invalid build index {index} (0..{max - 1})");
            return;
        }
        SceneManager.LoadScene(index);
    }
}
