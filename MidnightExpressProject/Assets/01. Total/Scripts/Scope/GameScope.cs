using VContainer;
using VContainer.Unity;

public sealed class GameScope : ManagedSceneScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameEndTrigger>();
        builder.Register<GameInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
