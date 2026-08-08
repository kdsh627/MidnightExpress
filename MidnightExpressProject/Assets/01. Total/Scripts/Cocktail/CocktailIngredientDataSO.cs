using UnityEngine;

[CreateAssetMenu(
    fileName = "CocktailIngredient",
    menuName = "Midnight Express/Cocktail/Ingredient")]
public sealed class CocktailIngredientDataSO : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private CocktailIngredientCategory _category;
    [SerializeField] private Sprite _icon;
    [SerializeField] private bool _unlockedByDefault = true;
    [SerializeField, Min(0)] private int _castingDialogueId;

    public string Id => _id;
    public string DisplayName => _displayName;
    public CocktailIngredientCategory Category => _category;
    public Sprite Icon => _icon;
    public bool UnlockedByDefault => _unlockedByDefault;
    public int CastingDialogueId => _castingDialogueId;

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            throw new System.InvalidOperationException(
                $"Cocktail ingredient '{name}' requires a stable ID.");
        }

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            throw new System.InvalidOperationException(
                $"Cocktail ingredient '{name}' requires a display name.");
        }
    }
}
