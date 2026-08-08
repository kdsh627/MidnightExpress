using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CocktailBundle",
    menuName = "Midnight Express/Cocktail/Collection Bundle")]
public sealed class CocktailBundleDataSO : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea(2, 4)] private string _description;
    [SerializeField] private List<CocktailRecipeDataSO> _recipes =
        new List<CocktailRecipeDataSO>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public IReadOnlyList<CocktailRecipeDataSO> Recipes => _recipes;

    public void ValidateOrThrow(ISet<CocktailRecipeDataSO> registeredRecipes)
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            throw new System.InvalidOperationException(
                $"Cocktail bundle '{name}' requires a stable ID.");
        }

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            throw new System.InvalidOperationException(
                $"Cocktail bundle '{name}' requires a display name.");
        }

        if (_recipes.Count == 0)
        {
            throw new System.InvalidOperationException(
                $"Cocktail bundle '{name}' requires at least one recipe.");
        }

        var uniqueRecipes = new HashSet<CocktailRecipeDataSO>();
        foreach (CocktailRecipeDataSO recipe in _recipes)
        {
            if (recipe == null || !registeredRecipes.Contains(recipe))
            {
                throw new System.InvalidOperationException(
                    $"Cocktail bundle '{name}' contains an unregistered recipe.");
            }

            if (!uniqueRecipes.Add(recipe))
            {
                throw new System.InvalidOperationException(
                    $"Cocktail bundle '{name}' contains duplicate recipe '{recipe.Id}'.");
            }
        }
    }
}
