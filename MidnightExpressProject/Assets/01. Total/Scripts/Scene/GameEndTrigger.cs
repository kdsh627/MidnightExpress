using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameEndTrigger : MonoBehaviour
{
    private SceneFlowController _sceneFlow;
    private bool _transitionRequested;

    public void Initialize(SceneFlowController sceneFlow)
    {
        _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        _transitionRequested = false;
    }

    /// <summary>
    /// Call this once the game result/ending sequence has completed.
    /// </summary>
    public void CompleteGame()
    {
        ReturnToTitleAsync().Forget();
    }

    private async UniTask ReturnToTitleAsync()
    {
        if (_transitionRequested || _sceneFlow == null)
        {
            return;
        }

        _transitionRequested = true;

        try
        {
            var accepted = await _sceneFlow.GoToTitleAsync();
            if (!accepted && this != null)
            {
                _transitionRequested = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this != null)
            {
                _transitionRequested = false;
            }
        }
    }

    private void OnDestroy()
    {
        _sceneFlow = null;
    }
}
