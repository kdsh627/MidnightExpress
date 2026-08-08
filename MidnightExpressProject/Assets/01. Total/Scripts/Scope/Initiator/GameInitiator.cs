using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class GameInitiator : IInitiator, IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly GameEndTrigger _gameEndTrigger;
    private readonly SceneFlowController _sceneFlow;
    private readonly DialogueManager _dialogueManager;
    private readonly DialogueEventBus _dialogueEventBus;
    private readonly DialogueActorRegistry _actorRegistry;
    private readonly DialogueSceneController _dialogueSceneController;
    private readonly DialogueSceneEventBindings _dialogueEventBindings;
    private readonly CocktailCastingController _cocktailCastingController;
    private readonly CocktailCollectionController _cocktailCollectionController;
    private readonly CocktailDatabaseSO _cocktailDatabase;
    private readonly CocktailCollectionState _cocktailCollection;
    private readonly CocktailEventBus _cocktailEventBus;

    private CancellationTokenRegistration _cleanupRegistration;
    private bool _isInitialized;

    [Inject]
    public GameInitiator(
        AudioManager audioManager,
        GameEndTrigger gameEndTrigger,
        SceneFlowController sceneFlow,
        DialogueManager dialogueManager,
        DialogueEventBus dialogueEventBus,
        DialogueActorRegistry actorRegistry,
        DialogueSceneController dialogueSceneController,
        DialogueSceneEventBindings dialogueEventBindings,
        CocktailCastingController cocktailCastingController,
        CocktailCollectionController cocktailCollectionController,
        CocktailDatabaseSO cocktailDatabase,
        CocktailCollectionState cocktailCollection,
        CocktailEventBus cocktailEventBus)
    {
        _audioManager = audioManager;
        _gameEndTrigger = gameEndTrigger;
        _sceneFlow = sceneFlow;
        _dialogueManager = dialogueManager;
        _dialogueEventBus = dialogueEventBus;
        _actorRegistry = actorRegistry;
        _dialogueSceneController = dialogueSceneController;
        _dialogueEventBindings = dialogueEventBindings;
        _cocktailCastingController = cocktailCastingController;
        _cocktailCollectionController = cocktailCollectionController;
        _cocktailDatabase = cocktailDatabase;
        _cocktailCollection = cocktailCollection;
        _cocktailEventBus = cocktailEventBus;
    }

    public UniTask GameInitialize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        _isInitialized = true;

        try
        {
            _actorRegistry.Initialize();
            _dialogueManager.AttachScene(_actorRegistry, token);
            _dialogueEventBindings.Initialize(_dialogueEventBus);
            _dialogueSceneController.Initialize(
                _dialogueManager,
                _dialogueEventBus,
                _actorRegistry,
                token);
            _cocktailCastingController.Initialize(
                _cocktailDatabase,
                _cocktailCollection,
                _cocktailEventBus,
                _dialogueSceneController,
                token);
            _cocktailCollectionController.Initialize(
                _cocktailDatabase,
                _cocktailCollection,
                _cocktailCastingController,
                _dialogueManager,
                token);
            _gameEndTrigger.Initialize(_sceneFlow, _dialogueEventBus);
            _audioManager.TryPlayBgm(AudioManager.Bgm.InGame);

            var registration = token.RegisterWithoutCaptureExecutionContext(CleanupFromCancellation);
            if (_isInitialized)
            {
                _cleanupRegistration = registration;
            }
            else
            {
                registration.Dispose();
            }
        }
        catch
        {
            Cleanup(true);
            throw;
        }

        return UniTask.CompletedTask;
    }

    public void Dispose()
    {
        Cleanup(true);
    }

    private void CleanupFromCancellation()
    {
        _cleanupRegistration = default;
        Cleanup(false);
    }

    private void Cleanup(bool disposeRegistration = true)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (disposeRegistration)
        {
            _cleanupRegistration.Dispose();
        }

        _cleanupRegistration = default;
        _cocktailCollectionController.Shutdown();
        _cocktailCastingController.Shutdown();
        _dialogueSceneController.Shutdown();
        _dialogueEventBindings.Shutdown();
        _dialogueManager.DetachScene(_actorRegistry);
        _actorRegistry.Shutdown();
        _gameEndTrigger.Shutdown();
        _isInitialized = false;
    }
}
