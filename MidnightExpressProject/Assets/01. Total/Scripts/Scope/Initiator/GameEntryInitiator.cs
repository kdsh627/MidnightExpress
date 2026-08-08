using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class GameEntryInitiator : IInitiator
{
    private readonly GameEntrySequenceController _sequenceController;
    private readonly SceneFlowController _sceneFlow;

    [Inject]
    public GameEntryInitiator(
        GameEntrySequenceController sequenceController,
        SceneFlowController sceneFlow)
    {
        _sequenceController = sequenceController;
        _sceneFlow = sceneFlow;
    }

    public UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _sequenceController.Initialize(_sceneFlow, token);
        return UniTask.CompletedTask;
    }
}
