using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class DialogueSceneEventBindings : MonoBehaviour
{
    [Serializable]
    private sealed class SpecialCommandBinding
    {
        public int CommandId;
        public UnityEvent Response = new UnityEvent();
    }

    [Header("Built-in Command")]
    [Tooltip("Invoked when a Pre-Casting dialogue reaches NextID -1.")]
    [SerializeField] private UnityEvent _onGameStartRequested = new UnityEvent();

    [Header("Additional Negative-ID Commands")]
    [SerializeField] private List<SpecialCommandBinding> _additionalBindings =
        new List<SpecialCommandBinding>();

    private DialogueEventBus _eventBus;

    public void Initialize(DialogueEventBus eventBus)
    {
        Shutdown();
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _eventBus.SpecialCommandRequested += HandleSpecialCommand;

        if (_onGameStartRequested.GetPersistentEventCount() == 0)
        {
            Debug.Log(
                "Dialogue command -1 is mapped, but On Game Start Requested has no persistent listener yet. "
                + "Connect the gameplay-start method in DialogueSceneEventBindings.",
                this);
        }
    }

    public void Shutdown()
    {
        if (_eventBus != null)
        {
            _eventBus.SpecialCommandRequested -= HandleSpecialCommand;
            _eventBus = null;
        }
    }

    private void HandleSpecialCommand(int commandId)
    {
        if (commandId == -1)
        {
            _onGameStartRequested?.Invoke();
        }

        foreach (var binding in _additionalBindings)
        {
            if (binding != null && binding.CommandId == commandId)
            {
                binding.Response?.Invoke();
            }
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
