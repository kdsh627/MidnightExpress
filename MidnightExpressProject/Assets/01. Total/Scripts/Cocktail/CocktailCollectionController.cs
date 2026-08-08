using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CocktailCollectionController : MonoBehaviour
{
    private const int SlotsPerPage = 5;
    private const string ZodiacBundleId = "zodiac";

    private enum CollectionTab
    {
        Character,
        Cocktail,
        Achievement
    }

    [Header("Scene Objects")]
    [SerializeField] private GameObject _uiRoot;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _bookRoot;

    [Header("Tabs")]
    [SerializeField] private Button _characterTabButton;
    [SerializeField] private Button _cocktailTabButton;
    [SerializeField] private Button _achievementTabButton;
    [SerializeField] private Material _activeTabMaterial;

    [Header("Tab Content")]
    [SerializeField] private GameObject _cocktailContentRoot;
    [SerializeField] private GameObject _placeholderContentRoot;
    [SerializeField] private Image _placeholderIcon;
    [SerializeField] private TMP_Text _placeholderSectionTitle;
    [SerializeField] private TMP_Text _placeholderTitle;
    [SerializeField] private TMP_Text _placeholderDescription;
    [SerializeField] private TMP_Text _placeholderDetailTitle;
    [SerializeField] private TMP_Text _placeholderDetailDescription;
    [SerializeField] private Sprite _characterTabSprite;
    [SerializeField] private Sprite _achievementTabSprite;

    [Header("Index")]
    [SerializeField] private TMP_Text _collectionTitle;
    [SerializeField] private TMP_Text _progressLabel;
    [SerializeField] private List<CocktailCollectionSlotView> _slots =
        new List<CocktailCollectionSlotView>();
    [SerializeField] private Button _previousPageButton;
    [SerializeField] private Button _nextPageButton;
    [SerializeField] private TMP_Text _pageLabel;

    [Header("Detail")]
    [SerializeField] private Image _detailArtwork;
    [SerializeField] private TMP_Text _detailConstellation;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailSubtitle;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private TMP_Text _detailKeywords;
    [SerializeField] private TMP_Text _detailRecipe;
    [SerializeField] private TMP_Text _bundleStatus;
    [SerializeField] private GameObject _detailNewBadge;
    [SerializeField] private Material _silhouetteMaterial;
    [SerializeField] private Sprite _lockedFallbackSprite;

    [Header("Controls")]
    [SerializeField] private Button _closeButton;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float _openDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.16f;
    [SerializeField, Min(0f)] private float _bookOffsetPixels = 18f;

    private readonly List<CocktailRecipeDataSO> _recipes =
        new List<CocktailRecipeDataSO>();

    private CocktailDatabaseSO _database;
    private CocktailBundleDataSO _activeBundle;
    private CocktailCollectionState _collection;
    private CocktailCastingController _castingController;
    private DialogueManager _dialogueManager;
    private CancellationToken _sceneToken;
    private CancellationTokenSource _operationCancellation;
    private Vector2 _openedBookPosition;
    private int _currentPage;
    private int _selectedRecipeIndex;
    private CollectionTab _activeTab = CollectionTab.Cocktail;
    private bool _initialized;
    private bool _isOpen;
    private bool _isTransitioning;

    public bool IsOpen => _isOpen;
    public bool CanOpen =>
        _initialized &&
        !_isOpen &&
        !_isTransitioning &&
        (_castingController == null || !_castingController.IsBusy) &&
        (_dialogueManager == null ||
         (!_dialogueManager.IsPreCastingPlaying && !_dialogueManager.IsCastingActive));

    public void Initialize(
        CocktailDatabaseSO database,
        CocktailCollectionState collection,
        CocktailCastingController castingController,
        DialogueManager dialogueManager,
        CancellationToken sceneToken)
    {
        Vector2 configuredBookPosition = _initialized
            ? _openedBookPosition
            : (_bookRoot != null ? _bookRoot.anchoredPosition : Vector2.zero);
        Shutdown();

        _database = database ?? throw new ArgumentNullException(nameof(database));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _castingController = castingController ??
            throw new ArgumentNullException(nameof(castingController));
        _dialogueManager = dialogueManager ??
            throw new ArgumentNullException(nameof(dialogueManager));
        _sceneToken = sceneToken;

        ValidateViewReferences();
        BuildRecipeIndex();
        _openedBookPosition = configuredBookPosition;
        _currentPage = 0;
        _selectedRecipeIndex = 0;
        _activeTab = CollectionTab.Cocktail;

        RegisterListeners();
        _collection.RecipeDiscovered += HandleCollectionChanged;
        _collection.RecipeSeen += HandleCollectionChanged;
        _initialized = true;
        RefreshView();
        HideImmediate();
    }

    public bool RequestOpen()
    {
        if (!CanOpen)
        {
            return false;
        }

        OpenAsync(CreateOperationToken()).Forget();
        return true;
    }

    public bool RequestClose()
    {
        if (!_initialized || !_isOpen || _isTransitioning)
        {
            return false;
        }

        CloseAsync(CreateOperationToken()).Forget();
        return true;
    }

    public void Shutdown()
    {
        CancelOperation();

        if (_collection != null)
        {
            _collection.RecipeDiscovered -= HandleCollectionChanged;
            _collection.RecipeSeen -= HandleCollectionChanged;
        }

        UnregisterListeners();
        foreach (CocktailCollectionSlotView slot in _slots)
        {
            slot?.Shutdown();
        }

        HideImmediate();
        _recipes.Clear();
        _database = null;
        _activeBundle = null;
        _collection = null;
        _castingController = null;
        _dialogueManager = null;
        _sceneToken = default;
        _initialized = false;
    }

    private void Update()
    {
        if (!_isOpen || _isTransitioning)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            RequestClose();
        }
        else if (_activeTab == CollectionTab.Cocktail &&
                 keyboard.leftArrowKey.wasPressedThisFrame)
        {
            HandlePreviousPage();
        }
        else if (_activeTab == CollectionTab.Cocktail &&
                 keyboard.rightArrowKey.wasPressedThisFrame)
        {
            HandleNextPage();
        }
    }

    private void BuildRecipeIndex()
    {
        _recipes.Clear();
        CocktailBundleDataSO bundle = _database.FindBundle(ZodiacBundleId);
        _activeBundle = bundle;
        IReadOnlyList<CocktailRecipeDataSO> source =
            bundle != null ? bundle.Recipes : _database.Recipes;

        foreach (CocktailRecipeDataSO recipe in source)
        {
            if (recipe != null && !recipe.Hidden)
            {
                _recipes.Add(recipe);
            }
        }

        _recipes.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
        if (_recipes.Count == 0)
        {
            throw new InvalidOperationException(
                "Cocktail collection requires at least one visible recipe.");
        }
    }

    private async UniTask OpenAsync(CancellationToken token)
    {
        _isOpen = true;
        _isTransitioning = true;

        try
        {
            RefreshView();
            _uiRoot.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;

            Vector2 start =
                _openedBookPosition + new Vector2(0f, -_bookOffsetPixels);
            _bookRoot.anchoredPosition = Round(start);
            await AnimateBookAsync(start, _openedBookPosition, 0f, 1f, _openDuration, token);
            token.ThrowIfCancellationRequested();
            _canvasGroup.interactable = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            HideImmediate();
            Debug.LogException(exception, this);
        }
        finally
        {
            ReleaseOperation(token);
            _isTransitioning = false;
        }
    }

    private async UniTask CloseAsync(CancellationToken token)
    {
        _isTransitioning = true;
        _canvasGroup.interactable = false;
        Vector2 end = _openedBookPosition + new Vector2(0f, -_bookOffsetPixels);

        try
        {
            await AnimateBookAsync(
                _bookRoot.anchoredPosition,
                end,
                _canvasGroup.alpha,
                0f,
                _closeDuration,
                token);
            token.ThrowIfCancellationRequested();
            HideImmediate();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            HideImmediate();
            Debug.LogException(exception, this);
        }
        finally
        {
            ReleaseOperation(token);
            _isTransitioning = false;
        }
    }

    private async UniTask AnimateBookAsync(
        Vector2 from,
        Vector2 to,
        float fromAlpha,
        float toAlpha,
        float duration,
        CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            _bookRoot.anchoredPosition = Round(Vector2.LerpUnclamped(from, to, eased));
            _canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        _bookRoot.anchoredPosition = Round(to);
        _canvasGroup.alpha = toAlpha;
    }

    private void RefreshView()
    {
        if (_recipes.Count == 0 || _collection == null)
        {
            return;
        }

        RefreshTabVisuals();
        bool showCocktailCollection = _activeTab == CollectionTab.Cocktail;
        _cocktailContentRoot.SetActive(showCocktailCollection);
        _placeholderContentRoot.SetActive(!showCocktailCollection);
        if (!showCocktailCollection)
        {
            RefreshPlaceholder();
            return;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_recipes.Count / (float)SlotsPerPage));
        _currentPage = Mathf.Clamp(_currentPage, 0, pageCount - 1);
        int firstIndex = _currentPage * SlotsPerPage;
        int lastIndex = Mathf.Min(firstIndex + SlotsPerPage, _recipes.Count) - 1;
        if (_selectedRecipeIndex < firstIndex || _selectedRecipeIndex > lastIndex)
        {
            _selectedRecipeIndex = firstIndex;
        }

        int discoveredCount = 0;
        foreach (CocktailRecipeDataSO recipe in _recipes)
        {
            if (_collection.IsDiscovered(recipe.Id))
            {
                discoveredCount++;
            }
        }

        _collectionTitle.text = _activeBundle != null
            ? _activeBundle.DisplayName
            : "별자리 칵테일";
        _progressLabel.text = $"발견  {discoveredCount:00} / {_recipes.Count:00}";
        _pageLabel.text = $"{_currentPage + 1:00} / {pageCount:00}";
        _previousPageButton.interactable = _currentPage > 0;
        _nextPageButton.interactable = _currentPage < pageCount - 1;

        for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
        {
            int recipeIndex = firstIndex + slotIndex;
            CocktailCollectionSlotView slot = _slots[slotIndex];
            if (recipeIndex >= _recipes.Count)
            {
                slot.Clear();
                continue;
            }

            CocktailRecipeDataSO recipe = _recipes[recipeIndex];
            bool discovered = _collection.IsDiscovered(recipe.Id);
            slot.Bind(
                recipe,
                discovered,
                _collection.IsSeen(recipe.Id),
                recipeIndex == _selectedRecipeIndex);
        }

        RefreshDetail(_recipes[_selectedRecipeIndex], discoveredCount);
    }

    private void RefreshDetail(CocktailRecipeDataSO recipe, int discoveredCount)
    {
        bool discovered = _collection.IsDiscovered(recipe.Id);
        Sprite artwork = recipe.ResultSprite != null
            ? recipe.ResultSprite
            : recipe.Base != null
                ? recipe.Base.Icon
                : _lockedFallbackSprite;

        _detailArtwork.sprite = artwork != null ? artwork : _lockedFallbackSprite;
        _detailArtwork.enabled = _detailArtwork.sprite != null;
        _detailArtwork.preserveAspect = true;
        _detailArtwork.material = discovered ? null : _silhouetteMaterial;
        _detailArtwork.color = Color.white;

        _detailConstellation.text =
            $"NO. {recipe.DisplayOrder:00}  ·  {recipe.Constellation}";
        _detailName.text = discovered ? recipe.CocktailName : "???";
        _detailSubtitle.text = discovered
            ? recipe.EmotionKeyword
            : "별빛 속에 감춰진 레시피";
        _detailDescription.text = discovered
            ? recipe.OneLineDescription
            : "아직 발견하지 못한 칵테일입니다. 새로운 조합을 완성해 보세요.";
        _detailKeywords.text = discovered
            ? BuildKeywordLine(recipe)
            : "맛과 향에 대한 기록이 비어 있습니다.";
        _detailRecipe.text = discovered
            ? BuildRecipeText(recipe)
            : "기주        ???\n믹서        ???\n모디파이어  ???\n기법        ???\n가니시      ???";
        _detailNewBadge.SetActive(discovered && !_collection.IsSeen(recipe.Id));
        _bundleStatus.text = discoveredCount >= _recipes.Count
            ? "★  별자리 컬렉션 완성"
            : $"별자리의 기록  {discoveredCount} / {_recipes.Count}";
    }

    private static string BuildKeywordLine(CocktailRecipeDataSO recipe)
    {
        string tastes = recipe.TasteKeywords.Count > 0
            ? string.Join("  ·  ", recipe.TasteKeywords)
            : "기록 없음";
        return $"정서  {recipe.EmotionKeyword}     맛  {tastes}";
    }

    private static string BuildRecipeText(CocktailRecipeDataSO recipe)
    {
        return string.Join(
            "\n",
            "기주        " + GetIngredientName(recipe.Base),
            "믹서        " + GetIngredientName(recipe.Mixer),
            "모디파이어  " + GetIngredientName(recipe.Modifier),
            "기법        " + GetIngredientName(recipe.Technique),
            "가니시      " + GetIngredientName(recipe.Garnish));
    }

    private static string GetIngredientName(CocktailIngredientDataSO ingredient)
    {
        return ingredient != null ? ingredient.DisplayName : "없음";
    }

    private void HandleSlotSelected(CocktailRecipeDataSO recipe)
    {
        int index = _recipes.IndexOf(recipe);
        if (index < 0)
        {
            return;
        }

        _selectedRecipeIndex = index;
        bool refreshedBySeenEvent =
            _collection.IsDiscovered(recipe.Id) &&
            _collection.MarkSeen(recipe.Id);
        if (!refreshedBySeenEvent)
        {
            RefreshView();
        }
    }

    private void HandlePreviousPage()
    {
        if (!_isTransitioning && _currentPage > 0)
        {
            _currentPage--;
            _selectedRecipeIndex = _currentPage * SlotsPerPage;
            RefreshView();
        }
    }

    private void HandleNextPage()
    {
        int pageCount = Mathf.CeilToInt(_recipes.Count / (float)SlotsPerPage);
        if (!_isTransitioning && _currentPage < pageCount - 1)
        {
            _currentPage++;
            _selectedRecipeIndex = _currentPage * SlotsPerPage;
            RefreshView();
        }
    }

    private void HandleCollectionChanged(string recipeId)
    {
        if (_isOpen)
        {
            RefreshView();
        }
    }

    private void RegisterListeners()
    {
        _closeButton.onClick.AddListener(HandleCloseClicked);
        _previousPageButton.onClick.AddListener(HandlePreviousPage);
        _nextPageButton.onClick.AddListener(HandleNextPage);
        _characterTabButton.onClick.AddListener(HandleCharacterTabClicked);
        _cocktailTabButton.onClick.AddListener(HandleCocktailTabClicked);
        _achievementTabButton.onClick.AddListener(HandleAchievementTabClicked);
        _characterTabButton.interactable = true;
        _cocktailTabButton.interactable = true;
        _achievementTabButton.interactable = true;

        foreach (CocktailCollectionSlotView slot in _slots)
        {
            slot.Initialize(HandleSlotSelected);
        }
    }

    private void UnregisterListeners()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        if (_previousPageButton != null)
        {
            _previousPageButton.onClick.RemoveListener(HandlePreviousPage);
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.onClick.RemoveListener(HandleNextPage);
        }

        if (_characterTabButton != null)
        {
            _characterTabButton.onClick.RemoveListener(HandleCharacterTabClicked);
        }

        if (_cocktailTabButton != null)
        {
            _cocktailTabButton.onClick.RemoveListener(HandleCocktailTabClicked);
        }

        if (_achievementTabButton != null)
        {
            _achievementTabButton.onClick.RemoveListener(HandleAchievementTabClicked);
        }
    }

    private void HandleCharacterTabClicked()
    {
        SelectTab(CollectionTab.Character);
    }

    private void HandleCocktailTabClicked()
    {
        SelectTab(CollectionTab.Cocktail);
    }

    private void HandleAchievementTabClicked()
    {
        SelectTab(CollectionTab.Achievement);
    }

    private void SelectTab(CollectionTab tab)
    {
        if (_isTransitioning || _activeTab == tab)
        {
            return;
        }

        _activeTab = tab;
        RefreshView();
    }

    private void RefreshTabVisuals()
    {
        ApplyTabVisual(_characterTabButton, _activeTab == CollectionTab.Character);
        ApplyTabVisual(_cocktailTabButton, _activeTab == CollectionTab.Cocktail);
        ApplyTabVisual(_achievementTabButton, _activeTab == CollectionTab.Achievement);
    }

    private void ApplyTabVisual(Button button, bool selected)
    {
        if (button.targetGraphic is Image icon)
        {
            icon.color = Color.white;
            icon.material = selected ? _activeTabMaterial : null;
        }

        Transform arrow = button.transform.Find("Arrow");
        if (arrow != null)
        {
            arrow.gameObject.SetActive(selected);
        }
    }

    private void RefreshPlaceholder()
    {
        bool character = _activeTab == CollectionTab.Character;
        _placeholderIcon.sprite = character
            ? _characterTabSprite
            : _achievementTabSprite;
        _placeholderIcon.material = _activeTabMaterial;
        _placeholderIcon.color = Color.white;
        _placeholderSectionTitle.text = character ? "승객 기록" : "업적 기록";
        _placeholderTitle.text = character ? "승객 기록" : "업적 기록";
        _placeholderDescription.text = character
            ? "아직 만난 승객이 없습니다."
            : "아직 달성한 업적이 없습니다.";
        _placeholderDetailTitle.text = character ? "새로운 만남을 기다리는 중" : "새로운 기록을 기다리는 중";
        _placeholderDetailDescription.text = character
            ? "열차에서 새로운 승객을 만나면\n그들의 이야기와 취향이 이곳에 기록됩니다."
            : "여정 속 특별한 순간을 완성하면\n달성한 업적과 추억이 이곳에 기록됩니다.";
    }

    private void HandleCloseClicked()
    {
        RequestClose();
    }

    private CancellationToken CreateOperationToken()
    {
        CancelOperation();
        _operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_sceneToken);
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

    private void ReleaseOperation(CancellationToken token)
    {
        CancellationTokenSource cancellation = _operationCancellation;
        if (cancellation == null || cancellation.Token != token)
        {
            return;
        }

        _operationCancellation = null;
        cancellation.Dispose();
    }

    private void HideImmediate()
    {
        _isOpen = false;
        _isTransitioning = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_bookRoot != null)
        {
            _bookRoot.anchoredPosition = Round(_openedBookPosition);
        }

        if (_uiRoot != null)
        {
            _uiRoot.SetActive(false);
        }
    }

    private void ValidateViewReferences()
    {
        if (_uiRoot == null || _canvasGroup == null || _bookRoot == null ||
            _characterTabButton == null || _cocktailTabButton == null ||
            _achievementTabButton == null || _activeTabMaterial == null ||
            _cocktailContentRoot == null || _placeholderContentRoot == null ||
            _placeholderIcon == null || _placeholderSectionTitle == null ||
            _placeholderTitle == null ||
            _placeholderDescription == null || _placeholderDetailTitle == null ||
            _placeholderDetailDescription == null || _characterTabSprite == null ||
            _achievementTabSprite == null || _collectionTitle == null ||
            _progressLabel == null || _previousPageButton == null ||
            _nextPageButton == null || _pageLabel == null ||
            _detailArtwork == null || _detailConstellation == null ||
            _detailName == null || _detailSubtitle == null ||
            _detailDescription == null || _detailKeywords == null ||
            _detailRecipe == null || _bundleStatus == null ||
            _detailNewBadge == null || _closeButton == null)
        {
            throw new InvalidOperationException(
                $"Cocktail collection controller '{name}' has missing view references.");
        }

        if (_slots.Count != SlotsPerPage ||
            _slots.Exists(slot => slot == null || !slot.IsConfigured))
        {
            throw new InvalidOperationException(
                $"Cocktail collection controller '{name}' requires exactly " +
                $"{SlotsPerPage} fully configured slots.");
        }
    }

    private static Vector2 Round(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
