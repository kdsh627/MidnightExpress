using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoadingUIStarter
{
    private const string LoadingTextBase = "로딩중";

    private readonly GameObject _loadingUI;
    private readonly CanvasGroup _fadeUI;
    private readonly RectTransform _train;
    private readonly Image _buildingsImage;
    private readonly TMP_Text _loadingText;
    private readonly float _fadeDuration;
    private readonly float _minimumDisplayDuration;
    private readonly float _trainBounceHeight;
    private readonly float _trainBounceStepDuration;
    private readonly float _loadingDotInterval;
    private readonly int _speedLineCount;
    private readonly Vector2 _speedLineSpeedRange;
    private readonly Vector2 _speedLineLengthRange;
    private readonly Vector2 _speedLineVerticalRange;
    private readonly float _buildingScrollSpeed;
    private readonly Vector2 _trainOrigin;
    private readonly List<SpeedLine> _speedLines = new List<SpeedLine>();

    private CancellationTokenSource _animationCts;
    private float _shownAt;
    private System.Random _random;
    private Material _buildingsSourceMaterial;
    private Material _buildingsRuntimeMaterial;
    private float _buildingScrollOffset;

    private static readonly int BuildingScrollOffsetId = Shader.PropertyToID("_BuildingScrollOffset");
    private static readonly int BuildingRegionStartYId = Shader.PropertyToID("_BuildingRegionStartY");

    private sealed class SpeedLine
    {
        public RectTransform RectTransform;
        public float Speed;
        public float Length;
    }

    public LoadingUIStarter(
        GameObject loadingUI,
        CanvasGroup fadeUI,
        RectTransform train,
        Image buildingsImage,
        TMP_Text loadingText,
        float fadeDuration,
        float minimumDisplayDuration,
        float trainBounceHeight,
        float trainBounceStepDuration,
        float loadingDotInterval,
        int speedLineCount,
        Vector2 speedLineSpeedRange,
        Vector2 speedLineLengthRange,
        Vector2 speedLineVerticalRange,
        float buildingScrollSpeed)
    {
        _loadingUI = loadingUI != null
            ? loadingUI
            : throw new ArgumentNullException(nameof(loadingUI));
        _fadeUI = fadeUI != null
            ? fadeUI
            : throw new ArgumentNullException(nameof(fadeUI));
        _train = train != null
            ? train
            : throw new ArgumentNullException(nameof(train));
        _buildingsImage = buildingsImage != null
            ? buildingsImage
            : throw new ArgumentNullException(nameof(buildingsImage));
        _loadingText = loadingText != null
            ? loadingText
            : throw new ArgumentNullException(nameof(loadingText));
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _minimumDisplayDuration = Mathf.Max(0f, minimumDisplayDuration);
        _trainBounceHeight = Mathf.Max(0f, trainBounceHeight);
        _trainBounceStepDuration = Mathf.Max(0.01f, trainBounceStepDuration);
        _loadingDotInterval = Mathf.Max(0.05f, loadingDotInterval);
        _speedLineCount = Mathf.Max(0, speedLineCount);
        _speedLineSpeedRange = SortRange(speedLineSpeedRange, 1f);
        _speedLineLengthRange = SortRange(speedLineLengthRange, 1f);
        _speedLineVerticalRange = new Vector2(
            Mathf.Min(speedLineVerticalRange.x, speedLineVerticalRange.y),
            Mathf.Max(speedLineVerticalRange.x, speedLineVerticalRange.y));
        _buildingScrollSpeed = Mathf.Max(0f, buildingScrollSpeed);
        _trainOrigin = _train.anchoredPosition;

        HideImmediate();
    }

    public async UniTask ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _loadingUI.SetActive(false);
        SetFadeActive(true);
        _fadeUI.alpha = 0f;

        await FadeToAsync(1f, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _loadingUI.SetActive(true);
        StartAnimations(cancellationToken);
        _shownAt = Time.realtimeSinceStartup;

        await FadeToAsync(0f, cancellationToken);
        SetFadeActive(false);
    }

    public async UniTask HideAsync(CancellationToken cancellationToken)
    {
        var remainingDisplayTime = _minimumDisplayDuration
            - (Time.realtimeSinceStartup - _shownAt);
        if (remainingDisplayTime > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(remainingDisplayTime),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update,
                cancellationToken);
        }

        SetFadeActive(true);
        _fadeUI.alpha = 0f;
        await FadeToAsync(1f, cancellationToken);

        StopAnimations();
        _loadingUI.SetActive(false);

        await FadeToAsync(0f, cancellationToken);
        SetFadeActive(false);
    }

    public void HideImmediate()
    {
        StopAnimations();
        if (_loadingUI != null)
        {
            _loadingUI.SetActive(false);
        }

        ResetAnimationVisuals();

        if (_fadeUI == null)
        {
            return;
        }

        _fadeUI.alpha = 0f;
        SetFadeActive(false);
    }

    private void StartAnimations(CancellationToken cancellationToken)
    {
        StopAnimations();
        ResetAnimationVisuals();
        SetupBuildingsRuntimeMaterial();
        CreateSpeedLines();
        _animationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        AnimateLoadingUIAsync(_animationCts.Token).Forget();
    }

    private void StopAnimations()
    {
        if (_animationCts == null)
        {
            return;
        }

        if (!_animationCts.IsCancellationRequested)
        {
            _animationCts.Cancel();
        }

        _animationCts.Dispose();
        _animationCts = null;
        DestroySpeedLines();
        RestoreBuildingsMaterial();
        ResetAnimationVisuals();
    }

    private async UniTask AnimateLoadingUIAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WhenAll(
                AnimateTrainAsync(cancellationToken),
                AnimateLoadingTextAsync(cancellationToken),
                AnimateSpeedLinesAsync(cancellationToken),
                AnimateBuildingsAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal completion path when the loading screen closes.
        }
    }

    private async UniTask AnimateTrainAsync(CancellationToken cancellationToken)
    {
        // Short asymmetric steps resemble rail joints without introducing
        // sub-pixel blur to the pixel-art UI.
        var bouncePattern = new[] { 0f, 0.35f, 0f, -0.25f, 0f, 0.7f, 0.15f, -0.35f };
        var stepIndex = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = Mathf.Round(bouncePattern[stepIndex] * _trainBounceHeight);
            _train.anchoredPosition = _trainOrigin + Vector2.up * offset;
            stepIndex = (stepIndex + 1) % bouncePattern.Length;

            await UniTask.Delay(
                TimeSpan.FromSeconds(_trainBounceStepDuration),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update,
                cancellationToken);
        }
    }

    private async UniTask AnimateLoadingTextAsync(CancellationToken cancellationToken)
    {
        var dotCount = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _loadingText.text = LoadingTextBase + new string('.', dotCount);
            dotCount = dotCount % 3 + 1;

            await UniTask.Delay(
                TimeSpan.FromSeconds(_loadingDotInterval),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update,
                cancellationToken);
        }
    }

    private async UniTask AnimateSpeedLinesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var halfWidth = ((RectTransform)_loadingUI.transform).rect.width * 0.5f;
            for (var index = 0; index < _speedLines.Count; index++)
            {
                var line = _speedLines[index];
                var position = line.RectTransform.anchoredPosition;
                position.x -= line.Speed * Time.unscaledDeltaTime;
                position.x = Mathf.Round(position.x);

                if (position.x + line.Length * 0.5f < -halfWidth)
                {
                    RespawnSpeedLine(line, halfWidth, false);
                    continue;
                }

                line.RectTransform.anchoredPosition = position;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private async UniTask AnimateBuildingsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_buildingsRuntimeMaterial != null)
            {
                _buildingScrollOffset = Mathf.Repeat(
                    _buildingScrollOffset + _buildingScrollSpeed * Time.unscaledDeltaTime,
                    1f);
                _buildingsRuntimeMaterial.SetFloat(BuildingScrollOffsetId, _buildingScrollOffset);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private void SetupBuildingsRuntimeMaterial()
    {
        RestoreBuildingsMaterial();
        _buildingsSourceMaterial = _buildingsImage.material;
        if (_buildingsSourceMaterial == null ||
            !_buildingsSourceMaterial.HasProperty(BuildingScrollOffsetId) ||
            !_buildingsSourceMaterial.HasProperty(BuildingRegionStartYId))
        {
            return;
        }

        _buildingsRuntimeMaterial = new Material(_buildingsSourceMaterial)
        {
            name = _buildingsSourceMaterial.name + " (Buildings Runtime)",
            hideFlags = HideFlags.DontSave
        };
        _buildingScrollOffset = 0f;
        _buildingsRuntimeMaterial.SetFloat(BuildingScrollOffsetId, 0f);
        _buildingsRuntimeMaterial.SetFloat(BuildingRegionStartYId, 0f);
        _buildingsImage.material = _buildingsRuntimeMaterial;
    }

    private void RestoreBuildingsMaterial()
    {
        if (_buildingsRuntimeMaterial == null)
        {
            return;
        }

        if (_buildingsImage != null)
        {
            _buildingsImage.material = _buildingsSourceMaterial;
        }

        if (_buildingsRuntimeMaterial != null)
        {
            UnityEngine.Object.Destroy(_buildingsRuntimeMaterial);
        }

        _buildingsRuntimeMaterial = null;
        _buildingsSourceMaterial = null;
        _buildingScrollOffset = 0f;
    }

    private void CreateSpeedLines()
    {
        DestroySpeedLines();
        _random = new System.Random(1977);
        var parentRect = (RectTransform)_loadingUI.transform;
        var halfWidth = parentRect.rect.width * 0.5f;

        for (var index = 0; index < _speedLineCount; index++)
        {
            var lineObject = new GameObject(
                "LoadingSpeedLine_" + index,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            lineObject.transform.SetParent(parentRect, false);
            lineObject.transform.SetAsFirstSibling();

            var image = lineObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            var speedLine = new SpeedLine
            {
                RectTransform = lineObject.GetComponent<RectTransform>()
            };
            speedLine.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            speedLine.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            speedLine.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            _speedLines.Add(speedLine);
            RespawnSpeedLine(speedLine, halfWidth, true);
        }
    }

    private void RespawnSpeedLine(SpeedLine line, float halfWidth, bool scatterAcrossScreen)
    {
        line.Length = RandomRange(_speedLineLengthRange);
        line.Speed = RandomRange(_speedLineSpeedRange);
        line.RectTransform.sizeDelta = new Vector2(Mathf.Round(line.Length), 1f);
        line.RectTransform.anchoredPosition = new Vector2(
            scatterAcrossScreen
                ? Mathf.Round(RandomRange(new Vector2(-halfWidth, halfWidth)))
                : Mathf.Round(halfWidth + line.Length * 0.5f),
            Mathf.Round(RandomRange(_speedLineVerticalRange)));
    }

    private void DestroySpeedLines()
    {
        for (var index = _speedLines.Count - 1; index >= 0; index--)
        {
            var line = _speedLines[index];
            if (line?.RectTransform != null)
            {
                UnityEngine.Object.Destroy(line.RectTransform.gameObject);
            }
        }

        _speedLines.Clear();
    }

    private float RandomRange(Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
    }

    private static Vector2 SortRange(Vector2 range, float minimumValue)
    {
        var minimum = Mathf.Max(minimumValue, Mathf.Min(range.x, range.y));
        var maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
        return new Vector2(minimum, maximum);
    }

    private void ResetAnimationVisuals()
    {
        if (_train != null)
        {
            _train.anchoredPosition = _trainOrigin;
        }

        if (_loadingText != null)
        {
            _loadingText.text = LoadingTextBase + ".";
        }
    }

    private async UniTask FadeToAsync(float targetAlpha, CancellationToken cancellationToken)
    {
        var startAlpha = _fadeUI.alpha;
        if (_fadeDuration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
        {
            _fadeUI.alpha = targetAlpha;
            return;
        }

        var elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            _fadeUI.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / _fadeDuration);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        _fadeUI.alpha = targetAlpha;
    }

    private void SetFadeActive(bool isActive)
    {
        if (_fadeUI == null)
        {
            return;
        }

        _fadeUI.interactable = false;
        _fadeUI.blocksRaycasts = isActive;
        _fadeUI.gameObject.SetActive(isActive);
    }
}
