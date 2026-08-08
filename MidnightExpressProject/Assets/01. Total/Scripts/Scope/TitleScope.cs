using VContainer;
using VContainer.Unity;

public sealed class TitleScope : ManagedSceneScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<StartTicketButton>();
        builder.Register<TitleInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
