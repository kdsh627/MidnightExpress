using System;
using Febucci.UI;
using Febucci.UI.Core;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueBubbleView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _tailRenderer;
    [SerializeField] private SpriteRenderer _textAreaRenderer;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private TextAnimator_TMP _textAnimator;
    [SerializeField] private TypewriterByCharacter _typewriter;

    [Header("Dynamic Layout")]
    [SerializeField] private Vector2 _minimumSize = new Vector2(3.2f, 1.8f);
    [SerializeField] private Vector2 _maximumSize = new Vector2(6.5f, 3.2f);
    [SerializeField] private Vector2 _textPadding = new Vector2(0.8f, 0.55f);
    [SerializeField, Min(0.01f)] private float _minimumFontSize = 0.18f;
    [SerializeField, Min(0.01f)] private float _maximumFontSize = 0.3f;

    [Header("Editor Preview")]
    [SerializeField, TextArea(1, 3)] private string _editorPreviewText = "말풍선 크기 미리보기 대사입니다.";

    private RectTransform _textRect;
    private Vector3 _textAreaAuthoredLocalPosition;
    private Vector3 _canvasAuthoredLocalPosition;
    private bool _layoutOriginsCaptured;
#if UNITY_EDITOR
    private bool _isApplyingEditorPreview;
#endif

    public bool IsTyping =>
        gameObject.activeInHierarchy &&
        _typewriter != null &&
        _typewriter.isShowingText;

    private void Awake()
    {
        ResolveReferences();
        CaptureLayoutOrigins();
        ConfigureComponents();
        if (_text != null)
        {
            _text.text = string.Empty;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyEditorPreview();
    }

    private void ApplyEditorPreview()
    {
        if (this == null || Application.isPlaying || _isApplyingEditorPreview)
        {
            return;
        }

        _isApplyingEditorPreview = true;
        try
        {
            ResolveReferences();
            if (_textAreaRenderer == null || _canvasRect == null || _text == null || _textRect == null)
            {
                return;
            }

            CaptureLayoutOrigins();
            ConfigureTextLayout();

            string previewText = _editorPreviewText ?? string.Empty;
            _text.text = previewText;
            UpdateLayout(previewText);
            _text.ForceMeshUpdate();

            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
        finally
        {
            _isApplyingEditorPreview = false;
        }
    }
#endif

    public void ShowText(string script)
    {
        ResolveReferences();
        ValidateReferences();
        CaptureLayoutOrigins();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        ConfigureComponents();
        script ??= string.Empty;
        UpdateLayout(script);

        if (script.Length == 0)
        {
            _text.text = string.Empty;
            return;
        }

        _typewriter.ShowText(script);
    }

    public void CompleteTyping()
    {
        if (_typewriter != null && _typewriter.isShowingText)
        {
            _typewriter.SkipTypewriter();
        }
    }

    public void HideImmediate()
    {
        if (_typewriter != null && _typewriter.isShowingText)
        {
            _typewriter.SkipTypewriter();
        }

        if (_text != null)
        {
            _text.text = string.Empty;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void ValidateReferences()
    {
        ResolveReferences();

        if (_tailRenderer == null ||
            _textAreaRenderer == null ||
            _canvas == null ||
            _canvasRect == null ||
            _text == null ||
            _textAnimator == null ||
            _typewriter == null)
        {
            throw new InvalidOperationException(
                $"DialogueBubbleView '{name}' requires Bubble/TextArea/Canvas/Text (TMP) with " +
                "SpriteRenderer, TextAnimator_TMP, and TypewriterByCharacter components.");
        }
    }

    private void UpdateLayout(string script)
    {
        var minWidth = Mathf.Max(0.1f, Mathf.Min(_minimumSize.x, _maximumSize.x));
        var minHeight = Mathf.Max(0.1f, Mathf.Min(_minimumSize.y, _maximumSize.y));
        var maxWidth = Mathf.Max(minWidth, _maximumSize.x);
        var maxHeight = Mathf.Max(minHeight, _maximumSize.y);
        var availableTextWidth = Mathf.Max(0.1f, maxWidth - _textPadding.x);

        var preferred = string.IsNullOrEmpty(script)
            ? Vector2.zero
            : _text.GetPreferredValues(script, availableTextWidth, 0f);

        var bodySize = new Vector2(
            Mathf.Clamp(preferred.x + _textPadding.x, minWidth, maxWidth),
            Mathf.Clamp(preferred.y + _textPadding.y, minHeight, maxHeight));

        var textSize = new Vector2(
            Mathf.Max(0.1f, bodySize.x - _textPadding.x),
            Mathf.Max(0.1f, bodySize.y - _textPadding.y));

        _textAreaRenderer.size = bodySize;

        var textAreaTransform = _textAreaRenderer.transform;
        textAreaTransform.localPosition = _textAreaAuthoredLocalPosition;

        var heightGrowth = Mathf.Max(0f, bodySize.y - minHeight);
        var canvasLocalPosition = _canvasAuthoredLocalPosition;
        canvasLocalPosition.y += heightGrowth * 0.5f;
        _canvasRect.localPosition = canvasLocalPosition;

        _canvasRect.sizeDelta = textSize;
        _textRect.sizeDelta = textSize;
    }

    private void ConfigureComponents()
    {
        if (_textAreaRenderer != null)
        {
            _textAreaRenderer.transform.localScale = Vector3.one;
            _textAreaRenderer.drawMode = SpriteDrawMode.Sliced;
        }

        ConfigureTextLayout();

        if (_textAnimator != null)
        {
            // Keep the typewriter reveal, but show each character at its final scale immediately.
            _textAnimator.DefaultAppearancesTags = Array.Empty<string>();
        }

        if (_typewriter != null)
        {
            _typewriter.useTypeWriter = true;
            _typewriter.startTypewriterMode = TypewriterCore.StartTypewriterMode.OnShowText;
        }
    }

    private void ConfigureTextLayout()
    {
        if (_text == null)
        {
            return;
        }

        _text.raycastTarget = false;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = Mathf.Min(_minimumFontSize, _maximumFontSize);
        _text.fontSizeMax = Mathf.Max(_minimumFontSize, _maximumFontSize);
        _text.overflowMode = TextOverflowModes.Overflow;
    }

    private void CaptureLayoutOrigins()
    {
        if (_layoutOriginsCaptured || _textAreaRenderer == null || _canvasRect == null)
        {
            return;
        }

        _textAreaAuthoredLocalPosition = _textAreaRenderer.transform.localPosition;
        _canvasAuthoredLocalPosition = _canvasRect.localPosition;
        var minHeight = Mathf.Max(0.1f, Mathf.Min(_minimumSize.y, _maximumSize.y));
        var previewHeightGrowth = Mathf.Max(0f, _textAreaRenderer.size.y - minHeight);
        _canvasAuthoredLocalPosition.y -= previewHeightGrowth * 0.5f;
        _layoutOriginsCaptured = true;
    }

    private void ResolveReferences()
    {
        if (_tailRenderer == null)
        {
            _tailRenderer = GetComponent<SpriteRenderer>();
        }

        var textArea = transform.Find("TextArea");
        if (_textAreaRenderer == null && textArea != null)
        {
            _textAreaRenderer = textArea.GetComponent<SpriteRenderer>();
        }

        if (_canvas == null)
        {
            _canvas = GetComponentInChildren<Canvas>(true);
        }

        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

        if (_text == null)
        {
            _text = GetComponentInChildren<TMP_Text>(true);
        }

        _textRect = _text != null ? _text.rectTransform : null;

        if (_textAnimator == null && _text != null)
        {
            _textAnimator = _text.GetComponent<TextAnimator_TMP>();
        }

        if (_typewriter == null && _text != null)
        {
            _typewriter = _text.GetComponent<TypewriterByCharacter>();
        }
    }
}
