using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public sealed class BaseInitiator : IInitiator
{
    private readonly SceneFlowController _sceneFlow;
    private readonly SceneData _sceneData;
    private readonly CocktailCollectionPersistence _cocktailCollectionPersistence;

    [Inject]
    public BaseInitiator(
        SceneFlowController sceneFlow,
        SceneData sceneData,
        CocktailCollectionPersistence cocktailCollectionPersistence)
    {
        _sceneFlow = sceneFlow;
        _sceneData = sceneData;
        _cocktailCollectionPersistence = cocktailCollectionPersistence ??
            throw new System.ArgumentNullException(nameof(cocktailCollectionPersistence));
    }

    public async UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        System.GC.KeepAlive(_cocktailCollectionPersistence);

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
