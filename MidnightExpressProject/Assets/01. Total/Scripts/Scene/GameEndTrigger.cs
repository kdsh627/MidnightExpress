using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameEndTrigger : MonoBehaviour
{
    private SceneFlowController _sceneFlow;
    private DialogueEventBus _dialogueEventBus;
    private bool _transitionRequested;

    public void Initialize(SceneFlowController sceneFlow, DialogueEventBus dialogueEventBus)
    {
        _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        _dialogueEventBus = dialogueEventBus ?? throw new ArgumentNullException(nameof(dialogueEventBus));
        _transitionRequested = false;
    }

    /// <summary>
    /// Call this once the game result/ending sequence has completed.
    /// </summary>
    public void CompleteGame()
    {
        ReturnToTitleAsync().Forget();
    }

    public void Shutdown()
    {
        _sceneFlow = null;
        _dialogueEventBus = null;
        _transitionRequested = false;
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
            _dialogueEventBus.PublishCastingProcessEnded();
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
        Shutdown();
    }
}
