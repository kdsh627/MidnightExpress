using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class HomeInitiator : IInitiator
{
    public HomeInitiator(AudioManager audioManager)
    {

    }

    public async UniTask GameInitialize(CancellationToken token)
    {
        await LoadImage(token);
    }

    private UniTask LoadImage(CancellationToken token)
    {
        return UniTask.CompletedTask;
    }
}
