using System;

[Serializable]
public sealed class CocktailSelection
{
    public CocktailIngredientDataSO Base;
    public CocktailIngredientDataSO Mixer;
    public CocktailIngredientDataSO Modifier;
    public CocktailIngredientDataSO Technique;
    public CocktailIngredientDataSO Garnish;

    public bool IsComplete =>
        Base != null &&
        Technique != null &&
        (Mixer != null || Modifier != null);

    public CocktailIngredientDataSO Get(CocktailIngredientCategory category)
    {
        switch (category)
        {
            case CocktailIngredientCategory.Base:
                return Base;
            case CocktailIngredientCategory.Mixer:
                return Mixer;
            case CocktailIngredientCategory.Modifier:
                return Modifier;
            case CocktailIngredientCategory.Technique:
                return Technique;
            case CocktailIngredientCategory.Garnish:
                return Garnish;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, null);
        }
    }

    public void Set(CocktailIngredientCategory category, CocktailIngredientDataSO ingredient)
    {
        if (ingredient != null && ingredient.Category != category)
        {
            throw new ArgumentException(
                $"Ingredient '{ingredient.name}' belongs to {ingredient.Category}, not {category}.",
                nameof(ingredient));
        }

        switch (category)
        {
            case CocktailIngredientCategory.Base:
                Base = ingredient;
                break;
            case CocktailIngredientCategory.Mixer:
                Mixer = ingredient;
                break;
            case CocktailIngredientCategory.Modifier:
                Modifier = ingredient;
                break;
            case CocktailIngredientCategory.Technique:
                Technique = ingredient;
                break;
            case CocktailIngredientCategory.Garnish:
                Garnish = ingredient;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, null);
        }
    }

    public void Clear()
    {
        Base = null;
        Mixer = null;
        Modifier = null;
        Technique = null;
        Garnish = null;
    }

    public CocktailSelection Clone()
    {
        return new CocktailSelection
        {
            Base = Base,
            Mixer = Mixer,
            Modifier = Modifier,
            Technique = Technique,
            Garnish = Garnish
        };
    }
}
