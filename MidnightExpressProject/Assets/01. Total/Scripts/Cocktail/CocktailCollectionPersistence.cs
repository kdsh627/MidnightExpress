using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CocktailCollectionPersistence : IDisposable
{
    private const string PlayerPrefsKey =
        "MidnightExpress.CocktailCollection.v1";

    private readonly CocktailCollectionState _state;
    private readonly CocktailDatabaseSO _database;
    private bool _disposed;
    private bool _savePending;

    public CocktailCollectionPersistence(
        CocktailCollectionState state,
        CocktailDatabaseSO database)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _database = database ?? throw new ArgumentNullException(nameof(database));

        Load();
        _state.CollectionChanged += HandleCollectionChanged;
        Application.quitting += HandleApplicationQuitting;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        TrySave();
        _state.CollectionChanged -= HandleCollectionChanged;
        Application.quitting -= HandleApplicationQuitting;
        _disposed = true;
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            _state.Load(Array.Empty<string>(), Array.Empty<string>());
            return;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            var discovered = FilterValidRecipeIds(data.discoveredRecipeIds);
            var seen = FilterValidRecipeIds(data.seenRecipeIds);
            _state.Load(discovered, seen);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Cocktail collection save data could not be read. " +
                "An empty collection will be used instead.");
            Debug.LogException(exception);
            _state.Load(Array.Empty<string>(), Array.Empty<string>());
        }
    }

    private List<string> FilterValidRecipeIds(IEnumerable<string> recipeIds)
    {
        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        if (recipeIds == null)
        {
            return result;
        }

        foreach (string recipeId in recipeIds)
        {
            CocktailRecipeDataSO recipe = _database.FindRecipeById(recipeId);
            if (recipe != null && !recipe.Hidden && unique.Add(recipe.Id))
            {
                result.Add(recipe.Id);
            }
        }

        return result;
    }

    private void HandleCollectionChanged()
    {
        _savePending = true;
        TrySave();
    }

    private void HandleApplicationQuitting()
    {
        TrySave();
    }

    private void TrySave()
    {
        if (_disposed || !_savePending)
        {
            return;
        }

        var data = new SaveData
        {
            version = 1,
            discoveredRecipeIds = new List<string>(_state.DiscoveredRecipeIds),
            seenRecipeIds = new List<string>(_state.SeenRecipeIds)
        };

        try
        {
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            _savePending = false;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Cocktail collection progress could not be saved. " +
                "The current session will continue and saving will be retried later.");
            Debug.LogException(exception);
        }
    }

    [Serializable]
    private sealed class SaveData
    {
        public int version = 1;
        public List<string> discoveredRecipeIds = new List<string>();
        public List<string> seenRecipeIds = new List<string>();
    }
}
