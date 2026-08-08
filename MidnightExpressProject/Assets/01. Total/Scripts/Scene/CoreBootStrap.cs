#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoreBootStrap
{
    private const string BootstrapSceneName = "BootstrapScene";
    private const string RequestedNameKey = "MidnightExpress.RequestedStartSceneName";
    private const string RequestedPathKey = "MidnightExpress.RequestedStartScenePath";

    public static string RequestedStartSceneName
    {
        get => SessionState.GetString(RequestedNameKey, string.Empty);
        private set => SessionState.SetString(RequestedNameKey, value);
    }

    public static string RequestedStartScenePath
    {
        get => SessionState.GetString(RequestedPathKey, string.Empty);
        private set => SessionState.SetString(RequestedPathKey, value);
    }

    static CoreBootStrap()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        RequestedStartSceneName = activeScene.name;
        RequestedStartScenePath = activeScene.path;

        if (!ToolbarPlayButtonsView.OnGetCoreMode)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var bootstrapPath = FindBootstrapScenePath();
        if (string.IsNullOrEmpty(bootstrapPath))
        {
            Debug.LogError(
                $"[CoreBootStrap] Enabled scene '{BootstrapSceneName}' was not found in Build Settings.");
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapPath);
        if (sceneAsset == null)
        {
            Debug.LogError($"[CoreBootStrap] Bootstrap scene asset was not found: {bootstrapPath}");
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;
        Debug.Log($"[CoreBootStrap] Starting from BootstrapScene. Requested scene: {RequestedStartSceneName}");
    }

    private static string FindBootstrapScenePath()
    {
        var scenes = EditorBuildSettings.scenes;
        for (var index = 0; index < scenes.Length; index++)
        {
            var scene = scenes[index];
            if (!scene.enabled)
            {
                continue;
            }

            if (string.Equals(
                    Path.GetFileNameWithoutExtension(scene.path),
                    BootstrapSceneName,
                    System.StringComparison.Ordinal))
            {
                if (index != 0)
                {
                    Debug.LogWarning("[CoreBootStrap] BootstrapScene should be Build Settings index 0.");
                }

                return scene.path;
            }
        }

        return string.Empty;
    }
}
#endif
