using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CocktailIngredientButtonView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _frame;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private GameObject _selectedMarker;
    [SerializeField] private Color _normalColor = new Color32(2, 13, 41, 245);
    [SerializeField] private Color _hoverColor = new Color32(6, 20, 45, 255);
    [SerializeField] private Color _selectedColor = new Color32(4, 18, 45, 255);
    [SerializeField] private Color _normalFrameColor = new Color32(99, 52, 24, 255);
    [SerializeField] private Color _hoverFrameColor = new Color32(144, 80, 24, 255);
    [SerializeField] private Color _selectedFrameColor = new Color32(160, 96, 56, 255);

    private CocktailIngredientDataSO _ingredient;
    private Action<CocktailIngredientDataSO> _onSelected;
    private bool _selected;
    private bool _hovered;

    public CocktailIngredientDataSO Ingredient => _ingredient;

    public void Bind(
        CocktailIngredientDataSO ingredient,
        bool selected,
        Action<CocktailIngredientDataSO> onSelected)
    {
        _ingredient = ingredient;
        _onSelected = onSelected;
        _selected = selected;
        _hovered = false;

        gameObject.SetActive(ingredient != null);
        if (ingredient == null)
        {
            return;
        }

        if (_icon != null)
        {
            _icon.sprite = ingredient.Icon;
            _icon.enabled = ingredient.Icon != null;
            _icon.preserveAspect = true;
        }

        if (_label != null)
        {
            _label.text = ingredient.DisplayName;
        }

        if (_button != null)
        {
            _button.interactable = true;
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        RefreshVisual();
    }

    public void Clear()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _ingredient = null;
        _onSelected = null;
        _selected = false;
        _hovered = false;
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && _button.interactable)
        {
            _hovered = true;
            RefreshVisual();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RefreshVisual();
    }

    private void HandleClick()
    {
        if (_ingredient != null)
        {
            _onSelected?.Invoke(_ingredient);
        }
    }

    private void RefreshVisual()
    {
        if (_background != null)
        {
            _background.color = _selected
                ? _selectedColor
                : _hovered
                    ? _hoverColor
                    : _normalColor;
        }

        if (_frame != null)
        {
            _frame.color = _selected
                ? _selectedFrameColor
                : _hovered
                    ? _hoverFrameColor
                    : _normalFrameColor;
        }

        if (_selectedMarker != null)
        {
            _selectedMarker.SetActive(_selected);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }
    }
}
