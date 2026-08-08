using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CocktailCollectionOpenTrigger : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private CocktailCollectionController _controller;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _hoverColor = new Color32(255, 234, 176, 255);

    private Color _normalColor = Color.white;
    private bool _isHovered;

    private void Awake()
    {
        if (_spriteRenderer != null)
        {
            _normalColor = _spriteRenderer.color;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _controller?.RequestOpen();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        RefreshColor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        RefreshColor();
    }

    private void Update()
    {
        if (_isHovered)
        {
            RefreshColor();
        }
    }

    private void OnDisable()
    {
        _isHovered = false;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _normalColor;
        }
    }

    private void RefreshColor()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.color =
            _isHovered && _controller != null && _controller.CanOpen
                ? _hoverColor
                : _normalColor;
    }
}
