using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueActorHoverOutline : MonoBehaviour
{
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Transform _body;
    [SerializeField] private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
    [SerializeField] private Shader _outlineShader;
    [SerializeField] private Color _outlineColor = new Color(1f, 0.76f, 0.34f, 1f);
    [SerializeField, Range(0.5f, 4f)] private float _outlineWidth = 1.5f;

    private Material[] _originalMaterials = Array.Empty<Material>();
    private Material[] _outlineMaterials = Array.Empty<Material>();
    private bool _selectionEnabled = true;
    private bool _isInitialized;

    private void Awake()
    {
        Initialize();
        SetHovered(false);
    }

    public void SetHovered(bool hovered)
    {
        Initialize();
        float enabled = hovered && _selectionEnabled ? 1f : 0f;

        foreach (Material material in _outlineMaterials)
        {
            if (material == null)
            {
                continue;
            }

            material.SetFloat(OutlineEnabledId, enabled);
            material.SetColor(OutlineColorId, _outlineColor);
            material.SetFloat(OutlineWidthId, _outlineWidth);
        }
    }

    public void SetSelectionEnabled(bool enabled)
    {
        _selectionEnabled = enabled;
        if (!enabled)
        {
            SetHovered(false);
        }
    }

    public void ValidateConfiguration()
    {
        ResolveReferences();
        if (_outlineShader == null)
        {
            throw new InvalidOperationException($"Dialogue hover outline on '{name}' requires an outline shader.");
        }

        if (_renderers.Length == 0)
        {
            throw new InvalidOperationException($"Dialogue hover outline on '{name}' requires at least one Body SpriteRenderer.");
        }
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        ResolveReferences();
        if (_outlineShader == null || _renderers.Length == 0)
        {
            return;
        }

        _originalMaterials = new Material[_renderers.Length];
        _outlineMaterials = new Material[_renderers.Length];

        for (int index = 0; index < _renderers.Length; index++)
        {
            SpriteRenderer renderer = _renderers[index];
            if (renderer == null)
            {
                continue;
            }

            Material original = renderer.sharedMaterial;
            var outlineMaterial = new Material(_outlineShader)
            {
                name = $"{name} Hover Outline (Runtime)",
                hideFlags = HideFlags.DontSave
            };

            if (original != null)
            {
                outlineMaterial.CopyPropertiesFromMaterial(original);
            }

            outlineMaterial.SetFloat(OutlineEnabledId, 0f);
            outlineMaterial.SetColor(OutlineColorId, _outlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, _outlineWidth);
            _originalMaterials[index] = original;
            _outlineMaterials[index] = outlineMaterial;
            renderer.sharedMaterial = outlineMaterial;
        }

        _isInitialized = true;
    }

    private void ResolveReferences()
    {
        if (_body == null)
        {
            _body = transform.Find("Body");
        }

        if (_body != null && !HasAnyRenderer(_renderers))
        {
            _renderers = _body.GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void OnDestroy()
    {
        for (int index = 0; index < _outlineMaterials.Length; index++)
        {
            if (index < _renderers.Length
                && _renderers[index] != null
                && _renderers[index].sharedMaterial == _outlineMaterials[index])
            {
                _renderers[index].sharedMaterial = _originalMaterials[index];
            }

            if (_outlineMaterials[index] != null)
            {
                Destroy(_outlineMaterials[index]);
            }
        }
    }

    private static bool HasAnyRenderer(SpriteRenderer[] renderers)
    {
        if (renderers == null)
        {
            return false;
        }

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null)
            {
                return true;
            }
        }

        return false;
    }
}
