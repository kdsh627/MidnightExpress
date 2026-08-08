using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CocktailCollectionState
{
    private readonly HashSet<string> _discoveredRecipeIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _seenRecipeIds =
        new HashSet<string>(StringComparer.Ordinal);

    public event Action<string> RecipeDiscovered;
    public event Action<string> RecipeSeen;
    public event Action CollectionChanged;

    public IReadOnlyCollection<string> DiscoveredRecipeIds => _discoveredRecipeIds;
    public IReadOnlyCollection<string> SeenRecipeIds => _seenRecipeIds;

    public bool IsDiscovered(string recipeId)
    {
        return !string.IsNullOrWhiteSpace(recipeId) &&
               _discoveredRecipeIds.Contains(recipeId);
    }

    public bool IsSeen(string recipeId)
    {
        return !string.IsNullOrWhiteSpace(recipeId) &&
               _seenRecipeIds.Contains(recipeId);
    }

    public bool RegisterDiscovery(CocktailRecipeDataSO recipe)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
        {
            return false;
        }

        if (!_discoveredRecipeIds.Add(recipe.Id))
        {
            return false;
        }

        InvokeSafely(CollectionChanged);
        InvokeSafely(RecipeDiscovered, recipe.Id);
        return true;
    }

    public bool MarkSeen(string recipeId)
    {
        if (!IsDiscovered(recipeId) || !_seenRecipeIds.Add(recipeId))
        {
            return false;
        }

        InvokeSafely(CollectionChanged);
        InvokeSafely(RecipeSeen, recipeId);
        return true;
    }

    public void Load(
        IEnumerable<string> discoveredRecipeIds,
        IEnumerable<string> seenRecipeIds = null)
    {
        _discoveredRecipeIds.Clear();
        _seenRecipeIds.Clear();

        if (discoveredRecipeIds != null)
        {
            foreach (string recipeId in discoveredRecipeIds)
            {
                if (!string.IsNullOrWhiteSpace(recipeId))
                {
                    _discoveredRecipeIds.Add(recipeId);
                }
            }
        }

        if (seenRecipeIds == null)
        {
            return;
        }

        foreach (string recipeId in seenRecipeIds)
        {
            if (!string.IsNullOrWhiteSpace(recipeId) &&
                _discoveredRecipeIds.Contains(recipeId))
            {
                _seenRecipeIds.Add(recipeId);
            }
        }
    }

    private static void InvokeSafely(Action handlers)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void InvokeSafely(Action<string> handlers, string recipeId)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(recipeId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
