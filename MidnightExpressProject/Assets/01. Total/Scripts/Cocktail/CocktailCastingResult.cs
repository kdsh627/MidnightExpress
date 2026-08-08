public sealed class CocktailCastingResult
{
    public CocktailCastingResult(
        CocktailSelection selection,
        CocktailRecipeDataSO recipe,
        CocktailGuestReaction reaction,
        bool isNewDiscovery)
    {
        Selection = selection != null ? selection.Clone() : new CocktailSelection();
        Recipe = recipe;
        Reaction = reaction;
        IsNewDiscovery = isNewDiscovery;
    }

    public CocktailSelection Selection { get; }
    public CocktailRecipeDataSO Recipe { get; }
    public CocktailGuestReaction Reaction { get; }
    public bool IsNewDiscovery { get; }
    public bool IsExperimental => Recipe == null;
}
