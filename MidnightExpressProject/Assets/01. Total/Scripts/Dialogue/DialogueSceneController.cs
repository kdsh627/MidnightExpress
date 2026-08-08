using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueSceneController : MonoBehaviour
{
    private DialogueManager _dialogueManager;
    private DialogueEventBus _eventBus;
    private DialogueActorRegistry _actorRegistry;
    private CancellationToken _sceneToken;

    public void Initialize(
        DialogueManager dialogueManager,
        DialogueEventBus eventBus,
        DialogueActorRegistry actorRegistry,
        CancellationToken sceneToken)
    {
        _dialogueManager = dialogueManager ?? throw new ArgumentNullException(nameof(dialogueManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _actorRegistry = actorRegistry ?? throw new ArgumentNullException(nameof(actorRegistry));
        _sceneToken = sceneToken;
    }

    public void PlayPreCastingDialogue(int eventId)
    {
        if (_dialogueManager == null)
        {
            Debug.LogWarning("DialogueSceneController has not been initialized.", this);
            return;
        }

        PlayPreCastingSafelyAsync(eventId).Forget();
    }

    public void ShowCastingDialogue(int eventId)
    {
        if (_dialogueManager == null || !_dialogueManager.ShowCastingDialogue(eventId))
        {
            Debug.LogWarning($"Casting dialogue ID {eventId} could not be shown.", this);
        }
    }

    public void EndCastingDialogues()
    {
        _eventBus?.PublishCastingProcessEnded();
    }

    public void AdvanceDialogue()
    {
        _dialogueManager?.RequestAdvance();
    }

    public void SetCharacterPreCastingEvent(string characterName, int eventId)
    {
        if (_actorRegistry == null || !_actorRegistry.TrySetPreCastingEventId(characterName, eventId))
        {
            Debug.LogWarning($"Dialogue actor '{characterName}' was not found.", this);
        }
    }

    public void Shutdown()
    {
        _dialogueManager = null;
        _eventBus = null;
        _actorRegistry = null;
        _sceneToken = default;
    }

    private async UniTask PlayPreCastingSafelyAsync(int eventId)
    {
        try
        {
            await _dialogueManager.PlayPreCastingAsync(eventId, _sceneToken);
        }
        catch (OperationCanceledException)
        {
            // Scene shutdown and dialogue replacement are expected cancellation paths.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }
}
