using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CocktailStepNodeView : MonoBehaviour
{
    [SerializeField] private CocktailIngredientCategory _category;
    [SerializeField] private Button _button;
    [SerializeField] private Image _baseRing;
    [SerializeField] private Image _glow;
    [SerializeField] private Image _ingredientIcon;
    [SerializeField] private TMP_Text _categoryLabel;
    [SerializeField] private TMP_Text _selectionLabel;
    [SerializeField] private Sprite[] _glowFrames = Array.Empty<Sprite>();
    [SerializeField, Min(0.03f)] private float _frameInterval = 0.11f;

    [Header("Ring State Colors")]
    [SerializeField] private Color _selectedRingColor = Color.white;
    [SerializeField] private Color _availableRingColor = Color.white;
    [SerializeField] private Color _lockedRingColor = new Color32(255, 255, 255, 210);

    private Action<CocktailIngredientCategory> _onClicked;
    private float _frameTimer;
    private int _frameIndex;
    private bool _isCurrent;

    public CocktailIngredientCategory Category => _category;

    public void Initialize(Action<CocktailIngredientCategory> onClicked)
    {
        _onClicked = onClicked;
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        if (_categoryLabel != null)
        {
            _categoryLabel.text = GetCategoryLabel(_category);
        }
    }

    public void SetState(
        bool reached,
        bool current,
        CocktailIngredientDataSO selection)
    {
        _isCurrent = current;
        _frameTimer = 0f;

        if (_button != null)
        {
            _button.interactable = reached || current;
        }

        if (_baseRing != null)
        {
            _baseRing.enabled = true;
            _baseRing.gameObject.SetActive(true);
            _baseRing.color = selection != null
                ? _selectedRingColor
                : reached || current
                    ? _availableRingColor
                    : _lockedRingColor;
        }

        if (_glow != null)
        {
            _glow.gameObject.SetActive(current);
            if (current && _glowFrames.Length > 0)
            {
                _frameIndex = 0;
                _glow.sprite = _glowFrames[0];
            }
        }

        if (_ingredientIcon != null)
        {
            _ingredientIcon.sprite = selection != null ? selection.Icon : null;
            _ingredientIcon.enabled = selection != null && selection.Icon != null;
            _ingredientIcon.preserveAspect = true;
        }

        if (_selectionLabel != null)
        {
            _selectionLabel.text = selection != null ? selection.DisplayName : "";
        }
    }

    private void Update()
    {
        if (!_isCurrent || _glow == null || _glowFrames.Length <= 1)
        {
            return;
        }

        _frameTimer += Time.unscaledDeltaTime;
        if (_frameTimer < _frameInterval)
        {
            return;
        }

        _frameTimer %= _frameInterval;
        _frameIndex = (_frameIndex + 1) % _glowFrames.Length;
        _glow.sprite = _glowFrames[_frameIndex];
    }

    private void HandleClick()
    {
        _onClicked?.Invoke(_category);
    }

    private static string GetCategoryLabel(CocktailIngredientCategory category)
    {
        switch (category)
        {
            case CocktailIngredientCategory.Base:
                return "기주";
            case CocktailIngredientCategory.Mixer:
                return "믹서";
            case CocktailIngredientCategory.Modifier:
                return "모디파이어";
            case CocktailIngredientCategory.Technique:
                return "기법";
            case CocktailIngredientCategory.Garnish:
                return "가니시";
            default:
                return category.ToString();
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
