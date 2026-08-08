using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class SceneFlowController
{
    private readonly SceneData _sceneData;
    private readonly SceneTransitionManager _transitionManager;

    [Inject]
    public SceneFlowController(SceneData sceneData, SceneTransitionManager transitionManager)
    {
        _sceneData = sceneData;
        _transitionManager = transitionManager;
    }

    public UniTask<bool> GoToTitleAsync()
    {
        return GoToAsync(GameSceneState.Title);
    }

    public UniTask<bool> GoToGameEntryAsync()
    {
        return GoToAsync(GameSceneState.GameEntry);
    }

    public UniTask<bool> GoToGameAsync()
    {
        return GoToAsync(GameSceneState.Game);
    }

    public UniTask<bool> GoToAsync(GameSceneState state)
    {
        if (state == GameSceneState.Bootstrap)
        {
            throw new InvalidOperationException("BootstrapScene is persistent and cannot be a transition target.");
        }

        var targetScenes = new List<string>(1)
        {
            _sceneData.GetSceneName(state)
        };

        return _transitionManager.TransitionToScenesAsync(targetScenes);
    }
}
