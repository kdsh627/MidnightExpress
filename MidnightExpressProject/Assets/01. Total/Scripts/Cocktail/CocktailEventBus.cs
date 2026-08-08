using System;
using UnityEngine;

public sealed class CocktailEventBus
{
    public event Action CastingStarted;
    public event Action<CocktailIngredientDataSO> IngredientSelected;
    public event Action<CocktailCastingResult> CastingCompleted;
    public event Action CastingClosed;

    public void PublishStarted()
    {
        InvokeSafely(CastingStarted, "cocktail casting start");
    }

    public void PublishIngredientSelected(CocktailIngredientDataSO ingredient)
    {
        InvokeSafely(IngredientSelected, ingredient, "cocktail ingredient selection");
    }

    public void PublishCompleted(CocktailCastingResult result)
    {
        InvokeSafely(CastingCompleted, result, "cocktail casting completion");
    }

    public void PublishClosed()
    {
        InvokeSafely(CastingClosed, "cocktail casting close");
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

    private static void InvokeSafely<T>(Action<T> handlers, T value, string context)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action<T> handler in handlers.GetInvocationList())
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
