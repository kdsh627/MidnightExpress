#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CocktailCollectionSetupTool
{
    private const string PlayScenePath = "Assets/02. Scenes/Play/Play.unity";
    private const string DatabasePath =
        "Assets/01. Total/Data/Cocktail/CocktailDatabaseSO.asset";
    private const string DictArtRoot = "Assets/02. Scenes/Play/Arts/Dict";
    private const string CocktailArtRoot = "Assets/01. Total/Arts/칵테일";
    private const string FontPath = "Assets/01. Total/Fonts/Galmuri11.asset";
    private const string BoldFontPath = "Assets/01. Total/Fonts/Galmuri11-Bold.asset";
    private const string SilhouetteShaderPath =
        DictArtRoot + "/DictCocktailSilhouette.shader";
    private const string SilhouetteMaterialPath =
        DictArtRoot + "/DictCocktailSilhouette.mat";
    private const string ActiveTabShaderPath =
        DictArtRoot + "/DictTabActive.shader";
    private const string ActiveTabMaterialPath =
        DictArtRoot + "/DictTabActive.mat";

    private static readonly Color Ink = new Color32(44, 21, 11, 255);
    private static readonly Color InkSoft = new Color32(84, 49, 25, 255);
    private static readonly Color Navy = new Color32(2, 10, 29, 255);
    private static readonly Color Bronze = new Color32(99, 52, 24, 255);
    private static readonly Color BronzeHighlight = new Color32(160, 96, 56, 255);
    private static readonly Color ParchmentHighlight = new Color32(255, 238, 191, 255);
    private static readonly Color PaleBlue = new Color32(232, 240, 248, 255);

    [MenuItem("Tools/Cocktail/Setup Cocktail Collection")]
    public static void SetupCocktailCollection()
    {
        try
        {
            EditorSceneManager.SaveOpenScenes();
            CocktailDatabaseSO database =
                AssetDatabase.LoadAssetAtPath<CocktailDatabaseSO>(DatabasePath)
                ?? throw new InvalidOperationException(
                    "Cocktail database is missing. Run Setup Cocktail Casting first.");
            ConfigurePlayScene(database);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Cocktail collection setup completed: five pooled entries, three pages, " +
                "discovered/NEW states, detail view, persistence, and world-book trigger are connected.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void ConfigurePlayScene(CocktailDatabaseSO database)
    {
        if (database == null)
        {
            // ConfigurePlayScene in the casting setup opens and saves scenes. That can
            // invalidate the UnityEngine.Object wrapper held by the caller even though
            // the database asset itself still exists, so always recover it by path.
            database = AssetDatabase.LoadAssetAtPath<CocktailDatabaseSO>(DatabasePath);
        }

        if (database == null)
        {
            throw new InvalidOperationException(
                "Cocktail database is missing. Run Setup Cocktail Casting first.");
        }

        ConfigureTextureImporters();
        Material silhouetteMaterial = LoadOrCreateSilhouetteMaterial();
        Material activeTabMaterial = LoadOrCreateActiveTabMaterial();

        Scene scene = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);
        Transform ui = FindRoot(scene, "UI")
            ?? throw new InvalidOperationException("Play scene requires a root UI Canvas.");
        RectTransform root = ui.Find("Dict") as RectTransform;
        if (root == null)
        {
            root = CreateUiObject("Dict", ui).GetComponent<RectTransform>();
        }

        root.gameObject.SetActive(false);
        root.SetAsLastSibling();
        SetStretch(root);
        Undo.RegisterFullObjectHierarchyUndo(
            root.gameObject,
            "Rebuild Cocktail Collection UI");
        ClearChildren(root);
        Image legacyRootImage = root.GetComponent<Image>();
        if (legacyRootImage != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyRootImage);
        }

        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(root.gameObject);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform backdrop = CreateUiObject("Backdrop", root).GetComponent<RectTransform>();
        SetStretch(backdrop);
        Image backdropImage = backdrop.gameObject.AddComponent<Image>();
        backdropImage.color = new Color32(4, 7, 20, 238);
        backdropImage.raycastTarget = true;

        RectTransform book = CreateUiObject("BookRoot", root).GetComponent<RectTransform>();
        SetRect(book, new Vector2(-39f, -17f), new Vector2(1356f, 990f));
        Image bookImage = book.gameObject.AddComponent<Image>();
        bookImage.sprite = LoadSprite(DictArtRoot + "/Dict_Book.png");
        bookImage.preserveAspect = true;
        bookImage.raycastTarget = false;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)
            ?? throw new InvalidOperationException($"Collection font is missing at '{FontPath}'.");
        TMP_FontAsset boldFont =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath) ?? font;

        Sprite rowSprite = LoadSprite(DictArtRoot + "/Dict_UI_Left.png");
        Sprite frameSprite = LoadSprite(DictArtRoot + "/Dict_UI_Frame.png");
        Sprite pageMoveSprite = LoadSprite(DictArtRoot + "/Dict_UI_Pagemove.png");
        Sprite tabCharacter = LoadSprite(DictArtRoot + "/Dict_UI_Index_Character.png");
        Sprite tabCocktail = LoadSprite(DictArtRoot + "/Dict_UI_Index_Cacktail.png");
        Sprite tabAchievement = LoadSprite(DictArtRoot + "/Dict_UI_Index_Achievement.png");
        Sprite tabArrow = LoadSprite(DictArtRoot + "/Dict_UI_Arrow.png");
        Sprite starSprite = LoadSprite(DictArtRoot + "/Dict_Star.png");
        Sprite lockedFallback = LoadSprite(DictArtRoot + "/Dict_Star_Empty.png");

        RectTransform cocktailContent = CreateUiObject(
            "CocktailContent", book).GetComponent<RectTransform>();
        SetStretch(cocktailContent);
        IndexReferences index = CreateIndex(
            cocktailContent,
            font,
            boldFont,
            rowSprite,
            pageMoveSprite,
            starSprite,
            lockedFallback,
            silhouetteMaterial);
        DetailReferences detail = CreateDetail(
            cocktailContent,
            font,
            boldFont,
            frameSprite,
            lockedFallback);
        PlaceholderReferences placeholder = CreatePlaceholder(
            book,
            font,
            boldFont,
            frameSprite,
            starSprite,
            tabCharacter,
            tabAchievement,
            activeTabMaterial);
        TabReferences tabs = CreateTabs(
            book,
            tabCharacter,
            tabCocktail,
            tabAchievement,
            tabArrow,
            activeTabMaterial);
        Button closeButton = CreateCloseButton(book, font);

        GameScope gameScope = FindComponentInScene<GameScope>(scene)
            ?? throw new InvalidOperationException("Play scene requires GameScope.");
        CocktailCollectionController controller =
            GetOrAddComponent<CocktailCollectionController>(gameScope.gameObject);
        ConfigureController(
            controller,
            root.gameObject,
            canvasGroup,
            book,
            cocktailContent.gameObject,
            tabs,
            index,
            detail,
            placeholder,
            activeTabMaterial,
            tabCharacter,
            tabAchievement,
            silhouetteMaterial,
            lockedFallback,
            closeButton);
        ConfigureWorldTrigger(scene, controller);

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(root.gameObject);
        EditorUtility.SetDirty(gameScope.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Play scene could not be saved.");
        }
    }

    private static TabReferences CreateTabs(
        RectTransform book,
        Sprite character,
        Sprite cocktail,
        Sprite achievement,
        Sprite arrow,
        Material activeMaterial)
    {
        RectTransform root = CreateUiObject("Tabs", book).GetComponent<RectTransform>();
        SetRect(root, new Vector2(-622f, 110f), new Vector2(114f, 480f));

        Button characterButton = CreateTabButton(
            root, "CharacterTab", new Vector2(0f, 160f), character, arrow, activeMaterial, false);
        Button cocktailButton = CreateTabButton(
            root, "CocktailTab", Vector2.zero, cocktail, arrow, activeMaterial, true);
        Button achievementButton = CreateTabButton(
            root, "AchievementTab", new Vector2(0f, -160f), achievement, arrow, activeMaterial, false);

        return new TabReferences
        {
            Character = characterButton,
            Cocktail = cocktailButton,
            Achievement = achievementButton
        };
    }

    private static Button CreateTabButton(
        RectTransform parent,
        string name,
        Vector2 position,
        Sprite iconSprite,
        Sprite arrowSprite,
        Material activeMaterial,
        bool selected)
    {
        RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
        SetRect(rect, position, new Vector2(114f, 137f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        RectTransform iconRect = CreateUiObject("Icon", rect).GetComponent<RectTransform>();
        SetStretch(iconRect);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = true;
        icon.color = Color.white;
        icon.material = selected ? activeMaterial : null;
        button.targetGraphic = icon;

        RectTransform arrowRect = CreateUiObject("Arrow", rect).GetComponent<RectTransform>();
        SetRect(arrowRect, new Vector2(76f, 0f), new Vector2(91f, 40f));
        Image arrow = arrowRect.gameObject.AddComponent<Image>();
        arrow.sprite = arrowSprite;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        arrowRect.gameObject.SetActive(selected);
        return button;
    }

    private static IndexReferences CreateIndex(
        RectTransform book,
        TMP_FontAsset font,
        TMP_FontAsset boldFont,
        Sprite rowSprite,
        Sprite pageMoveSprite,
        Sprite starSprite,
        Sprite lockedFallback,
        Material silhouetteMaterial)
    {
        TMP_Text title = CreateText(
            book, "CollectionTitle", boldFont, 28f, FontStyles.Bold,
            TextAlignmentOptions.Left, Ink,
            new Vector2(-325f, 365f), new Vector2(286f, 40f));
        TMP_Text progress = CreateText(
            book, "CollectionProgress", boldFont, 17f, FontStyles.Bold,
            TextAlignmentOptions.Right, InkSoft,
            new Vector2(-102f, 365f), new Vector2(176f, 30f));

        RectTransform leftPage = CreateUiObject("LeftPage", book).GetComponent<RectTransform>();
        SetRect(leftPage, new Vector2(-240f, 0f), new Vector2(466f, 754f));

        var slots = new List<CocktailCollectionSlotView>();
        float[] yPositions = { 282f, 141f, 0f, -141f, -282f };
        for (int index = 0; index < yPositions.Length; index++)
        {
            slots.Add(CreateSlot(
                leftPage,
                index,
                yPositions[index],
                font,
                boldFont,
                rowSprite,
                starSprite,
                lockedFallback,
                silhouetteMaterial));
        }

        RectTransform pager = CreateUiObject("Pager", book).GetComponent<RectTransform>();
        SetRect(pager, new Vector2(-240f, -385f), new Vector2(270f, 44f));
        Button previous = CreatePageButton(
            pager, "PreviousPage", new Vector2(-92f, 0f), pageMoveSprite, false);
        TMP_Text pageLabel = CreateText(
            pager, "PageLabel", boldFont, 16f, FontStyles.Bold,
            TextAlignmentOptions.Center, Ink,
            Vector2.zero, new Vector2(110f, 30f));
        Button next = CreatePageButton(
            pager, "NextPage", new Vector2(92f, 0f), pageMoveSprite, true);

        return new IndexReferences
        {
            Title = title,
            Progress = progress,
            Slots = slots,
            Previous = previous,
            Next = next,
            PageLabel = pageLabel
        };
    }

    private static CocktailCollectionSlotView CreateSlot(
        RectTransform parent,
        int index,
        float y,
        TMP_FontAsset font,
        TMP_FontAsset boldFont,
        Sprite rowSprite,
        Sprite starSprite,
        Sprite lockedFallback,
        Material silhouetteMaterial)
    {
        RectTransform rect = CreateUiObject(
            "CocktailSlot" + (index + 1).ToString("00"), parent).GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0f, y), new Vector2(435f, 141f));
        Image background = rect.gameObject.AddComponent<Image>();
        background.sprite = rowSprite;
        background.preserveAspect = true;
        background.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ParchmentHighlight;
        colors.pressedColor = new Color32(224, 190, 126, 255);
        colors.selectedColor = ParchmentHighlight;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        RectTransform thumbnailRect =
            CreateUiObject("Thumbnail", rect).GetComponent<RectTransform>();
        SetRect(thumbnailRect, new Vector2(-155f, 0f), new Vector2(104f, 104f));
        Image thumbnail = thumbnailRect.gameObject.AddComponent<Image>();
        thumbnail.preserveAspect = true;
        thumbnail.raycastTarget = false;

        TMP_Text constellation = CreateText(
            rect, "Constellation", boldFont, 16f, FontStyles.Bold,
            TextAlignmentOptions.Left, InkSoft,
            new Vector2(40f, 30f), new Vector2(210f, 24f));
        constellation.enableWordWrapping = false;
        TMP_Text cocktailName = CreateText(
            rect, "CocktailName", boldFont, 20f, FontStyles.Bold,
            TextAlignmentOptions.Left, Ink,
            new Vector2(48f, -2f), new Vector2(252f, 31f));
        cocktailName.enableWordWrapping = false;
        cocktailName.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text status = CreateText(
            rect, "Status", font, 14f, FontStyles.Normal,
            TextAlignmentOptions.Right, InkSoft,
            new Vector2(112f, -37f), new Vector2(120f, 22f));

        RectTransform newBadge = CreateUiObject("NewBadge", rect).GetComponent<RectTransform>();
        SetRect(newBadge, new Vector2(176f, 50f), new Vector2(56f, 24f));
        Image newBadgeBack = newBadge.gameObject.AddComponent<Image>();
        newBadgeBack.color = Navy;
        newBadgeBack.raycastTarget = false;
        AddPixelBorder(newBadge, BronzeHighlight, 2f);
        TMP_Text newLabel = CreateText(
            newBadge, "Label", boldFont, 13f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(255, 222, 128, 255),
            Vector2.zero, new Vector2(52f, 20f));
        newLabel.text = "NEW";

        RectTransform selection =
            CreateUiObject("SelectionFrame", rect).GetComponent<RectTransform>();
        SetStretch(selection);
        AddPixelBorder(selection, BronzeHighlight, 4f);
        RectTransform starRect = CreateUiObject("Star", selection).GetComponent<RectTransform>();
        SetRect(starRect, new Vector2(190f, 0f), new Vector2(35f, 35f));
        Image star = starRect.gameObject.AddComponent<Image>();
        star.sprite = starSprite;
        star.preserveAspect = true;
        star.raycastTarget = false;
        selection.gameObject.SetActive(false);

        CocktailCollectionSlotView view =
            rect.gameObject.AddComponent<CocktailCollectionSlotView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_button").objectReferenceValue = button;
        serialized.FindProperty("_background").objectReferenceValue = background;
        serialized.FindProperty("_thumbnail").objectReferenceValue = thumbnail;
        serialized.FindProperty("_constellationLabel").objectReferenceValue = constellation;
        serialized.FindProperty("_nameLabel").objectReferenceValue = cocktailName;
        serialized.FindProperty("_statusLabel").objectReferenceValue = status;
        serialized.FindProperty("_newBadge").objectReferenceValue = newBadge.gameObject;
        serialized.FindProperty("_selectionFrame").objectReferenceValue = selection.gameObject;
        serialized.FindProperty("_lockedFallbackSprite").objectReferenceValue = lockedFallback;
        serialized.FindProperty("_silhouetteMaterial").objectReferenceValue = silhouetteMaterial;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
        return view;
    }

    private static Button CreatePageButton(
        RectTransform parent,
        string name,
        Vector2 position,
        Sprite sprite,
        bool flip)
    {
        RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
        SetRect(rect, position, new Vector2(60f, 42f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        if (flip)
        {
            rect.localScale = new Vector3(-1f, 1f, 1f);
        }

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color32(255, 222, 150, 255);
        colors.pressedColor = new Color32(194, 126, 46, 255);
        colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        return button;
    }

    private static DetailReferences CreateDetail(
        RectTransform book,
        TMP_FontAsset font,
        TMP_FontAsset boldFont,
        Sprite frameSprite,
        Sprite lockedFallback)
    {
        RectTransform rightPage = CreateUiObject("RightPage", book).GetComponent<RectTransform>();
        SetRect(rightPage, new Vector2(292f, 0f), new Vector2(466f, 754f));

        TMP_Text header = CreateText(
            rightPage, "DetailHeader", boldFont, 23f, FontStyles.Bold,
            TextAlignmentOptions.Center, Ink,
            new Vector2(0f, 368f), new Vector2(410f, 34f));
        header.text = "칵테일 기록";

        RectTransform frameRect = CreateUiObject("ArtworkFrame", rightPage)
            .GetComponent<RectTransform>();
        SetRect(frameRect, new Vector2(0f, 150f), new Vector2(434f, 374f));
        Image frame = frameRect.gameObject.AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.preserveAspect = true;
        frame.raycastTarget = false;

        RectTransform artworkRect = CreateUiObject("Artwork", rightPage)
            .GetComponent<RectTransform>();
        SetRect(artworkRect, new Vector2(0f, 166f), new Vector2(266f, 266f));
        Image artwork = artworkRect.gameObject.AddComponent<Image>();
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;

        TMP_Text constellation = CreateText(
            rightPage, "Constellation", boldFont, 15f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(232, 184, 78, 255),
            new Vector2(0f, 311f), new Vector2(330f, 24f));
        TMP_Text name = CreateText(
            rightPage, "CocktailName", boldFont, 23f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(242, 212, 147, 255),
            new Vector2(0f, -20f), new Vector2(308f, 34f));
        name.enableWordWrapping = false;
        name.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform newBadge = CreateUiObject("NewBadge", rightPage)
            .GetComponent<RectTransform>();
        SetRect(newBadge, new Vector2(177f, 313f), new Vector2(56f, 24f));
        Image newBadgeBack = newBadge.gameObject.AddComponent<Image>();
        newBadgeBack.color = Navy;
        newBadgeBack.raycastTarget = false;
        AddPixelBorder(newBadge, BronzeHighlight, 2f);
        TMP_Text newBadgeText = CreateText(
            newBadge, "Label", boldFont, 13f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(255, 222, 128, 255),
            Vector2.zero, new Vector2(52f, 20f));
        newBadgeText.text = "NEW";

        TMP_Text subtitle = CreateText(
            rightPage, "Subtitle", boldFont, 18f, FontStyles.Bold,
            TextAlignmentOptions.Center, InkSoft,
            new Vector2(0f, -70f), new Vector2(400f, 28f));
        TMP_Text description = CreateText(
            rightPage, "Description", font, 16f, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, Ink,
            new Vector2(0f, -119f), new Vector2(402f, 64f));
        description.lineSpacing = 3f;
        TMP_Text keywords = CreateText(
            rightPage, "Keywords", boldFont, 15f, FontStyles.Bold,
            TextAlignmentOptions.Left, InkSoft,
            new Vector2(0f, -172f), new Vector2(402f, 28f));
        keywords.enableWordWrapping = false;
        keywords.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform divider = CreateUiObject("Divider", rightPage)
            .GetComponent<RectTransform>();
        SetRect(divider, new Vector2(0f, -199f), new Vector2(400f, 2f));
        Image dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = Bronze;
        dividerImage.raycastTarget = false;

        TMP_Text recipe = CreateText(
            rightPage, "Recipe", font, 16f, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, Ink,
            new Vector2(0f, -273f), new Vector2(402f, 130f));
        recipe.lineSpacing = 6f;
        TMP_Text bundle = CreateText(
            rightPage, "BundleStatus", boldFont, 16f, FontStyles.Bold,
            TextAlignmentOptions.Center, InkSoft,
            new Vector2(0f, -355f), new Vector2(410f, 28f));

        return new DetailReferences
        {
            Artwork = artwork,
            Constellation = constellation,
            Name = name,
            Subtitle = subtitle,
            Description = description,
            Keywords = keywords,
            Recipe = recipe,
            BundleStatus = bundle,
            NewBadge = newBadge.gameObject
        };
    }

    private static PlaceholderReferences CreatePlaceholder(
        RectTransform book,
        TMP_FontAsset font,
        TMP_FontAsset boldFont,
        Sprite frameSprite,
        Sprite starSprite,
        Sprite characterSprite,
        Sprite achievementSprite,
        Material activeTabMaterial)
    {
        RectTransform root = CreateUiObject(
            "PlaceholderContent", book).GetComponent<RectTransform>();
        SetStretch(root);

        TMP_Text sectionTitle = CreateText(
            root, "SectionTitle", boldFont, 28f, FontStyles.Bold,
            TextAlignmentOptions.Left, Ink,
            new Vector2(-325f, 365f), new Vector2(286f, 40f));
        sectionTitle.text = "승객 기록";
        TMP_Text sectionState = CreateText(
            root, "SectionState", boldFont, 17f, FontStyles.Bold,
            TextAlignmentOptions.Right, InkSoft,
            new Vector2(-102f, 365f), new Vector2(176f, 30f));
        sectionState.text = "기록 준비 중";

        RectTransform leftPanel = CreateUiObject(
            "EmptyIndexPanel", root).GetComponent<RectTransform>();
        SetRect(leftPanel, new Vector2(-240f, 38f), new Vector2(434f, 374f));
        Image leftUnderlay = leftPanel.gameObject.AddComponent<Image>();
        leftUnderlay.color = Navy;
        leftUnderlay.raycastTarget = false;
        RectTransform leftFrame = CreateUiObject(
            "Frame", leftPanel).GetComponent<RectTransform>();
        SetStretch(leftFrame);
        Image leftFrameImage = leftFrame.gameObject.AddComponent<Image>();
        leftFrameImage.sprite = frameSprite;
        leftFrameImage.preserveAspect = true;
        leftFrameImage.raycastTarget = false;

        RectTransform iconRect = CreateUiObject(
            "ArchiveIcon", leftPanel).GetComponent<RectTransform>();
        SetRect(iconRect, new Vector2(0f, 66f), new Vector2(132f, 158f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = characterSprite;
        icon.material = activeTabMaterial;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = CreateText(
            leftPanel, "Title", boldFont, 24f, FontStyles.Bold,
            TextAlignmentOptions.Center, PaleBlue,
            new Vector2(0f, -70f), new Vector2(360f, 34f));
        title.text = "승객 기록";
        TMP_Text description = CreateText(
            leftPanel, "Description", font, 16f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color32(190, 204, 222, 255),
            new Vector2(0f, -116f), new Vector2(360f, 30f));
        description.text = "아직 만난 승객이 없습니다.";

        RectTransform rightPanel = CreateUiObject(
            "EmptyDetailPanel", root).GetComponent<RectTransform>();
        SetRect(rightPanel, new Vector2(292f, 38f), new Vector2(434f, 374f));
        Image rightUnderlay = rightPanel.gameObject.AddComponent<Image>();
        rightUnderlay.color = Navy;
        rightUnderlay.raycastTarget = false;
        RectTransform rightFrame = CreateUiObject(
            "Frame", rightPanel).GetComponent<RectTransform>();
        SetStretch(rightFrame);
        Image rightFrameImage = rightFrame.gameObject.AddComponent<Image>();
        rightFrameImage.sprite = frameSprite;
        rightFrameImage.preserveAspect = true;
        rightFrameImage.raycastTarget = false;

        RectTransform starRect = CreateUiObject(
            "ArchiveStar", rightPanel).GetComponent<RectTransform>();
        SetRect(starRect, new Vector2(0f, 83f), new Vector2(70f, 70f));
        Image star = starRect.gameObject.AddComponent<Image>();
        star.sprite = starSprite;
        star.preserveAspect = true;
        star.raycastTarget = false;

        TMP_Text detailTitle = CreateText(
            rightPanel, "Title", boldFont, 22f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(255, 222, 128, 255),
            new Vector2(0f, 14f), new Vector2(360f, 36f));
        detailTitle.text = "새로운 만남을 기다리는 중";
        TMP_Text detailDescription = CreateText(
            rightPanel, "Description", font, 16f, FontStyles.Normal,
            TextAlignmentOptions.Center, PaleBlue,
            new Vector2(0f, -58f), new Vector2(360f, 72f));
        detailDescription.text =
            "열차에서 새로운 승객을 만나면\n그들의 이야기와 취향이 이곳에 기록됩니다.";
        detailDescription.lineSpacing = 5f;

        root.gameObject.SetActive(false);
        return new PlaceholderReferences
        {
            Root = root.gameObject,
            Icon = icon,
            SectionTitle = sectionTitle,
            Title = title,
            Description = description,
            DetailTitle = detailTitle,
            DetailDescription = detailDescription
        };
    }

    private static Button CreateCloseButton(RectTransform book, TMP_FontAsset font)
    {
        RectTransform rect = CreateUiObject("CloseButton", book).GetComponent<RectTransform>();
        SetRect(rect, new Vector2(562f, 340f), new Vector2(78f, 34f));
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = Navy;
        background.raycastTarget = true;
        AddPixelBorder(rect, BronzeHighlight, 3f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color32(20, 34, 58, 255);
        colors.pressedColor = new Color32(0, 4, 14, 255);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        TMP_Text label = CreateText(
            rect, "Label", font, 13f, FontStyles.Bold,
            TextAlignmentOptions.Center, PaleBlue,
            Vector2.zero, new Vector2(70f, 26f));
        label.text = "닫기";
        return button;
    }

    private static void ConfigureController(
        CocktailCollectionController controller,
        GameObject uiRoot,
        CanvasGroup canvasGroup,
        RectTransform book,
        GameObject cocktailContentRoot,
        TabReferences tabs,
        IndexReferences index,
        DetailReferences detail,
        PlaceholderReferences placeholder,
        Material activeTabMaterial,
        Sprite characterTabSprite,
        Sprite achievementTabSprite,
        Material silhouetteMaterial,
        Sprite lockedFallback,
        Button closeButton)
    {
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("_uiRoot").objectReferenceValue = uiRoot;
        serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        serialized.FindProperty("_bookRoot").objectReferenceValue = book;
        serialized.FindProperty("_characterTabButton").objectReferenceValue = tabs.Character;
        serialized.FindProperty("_cocktailTabButton").objectReferenceValue = tabs.Cocktail;
        serialized.FindProperty("_achievementTabButton").objectReferenceValue = tabs.Achievement;
        serialized.FindProperty("_activeTabMaterial").objectReferenceValue = activeTabMaterial;
        serialized.FindProperty("_cocktailContentRoot").objectReferenceValue = cocktailContentRoot;
        serialized.FindProperty("_placeholderContentRoot").objectReferenceValue = placeholder.Root;
        serialized.FindProperty("_placeholderIcon").objectReferenceValue = placeholder.Icon;
        serialized.FindProperty("_placeholderSectionTitle").objectReferenceValue =
            placeholder.SectionTitle;
        serialized.FindProperty("_placeholderTitle").objectReferenceValue = placeholder.Title;
        serialized.FindProperty("_placeholderDescription").objectReferenceValue =
            placeholder.Description;
        serialized.FindProperty("_placeholderDetailTitle").objectReferenceValue =
            placeholder.DetailTitle;
        serialized.FindProperty("_placeholderDetailDescription").objectReferenceValue =
            placeholder.DetailDescription;
        serialized.FindProperty("_characterTabSprite").objectReferenceValue = characterTabSprite;
        serialized.FindProperty("_achievementTabSprite").objectReferenceValue =
            achievementTabSprite;
        serialized.FindProperty("_collectionTitle").objectReferenceValue = index.Title;
        serialized.FindProperty("_progressLabel").objectReferenceValue = index.Progress;
        SetObjectList(
            serialized.FindProperty("_slots"),
            index.Slots.Cast<UnityEngine.Object>());
        serialized.FindProperty("_previousPageButton").objectReferenceValue = index.Previous;
        serialized.FindProperty("_nextPageButton").objectReferenceValue = index.Next;
        serialized.FindProperty("_pageLabel").objectReferenceValue = index.PageLabel;
        serialized.FindProperty("_detailArtwork").objectReferenceValue = detail.Artwork;
        serialized.FindProperty("_detailConstellation").objectReferenceValue = detail.Constellation;
        serialized.FindProperty("_detailName").objectReferenceValue = detail.Name;
        serialized.FindProperty("_detailSubtitle").objectReferenceValue = detail.Subtitle;
        serialized.FindProperty("_detailDescription").objectReferenceValue = detail.Description;
        serialized.FindProperty("_detailKeywords").objectReferenceValue = detail.Keywords;
        serialized.FindProperty("_detailRecipe").objectReferenceValue = detail.Recipe;
        serialized.FindProperty("_bundleStatus").objectReferenceValue = detail.BundleStatus;
        serialized.FindProperty("_detailNewBadge").objectReferenceValue = detail.NewBadge;
        serialized.FindProperty("_silhouetteMaterial").objectReferenceValue = silhouetteMaterial;
        serialized.FindProperty("_lockedFallbackSprite").objectReferenceValue = lockedFallback;
        serialized.FindProperty("_closeButton").objectReferenceValue = closeButton;
        serialized.FindProperty("_openDuration").floatValue = 0.22f;
        serialized.FindProperty("_closeDuration").floatValue = 0.16f;
        serialized.FindProperty("_bookOffsetPixels").floatValue = 18f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureWorldTrigger(
        Scene scene,
        CocktailCollectionController controller)
    {
        Transform article = FindRoot(scene, "Article")
            ?? throw new InvalidOperationException("Play scene requires Article root.");
        Transform worldBook = article.Find("Dict")
            ?? throw new InvalidOperationException("Article/Dict world book was not found.");
        SpriteRenderer renderer = worldBook.GetComponent<SpriteRenderer>()
            ?? throw new InvalidOperationException("Article/Dict requires SpriteRenderer.");

        BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(worldBook.gameObject);
        collider.isTrigger = true;
        if (renderer.sprite != null)
        {
            collider.size = renderer.sprite.bounds.size;
            collider.offset = renderer.sprite.bounds.center;
        }

        CocktailCollectionOpenTrigger trigger =
            GetOrAddComponent<CocktailCollectionOpenTrigger>(worldBook.gameObject);
        var triggerSerialized = new SerializedObject(trigger);
        triggerSerialized.FindProperty("_controller").objectReferenceValue = controller;
        triggerSerialized.FindProperty("_spriteRenderer").objectReferenceValue = renderer;
        triggerSerialized.FindProperty("_hoverColor").colorValue =
            new Color32(255, 234, 176, 255);
        triggerSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);

        Camera mainCamera = Camera.main ?? FindComponentInScene<Camera>(scene);
        if (mainCamera == null)
        {
            throw new InvalidOperationException("Play scene requires a camera for Dict interaction.");
        }

        GetOrAddComponent<Physics2DRaycaster>(mainCamera.gameObject);
        EditorUtility.SetDirty(mainCamera.gameObject);
    }

    private static Material LoadOrCreateSilhouetteMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SilhouetteShaderPath)
            ?? Shader.Find("MidnightExpress/UI/Cocktail Silhouette");
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Collection silhouette shader is missing at '{SilhouetteShaderPath}'.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "DictCocktailSilhouette" };
            AssetDatabase.CreateAsset(material, SilhouetteMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetColor("_SilhouetteColor", new Color32(106, 70, 41, 242));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateActiveTabMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ActiveTabShaderPath)
            ?? Shader.Find("MidnightExpress/UI/Collection Active Tab");
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Collection active-tab shader is missing at '{ActiveTabShaderPath}'.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ActiveTabMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "DictTabActive" };
            AssetDatabase.CreateAsset(material, ActiveTabMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetColor("_ShadowColor", new Color32(60, 36, 12, 255));
        material.SetColor("_MidColor", new Color32(138, 79, 19, 255));
        material.SetColor("_HighlightColor", new Color32(232, 184, 78, 255));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTextureImporters()
    {
        string[] roots = { DictArtRoot, CocktailArtRoot };
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = importer.filterMode != FilterMode.Point ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed ||
                           importer.mipmapEnabled;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            if (path.StartsWith(CocktailArtRoot, StringComparison.Ordinal) &&
                importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
        SetRect(rect, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static void AddPixelBorder(RectTransform parent, Color color, float thickness)
    {
        CreateBorderLine(parent, "Top", color,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        CreateBorderLine(parent, "Bottom", color,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        CreateBorderLine(parent, "Left", color,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        CreateBorderLine(parent, "Right", color,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
    }

    private static void CreateBorderLine(
        RectTransform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size)
    {
        RectTransform rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return sprite != null
            ? sprite
            : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(gameObject => gameObject.name == name)
            ?.transform;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component == null ? gameObject.AddComponent<T>() : component;
    }

    private static void SetObjectList(
        SerializedProperty property,
        IEnumerable<UnityEngine.Object> values)
    {
        UnityEngine.Object[] array = values.ToArray();
        property.arraySize = array.Length;
        for (int index = 0; index < array.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = array[index];
        }
    }

    private sealed class TabReferences
    {
        public Button Character;
        public Button Cocktail;
        public Button Achievement;
    }

    private sealed class IndexReferences
    {
        public TMP_Text Title;
        public TMP_Text Progress;
        public List<CocktailCollectionSlotView> Slots;
        public Button Previous;
        public Button Next;
        public TMP_Text PageLabel;
    }

    private sealed class DetailReferences
    {
        public Image Artwork;
        public TMP_Text Constellation;
        public TMP_Text Name;
        public TMP_Text Subtitle;
        public TMP_Text Description;
        public TMP_Text Keywords;
        public TMP_Text Recipe;
        public TMP_Text BundleStatus;
        public GameObject NewBadge;
    }

    private sealed class PlaceholderReferences
    {
        public GameObject Root;
        public Image Icon;
        public TMP_Text SectionTitle;
        public TMP_Text Title;
        public TMP_Text Description;
        public TMP_Text DetailTitle;
        public TMP_Text DetailDescription;
    }
}
#endif
