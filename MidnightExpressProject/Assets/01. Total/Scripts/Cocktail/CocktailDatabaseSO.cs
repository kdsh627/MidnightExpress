using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CocktailDatabase",
    menuName = "Midnight Express/Cocktail/Database")]
public sealed class CocktailDatabaseSO : ScriptableObject
{
    [SerializeField] private List<CocktailIngredientDataSO> _ingredients =
        new List<CocktailIngredientDataSO>();
    [SerializeField] private List<CocktailRecipeDataSO> _recipes =
        new List<CocktailRecipeDataSO>();
    [SerializeField] private List<CocktailGuestPreferenceSO> _guestPreferences =
        new List<CocktailGuestPreferenceSO>();
    [SerializeField] private List<CocktailBundleDataSO> _bundles =
        new List<CocktailBundleDataSO>();

    public IReadOnlyList<CocktailIngredientDataSO> Ingredients => _ingredients;
    public IReadOnlyList<CocktailRecipeDataSO> Recipes => _recipes;
    public IReadOnlyList<CocktailGuestPreferenceSO> GuestPreferences => _guestPreferences;
    public IReadOnlyList<CocktailBundleDataSO> Bundles => _bundles;

    public CocktailRecipeDataSO FindRecipeById(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            return null;
        }

        foreach (CocktailRecipeDataSO recipe in _recipes)
        {
            if (recipe != null &&
                string.Equals(recipe.Id, recipeId, StringComparison.Ordinal))
            {
                return recipe;
            }
        }

        return null;
    }

    public CocktailBundleDataSO FindBundle(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            return null;
        }

        foreach (CocktailBundleDataSO bundle in _bundles)
        {
            if (bundle != null &&
                string.Equals(bundle.Id, bundleId, StringComparison.Ordinal))
            {
                return bundle;
            }
        }

        return null;
    }

    public void GetIngredients(
        CocktailIngredientCategory category,
        List<CocktailIngredientDataSO> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (CocktailIngredientDataSO ingredient in _ingredients)
        {
            if (ingredient != null &&
                ingredient.Category == category &&
                ingredient.UnlockedByDefault)
            {
                destination.Add(ingredient);
            }
        }
    }

    public CocktailRecipeDataSO FindRecipe(CocktailSelection selection)
    {
        if (selection == null || !selection.IsComplete)
        {
            return null;
        }

        foreach (CocktailRecipeDataSO recipe in _recipes)
        {
            if (recipe != null && recipe.Matches(selection))
            {
                return recipe;
            }
        }

        return null;
    }

    public CocktailGuestPreferenceSO FindGuestPreference(string guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId))
        {
            return null;
        }

        foreach (CocktailGuestPreferenceSO preference in _guestPreferences)
        {
            if (preference != null &&
                string.Equals(preference.GuestId, guestId, StringComparison.Ordinal))
            {
                return preference;
            }
        }

        return null;
    }

    public void ValidateOrThrow()
    {
        var ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        var registeredIngredients = new HashSet<CocktailIngredientDataSO>();

        foreach (CocktailIngredientDataSO ingredient in _ingredients)
        {
            if (ingredient == null)
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains a null ingredient.");
            }

            ingredient.ValidateOrThrow();
            if (!ingredientIds.Add(ingredient.Id))
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains duplicate ingredient ID '{ingredient.Id}'.");
            }

            registeredIngredients.Add(ingredient);
        }

        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        var recipeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CocktailRecipeDataSO recipe in _recipes)
        {
            if (recipe == null)
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains a null recipe.");
            }

            recipe.ValidateOrThrow();
            if (!recipeIds.Add(recipe.Id))
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains duplicate recipe ID '{recipe.Id}'.");
            }

            ValidateRegisteredIngredient(recipe, recipe.Base, registeredIngredients);
            ValidateRegisteredIngredient(recipe, recipe.Mixer, registeredIngredients);
            ValidateRegisteredIngredient(recipe, recipe.Modifier, registeredIngredients);
            ValidateRegisteredIngredient(recipe, recipe.Technique, registeredIngredients);
            ValidateRegisteredIngredient(recipe, recipe.Garnish, registeredIngredients);

            string key = CreateRecipeKey(recipe);
            if (!recipeKeys.Add(key))
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' has more than one recipe for combination '{key}'.");
            }
        }

        var guestIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CocktailGuestPreferenceSO preference in _guestPreferences)
        {
            if (preference == null)
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains a null guest preference.");
            }

            preference.ValidateOrThrow();
            if (!guestIds.Add(preference.GuestId))
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains duplicate guest ID '{preference.GuestId}'.");
            }
        }

        var bundleIds = new HashSet<string>(StringComparer.Ordinal);
        var registeredRecipes = new HashSet<CocktailRecipeDataSO>(_recipes);
        foreach (CocktailBundleDataSO bundle in _bundles)
        {
            if (bundle == null)
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains a null collection bundle.");
            }

            bundle.ValidateOrThrow(registeredRecipes);
            if (!bundleIds.Add(bundle.Id))
            {
                throw new InvalidOperationException(
                    $"Cocktail database '{name}' contains duplicate bundle ID '{bundle.Id}'.");
            }
        }
    }

    private static void ValidateRegisteredIngredient(
        CocktailRecipeDataSO recipe,
        CocktailIngredientDataSO ingredient,
        ISet<CocktailIngredientDataSO> registeredIngredients)
    {
        if (ingredient != null && !registeredIngredients.Contains(ingredient))
        {
            throw new InvalidOperationException(
                $"Cocktail recipe '{recipe.name}' references unregistered ingredient '{ingredient.name}'.");
        }
    }

    private static string CreateRecipeKey(CocktailRecipeDataSO recipe)
    {
        return string.Join(
            "|",
            recipe.Base != null ? recipe.Base.Id : string.Empty,
            recipe.Mixer != null ? recipe.Mixer.Id : string.Empty,
            recipe.Modifier != null ? recipe.Modifier.Id : string.Empty,
            recipe.Technique != null ? recipe.Technique.Id : string.Empty,
            recipe.Garnish != null ? recipe.Garnish.Id : string.Empty);
    }
}
