using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CocktailCastingController : MonoBehaviour
{
    [Header("Scene Objects")]
    [SerializeField] private GameObject _uiRoot;
    [SerializeField] private GameObject _backgroundOverlay;
    [SerializeField] private CanvasGroup _backgroundCanvasGroup;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _revealMask;
    [SerializeField] private RectTransform _panelShell;

    [Header("Header")]
    [SerializeField] private TMP_Text _stepTitle;
    [SerializeField] private TMP_Text _stepBadge;
    [SerializeField] private TMP_Text _instruction;
    [SerializeField] private TMP_Text _selectionSummary;

    [Header("Selection")]
    [SerializeField] private GameObject _optionContent;
    [SerializeField] private CocktailOptionLayout _optionLayout;
    [SerializeField] private List<CocktailIngredientButtonView> _optionButtons =
        new List<CocktailIngredientButtonView>();
    [SerializeField] private List<CocktailStepNodeView> _stepNodes =
        new List<CocktailStepNodeView>();

    [Header("Review")]
    [SerializeField] private GameObject _reviewContent;
    [SerializeField] private Image _reviewImage;
    [SerializeField] private TMP_Text _reviewName;
    [SerializeField] private TMP_Text _reviewDescription;

    [Header("Result")]
    [SerializeField] private GameObject _resultContent;
    [SerializeField] private Image _resultImage;
    [SerializeField] private TMP_Text _resultName;
    [SerializeField] private TMP_Text _resultBadge;
    [SerializeField] private TMP_Text _resultDescription;

    [Header("Controls")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _serveButton;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _startDelay = 0.32f;
    [SerializeField, Min(0f)] private float _panelLeadDelay = 0.08f;
    [SerializeField, Min(0.01f)] private float _backdropFadeInDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float _backdropTargetAlpha = 0.92f;
    [SerializeField, Min(0.01f)] private float _openTravelDuration = 0.82f;
    [SerializeField, Min(0.01f)] private float _openSettleDuration = 0.18f;
    [SerializeField, Min(0f)] private float _openOvershootPixels = 24f;
    [SerializeField, Min(0.01f)] private float _closeAnticipationDuration = 0.15f;
    [SerializeField, Min(0f)] private float _closeAnticipationPixels = 18f;
    [SerializeField, Min(0.01f)] private float _closeTravelDuration = 0.72f;
    [SerializeField, Min(0.01f)] private float _backdropFadeOutDuration = 0.30f;
    [SerializeField, Min(0)] private int _openingCastingDialogueId = 1001;
    [SerializeField] private string _guestId;

    private readonly CocktailSelection _selection = new CocktailSelection();
    private readonly List<CocktailIngredientDataSO> _availableIngredients =
        new List<CocktailIngredientDataSO>();

    private CocktailDatabaseSO _database;
    private CocktailCollectionState _collection;
    private CocktailEventBus _eventBus;
    private DialogueSceneController _dialogueController;
    private CancellationToken _sceneToken;
    private CancellationTokenSource _operationCancellation;
    private CocktailCastingStep _step = CocktailCastingStep.Hidden;
    private CocktailCastingResult _result;
    private int _furthestCategoryIndex;
    private bool _initialized;
    private bool _isActive;
    private bool _isTransitioning;
    private bool _pendingStart;
    private bool _startSequencePending;
    private Vector2 _openedMaskPosition;
    private float _panelWidth;

    public bool IsActive => _isActive;
    public bool IsBusy =>
        _pendingStart || _startSequencePending || _isActive || _isTransitioning;
    public CocktailCastingStep CurrentStep => _step;

    public void Initialize(
        CocktailDatabaseSO database,
        CocktailCollectionState collection,
        CocktailEventBus eventBus,
        DialogueSceneController dialogueController,
        CancellationToken sceneToken)
    {
        bool startWasRequested = _pendingStart || _startSequencePending;
        Shutdown();
        _pendingStart = startWasRequested;

        _database = database ?? throw new ArgumentNullException(nameof(database));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dialogueController = dialogueController ??
            throw new ArgumentNullException(nameof(dialogueController));
        _sceneToken = sceneToken;

        ValidateViewReferences();
        _database.ValidateOrThrow();
        if (_openedMaskPosition == default)
        {
            _openedMaskPosition = _revealMask.anchoredPosition;
        }
        _panelWidth = Mathf.Max(1f, _panelShell.rect.width);
        RegisterListeners();
        HideImmediate();
        _initialized = true;

        if (_pendingStart)
        {
            _pendingStart = false;
            RequestStartCasting();
        }
    }

    public void RequestStartCasting()
    {
        if (!_initialized)
        {
            _pendingStart = true;
            return;
        }

        if (_startSequencePending || _isActive || _isTransitioning)
        {
            return;
        }

        CancellationToken token = CreateOperationToken();
        _startSequencePending = true;
        StartCastingSafelyAsync(token).Forget();
    }

    public void SetGuestId(string guestId)
    {
        _guestId = guestId ?? string.Empty;
    }

    public void Shutdown()
    {
        _pendingStart = false;
        _startSequencePending = false;
        _initialized = false;
        CancelOperation();
        UnregisterListeners();

        if (_dialogueController != null && (_isActive || _isTransitioning))
        {
            _dialogueController.EndCastingDialogues();
        }

        HideImmediate();
        _database = null;
        _collection = null;
        _eventBus = null;
        _dialogueController = null;
        _sceneToken = default;
    }

    private async UniTaskVoid StartCastingSafelyAsync(CancellationToken token)
    {
        try
        {
            // The -1 command is published before DialogueManager clears its Pre-Casting flag.
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            await WaitUnscaledAsync(_startDelay, token);
            token.ThrowIfCancellationRequested();

            _selection.Clear();
            _result = null;
            _step = CocktailCastingStep.Base;
            _furthestCategoryIndex = 0;
            _isActive = true;
            _isTransitioning = true;

            _backgroundOverlay.SetActive(true);
            _backgroundCanvasGroup.alpha = 0f;
            _backgroundCanvasGroup.blocksRaycasts = true;
            _uiRoot.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;
            SetRevealWidth(_panelWidth);
            _revealMask.anchoredPosition = new Vector2(0f, _openedMaskPosition.y);
            _panelShell.localScale = Vector3.one;
            RefreshView();

            _eventBus?.PublishStarted();
            await UniTask.WhenAll(
                AnimateBackdropAsync(
                    0f,
                    _backdropTargetAlpha,
                    _backdropFadeInDuration,
                    0f,
                    token),
                AnimateOpenMotionAsync(token));

            _canvasGroup.interactable = true;
            _isTransitioning = false;

            if (_openingCastingDialogueId > 0)
            {
                token.ThrowIfCancellationRequested();
                _dialogueController?.ShowCastingDialogue(_openingCastingDialogueId);
            }
        }
        catch (OperationCanceledException)
        {
            // Scene shutdown and repeated requests cancel visual work by design.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            HideImmediate();
        }
        finally
        {
            if (ReleaseOperation(token))
            {
                _startSequencePending = false;
            }
        }
    }

    private async UniTask AnimateOpenMotionAsync(CancellationToken token)
    {
        await WaitUnscaledAsync(_panelLeadDelay, token);
        Vector2 overshoot = new Vector2(
            Mathf.Round(_openedMaskPosition.x - _openOvershootPixels),
            Mathf.Round(_openedMaskPosition.y));
        await AnimateMaskPositionAsync(
            _revealMask.anchoredPosition,
            overshoot,
            _openTravelDuration,
            MotionEase.SmoothStep,
            token);
        await AnimateMaskPositionAsync(
            overshoot,
            _openedMaskPosition,
            _openSettleDuration,
            MotionEase.OutQuad,
            token);
    }

    private async UniTask AnimateMaskPositionAsync(
        Vector2 from,
        Vector2 to,
        float duration,
        MotionEase ease,
        CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateEase(t, ease);
            Vector2 position = Vector2.LerpUnclamped(from, to, eased);
            position.x = Mathf.Round(position.x);
            position.y = Mathf.Round(position.y);
            _revealMask.anchoredPosition = position;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        _revealMask.anchoredPosition = new Vector2(
            Mathf.Round(to.x),
            Mathf.Round(to.y));
    }

    private async UniTask AnimateBackdropAsync(
        float from,
        float to,
        float duration,
        float delay,
        CancellationToken token)
    {
        await WaitUnscaledAsync(delay, token);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            _backgroundCanvasGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        _backgroundCanvasGroup.alpha = to;
    }

    private async UniTask CloseSafelyAsync(CancellationToken token)
    {
        try
        {
            _isTransitioning = true;
            _canvasGroup.interactable = false;

            Vector2 anticipation = new Vector2(
                Mathf.Round(_openedMaskPosition.x - _closeAnticipationPixels),
                Mathf.Round(_openedMaskPosition.y));
            await AnimateMaskPositionAsync(
                _revealMask.anchoredPosition,
                anticipation,
                _closeAnticipationDuration,
                MotionEase.OutQuad,
                token);
            float fadeDelay = Mathf.Max(0f, _closeTravelDuration - _backdropFadeOutDuration);
            await UniTask.WhenAll(
                AnimateMaskPositionAsync(
                    anticipation,
                    new Vector2(0f, _openedMaskPosition.y),
                    _closeTravelDuration,
                    MotionEase.SmoothStep,
                    token),
                AnimateBackdropAsync(
                    _backgroundCanvasGroup.alpha,
                    0f,
                    _backdropFadeOutDuration,
                    fadeDelay,
                    token));

            HideImmediate();
            _eventBus?.PublishClosed();
        }
        catch (OperationCanceledException)
        {
            // Scene shutdown owns final cleanup.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            HideImmediate();
        }
        finally
        {
            ReleaseOperation(token);
        }
    }

    private void HandleIngredientSelected(CocktailIngredientDataSO ingredient)
    {
        if (!_isActive || _isTransitioning || ingredient == null || !IsCategoryStep(_step))
        {
            return;
        }

        CocktailIngredientCategory category = StepToCategory(_step);
        if (ingredient.Category != category)
        {
            return;
        }

        _selection.Set(category, ingredient);
        _eventBus.PublishIngredientSelected(ingredient);

        if (ingredient.CastingDialogueId > 0)
        {
            _dialogueController.ShowCastingDialogue(ingredient.CastingDialogueId);
        }

        AdvanceFromCategory(category);
    }

    private void HandleNodeClicked(CocktailIngredientCategory category)
    {
        if (!_isActive || _isTransitioning)
        {
            return;
        }

        int index = (int)category;
        if (index <= _furthestCategoryIndex)
        {
            SetStep(CategoryToStep(category));
        }
    }

    private void HandleBack()
    {
        if (!_isActive || _isTransitioning)
        {
            return;
        }

        if (_step == CocktailCastingStep.Review)
        {
            SetStep(CocktailCastingStep.Garnish);
            return;
        }

        if (IsCategoryStep(_step) && _step > CocktailCastingStep.Base)
        {
            SetStep((CocktailCastingStep)((int)_step - 1));
        }
    }

    private void HandleSkip()
    {
        if (!_isActive || _isTransitioning)
        {
            return;
        }

        if (_step == CocktailCastingStep.Mixer)
        {
            _selection.Mixer = null;
            AdvanceFromCategory(CocktailIngredientCategory.Mixer);
        }
        else if (_step == CocktailCastingStep.Modifier && _selection.Mixer != null)
        {
            _selection.Modifier = null;
            AdvanceFromCategory(CocktailIngredientCategory.Modifier);
        }
        else if (_step == CocktailCastingStep.Garnish)
        {
            _selection.Garnish = null;
            AdvanceFromCategory(CocktailIngredientCategory.Garnish);
        }
    }

    private void HandleReset()
    {
        if (!_isActive || _isTransitioning)
        {
            return;
        }

        _selection.Clear();
        _result = null;
        _furthestCategoryIndex = 0;
        SetStep(CocktailCastingStep.Base);
    }

    private void HandleConfirm()
    {
        if (!_isActive || _isTransitioning || _step != CocktailCastingStep.Review)
        {
            return;
        }

        if (!_selection.IsComplete)
        {
            SetStep(_selection.Base == null
                ? CocktailCastingStep.Base
                : _selection.Mixer == null && _selection.Modifier == null
                    ? CocktailCastingStep.Modifier
                    : CocktailCastingStep.Technique);
            return;
        }

        CocktailRecipeDataSO recipe = _database.FindRecipe(_selection);
        CocktailGuestPreferenceSO guest = _database.FindGuestPreference(_guestId);
        CocktailGuestReaction reaction = recipe == null
            ? CocktailGuestReaction.Dislike
            : guest != null
                ? guest.Resolve(recipe)
                : CocktailGuestReaction.SoSo;
        bool isNewDiscovery = _collection.RegisterDiscovery(recipe);
        _result = new CocktailCastingResult(
            _selection,
            recipe,
            reaction,
            isNewDiscovery);

        SetStep(CocktailCastingStep.Result);
    }

    private void HandleServe()
    {
        if (!_isActive || _isTransitioning || _step != CocktailCastingStep.Result || _result == null)
        {
            return;
        }

        _dialogueController.EndCastingDialogues();
        _eventBus.PublishCompleted(_result);
        CloseSafelyAsync(CreateOperationToken()).Forget();
    }

    private void AdvanceFromCategory(CocktailIngredientCategory category)
    {
        int nextIndex = (int)category + 1;
        _furthestCategoryIndex = Mathf.Max(
            _furthestCategoryIndex,
            Mathf.Min(nextIndex, (int)CocktailIngredientCategory.Garnish));

        if (category == CocktailIngredientCategory.Garnish)
        {
            SetStep(CocktailCastingStep.Review);
        }
        else
        {
            SetStep((CocktailCastingStep)nextIndex);
        }
    }

    private void SetStep(CocktailCastingStep step)
    {
        _step = step;
        RefreshView();
    }

    private void RefreshView()
    {
        bool categoryStep = IsCategoryStep(_step);
        _optionContent.SetActive(categoryStep);
        _reviewContent.SetActive(_step == CocktailCastingStep.Review);
        _resultContent.SetActive(_step == CocktailCastingStep.Result);

        UpdateHeader();
        UpdateNodes();
        UpdateControls();

        if (categoryStep)
        {
            PopulateOptions(StepToCategory(_step));
        }
        else
        {
            ClearOptions();
        }

        if (_step == CocktailCastingStep.Review)
        {
            UpdateReview();
        }
        else if (_step == CocktailCastingStep.Result)
        {
            UpdateResult();
        }

        if (_selectionSummary != null)
        {
            _selectionSummary.text = BuildFooterStatus();
        }
    }

    private void PopulateOptions(CocktailIngredientCategory category)
    {
        _database.GetIngredients(category, _availableIngredients);
        CocktailIngredientDataSO selected = _selection.Get(category);

        for (int index = 0; index < _optionButtons.Count; index++)
        {
            CocktailIngredientButtonView button = _optionButtons[index];
            if (button == null)
            {
                continue;
            }

            if (index < _availableIngredients.Count)
            {
                CocktailIngredientDataSO ingredient = _availableIngredients[index];
                button.Bind(ingredient, ingredient == selected, HandleIngredientSelected);
            }
            else
            {
                button.Clear();
            }
        }

        if (_availableIngredients.Count > _optionButtons.Count)
        {
            Debug.LogError(
                $"Cocktail option pool has {_optionButtons.Count} buttons, but category {category} " +
                $"contains {_availableIngredients.Count} ingredients.",
                this);
        }

        _optionLayout.RefreshLayout();
    }

    private void ClearOptions()
    {
        foreach (CocktailIngredientButtonView button in _optionButtons)
        {
            if (button != null)
            {
                button.Clear();
            }
        }

        _optionLayout.RefreshLayout();
    }

    private void UpdateHeader()
    {
        if (_stepTitle == null || _stepBadge == null || _instruction == null)
        {
            return;
        }

        switch (_step)
        {
            case CocktailCastingStep.Base:
                _stepTitle.text = "01  기주 선택";
                _stepBadge.text = "필수";
                _instruction.text = "칵테일의 중심이 될 술을 고르세요. 선택하면 다음 단계로 이동합니다.";
                break;
            case CocktailCastingStep.Mixer:
                _stepTitle.text = "02  믹서 선택";
                _stepBadge.text = "선택";
                _instruction.text = "향과 질감을 더할 음료를 고르거나 건너뛰세요.";
                break;
            case CocktailCastingStep.Modifier:
                _stepTitle.text = "03  모디파이어 선택";
                _stepBadge.text = _selection.Mixer == null ? "필수" : "선택";
                _instruction.text = _selection.Mixer == null
                    ? "믹서를 생략했으므로 모디파이어는 반드시 선택해야 합니다."
                    : "맛의 방향을 정할 재료를 고르거나 건너뛰세요.";
                break;
            case CocktailCastingStep.Technique:
                _stepTitle.text = "04  기법 선택";
                _stepBadge.text = "필수";
                _instruction.text = "재료를 완성할 주조 기법을 고르세요.";
                break;
            case CocktailCastingStep.Garnish:
                _stepTitle.text = "05  가니시 선택";
                _stepBadge.text = "선택";
                _instruction.text = "마지막 장식을 고르거나 건너뛰세요.";
                break;
            case CocktailCastingStep.Review:
                _stepTitle.text = "별자리 완성";
                _stepBadge.text = "확인";
                _instruction.text = "선택한 조합을 확인한 뒤 칵테일을 제조하세요.";
                break;
            case CocktailCastingStep.Result:
                _stepTitle.text = "한 잔의 이야기";
                _stepBadge.text = "완료";
                _instruction.text = "완성된 칵테일을 손님에게 건네세요.";
                break;
        }
    }

    private void UpdateNodes()
    {
        foreach (CocktailStepNodeView node in _stepNodes)
        {
            if (node == null)
            {
                continue;
            }

            int index = (int)node.Category;
            bool current = IsCategoryStep(_step) && index == (int)_step;
            bool reached = index <= _furthestCategoryIndex || _selection.Get(node.Category) != null;
            node.SetState(reached, current, _selection.Get(node.Category));
        }
    }

    private void UpdateControls()
    {
        bool categoryStep = IsCategoryStep(_step);
        _backButton.gameObject.SetActive(
            _step == CocktailCastingStep.Review ||
            categoryStep && _step > CocktailCastingStep.Base);

        bool canSkip = _step == CocktailCastingStep.Mixer ||
                       _step == CocktailCastingStep.Garnish ||
                       _step == CocktailCastingStep.Modifier && _selection.Mixer != null;
        _skipButton.gameObject.SetActive(canSkip);
        _skipButton.interactable = canSkip;

        bool hasSelection = _selection.Base != null ||
                            _selection.Mixer != null ||
                            _selection.Modifier != null ||
                            _selection.Technique != null ||
                            _selection.Garnish != null;
        _resetButton.gameObject.SetActive(
            (categoryStep || _step == CocktailCastingStep.Review) && hasSelection);
        _confirmButton.gameObject.SetActive(_step == CocktailCastingStep.Review);
        _confirmButton.interactable = _selection.IsComplete;
        _serveButton.gameObject.SetActive(_step == CocktailCastingStep.Result);
    }

    private void UpdateReview()
    {
        CocktailRecipeDataSO recipe = _database.FindRecipe(_selection);
        Sprite preview = recipe != null && recipe.ResultSprite != null
            ? recipe.ResultSprite
            : _selection.Base != null
                ? _selection.Base.Icon
                : null;
        SetPreviewImage(_reviewImage, preview);

        _reviewName.text = recipe != null ? recipe.CocktailName : "미지의 조합";
        _reviewDescription.text = recipe != null
            ? recipe.OneLineDescription
            : "아직 도감에 기록되지 않은 조합입니다. 어떤 표정으로 돌아올까요?";
    }

    private void UpdateResult()
    {
        if (_result == null)
        {
            return;
        }

        CocktailRecipeDataSO recipe = _result.Recipe;
        Sprite preview = recipe != null && recipe.ResultSprite != null
            ? recipe.ResultSprite
            : _result.Selection.Base != null
                ? _result.Selection.Base.Icon
                : null;
        SetPreviewImage(_resultImage, preview);

        _resultName.text = recipe != null ? recipe.CocktailName : "실험작";
        _resultDescription.text = recipe != null
            ? recipe.OneLineDescription
            : "별자리에 없는 낯선 조합. 손님의 취향과는 조금 멀어진 듯하다.";

        if (recipe == null)
        {
            _resultBadge.text = "EXPERIMENTAL  ·  DISLIKE";
        }
        else
        {
            string discovery = _result.IsNewDiscovery ? "NEW RECIPE" : "DISCOVERED";
            _resultBadge.text = $"{discovery}  ·  {GetReactionLabel(_result.Reaction)}";
        }
    }

    private string BuildFooterStatus()
    {
        if (IsCategoryStep(_step))
        {
            return $"{(int)_step + 1:00} / 05";
        }

        switch (_step)
        {
            case CocktailCastingStep.Review:
                return "조합 확인";
            case CocktailCastingStep.Result:
                return "제조 완료";
            default:
                return string.Empty;
        }
    }

    private static string GetReactionLabel(CocktailGuestReaction reaction)
    {
        switch (reaction)
        {
            case CocktailGuestReaction.Good:
                return "GOOD";
            case CocktailGuestReaction.SoSo:
                return "SOSO";
            case CocktailGuestReaction.Dislike:
                return "DISLIKE";
            default:
                return reaction.ToString().ToUpperInvariant();
        }
    }

    private static void SetPreviewImage(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private void RegisterListeners()
    {
        _backButton.onClick.AddListener(HandleBack);
        _skipButton.onClick.AddListener(HandleSkip);
        _resetButton.onClick.AddListener(HandleReset);
        _confirmButton.onClick.AddListener(HandleConfirm);
        _serveButton.onClick.AddListener(HandleServe);

        foreach (CocktailStepNodeView node in _stepNodes)
        {
            node?.Initialize(HandleNodeClicked);
        }
    }

    private void UnregisterListeners()
    {
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(HandleBack);
        }

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(HandleSkip);
        }

        if (_resetButton != null)
        {
            _resetButton.onClick.RemoveListener(HandleReset);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        if (_serveButton != null)
        {
            _serveButton.onClick.RemoveListener(HandleServe);
        }
    }

    private CancellationToken CreateOperationToken()
    {
        CancelOperation();
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_sceneToken);
        return _operationCancellation.Token;
    }

    private void CancelOperation()
    {
        if (_operationCancellation == null)
        {
            return;
        }

        CancellationTokenSource cancellation = _operationCancellation;
        _operationCancellation = null;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private bool ReleaseOperation(CancellationToken token)
    {
        CancellationTokenSource cancellation = _operationCancellation;
        if (cancellation == null || cancellation.Token != token)
        {
            return false;
        }

        _operationCancellation = null;
        cancellation.Dispose();
        return true;
    }

    private async UniTask WaitUnscaledAsync(float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private static float EvaluateEase(float t, MotionEase ease)
    {
        switch (ease)
        {
            case MotionEase.OutQuad:
                return 1f - (1f - t) * (1f - t);
            default:
                return t * t * (3f - 2f * t);
        }
    }

    private void SetRevealWidth(float width)
    {
        Vector2 size = _revealMask.sizeDelta;
        size.x = Mathf.Max(0f, width);
        _revealMask.sizeDelta = size;
    }

    private void HideImmediate()
    {
        _isActive = false;
        _isTransitioning = false;
        _step = CocktailCastingStep.Hidden;
        _result = null;
        _selection.Clear();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_backgroundCanvasGroup != null)
        {
            _backgroundCanvasGroup.alpha = 0f;
            _backgroundCanvasGroup.blocksRaycasts = false;
        }

        if (_panelShell != null)
        {
            _panelShell.localScale = Vector3.one;
        }

        if (_revealMask != null)
        {
            if (_panelWidth > 0f)
            {
                SetRevealWidth(_panelWidth);
            }

            if (_openedMaskPosition != default)
            {
                _revealMask.anchoredPosition = new Vector2(0f, _openedMaskPosition.y);
            }
        }

        if (_uiRoot != null)
        {
            _uiRoot.SetActive(false);
        }

        if (_backgroundOverlay != null)
        {
            _backgroundOverlay.SetActive(false);
        }
    }

    private void ValidateViewReferences()
    {
        if (_uiRoot == null || _backgroundOverlay == null || _backgroundCanvasGroup == null ||
            _canvasGroup == null ||
            _revealMask == null || _panelShell == null || _stepTitle == null ||
            _stepBadge == null || _instruction == null || _selectionSummary == null ||
            _optionContent == null || _optionLayout == null ||
            _reviewContent == null || _resultContent == null || _reviewImage == null ||
            _reviewName == null || _reviewDescription == null || _resultImage == null ||
            _resultName == null || _resultBadge == null || _resultDescription == null ||
            _backButton == null || _skipButton == null || _resetButton == null ||
            _confirmButton == null || _serveButton == null)
        {
            throw new InvalidOperationException(
                $"CocktailCastingController '{name}' has one or more missing view references.");
        }

        if (_stepNodes.Count != 5)
        {
            throw new InvalidOperationException(
                $"CocktailCastingController '{name}' requires exactly five step nodes.");
        }

        if (_optionButtons.Count < 7)
        {
            throw new InvalidOperationException(
                $"CocktailCastingController '{name}' requires at least seven option buttons.");
        }
    }

    private enum MotionEase
    {
        SmoothStep,
        OutQuad
    }

    private static bool IsCategoryStep(CocktailCastingStep step)
    {
        return step >= CocktailCastingStep.Base && step <= CocktailCastingStep.Garnish;
    }

    private static CocktailIngredientCategory StepToCategory(CocktailCastingStep step)
    {
        if (!IsCategoryStep(step))
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Step is not an ingredient category.");
        }

        return (CocktailIngredientCategory)(int)step;
    }

    private static CocktailCastingStep CategoryToStep(CocktailIngredientCategory category)
    {
        return (CocktailCastingStep)(int)category;
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
