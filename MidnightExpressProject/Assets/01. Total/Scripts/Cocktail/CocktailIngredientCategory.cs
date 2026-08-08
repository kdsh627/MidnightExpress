public enum CocktailIngredientCategory
{
    Base,
    Mixer,
    Modifier,
    Technique,
    Garnish
}

public enum CocktailCastingStep
{
    Hidden = -1,
    Base = 0,
    Mixer = 1,
    Modifier = 2,
    Technique = 3,
    Garnish = 4,
    Review = 5,
    Result = 6
}

public enum CocktailGuestReaction
{
    Good,
    SoSo,
    Dislike
}
