using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public sealed class BaseInitiator : IInitiator
{
    private readonly SceneFlowController _sceneFlow;
    private readonly SceneData _sceneData;

    [Inject]
    public BaseInitiator(SceneFlowController sceneFlow, SceneData sceneData)
    {
        _sceneFlow = sceneFlow;
        _sceneData = sceneData;
    }

    public async UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

#if UNITY_EDITOR
        if (ToolbarPlayButtonsView.OnGetCoreMode)
        {
            var requestedSceneName = CoreBootStrap.RequestedStartSceneName;
            if (_sceneData.TryGetState(requestedSceneName, out var requestedState))
            {
                if (requestedState != GameSceneState.Bootstrap)
                {
                    await _sceneFlow.GoToAsync(requestedState);
                    return;
                }
            }
            else if (!string.IsNullOrWhiteSpace(requestedSceneName))
            {
                Debug.LogWarning(
                    $"[BaseInitiator] '{requestedSceneName}' is not registered in SceneData. Loading Title instead.");
            }
        }
#endif

        await _sceneFlow.GoToTitleAsync();
    }
}
