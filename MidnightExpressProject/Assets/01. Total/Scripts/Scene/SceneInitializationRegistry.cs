using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class SceneInitializationRegistry
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, UniTaskCompletionSource> _pendingScenes =
        new Dictionary<string, UniTaskCompletionSource>(StringComparer.Ordinal);

    public void Prepare(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            throw new ArgumentException("Scene name is required.", nameof(sceneName));
        }

        if (_pendingScenes.ContainsKey(sceneName))
        {
            throw new InvalidOperationException($"Scene initialization is already pending: {sceneName}");
        }

        _pendingScenes.Add(sceneName, new UniTaskCompletionSource());
    }

    public async UniTask WaitForReadyAsync(string sceneName, CancellationToken token)
    {
        if (!_pendingScenes.TryGetValue(sceneName, out var completionSource))
        {
            throw new InvalidOperationException($"Scene initialization was not prepared: {sceneName}");
        }

        try
        {
            await completionSource.Task
                .AttachExternalCancellation(token)
                .Timeout(InitializationTimeout, DelayType.UnscaledDeltaTime);
        }
        catch (TimeoutException exception)
        {
            completionSource.TrySetCanceled();
            throw new TimeoutException(
                $"Scene initialization did not complete within {InitializationTimeout.TotalSeconds:0} seconds: {sceneName}",
                exception);
        }
        finally
        {
            _pendingScenes.Remove(sceneName);
        }
    }

    public void ReportReady(string sceneName)
    {
        if (_pendingScenes.TryGetValue(sceneName, out var completionSource))
        {
            completionSource.TrySetResult();
        }
    }

    public void ReportFailure(string sceneName, Exception exception)
    {
        if (_pendingScenes.TryGetValue(sceneName, out var completionSource))
        {
            completionSource.TrySetException(exception);
        }
    }

    public void ReportCanceled(string sceneName, CancellationToken token)
    {
        if (_pendingScenes.TryGetValue(sceneName, out var completionSource))
        {
            completionSource.TrySetCanceled(token);
        }
    }

    public void Abandon(string sceneName)
    {
        if (_pendingScenes.TryGetValue(sceneName, out var completionSource))
        {
            completionSource.TrySetCanceled();
            _pendingScenes.Remove(sceneName);
        }
    }
}
