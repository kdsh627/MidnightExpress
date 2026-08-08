using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DialogueActor : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string _characterName;
    [SerializeField, Min(0)] private int _preCastingEventId;
    [SerializeField] private DialogueBubbleView _bubble;
    [SerializeField] private DialogueActorAppearance _appearance;
    [SerializeField] private DialogueActorHoverOutline _hoverOutline;

    private bool _isSelected;

    public string CharacterName => NormalizeName(_characterName);
    public int PreCastingEventId => _preCastingEventId;
    public DialogueBubbleView Bubble => _bubble;
    public DialogueActorAppearance Appearance => _appearance;

    public event Action<DialogueActor> Selected;

    private void Awake()
    {
        ResolveReferences();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left
            && isActiveAndEnabled
            && _preCastingEventId > 0)
        {
            MarkSelected();
            Selected?.Invoke(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isSelected && isActiveAndEnabled && _preCastingEventId > 0)
        {
            _hoverOutline?.SetHovered(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hoverOutline?.SetHovered(false);
    }

    public void MarkSelected()
    {
        _isSelected = true;
        _hoverOutline?.SetSelectionEnabled(false);
    }

    public void SetPreCastingEventId(int eventId)
    {
        if (eventId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "A starting dialogue ID cannot be negative.");
        }

        _preCastingEventId = eventId;
    }

    public void ValidateConfiguration()
    {
        ResolveReferences();

        if (string.IsNullOrWhiteSpace(CharacterName))
        {
            throw new InvalidOperationException($"DialogueActor '{name}' requires a character name.");
        }

        if (_bubble == null)
        {
            throw new InvalidOperationException($"DialogueActor '{name}' requires a DialogueBubbleView child.");
        }

        if (_hoverOutline != null)
        {
            _hoverOutline.ValidateConfiguration();
        }
    }

    public void PrepareAppearance()
    {
        ResolveReferences();
        if (_appearance != null)
        {
            _appearance.ValidateConfiguration();
        }
    }

    private void ResolveReferences()
    {
        if (_bubble == null)
        {
            _bubble = GetComponentInChildren<DialogueBubbleView>(true);
        }

        if (_appearance == null)
        {
            _appearance = GetComponent<DialogueActorAppearance>();
        }
        if (_hoverOutline == null)
        {
            _hoverOutline = GetComponent<DialogueActorHoverOutline>();
        }
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);
    }
}
