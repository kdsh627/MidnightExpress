using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class TitleInitiator : IInitiator, IDisposable
{
    private readonly StartTicketButton _startButton;
    private readonly SceneFlowController _sceneFlow;

    [Inject]
    public TitleInitiator(StartTicketButton startButton, SceneFlowController sceneFlow)
    {
        _startButton = startButton;
        _sceneFlow = sceneFlow;
    }

    public UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _startButton.Initialize(_sceneFlow);
        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_startButton != null)
        {
            _startButton.Shutdown();
        }
    }
}
