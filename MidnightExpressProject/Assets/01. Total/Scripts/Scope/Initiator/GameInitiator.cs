using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class GameInitiator : IInitiator
{
    private readonly AudioManager _audioManager;
    private readonly GameEndTrigger _gameEndTrigger;
    private readonly SceneFlowController _sceneFlow;

    [Inject]
    public GameInitiator(
        AudioManager audioManager,
        GameEndTrigger gameEndTrigger,
        SceneFlowController sceneFlow)
    {
        _audioManager = audioManager;
        _gameEndTrigger = gameEndTrigger;
        _sceneFlow = sceneFlow;
    }

    public UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        _gameEndTrigger.Initialize(_sceneFlow);
        _audioManager.TryPlayBgm(AudioManager.Bgm.InGame);

        return UniTask.CompletedTask;
    }
}
