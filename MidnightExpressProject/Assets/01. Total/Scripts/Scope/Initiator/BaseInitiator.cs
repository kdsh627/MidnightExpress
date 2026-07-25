using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public class BaseInitiator : IInitiator
{
    private readonly SceneTransitionManager _transitionManager;
    private readonly SceneData _sceneData;

    [Inject]
    public BaseInitiator(SceneTransitionManager transitionManager, SceneData sceneData)
    {
        _transitionManager = transitionManager;
        _sceneData = sceneData;
    }

    public async UniTask GameInitialize(CancellationToken token)
    {
        List<string> scenesToLoad = new List<string>();
#if UNITY_EDITOR
        string reqPath = CoreBootStrap.RequestedStartScenePath;
        string reqName = CoreBootStrap.RequestedStartSceneName;

        if (!string.IsNullOrEmpty(reqPath) && !reqName.Contains("BaseScene") && ToolbarPlayButtonsView.OnGetCoreMode)
        {
            scenesToLoad.Add(reqPath);

            await _transitionManager.TransitionToScenes(scenesToLoad, token);
            return;
        }
#endif
        scenesToLoad.Add(_sceneData.HomeScenePath);
        await _transitionManager.TransitionToScenes(scenesToLoad, token);
    }
}
