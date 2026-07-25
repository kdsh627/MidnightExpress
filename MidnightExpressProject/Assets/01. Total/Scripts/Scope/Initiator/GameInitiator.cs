using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public class GameInitiator : IInitiator
{
    private readonly AudioManager _audioManager;

    [Inject]
    public GameInitiator(AudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public UniTask GameInitialize(CancellationToken token)
    {
        _audioManager.PlayBgm(AudioManager.Bgm.InGame);
        return UniTask.CompletedTask;
    }
}
