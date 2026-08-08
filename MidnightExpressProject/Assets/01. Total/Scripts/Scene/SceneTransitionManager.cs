using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public sealed class SceneTransitionManager : IDisposable
{
    private readonly LoadingUIStarter _loadingUI;
    private readonly CoreSceneLoader _sceneLoader;
    private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

    private int _isTransitioning;

    [Inject]
    public SceneTransitionManager(LoadingUIStarter loadingUI, CoreSceneLoader sceneLoader)
    {
        _loadingUI = loadingUI;
        _sceneLoader = sceneLoader;
    }

    public void Dispose()
    {
        if (!_lifetimeCts.IsCancellationRequested)
        {
            _lifetimeCts.Cancel();
        }

        _lifetimeCts.Dispose();
    }

    public async UniTask<bool> TransitionToScenesAsync(IReadOnlyList<string> requestedScenes)
    {
        var normalizedScenes = NormalizeRequestedScenes(requestedScenes);

        if (Interlocked.CompareExchange(ref _isTransitioning, 1, 0) != 0)
        {
            Debug.LogWarning("[SceneTransition] A transition is already in progress. The duplicate request was ignored.");
            return false;
        }

        try
        {
            if (SceneSetsMatch(_sceneLoader.LoadedScenes, normalizedScenes))
            {
                return true;
            }

            try
            {
                await _loadingUI.ShowAsync(_lifetimeCts.Token);
                await ApplySceneChangesAsync(normalizedScenes, _lifetimeCts.Token);
                await _loadingUI.HideAsync(_lifetimeCts.Token);
            }
            finally
            {
                _loadingUI.HideImmediate();
            }

            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError("[SceneTransition] Scene transition failed.");
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            Volatile.Write(ref _isTransitioning, 0);
        }
    }

    private async UniTask ApplySceneChangesAsync(
        IReadOnlyList<string> requestedScenes,
        CancellationToken token)
    {
        var currentScenes = new List<string>(_sceneLoader.LoadedScenes);

        for (var index = currentScenes.Count - 1; index >= 0; index--)
        {
            var sceneName = currentScenes[index];
            if (!Contains(requestedScenes, sceneName))
            {
                await _sceneLoader.UnloadSceneByNameAsync(sceneName, token);
            }
        }

        for (var index = 0; index < requestedScenes.Count; index++)
        {
            var sceneName = requestedScenes[index];
            if (!Contains(currentScenes, sceneName))
            {
                await _sceneLoader.LoadSceneByNameAsync(sceneName, token);
            }
        }
    }

    private static List<string> NormalizeRequestedScenes(IReadOnlyList<string> requestedScenes)
    {
        if (requestedScenes == null || requestedScenes.Count == 0)
        {
            throw new ArgumentException("At least one target scene is required.", nameof(requestedScenes));
        }

        var result = new List<string>(requestedScenes.Count);
        for (var index = 0; index < requestedScenes.Count; index++)
        {
            var sceneName = requestedScenes[index]?.Trim();
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("Target scene names cannot be empty.", nameof(requestedScenes));
            }

            if (!Contains(result, sceneName))
            {
                result.Add(sceneName);
            }
        }

        return result;
    }

    private static bool SceneSetsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(IReadOnlyList<string> scenes, string sceneName)
    {
        for (var index = 0; index < scenes.Count; index++)
        {
            if (string.Equals(scenes[index], sceneName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
