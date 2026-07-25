using VContainer;
using VContainer.Unity;

public class HomeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<HomeInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
