using VContainer;
using VContainer.Unity;

public sealed class GameScope : ManagedSceneScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameEndTrigger>();
        builder.RegisterComponentInHierarchy<DialogueActorRegistry>();
        builder.RegisterComponentInHierarchy<DialogueSceneController>();
        builder.RegisterComponentInHierarchy<DialogueSceneEventBindings>();
        builder.RegisterComponentInHierarchy<CocktailCastingController>();
        builder.RegisterComponentInHierarchy<CocktailCollectionController>();
        builder.Register<GameInitiator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Scoped).AsSelf();
    }
}
