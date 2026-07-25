#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoreBootStrap
{
    public static string RequestedStartSceneName 
    { 
        get => SessionState.GetString("RequestedStartSceneName", ""); 
        private set => SessionState.SetString("RequestedStartSceneName", value); 
    }

    public static string RequestedStartScenePath 
    { 
        get => SessionState.GetString("RequestedStartScenePath", ""); 
        private set => SessionState.SetString("RequestedStartScenePath", value); 
    }

    public const string playFromBaseKey = "CoreBootStrap.PlayFromBaseScene";

    static CoreBootStrap()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;
        

        RequestedStartSceneName = EditorSceneManager.GetActiveScene().name;
        RequestedStartScenePath = EditorSceneManager.GetActiveScene().path;
        Debug.Log(RequestedStartSceneName);

        if(!ToolbarPlayButtonsView.OnGetCoreMode)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        // 체크박스 ON이면: 기존 로직

        var pathOfFirstScene = EditorBuildSettings.scenes[0].path;
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathOfFirstScene);

        if (sceneAsset == null)
        {
            Debug.LogError($"[CoreBootStrap] BuildSettings 0 scene not found: {pathOfFirstScene}");
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;
        Debug.Log($"[CoreBootStrap] ON -> start from base. requested: {RequestedStartSceneName}");
    }
}
#endif