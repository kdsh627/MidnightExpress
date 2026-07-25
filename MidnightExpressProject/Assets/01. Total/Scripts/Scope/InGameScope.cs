using VContainer;
using VContainer.Unity;

public class InGameScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<GameInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
