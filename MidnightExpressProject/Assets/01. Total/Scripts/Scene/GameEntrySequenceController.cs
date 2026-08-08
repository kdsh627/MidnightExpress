using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameEntrySequenceController : MonoBehaviour
{
    [Header("Completion")]
    [Tooltip("Animation or Timeline events can call CompleteSequence earlier.")]
    [SerializeField] private bool _useFallbackTimer = true;
    [Min(0f)]
    [SerializeField] private float _fallbackDurationSeconds = 5f;

    private SceneFlowController _sceneFlow;
    private CancellationTokenSource _sequenceCts;
    private bool _transitionRequested;

    public void Initialize(SceneFlowController sceneFlow, CancellationToken sceneLifetimeToken)
    {
        if (_sequenceCts != null)
        {
            return;
        }

        _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(sceneLifetimeToken);

        if (_useFallbackTimer)
        {
            CompleteAfterFallbackAsync(_sequenceCts.Token).Forget();
        }
    }

    /// <summary>
    /// Call this from the final Animation/Timeline event of the entry cinematic.
    /// </summary>
    public void CompleteSequence()
    {
        RequestGameSceneAsync().Forget();
    }

    private async UniTask CompleteAfterFallbackAsync(CancellationToken token)
    {
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_fallbackDurationSeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);

            await RequestGameSceneAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async UniTask RequestGameSceneAsync()
    {
        if (_transitionRequested || _sceneFlow == null)
        {
            return;
        }

        _transitionRequested = true;

        try
        {
            var accepted = await _sceneFlow.GoToGameAsync();
            if (!accepted && this != null)
            {
                _transitionRequested = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this != null)
            {
                _transitionRequested = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (_sequenceCts == null)
        {
            return;
        }

        _sequenceCts.Cancel();
        _sequenceCts.Dispose();
        _sequenceCts = null;
    }
}
