using System;
using ExcelData;
using TMPro;
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
    [SerializeField] private RectTransform _loadingTrain;
    [SerializeField] private UnityEngine.UI.Image _loadingBuildings;
    [SerializeField] private TMP_Text _loadingText;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float _minimumLoadingDuration = 1f;
    [SerializeField, Min(0f)] private float _trainBounceHeight = 3f;
    [SerializeField, Min(0.01f)] private float _trainBounceStepDuration = 0.11f;
    [SerializeField, Min(0.05f)] private float _loadingDotInterval = 0.4f;
    [SerializeField, Range(0, 16)] private int _loadingSpeedLineCount = 4;
    [SerializeField] private Vector2 _loadingSpeedLineSpeedRange = new Vector2(280f, 420f);
    [SerializeField] private Vector2 _loadingSpeedLineLengthRange = new Vector2(24f, 64f);
    [SerializeField] private Vector2 _loadingSpeedLineVerticalRange = new Vector2(-75f, 75f);
    [SerializeField, Min(0f)] private float _loadingBuildingScrollSpeed = 0.08f;

    [Header("Data")]
    [SerializeField] private SceneData _sceneData;
    [SerializeField] private DialogueDataSO _dialogueData;
    [SerializeField] private CocktailDatabaseSO _cocktailDatabase;

    protected override void Configure(IContainerBuilder builder)
    {
        ValidateReferences();

        builder.RegisterInstance(_sceneData);
        builder.RegisterInstance(_dialogueData);
        builder.RegisterInstance(_cocktailDatabase);
        builder.RegisterComponent(_audioManager);
        builder.RegisterInstance(new LoadingUIStarter(
            _loadingScreen,
            _fadeScreen,
            _loadingTrain,
            _loadingBuildings,
            _loadingText,
            _fadeDuration,
            _minimumLoadingDuration,
            _trainBounceHeight,
            _trainBounceStepDuration,
            _loadingDotInterval,
            _loadingSpeedLineCount,
            _loadingSpeedLineSpeedRange,
            _loadingSpeedLineLengthRange,
            _loadingSpeedLineVerticalRange,
            _loadingBuildingScrollSpeed));

        builder.Register<DialogueDB>(Lifetime.Singleton);
        builder.Register<DialogueEventBus>(Lifetime.Singleton);
        builder.Register<DialogueManager>(Lifetime.Singleton);
        builder.Register<CocktailCollectionState>(Lifetime.Singleton);
        builder.Register<CocktailCollectionPersistence>(Lifetime.Singleton);
        builder.Register<CocktailEventBus>(Lifetime.Singleton);
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

        if (_dialogueData == null)
        {
            throw new InvalidOperationException("BaseScope requires a DialogueDataSO asset.");
        }

        if (_cocktailDatabase == null)
        {
            throw new InvalidOperationException("BaseScope requires a CocktailDatabaseSO asset.");
        }

        _cocktailDatabase.ValidateOrThrow();

        if (_loadingScreen == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap loading screen GameObject.");
        }

        if (_fadeScreen == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap fade screen CanvasGroup.");
        }

        if (_loadingTrain == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap loading train RectTransform.");
        }

        if (_loadingBuildings == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap loading buildings Image.");
        }

        if (_loadingText == null)
        {
            throw new InvalidOperationException("BaseScope requires the Bootstrap loading text component.");
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
