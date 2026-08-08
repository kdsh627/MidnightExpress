using VContainer;
using VContainer.Unity;

public sealed class GameEntryScope : ManagedSceneScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameEntrySequenceController>();
        builder.Register<GameEntryInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
