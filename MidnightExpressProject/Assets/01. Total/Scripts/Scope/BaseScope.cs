using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BaseScope : LifetimeScope
{
    [Header("Audio")]
    [SerializeField] private AudioManager _audioManager;

    [Header("Loading UI")]
    [SerializeField] private GameObject _loadingScreen;
    [SerializeField] private CanvasGroup _fadeScreen;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.35f;

    [Header("Data")]
    [SerializeField] private SceneData _sceneData;

    protected override void Configure(IContainerBuilder builder)
    {
        ValidateReferences();

        builder.RegisterInstance(_sceneData);
        builder.RegisterComponent(_audioManager);
        builder.RegisterInstance(new LoadingUIStarter(_loadingScreen, _fadeScreen, _fadeDuration));

        builder.Register<SceneInitializationRegistry>(Lifetime.Singleton);
        builder.Register<CoreSceneLoader>(Lifetime.Singleton);
        builder.Register<SceneTransitionManager>(Lifetime.Singleton);
        builder.Register<SceneFlowController>(Lifetime.Singleton);
        builder.Register<BaseInitiator>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<EntryPoint>(Lifetime.Singleton).AsSelf();
    }

    private void ValidateReferences()
    {
        if (_sceneData == null)
        {
            throw new InvalidOperationException("BaseScope requires a SceneData asset.");
        }

        if (_loadingScreen == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap loading screen GameObject.");
        }

        if (_fadeScreen == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap fade screen CanvasGroup.");
        }

        if (_audioManager == null)
        {
            throw new InvalidOperationException("BaseScope requires an AudioManager component.");
        }

        if (!_audioManager.TryGetComponent<AudioListener>(out _))
        {
            throw new InvalidOperationException(
                "The Bootstrap AudioManager requires the persistent AudioListener component.");
        }
    }
}
