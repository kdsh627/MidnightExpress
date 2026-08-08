using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public sealed class CoreSceneLoader : IDisposable
{
    private readonly SceneData _sceneData;
    private readonly SceneInitializationRegistry _initializationRegistry;
    private readonly AudioManager _audioManager;
    private readonly List<string> _loadedSceneNames = new List<string>();

    public IReadOnlyList<string> LoadedScenes => _loadedSceneNames;

    [Inject]
    public CoreSceneLoader(
        SceneData sceneData,
        SceneInitializationRegistry initializationRegistry,
        AudioManager audioManager)
    {
        _sceneData = sceneData;
        _initializationRegistry = initializationRegistry;
        _audioManager = audioManager;
    }

    public void Dispose()
    {
        _loadedSceneNames.Clear();
    }

    public async UniTask LoadSceneByNameAsync(string sceneName, CancellationToken token)
    {
        sceneName = NormalizeSceneName(sceneName);

        var alreadyLoaded = SceneManager.GetSceneByName(sceneName);
        if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
        {
            AddLoadedScene(sceneName);
            SceneManager.SetActiveScene(alreadyLoaded);
            SetAudioListenerTarget(alreadyLoaded);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            throw new InvalidOperationException(
                $"Scene '{sceneName}' is not available. Check SceneData and Build Settings.");
        }

        _initializationRegistry.Prepare(sceneName);
        AsyncOperation loadOperation = null;

        try
        {
            loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                throw new InvalidOperationException($"Unity did not create a load operation for '{sceneName}'.");
            }

            await loadOperation.ToUniTask(cancellationToken: token);

            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                throw new InvalidOperationException($"Scene load completed but the scene is invalid: {sceneName}");
            }

            AddLoadedScene(sceneName);
            SceneManager.SetActiveScene(loadedScene);
            SetAudioListenerTarget(loadedScene);

            await _initializationRegistry.WaitForReadyAsync(sceneName, token);
        }
        catch (OperationCanceledException)
        {
            _initializationRegistry.Abandon(sceneName);
            await CleanupFailedLoadAsync(loadOperation, sceneName);
            throw;
        }
        catch
        {
            _initializationRegistry.Abandon(sceneName);
            await CleanupFailedLoadAsync(loadOperation, sceneName);
            throw;
        }
    }

    public async UniTask UnloadSceneByNameAsync(string sceneName, CancellationToken token)
    {
        sceneName = NormalizeSceneName(sceneName);
        var scene = SceneManager.GetSceneByName(sceneName);

        _loadedSceneNames.Remove(sceneName);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (SceneManager.GetActiveScene() == scene)
        {
            SetFallbackActiveScene();
        }

        var unloadOperation = SceneManager.UnloadSceneAsync(scene);
        if (unloadOperation != null)
        {
            await unloadOperation.ToUniTask(cancellationToken: token);
        }
    }

    private async UniTask CleanupFailedLoadAsync(AsyncOperation loadOperation, string sceneName)
    {
        if (loadOperation != null && !loadOperation.isDone)
        {
            await loadOperation.ToUniTask();
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        _loadedSceneNames.Remove(sceneName);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (SceneManager.GetActiveScene() == scene)
        {
            SetFallbackActiveScene();
        }

        var unloadOperation = SceneManager.UnloadSceneAsync(scene);
        if (unloadOperation != null)
        {
            await unloadOperation.ToUniTask();
        }
    }

    private void SetFallbackActiveScene()
    {
        for (var index = _loadedSceneNames.Count - 1; index >= 0; index--)
        {
            var loadedScene = SceneManager.GetSceneByName(_loadedSceneNames[index]);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                return;
            }
        }

        var bootstrapScene = SceneManager.GetSceneByName(_sceneData.BootstrapSceneName);
        if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
        {
            SceneManager.SetActiveScene(bootstrapScene);
        }
    }

    private void AddLoadedScene(string sceneName)
    {
        if (!_loadedSceneNames.Contains(sceneName))
        {
            _loadedSceneNames.Add(sceneName);
        }
    }

    private void SetAudioListenerTarget(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
            for (var cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            {
                if (cameras[cameraIndex].isActiveAndEnabled)
                {
                    _audioManager.SetListenerTarget(cameras[cameraIndex].gameObject);
                    return;
                }
            }
        }

        _audioManager.SetListenerTarget(null);
    }

    private static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            throw new ArgumentException("Scene name is required.", nameof(sceneName));
        }

        return sceneName.Trim();
    }
}
