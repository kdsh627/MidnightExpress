using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class LoadingUIStarter
{
    private readonly GameObject _loadingUI;
    private readonly CanvasGroup _fadeUI;
    private readonly float _fadeDuration;

    public LoadingUIStarter(GameObject loadingUI, CanvasGroup fadeUI, float fadeDuration)
    {
        _loadingUI = loadingUI != null
            ? loadingUI
            : throw new ArgumentNullException(nameof(loadingUI));
        _fadeUI = fadeUI != null
            ? fadeUI
            : throw new ArgumentNullException(nameof(fadeUI));
        _fadeDuration = Mathf.Max(0f, fadeDuration);

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
    }

    public async UniTask HideAsync(CancellationToken cancellationToken)
    {
        if (_loadingUI != null)
        {
            _loadingUI.SetActive(false);
        }

        if (_fadeUI == null)
        {
            return;
        }

        SetFadeActive(true);
        await FadeToAsync(0f, cancellationToken);
        SetFadeActive(false);
    }

    public void HideImmediate()
    {
        if (_loadingUI != null)
        {
            _loadingUI.SetActive(false);
        }

        if (_fadeUI == null)
        {
            return;
        }

        _fadeUI.alpha = 0f;
        SetFadeActive(false);
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
        _fadeUI.interactable = false;
        _fadeUI.blocksRaycasts = isActive;
        _fadeUI.gameObject.SetActive(isActive);
    }
}
