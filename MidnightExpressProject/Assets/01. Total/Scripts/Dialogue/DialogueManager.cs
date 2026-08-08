using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ExcelData;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DialogueManager : IDisposable
{
    private readonly DialogueDB _dialogueDB;
    private readonly DialogueEventBus _eventBus;
    private readonly Dictionary<DialogueActor, int> _preCastingTurns =
        new Dictionary<DialogueActor, int>();
    private readonly HashSet<DialogueActor> _castingActors =
        new HashSet<DialogueActor>();
    private readonly List<DialogueActor> _actorBuffer =
        new List<DialogueActor>();

    private DialogueActorRegistry _actorRegistry;
    private CancellationToken _sceneToken;
    private CancellationTokenSource _preCastingCancellation;
    private bool _advanceRequested;
    private bool _acceptAdvanceInput;
    private bool _isDisposed;

    public bool IsPreCastingPlaying => _preCastingCancellation != null;
    public bool IsCastingActive => _castingActors.Count > 0;

    public DialogueManager(DialogueDB dialogueDB, DialogueEventBus eventBus)
    {
        _dialogueDB = dialogueDB ?? throw new ArgumentNullException(nameof(dialogueDB));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _eventBus.CastingProcessEnded += EndCastingDialogues;
    }

    public void AttachScene(DialogueActorRegistry actorRegistry, CancellationToken sceneToken)
    {
        ThrowIfDisposed();
        DetachScene();

        _actorRegistry = actorRegistry ?? throw new ArgumentNullException(nameof(actorRegistry));
        _sceneToken = sceneToken;
        _actorRegistry.ActorSelected += HandleActorSelected;
    }

    public bool RequestAdvance()
    {
        ThrowIfDisposed();
        if (!IsPreCastingPlaying || !_acceptAdvanceInput)
        {
            return false;
        }

        _advanceRequested = true;
        return true;
    }

    public void DetachScene(DialogueActorRegistry actorRegistry = null)
    {
        if (actorRegistry != null && _actorRegistry != actorRegistry)
        {
            return;
        }

        if (_actorRegistry != null)
        {
            _actorRegistry.ActorSelected -= HandleActorSelected;
        }

        CancelPreCasting();
        ClosePreCastingBubbles();
        EndCastingDialogues();
        _actorRegistry = null;
        _sceneToken = default;
        _advanceRequested = false;
    }

    public async UniTask<bool> PlayPreCastingAsync(
        int startId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_actorRegistry == null)
        {
            Debug.LogWarning("A dialogue scene is not attached to DialogueManager.");
            return false;
        }

        if (IsPreCastingPlaying)
        {
            return false;
        }

        if (IsCastingActive)
        {
            Debug.LogWarning("Pre-Casting dialogue cannot start while the casting process is active.");
            return false;
        }

        if (!_dialogueDB.TryGetPreCastingDialogue(startId, out _))
        {
            Debug.LogError($"Pre-Casting dialogue ID {startId} does not exist.");
            return false;
        }

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _sceneToken,
            cancellationToken);
        _preCastingCancellation = linkedCancellation;
        _advanceRequested = false;
        var token = linkedCancellation.Token;
        var visitedIds = new HashSet<int>();

        try
        {
            var currentId = startId;
            while (currentId > 0)
            {
                token.ThrowIfCancellationRequested();

                if (!visitedIds.Add(currentId))
                {
                    throw new InvalidOperationException(
                        $"Pre-Casting dialogue cycle detected while playing ID {currentId}.");
                }

                var dialogue = _dialogueDB.GetPreCastingDialogue(currentId);
                if (!TryResolveActor(dialogue.Name, currentId, "Pre-Casting", out var actor))
                {
                    ClosePreCastingBubbles();
                    return false;
                }

                switch (dialogue.EventType)
                {
                    case DialogueEventType.Appeared:
                        await PlayAppearanceAsync(actor, currentId, token);
                        break;
                    case DialogueEventType.Script:
                        ShowPreCastingLine(actor, dialogue);
                        await WaitForAdvanceAsync(actor.Bubble, token);
                        AdvancePreCastingTurns();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Pre-Casting dialogue ID {currentId} has unsupported EventType '{dialogue.EventType}'.");
                }

                if (dialogue.NextID == 0)
                {
                    ClosePreCastingBubbles();
                    return true;
                }

                if (dialogue.NextID < 0)
                {
                    ClosePreCastingBubbles();
                    _eventBus.PublishSpecialCommand(dialogue.NextID);
                    return true;
                }

                currentId = dialogue.NextID;
            }

            ClosePreCastingBubbles();
            return true;
        }
        finally
        {
            if (ReferenceEquals(_preCastingCancellation, linkedCancellation))
            {
                _preCastingCancellation = null;
            }

            linkedCancellation.Dispose();
            _advanceRequested = false;
            _acceptAdvanceInput = false;
        }
    }

    public bool ShowCastingDialogue(int eventId)
    {
        ThrowIfDisposed();

        if (_actorRegistry == null)
        {
            Debug.LogWarning("A dialogue scene is not attached to DialogueManager.");
            return false;
        }

        if (IsPreCastingPlaying)
        {
            Debug.LogWarning("Casting dialogue cannot start while Pre-Casting dialogue is active.");
            return false;
        }

        if (!_dialogueDB.TryGetCastingDialogue(eventId, out var dialogue))
        {
            Debug.LogError($"Casting dialogue ID {eventId} does not exist.");
            return false;
        }

        if (!TryResolveActor(dialogue.Name, eventId, "Casting", out var actor))
        {
            return false;
        }

        _preCastingTurns.Remove(actor);
        actor.Bubble.ShowText(dialogue.Script);
        _castingActors.Add(actor);
        return true;
    }

    public void EndCastingDialogues()
    {
        foreach (var actor in _castingActors)
        {
            if (actor != null && !_preCastingTurns.ContainsKey(actor) && actor.Bubble != null)
            {
                actor.Bubble.HideImmediate();
            }
        }

        _castingActors.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _eventBus.CastingProcessEnded -= EndCastingDialogues;
        DetachScene();
        _isDisposed = true;
    }

    private void HandleActorSelected(DialogueActor actor)
    {
        if (actor == null
            || actor.PreCastingEventId <= 0
            || IsPreCastingPlaying
            || IsCastingActive)
        {
            return;
        }

        PlayFromActorSafelyAsync(actor.PreCastingEventId).Forget();
    }

    private async UniTask PlayFromActorSafelyAsync(int startId)
    {
        try
        {
            await PlayPreCastingAsync(startId, _sceneToken);
        }
        catch (OperationCanceledException)
        {
            // Scene changes cancel active dialogue by design.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ClosePreCastingBubbles();
        }
    }

    private void ShowPreCastingLine(DialogueActor actor, PreCastingDialogueData dialogue)
    {
        if (_castingActors.Remove(actor))
        {
            actor.Bubble.HideImmediate();
        }

        actor.Bubble.ShowText(dialogue.Script);
        _preCastingTurns[actor] = Mathf.Max(1, dialogue.Turn);
    }

    private async UniTask PlayAppearanceAsync(
        DialogueActor actor,
        int eventId,
        CancellationToken cancellationToken)
    {
        _acceptAdvanceInput = false;
        _advanceRequested = false;

        if (actor.Appearance == null)
        {
            throw new InvalidOperationException(
                $"Appeared event ID {eventId} references '{actor.CharacterName}', but that actor has no DialogueActorAppearance component.");
        }

        actor.Appearance.ValidateConfiguration();
        actor.MarkSelected();
        await actor.Appearance.PlayAsync(cancellationToken);
        _advanceRequested = false;
    }

    private async UniTask WaitForAdvanceAsync(
        DialogueBubbleView bubble,
        CancellationToken cancellationToken)
    {
        _acceptAdvanceInput = false;
        _advanceRequested = false;
        try
        {
            // Prevent the character-selection click or appearance input from skipping a line.
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            _acceptAdvanceInput = true;

            while (true)
            {
                if (!ConsumeAdvanceRequest())
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    continue;
                }

                if (bubble.IsTyping)
                {
                    bubble.CompleteTyping();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    continue;
                }

                return;
            }
        }
        finally
        {
            _acceptAdvanceInput = false;
            _advanceRequested = false;
        }
    }

    private void AdvancePreCastingTurns()
    {
        _actorBuffer.Clear();
        _actorBuffer.AddRange(_preCastingTurns.Keys);

        foreach (var actor in _actorBuffer)
        {
            if (actor == null)
            {
                _preCastingTurns.Remove(actor);
                continue;
            }

            var remainingTurns = _preCastingTurns[actor] - 1;
            if (remainingTurns > 0)
            {
                _preCastingTurns[actor] = remainingTurns;
                continue;
            }

            _preCastingTurns.Remove(actor);
            if (!_castingActors.Contains(actor) && actor.Bubble != null)
            {
                actor.Bubble.HideImmediate();
            }
        }
    }

    private void ClosePreCastingBubbles()
    {
        foreach (var actor in _preCastingTurns.Keys)
        {
            if (actor != null && !_castingActors.Contains(actor) && actor.Bubble != null)
            {
                actor.Bubble.HideImmediate();
            }
        }

        _preCastingTurns.Clear();
    }

    private bool TryResolveActor(
        string characterName,
        int eventId,
        string tableName,
        out DialogueActor actor)
    {
        if (!_actorRegistry.TryGetActor(characterName, out actor) || actor == null)
        {
            Debug.LogError(
                $"{tableName} dialogue ID {eventId} references unregistered character '{characterName}'.");
            actor = null;
            return false;
        }

        return true;
    }

    private void CancelPreCasting()
    {
        if (_preCastingCancellation == null)
        {
            return;
        }

        var cancellation = _preCastingCancellation;
        _preCastingCancellation = null;
        cancellation.Cancel();
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DialogueManager));
        }
    }

    private bool ConsumeAdvanceRequest()
    {
        if (_advanceRequested)
        {
            _advanceRequested = false;
            return true;
        }

        return (Keyboard.current?.anyKey.wasPressedThisFrame ?? false) ||
               (Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
               (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false) ||
               (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false);
    }
}
