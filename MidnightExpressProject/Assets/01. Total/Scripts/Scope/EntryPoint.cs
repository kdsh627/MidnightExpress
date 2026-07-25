using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public class EntryPoint : IInitializable, IAsyncStartable, IDisposable
{
    [Inject] private IInitiator _initiator;

    private CancellationTokenSource _cts;

    public void Initialize()
    {
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
       if(_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    public async UniTask StartAsync(CancellationToken cancellation = default)
    {
        await _initiator.GameInitialize(_cts.Token);
    }
}