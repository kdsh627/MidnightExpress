using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public sealed class EntryPoint : IInitializable, IAsyncStartable, IDisposable
{
    private readonly IInitiator _initiator;
    private readonly LifetimeScope _scope;
    private readonly SceneInitializationRegistry _initializationRegistry;

    private CancellationTokenSource _lifetimeCts;

    [Inject]
    public EntryPoint(
        IInitiator initiator,
        LifetimeScope scope,
        SceneInitializationRegistry initializationRegistry)
    {
        _initiator = initiator;
        _scope = scope;
        _initializationRegistry = initializationRegistry;
    }

    public void Initialize()
    {
        _lifetimeCts = new CancellationTokenSource();
    }

    public async UniTask StartAsync(CancellationToken cancellation = default)
    {
        var sceneName = _scope.gameObject.scene.name;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation,
            _lifetimeCts.Token);

        try
        {
            await _initiator.GameInitialize(linkedCts.Token);
            _initializationRegistry.ReportReady(sceneName);
        }
        catch (OperationCanceledException exception)
        {
            _initializationRegistry.ReportCanceled(sceneName, exception.CancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            _initializationRegistry.ReportFailure(sceneName, exception);
            throw;
        }
    }

    public void Dispose()
    {
        if (_lifetimeCts == null)
        {
            return;
        }

        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _lifetimeCts = null;
    }
}
