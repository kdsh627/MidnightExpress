using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueActorAppearance : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _body;
    [SerializeField] private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
    [SerializeField] private bool _startHidden = true;

    [Header("Depth Reveal")]
    [SerializeField] private Vector3 _hiddenLocalOffset = Vector3.zero;
    [SerializeField, Range(0.75f, 1f)] private float _hiddenScaleMultiplier = 0.94f;
    [SerializeField] private Color _hiddenTint = new Color(0.68f, 0.7f, 0.74f, 1f);
    [SerializeField, Min(0.05f)] private float _duration = 0.85f;
    [SerializeField, Min(0f)] private float _postAppearanceDelay = 0.4f;
    [SerializeField] private AnimationCurve _movementCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.78f, 1.035f, 0.25f, 0.25f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve _tintCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 _shownLocalPosition;
    private Vector3 _shownLocalScale;
    private Color[] _shownColors = Array.Empty<Color>();
    private bool _isPrepared;
    private bool _hasAppeared;

    public float PostAppearanceDelay => _postAppearanceDelay;
    public bool HasAppeared => _hasAppeared;

    private void Awake()
    {
        ResolveReferences();
        CacheShownState();

        if (_startHidden)
        {
            PrepareHiddenImmediate();
        }
    }

    public void PrepareHiddenImmediate()
    {
        ResolveReferences();
        if (!_isPrepared)
        {
            CacheShownState();
        }

        _body.localPosition = _shownLocalPosition + _hiddenLocalOffset;
        _body.localScale = _shownLocalScale * _hiddenScaleMultiplier;
        for (int index = 0; index < _renderers.Length; index++)
        {
            SpriteRenderer renderer = _renderers[index];
            if (renderer != null)
            {
                renderer.color = MultiplyRgb(_shownColors[index], _hiddenTint);
            }
        }

        _hasAppeared = false;
    }

    public async UniTask PlayAsync(CancellationToken cancellationToken)
    {
        ResolveReferences();
        if (!_isPrepared)
        {
            CacheShownState();
        }

        if (!_hasAppeared)
        {
            PrepareHiddenImmediate();
            Vector3 hiddenPosition = _shownLocalPosition + _hiddenLocalOffset;
            Vector3 hiddenScale = _shownLocalScale * _hiddenScaleMultiplier;
            float elapsed = 0f;

            while (elapsed < _duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01(elapsed / _duration);
                ApplyFrame(hiddenPosition, hiddenScale, progress);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.unscaledDeltaTime;
            }

            ApplyFrame(hiddenPosition, hiddenScale, 1f);
            _hasAppeared = true;
        }

        if (_postAppearanceDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_postAppearanceDelay),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update,
                cancellationToken);
        }
    }

    public void ValidateConfiguration()
    {
        ResolveReferences();
        if (_body == null)
        {
            throw new InvalidOperationException($"Dialogue appearance on '{name}' requires a Body transform.");
        }

        if (_renderers.Length == 0)
        {
            throw new InvalidOperationException($"Dialogue appearance on '{name}' requires at least one Body SpriteRenderer.");
        }
    }

    private void ApplyFrame(Vector3 hiddenPosition, Vector3 hiddenScale, float progress)
    {
        float movement = _movementCurve == null ? progress : _movementCurve.Evaluate(progress);
        float tint = _tintCurve == null ? progress : _tintCurve.Evaluate(progress);
        _body.localPosition = Vector3.LerpUnclamped(hiddenPosition, _shownLocalPosition, movement);
        _body.localScale = Vector3.LerpUnclamped(hiddenScale, _shownLocalScale, movement);

        for (int index = 0; index < _renderers.Length; index++)
        {
            SpriteRenderer renderer = _renderers[index];
            if (renderer != null)
            {
                Color hiddenColor = MultiplyRgb(_shownColors[index], _hiddenTint);
                renderer.color = Color.LerpUnclamped(hiddenColor, _shownColors[index], tint);
            }
        }
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

    private void CacheShownState()
    {
        if (_body == null)
        {
            return;
        }

        _shownLocalPosition = _body.localPosition;
        _shownLocalScale = _body.localScale;
        _shownColors = new Color[_renderers.Length];
        for (int index = 0; index < _renderers.Length; index++)
        {
            _shownColors[index] = _renderers[index] != null ? _renderers[index].color : Color.white;
        }

        _isPrepared = true;
    }

    private static Color MultiplyRgb(Color source, Color tint)
    {
        return new Color(source.r * tint.r, source.g * tint.g, source.b * tint.b, source.a);
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
