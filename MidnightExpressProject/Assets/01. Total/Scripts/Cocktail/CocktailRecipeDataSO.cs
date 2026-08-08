using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CocktailRecipe",
    menuName = "Midnight Express/Cocktail/Recipe")]
public sealed class CocktailRecipeDataSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _id;
    [SerializeField, Min(1)] private int _displayOrder = 1;
    [SerializeField] private string _constellation;
    [SerializeField] private string _cocktailName;
    [SerializeField] private string _subtitle;

    [Header("Description")]
    [SerializeField] private string _emotionKeyword;
    [SerializeField] private List<string> _tasteKeywords = new List<string>();
    [SerializeField, TextArea(2, 5)] private string _oneLineDescription;

    [Header("Recipe")]
    [SerializeField] private CocktailIngredientDataSO _base;
    [SerializeField] private CocktailIngredientDataSO _mixer;
    [SerializeField] private CocktailIngredientDataSO _modifier;
    [SerializeField] private CocktailIngredientDataSO _technique;
    [SerializeField] private CocktailIngredientDataSO _garnish;

    [Header("Collection")]
    [SerializeField] private Sprite _resultSprite;
    [SerializeField] private Sprite _hintSilhouette;
    [SerializeField] private List<string> _tags = new List<string>();
    [SerializeField] private bool _hidden;

    public string Id => _id;
    public int DisplayOrder => _displayOrder;
    public string Constellation => _constellation;
    public string CocktailName => _cocktailName;
    public string Subtitle => _subtitle;
    public string EmotionKeyword => _emotionKeyword;
    public IReadOnlyList<string> TasteKeywords => _tasteKeywords;
    public string OneLineDescription => _oneLineDescription;
    public CocktailIngredientDataSO Base => _base;
    public CocktailIngredientDataSO Mixer => _mixer;
    public CocktailIngredientDataSO Modifier => _modifier;
    public CocktailIngredientDataSO Technique => _technique;
    public CocktailIngredientDataSO Garnish => _garnish;
    public Sprite ResultSprite => _resultSprite;
    public Sprite HintSilhouette => _hintSilhouette;
    public IReadOnlyList<string> Tags => _tags;
    public bool Hidden => _hidden;

    public bool Matches(CocktailSelection selection)
    {
        return selection != null &&
               selection.Base == _base &&
               selection.Mixer == _mixer &&
               selection.Modifier == _modifier &&
               selection.Technique == _technique &&
               selection.Garnish == _garnish;
    }

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' requires a stable ID.");
        }

        if (string.IsNullOrWhiteSpace(_cocktailName))
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' requires a display name.");
        }

        if (_base == null || _base.Category != CocktailIngredientCategory.Base)
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' requires one Base ingredient.");
        }

        if (_technique == null || _technique.Category != CocktailIngredientCategory.Technique)
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' requires one Technique ingredient.");
        }

        if (_mixer == null && _modifier == null)
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' requires at least one Mixer or Modifier.");
        }

        ValidateOptionalCategory(_mixer, CocktailIngredientCategory.Mixer, nameof(_mixer));
        ValidateOptionalCategory(_modifier, CocktailIngredientCategory.Modifier, nameof(_modifier));
        ValidateOptionalCategory(_garnish, CocktailIngredientCategory.Garnish, nameof(_garnish));
    }

    private void ValidateOptionalCategory(
        CocktailIngredientDataSO ingredient,
        CocktailIngredientCategory expectedCategory,
        string fieldName)
    {
        if (ingredient != null && ingredient.Category != expectedCategory)
        {
            throw new System.InvalidOperationException(
                $"Cocktail recipe '{name}' field {fieldName} references category {ingredient.Category}, " +
                $"but {expectedCategory} is required.");
        }
    }
}
