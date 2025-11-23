using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Simple start-scene helper that exposes UI-callable methods:
/// - `PlayGame()` loads the main gameplay scene.
/// - `OpenCatalogue()` opens the catalogue scene.
/// - `OpenControls()` opens the controls scene.
/// - `QuitGame()` quits the application (stops Play mode in editor).
/// Assign scene names in the inspector or call `LoadSceneByIndex(int)` if you prefer build-index loading.
/// </summary>
public class StartScene : MonoBehaviour
{
	[Tooltip("Scene name to load when Play is pressed")]
	public string playSceneName = "MainScene";
	[Tooltip("Scene name to open for the catalogue UI")]
	public string catalogueSceneName = "CatalogueScene";
	[Tooltip("Scene name to open for controls/instructions")]
	public string controlsSceneName = "ControlsScene";

	public void PlayGame()
	{
		if (string.IsNullOrEmpty(playSceneName))
		{
			Debug.LogError("StartScene: playSceneName is empty. Cannot load scene.");
			return;
		}
		SceneManager.LoadScene(playSceneName);
	}

	public void OpenCatalogue()
	{
		if (string.IsNullOrEmpty(catalogueSceneName))
		{
			Debug.LogError("StartScene: catalogueSceneName is empty. Cannot load scene.");
			return;
		}
		SceneManager.LoadScene(catalogueSceneName);
	}

	public void OpenControls()
	{
		if (string.IsNullOrEmpty(controlsSceneName))
		{
			Debug.LogError("StartScene: controlsSceneName is empty. Cannot load scene.");
			return;
		}
		SceneManager.LoadScene(controlsSceneName);
	}

	public void QuitGame()
	{
#if UNITY_EDITOR
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	/// <summary>
	/// Helper: load a scene by build index. Useful for wiring buttons that pass an index.
	/// </summary>
	public void LoadSceneByIndex(int index)
	{
		int max = SceneManager.sceneCountInBuildSettings;
		if (index < 0 || index >= max)
		{
			Debug.LogError($"StartScene: invalid build index {index} (0..{max - 1})");
			return;
		}
		SceneManager.LoadScene(index);
	}
}
