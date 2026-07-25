using UnityEngine;
using VContainer;
using VContainer.Unity;

public class BaseScope : LifetimeScope
{
    [Header("----- Audio -----")]
    [SerializeField] private AudioManager _audioManager;

    [Header("----- UI Starter ------")]
    [SerializeField] private GameObject _loadingScreen;

    [Header("----- Data ------")]
    [SerializeField] private SceneData _sceneData;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponent(_loadingScreen);
        builder.RegisterComponent(_sceneData);
        builder.RegisterComponent(_audioManager);
        builder.Register<SceneLoader>(Lifetime.Singleton);
        builder.Register<SceneTransitionManager>(Lifetime.Singleton);
        builder.Register<BaseInitiator>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Singleton).AsSelf();
    }
}
