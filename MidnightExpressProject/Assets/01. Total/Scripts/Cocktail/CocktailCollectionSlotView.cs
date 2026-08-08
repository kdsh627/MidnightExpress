using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CocktailCollectionSlotView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Image _thumbnail;
    [SerializeField] private TMP_Text _constellationLabel;
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _statusLabel;
    [SerializeField] private GameObject _newBadge;
    [SerializeField] private GameObject _selectionFrame;
    [SerializeField] private Sprite _lockedFallbackSprite;
    [SerializeField] private Material _silhouetteMaterial;

    private CocktailRecipeDataSO _recipe;
    private Action<CocktailRecipeDataSO> _onSelected;

    public CocktailRecipeDataSO Recipe => _recipe;
    public bool IsConfigured =>
        _button != null && _background != null && _thumbnail != null &&
        _constellationLabel != null && _nameLabel != null &&
        _statusLabel != null && _newBadge != null && _selectionFrame != null;

    public void Initialize(Action<CocktailRecipeDataSO> onSelected)
    {
        _onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
        _button.onClick.RemoveListener(HandleClicked);
        _button.onClick.AddListener(HandleClicked);
    }

    public void Bind(
        CocktailRecipeDataSO recipe,
        bool discovered,
        bool seen,
        bool selected)
    {
        _recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        gameObject.SetActive(true);
        _button.interactable = true;

        Sprite artwork = recipe.ResultSprite != null
            ? recipe.ResultSprite
            : recipe.Base != null
                ? recipe.Base.Icon
                : _lockedFallbackSprite;
        _thumbnail.sprite = artwork != null ? artwork : _lockedFallbackSprite;
        _thumbnail.enabled = _thumbnail.sprite != null;
        _thumbnail.preserveAspect = true;
        _thumbnail.material = discovered ? null : _silhouetteMaterial;
        _thumbnail.color = Color.white;

        _constellationLabel.text =
            $"{recipe.DisplayOrder:00}  {recipe.Constellation}";
        _nameLabel.text = discovered ? recipe.CocktailName : "???";
        _statusLabel.text = discovered ? "발견" : "미발견";
        _newBadge.SetActive(discovered && !seen);
        _selectionFrame.SetActive(selected);
        _background.color = selected
            ? new Color32(255, 238, 191, 255)
            : Color.white;
    }

    public void Clear()
    {
        _recipe = null;
        _thumbnail.sprite = null;
        _thumbnail.material = null;
        _selectionFrame.SetActive(false);
        _newBadge.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Shutdown()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked);
        }

        _onSelected = null;
        _recipe = null;
    }

    private void HandleClicked()
    {
        if (_recipe != null)
        {
            _onSelected?.Invoke(_recipe);
        }
    }
}
