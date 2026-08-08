using System;
using UnityEngine;

public sealed class DialogueEventBus
{
    public event Action<int> SpecialCommandRequested;
    public event Action CastingProcessEnded;

    public void PublishSpecialCommand(int commandId)
    {
        if (commandId >= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandId),
                commandId,
                "Dialogue special command IDs must be negative.");
        }

        InvokeSafely(SpecialCommandRequested, commandId, $"dialogue command {commandId}");
    }

    public void PublishCastingProcessEnded()
    {
        InvokeSafely(CastingProcessEnded, "casting-process end");
    }

    private static void InvokeSafely(Action handlers, string context)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"A subscriber failed while handling {context}.");
                Debug.LogException(exception);
            }
        }
    }

    private static void InvokeSafely(Action<int> handlers, int value, string context)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action<int> handler in handlers.GetInvocationList())
        {
            try
            {
                handler.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogError($"A subscriber failed while handling {context}.");
                Debug.LogException(exception);
            }
        }
    }
}
