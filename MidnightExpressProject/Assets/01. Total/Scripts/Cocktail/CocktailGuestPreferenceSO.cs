using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CocktailGuestPreference",
    menuName = "Midnight Express/Cocktail/Guest Preference")]
public sealed class CocktailGuestPreferenceSO : ScriptableObject
{
    [SerializeField] private string _guestId;
    [SerializeField] private List<CocktailRecipeDataSO> _goodRecipes =
        new List<CocktailRecipeDataSO>();
    [SerializeField] private List<CocktailRecipeDataSO> _soSoRecipes =
        new List<CocktailRecipeDataSO>();
    [SerializeField, Min(0)] private int _goodDialogueId;
    [SerializeField, Min(0)] private int _soSoDialogueId;
    [SerializeField, Min(0)] private int _dislikeDialogueId;

    public string GuestId => _guestId;

    public CocktailGuestReaction Resolve(CocktailRecipeDataSO recipe)
    {
        if (recipe == null)
        {
            return CocktailGuestReaction.Dislike;
        }

        if (_goodRecipes.Contains(recipe))
        {
            return CocktailGuestReaction.Good;
        }

        // A designed recipe without an explicit preference is intentionally neutral.
        return CocktailGuestReaction.SoSo;
    }

    public int GetReactionDialogueId(CocktailGuestReaction reaction)
    {
        switch (reaction)
        {
            case CocktailGuestReaction.Good:
                return _goodDialogueId;
            case CocktailGuestReaction.SoSo:
                return _soSoDialogueId;
            case CocktailGuestReaction.Dislike:
                return _dislikeDialogueId;
            default:
                return 0;
        }
    }

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_guestId))
        {
            throw new System.InvalidOperationException(
                $"Cocktail guest preference '{name}' requires a stable Guest ID.");
        }
    }
}
