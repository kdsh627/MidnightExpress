using System.Threading;
using Cysharp.Threading.Tasks;

public interface IInitiator
{
    public UniTask GameInitialize(CancellationToken token);
}
