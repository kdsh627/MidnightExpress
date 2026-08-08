#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CocktailSceneSetupTool
{
    private const string BootstrapScenePath =
        "Assets/02. Scenes/Bootstrap/BootstrapScene.unity";
    private const string PlayScenePath = "Assets/02. Scenes/Play/Play.unity";
    private const string DataRoot = "Assets/01. Total/Data/Cocktail";
    private const string IngredientRoot = DataRoot + "/Ingredients";
    private const string RecipeRoot = DataRoot + "/Recipes";
    private const string BundleRoot = DataRoot + "/Bundles";
    private const string DatabasePath = DataRoot + "/CocktailDatabaseSO.asset";
    private const string FontPath = "Assets/01. Total/Fonts/Galmuri11.asset";
    private const string BoldFontPath = "Assets/01. Total/Fonts/Galmuri11-Bold.asset";
    private const string GlowPath =
        "Assets/02. Scenes/Play/Arts/Panel/Bar_Glowing_animation.png";

    private static readonly Color Navy = new Color32(2, 10, 29, 255);
    private static readonly Color NavyPanel = new Color32(2, 13, 41, 245);
    private static readonly Color NavyHover = new Color32(6, 20, 45, 255);
    private static readonly Color StarBlue = new Color32(55, 89, 166, 255);
    private static readonly Color StarBlueBright = new Color32(104, 136, 208, 255);
    private static readonly Color PaleBlue = new Color32(232, 240, 248, 255);
    private static readonly Color BronzeShadow = new Color32(44, 21, 11, 255);
    private static readonly Color Bronze = new Color32(99, 52, 24, 255);
    private static readonly Color BronzeBright = new Color32(144, 80, 24, 255);
    private static readonly Color BronzeHighlight = new Color32(160, 96, 56, 255);

    [MenuItem("Tools/Cocktail/Setup Cocktail Casting")]
    public static void SetupCocktailCasting()
    {
        try
        {
            EditorSceneManager.SaveOpenScenes();
            EnsureAssetFolder(IngredientRoot);
            EnsureAssetFolder(RecipeRoot);
            EnsureAssetFolder(BundleRoot);
            ConfigurePanelTextureImporters();

            Dictionary<string, CocktailIngredientDataSO> ingredients = CreateIngredients();
            List<CocktailRecipeDataSO> recipes = CreateRecipes(ingredients);
            CocktailBundleDataSO zodiacBundle = CreateZodiacBundle(recipes);
            CocktailDatabaseSO database = CreateDatabase(
                ingredients.Values,
                recipes,
                new[] { zodiacBundle });
            database.ValidateOrThrow();

            AssignDatabaseToBootstrap(database);
            ConfigurePlayScene();
            CocktailCollectionSetupTool.ConfigurePlayScene(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Cocktail casting setup completed: 26 ingredients, 12 recipes, Bootstrap data, " +
                "Play casting UI, cocktail collection, constellation steps, reveal animation references, " +
                "and NextID -1 binding are connected.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static Dictionary<string, CocktailIngredientDataSO> CreateIngredients()
    {
        var result = new Dictionary<string, CocktailIngredientDataSO>(StringComparer.Ordinal);

        AddIngredient(result, "gin", "진", CocktailIngredientCategory.Base,
            "Assets/01. Total/Arts/기주/Gin.png");
        AddIngredient(result, "vodka", "보드카", CocktailIngredientCategory.Base,
            "Assets/01. Total/Arts/기주/Vodka.png");
        AddIngredient(result, "rum", "럼", CocktailIngredientCategory.Base,
            "Assets/01. Total/Arts/기주/Rum.png");
        AddIngredient(result, "bourbon", "버번", CocktailIngredientCategory.Base,
            "Assets/01. Total/Arts/기주/Bourbon.png");

        AddIngredient(result, "soda", "탄산수", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Sparkling_Water.png");
        AddIngredient(result, "ginger_ale", "진저에일", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Ginger_ale.png");
        AddIngredient(result, "grapefruit_juice", "자몽주스", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Grapefruit_juice.png");
        AddIngredient(result, "iced_tea", "아이스티", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Ice_tea.png");
        AddIngredient(result, "coconut_milk", "코코넛밀크", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Coconut_milk.png");
        AddIngredient(result, "pineapple_juice", "파인애플주스", CocktailIngredientCategory.Mixer,
            "Assets/01. Total/Arts/믹서/Pineapple_juice.png");

        AddIngredient(result, "sugar_syrup", "설탕 시럽", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Sugar_Syrup.png");
        AddIngredient(result, "lime_juice", "라임즙", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Lime_Juice.png");
        AddIngredient(result, "bitters", "비터스", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Bitters.png");
        AddIngredient(result, "grenadine", "그레나딘", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Grenadines.png");
        AddIngredient(result, "blue_curacao", "블루큐라소", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/BlueCuracao.png");
        AddIngredient(result, "lychee_syrup", "리치 시럽", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Lychee_syrup.png");
        AddIngredient(result, "jasmine_syrup", "자스민 시럽", CocktailIngredientCategory.Modifier,
            "Assets/01. Total/Arts/모디파이어/Jasmine_syrup.png");

        AddIngredient(result, "build", "빌드", CocktailIngredientCategory.Technique,
            "Assets/01. Total/Arts/기법/Build.png");
        AddIngredient(result, "shake", "셰이크", CocktailIngredientCategory.Technique,
            "Assets/01. Total/Arts/기법/Shake.png");
        AddIngredient(result, "stir", "스터", CocktailIngredientCategory.Technique,
            "Assets/01. Total/Arts/기법/Stir.png");
        AddIngredient(result, "blend", "블렌드", CocktailIngredientCategory.Technique,
            "Assets/01. Total/Arts/기법/Blend.png");

        AddIngredient(result, "lemon_slice", "레몬 슬라이스", CocktailIngredientCategory.Garnish,
            "Assets/01. Total/Arts/가니쉬/Remon_slice.png");
        AddIngredient(result, "lime_wedge", "라임 웨지", CocktailIngredientCategory.Garnish,
            "Assets/01. Total/Arts/가니쉬/Lime_wedges.png");
        AddIngredient(result, "apple_mint", "애플민트", CocktailIngredientCategory.Garnish,
            "Assets/01. Total/Arts/가니쉬/Apple_mint.png");
        AddIngredient(result, "salt", "소금", CocktailIngredientCategory.Garnish,
            "Assets/01. Total/Arts/가니쉬/Salt.png");
        AddIngredient(result, "sugar", "설탕", CocktailIngredientCategory.Garnish,
            "Assets/01. Total/Arts/가니쉬/Sugar.png");

        return result;
    }

    private static void AddIngredient(
        IDictionary<string, CocktailIngredientDataSO> destination,
        string id,
        string displayName,
        CocktailIngredientCategory category,
        string iconPath)
    {
        string assetPath = IngredientRoot + "/" + id + ".asset";
        CocktailIngredientDataSO asset =
            AssetDatabase.LoadAssetAtPath<CocktailIngredientDataSO>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CocktailIngredientDataSO>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("_id").stringValue = id;
        serialized.FindProperty("_displayName").stringValue = displayName;
        serialized.FindProperty("_category").enumValueIndex = (int)category;
        serialized.FindProperty("_icon").objectReferenceValue =
            string.IsNullOrWhiteSpace(iconPath) ? null : LoadSprite(iconPath);
        serialized.FindProperty("_unlockedByDefault").boolValue = true;
        serialized.FindProperty("_castingDialogueId").intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        destination.Add(id, asset);
    }

    private static List<CocktailRecipeDataSO> CreateRecipes(
        IReadOnlyDictionary<string, CocktailIngredientDataSO> ingredients)
    {
        var recipes = new List<CocktailRecipeDataSO>
        {
            CreateRecipe(1, "aries", "양자리", "첫 유성의 충돌", "열정",
                new[] { "스파이시", "시트러스" },
                "유성이 밤하늘을 가르다 별에 부딪히는 순간처럼, 짧지만 잊히지 않는 한 잔.",
                ingredients, "bourbon", "ginger_ale", "bitters", "build", "lemon_slice",
                "Assets/01. Total/Arts/칵테일/1.Aries.png"),
            CreateRecipe(2, "taurus", "황소자리", "황금빛 노을을 바라보며", "풍요",
                new[] { "달콤", "부드러움" },
                "노을이 하루를 천천히 물들이듯, 마음도 조용히 따뜻해지는 한 잔.",
                ingredients, "rum", "coconut_milk", "lychee_syrup", "stir", "sugar",
                "Assets/01. Total/Arts/칵테일/2.Taurus.png"),
            CreateRecipe(3, "gemini", "쌍둥이자리", "두 개의 메아리", "조화",
                new[] { "자몽", "라임", "시트러스" },
                "두 개의 목소리가 하나의 선율이 되는 순간을 담은 한 잔.",
                ingredients, "gin", "grapefruit_juice", "lime_juice", "shake", "lime_wedge",
                "Assets/01. Total/Arts/칵테일/3.Gemini.png"),
            CreateRecipe(4, "cancer", "게자리", "하얀 파도의 향기", "포근함",
                new[] { "부드러움", "트로피칼" },
                "파도가 남기고 간 하얀 포말처럼, 부드럽게 스며드는 한 잔.",
                ingredients, "rum", "coconut_milk", "sugar_syrup", "blend", "salt",
                "Assets/01. Total/Arts/칵테일/4.Cancer.png"),
            CreateRecipe(5, "leo", "사자자리", "여름을 닮은 청춘", "청춘",
                new[] { "밝음", "상큼함" },
                "햇살 아래 가장 눈부셨던 계절을 병에 담아낸 한 잔.",
                ingredients, "vodka", "pineapple_juice", "grenadine", "shake", "lemon_slice",
                "Assets/01. Total/Arts/칵테일/5.Leo.png"),
            CreateRecipe(6, "virgo", "처녀자리", "들꽃을 살피는 봄", "싱그러움",
                new[] { "은은함", "산뜻함" },
                "들꽃을 스친 봄바람처럼, 은은한 향이 오래 머무는 한 잔.",
                ingredients, "gin", "iced_tea", "jasmine_syrup", "build", "apple_mint",
                "Assets/01. Total/Arts/칵테일/6.Virgo.png"),
            CreateRecipe(7, "libra", "천칭자리", "보랏빛 왈츠", "우아함",
                new[] { "플로랄", "균형" },
                "꽃잎이 춤을 추듯, 향기와 우아함이 균형을 이루는 한 잔.",
                ingredients, "gin", "soda", "lychee_syrup", "build", "lemon_slice",
                "Assets/01. Total/Arts/칵테일/7.Libra.png"),
            CreateRecipe(8, "scorpio", "전갈자리", "수줍은 붉은 동백", "매혹",
                new[] { "깊음", "달콤함" },
                "붉게 피어난 동백이 보여주는 달콤함 뒤에 깊은 여운을 숨긴 한 잔.",
                ingredients, "vodka", "soda", "grenadine", "stir", "salt",
                "Assets/01. Total/Arts/칵테일/8.Scorpio.png"),
            CreateRecipe(9, "sagittarius", "사수자리", "바람 끝의 항로", "자유",
                new[] { "청량", "시원함" },
                "낯선 바람을 따라 끝없이 나아가는 여행자를 위한 한 잔.",
                ingredients, "gin", "soda", "lime_juice", "build", "lime_wedge",
                "Assets/01. Total/Arts/칵테일/9.Sagittarius.png"),
            CreateRecipe(10, "capricorn", "염소자리", "겨울 산의 숨결", "절제",
                new[] { "드라이", "깔끔함" },
                "겨울 산의 맑은 공기처럼, 군더더기 없이 깊은 한 잔.",
                ingredients, "gin", "soda", "bitters", "stir", "lemon_slice",
                "Assets/01. Total/Arts/칵테일/10.Capricon.png"),
            CreateRecipe(11, "aquarius", "물병자리", "새벽 이슬의 아침 인사", "청량",
                new[] { "깨끗함", "청량함" },
                "새벽 이슬이 햇살을 머금는 순간처럼, 맑게 깨어나는 한 잔.",
                ingredients, "vodka", "soda", "jasmine_syrup", "build", "apple_mint",
                "Assets/01. Total/Arts/칵테일/11.Aquarius.png"),
            CreateRecipe(12, "pisces", "물고기자리", "산호 너머의 헤엄치는 꿈", "몽환",
                new[] { "시원함", "달콤함" },
                "푸른 바다 깊은 곳, 산호 사이를 유영하는 물결을 담은 한 잔.",
                ingredients, "rum", "coconut_milk", "blue_curacao", "blend", "lime_wedge",
                "Assets/01. Total/Arts/칵테일/12.Pisces.png")
        };

        return recipes;
    }

    private static CocktailRecipeDataSO CreateRecipe(
        int order,
        string id,
        string constellation,
        string cocktailName,
        string emotionKeyword,
        IReadOnlyList<string> tasteKeywords,
        string description,
        IReadOnlyDictionary<string, CocktailIngredientDataSO> ingredients,
        string baseId,
        string mixerId,
        string modifierId,
        string techniqueId,
        string garnishId,
        string resultSpritePath)
    {
        string path = RecipeRoot + "/" + order.ToString("00") + "_" + id + ".asset";
        CocktailRecipeDataSO asset = AssetDatabase.LoadAssetAtPath<CocktailRecipeDataSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CocktailRecipeDataSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("_id").stringValue = id;
        serialized.FindProperty("_displayOrder").intValue = order;
        serialized.FindProperty("_constellation").stringValue = constellation;
        serialized.FindProperty("_cocktailName").stringValue = cocktailName;
        serialized.FindProperty("_subtitle").stringValue = constellation;
        serialized.FindProperty("_emotionKeyword").stringValue = emotionKeyword;
        SetStringList(serialized.FindProperty("_tasteKeywords"), tasteKeywords);
        serialized.FindProperty("_oneLineDescription").stringValue = description;
        serialized.FindProperty("_base").objectReferenceValue = ingredients[baseId];
        serialized.FindProperty("_mixer").objectReferenceValue =
            string.IsNullOrWhiteSpace(mixerId) ? null : ingredients[mixerId];
        serialized.FindProperty("_modifier").objectReferenceValue =
            string.IsNullOrWhiteSpace(modifierId) ? null : ingredients[modifierId];
        serialized.FindProperty("_technique").objectReferenceValue = ingredients[techniqueId];
        serialized.FindProperty("_garnish").objectReferenceValue =
            string.IsNullOrWhiteSpace(garnishId) ? null : ingredients[garnishId];
        serialized.FindProperty("_resultSprite").objectReferenceValue =
            string.IsNullOrWhiteSpace(resultSpritePath) ? null : LoadSprite(resultSpritePath);
        serialized.FindProperty("_hintSilhouette").objectReferenceValue = null;
        SetStringList(serialized.FindProperty("_tags"), new[] { "zodiac", constellation });
        serialized.FindProperty("_hidden").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static CocktailBundleDataSO CreateZodiacBundle(
        IReadOnlyList<CocktailRecipeDataSO> recipes)
    {
        string path = BundleRoot + "/ZodiacBundle.asset";
        CocktailBundleDataSO asset =
            AssetDatabase.LoadAssetAtPath<CocktailBundleDataSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CocktailBundleDataSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("_id").stringValue = "zodiac";
        serialized.FindProperty("_displayName").stringValue = "별자리 칵테일";
        serialized.FindProperty("_description").stringValue =
            "열두 별자리의 정서와 맛을 기록하는 칵테일 컬렉션.";
        SetObjectList(
            serialized.FindProperty("_recipes"),
            recipes.Cast<UnityEngine.Object>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static CocktailDatabaseSO CreateDatabase(
        IEnumerable<CocktailIngredientDataSO> ingredients,
        IEnumerable<CocktailRecipeDataSO> recipes,
        IEnumerable<CocktailBundleDataSO> bundles)
    {
        CocktailDatabaseSO database =
            AssetDatabase.LoadAssetAtPath<CocktailDatabaseSO>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CocktailDatabaseSO>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        var serialized = new SerializedObject(database);
        SetObjectList(serialized.FindProperty("_ingredients"), ingredients.Cast<UnityEngine.Object>());
        SetObjectList(serialized.FindProperty("_recipes"), recipes.Cast<UnityEngine.Object>());
        SetObjectList(
            serialized.FindProperty("_guestPreferences"),
            Array.Empty<UnityEngine.Object>());
        SetObjectList(
            serialized.FindProperty("_bundles"),
            bundles.Cast<UnityEngine.Object>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
        return database;
    }

    private static void AssignDatabaseToBootstrap(CocktailDatabaseSO database)
    {
        Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        BaseScope baseScope = FindComponentInScene<BaseScope>(scene)
            ?? throw new InvalidOperationException("BootstrapScene requires BaseScope.");
        var serialized = new SerializedObject(baseScope);
        SerializedProperty property = serialized.FindProperty("_cocktailDatabase");
        if (property == null)
        {
            throw new InvalidOperationException("BaseScope has no _cocktailDatabase field.");
        }

        property.objectReferenceValue = database;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(baseScope);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("BootstrapScene could not be saved.");
        }
    }

    private static void ConfigurePlayScene()
    {
        Scene scene = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);
        Transform ui = FindRoot(scene, "UI")
            ?? throw new InvalidOperationException("Play scene requires a root UI Canvas.");
        RectTransform cocktailRoot = ui.Find("Cocktail") as RectTransform;
        if (cocktailRoot == null)
        {
            cocktailRoot = CreateUiObject("Cocktail", ui).GetComponent<RectTransform>();
        }

        cocktailRoot.gameObject.SetActive(true);
        SetStretch(cocktailRoot);
        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(cocktailRoot.gameObject);

        RectTransform revealMask = cocktailRoot.Find("RevealMask") as RectTransform;
        RectTransform panelShell;
        if (revealMask == null)
        {
            panelShell = FindExistingPanelShell(cocktailRoot);
            revealMask = CreateUiObject("RevealMask", cocktailRoot).GetComponent<RectTransform>();
            panelShell.SetParent(revealMask, false);
        }
        else
        {
            panelShell = revealMask.Find("PanelShell") as RectTransform;
        }

        if (panelShell == null)
        {
            throw new InvalidOperationException("UI/Cocktail has no existing panel artwork root.");
        }

        revealMask.anchorMin = revealMask.anchorMax = new Vector2(1f, 0.5f);
        revealMask.pivot = new Vector2(0f, 0.5f);
        revealMask.anchoredPosition = new Vector2(-1191f, 0f);
        revealMask.sizeDelta = new Vector2(1191f, 1080f);
        GetOrAddComponent<RectMask2D>(revealMask.gameObject);

        panelShell.name = "PanelShell";
        panelShell.anchorMin = panelShell.anchorMax = new Vector2(0f, 0.5f);
        panelShell.pivot = new Vector2(0f, 0.5f);
        panelShell.anchoredPosition = Vector2.zero;
        panelShell.sizeDelta = new Vector2(1191f, 1080f);
        panelShell.localScale = Vector3.one;

        RenamePanelArtwork(panelShell);
        DisableDecorativeRaycasts(panelShell);

        RectTransform inventoryPanel = FindChildBySpriteName(panelShell, "Bar_Casting_UI_OutLine")
            ?? throw new InvalidOperationException("Cocktail panel requires the inventory frame sprite.");
        inventoryPanel.name = "InventoryPanel";
        RectTransform recipeGraph = panelShell.Find("RecipeGraph") as RectTransform
            ?? FindGraphContainer(panelShell);
        if (recipeGraph == null)
        {
            throw new InvalidOperationException("Cocktail panel requires its five-node graph container.");
        }

        recipeGraph.name = "RecipeGraph";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath) ?? font;
        if (font == null)
        {
            throw new InvalidOperationException($"Cocktail UI font was not found at '{FontPath}'.");
        }

        Sprite[] glowFrames = AssetDatabase.LoadAllAssetsAtPath(GlowPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
        List<CocktailStepNodeView> nodes = ConfigureRecipeGraph(
            recipeGraph,
            font,
            glowFrames);
        InventoryViewReferences view = ConfigureInventoryPanel(inventoryPanel, font, boldFont);

        GameObject overlay = FindCocktailOverlay(scene);
        overlay.name = "CocktailBackground";
        CanvasGroup backgroundCanvasGroup = GetOrAddComponent<CanvasGroup>(overlay);
        backgroundCanvasGroup.alpha = 0f;
        backgroundCanvasGroup.interactable = false;
        backgroundCanvasGroup.blocksRaycasts = false;
        overlay.SetActive(false);

        GameScope gameScope = FindComponentInScene<GameScope>(scene)
            ?? throw new InvalidOperationException("Play scene requires GameScope.");
        CocktailCastingController controller =
            GetOrAddComponent<CocktailCastingController>(gameScope.gameObject);
        ConfigureController(
            controller,
            cocktailRoot.gameObject,
            overlay,
            backgroundCanvasGroup,
            canvasGroup,
            revealMask,
            panelShell,
            nodes,
            view);
        ConnectDialogueStart(gameScope.gameObject, controller);

        cocktailRoot.gameObject.SetActive(false);
        overlay.SetActive(false);
        EditorUtility.SetDirty(cocktailRoot.gameObject);
        EditorUtility.SetDirty(gameScope.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Play scene could not be saved.");
        }
    }

    private static RectTransform FindExistingPanelShell(RectTransform cocktailRoot)
    {
        for (int index = 0; index < cocktailRoot.childCount; index++)
        {
            RectTransform child = cocktailRoot.GetChild(index) as RectTransform;
            if (child != null && child.GetComponent<Image>() != null)
            {
                return child;
            }
        }

        return null;
    }

    private static void RenamePanelArtwork(RectTransform panelShell)
    {
        for (int index = 0; index < panelShell.childCount; index++)
        {
            Transform child = panelShell.GetChild(index);
            Image image = child.GetComponent<Image>();
            string spriteName = image != null && image.sprite != null ? image.sprite.name : string.Empty;

            if (spriteName.Contains("Drawer"))
            {
                child.name = "Drawer";
            }
            else if (spriteName.Contains("Panel_OutLine"))
            {
                child.name = "OuterFrame";
            }
            else if (spriteName.Contains("Casting_UI_OutLine"))
            {
                child.name = "InventoryPanel";
            }
            else if (child.childCount >= 5 && image == null)
            {
                child.name = "RecipeGraph";
            }
        }

        RectTransform inventory = panelShell.Find("InventoryPanel") as RectTransform;
        if (inventory != null)
        {
            RectTransform back = FindChildBySpriteName(inventory, "Bar_Casting_UI_Back");
            if (back != null)
            {
                back.name = "Back";
                back.SetAsFirstSibling();
            }
        }
    }

    private static RectTransform FindGraphContainer(RectTransform panelShell)
    {
        for (int index = 0; index < panelShell.childCount; index++)
        {
            RectTransform child = panelShell.GetChild(index) as RectTransform;
            if (child == null || child.GetComponent<Image>() != null)
            {
                continue;
            }

            int numberNodes = 0;
            for (int childIndex = 0; childIndex < child.childCount; childIndex++)
            {
                if (int.TryParse(child.GetChild(childIndex).name, out _))
                {
                    numberNodes++;
                }
            }

            if (numberNodes >= 5)
            {
                return child;
            }
        }

        return null;
    }

    private static List<CocktailStepNodeView> ConfigureRecipeGraph(
        RectTransform recipeGraph,
        TMP_FontAsset font,
        Sprite[] glowFrames)
    {
        RectTransform connectorLayer = EnsureRectChild(recipeGraph, "ConnectorLayer");
        SetStretch(connectorLayer);
        connectorLayer.SetAsFirstSibling();

        var nodeTransforms = new List<RectTransform>();
        for (int number = 1; number <= 5; number++)
        {
            RectTransform node = recipeGraph.Find("RecipeNode" + number.ToString("00")) as RectTransform
                                 ?? recipeGraph.Find(number.ToString()) as RectTransform;
            if (node == null)
            {
                throw new InvalidOperationException($"RecipeGraph requires node {number}.");
            }

            node.name = "RecipeNode" + number.ToString("00");
            nodeTransforms.Add(node);
        }

        ClearChildren(connectorLayer);
        for (int index = 0; index < nodeTransforms.Count - 1; index++)
        {
            CreateConnector(connectorLayer, nodeTransforms[index].anchoredPosition,
                nodeTransforms[index + 1].anchoredPosition, index);
        }

        var result = new List<CocktailStepNodeView>();
        for (int index = 0; index < nodeTransforms.Count; index++)
        {
            RectTransform node = nodeTransforms[index];
            Image baseRing = GetOrAddComponent<Image>(node.gameObject);
            baseRing.raycastTarget = true;
            Button button = GetOrAddComponent<Button>(node.gameObject);
            button.targetGraphic = baseRing;
            button.transition = Selectable.Transition.None;

            Image glow = node.Find("Star") != null
                ? node.Find("Star").GetComponent<Image>()
                : null;
            if (glow == null)
            {
                RectTransform glowRect = EnsureRectChild(node, "Star");
                glow = GetOrAddComponent<Image>(glowRect.gameObject);
            }
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            RectTransform iconRect = EnsureRectChild(node, "IngredientIcon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 5f);
            iconRect.sizeDelta = new Vector2(104f, 116f);
            Image icon = GetOrAddComponent<Image>(iconRect.gameObject);
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            TMP_Text categoryLabel = EnsureText(
                node,
                "CategoryLabel",
                font,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                PaleBlue);
            SetRect(categoryLabel.rectTransform, new Vector2(0f, -79f), new Vector2(150f, 28f));

            TMP_Text selectionLabel = EnsureText(
                node,
                "SelectionLabel",
                font,
                15f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                StarBlueBright);
            SetRect(selectionLabel.rectTransform, new Vector2(0f, -105f), new Vector2(180f, 26f));

            CocktailStepNodeView view = GetOrAddComponent<CocktailStepNodeView>(node.gameObject);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_category").enumValueIndex = index;
            serialized.FindProperty("_button").objectReferenceValue = button;
            serialized.FindProperty("_baseRing").objectReferenceValue = baseRing;
            serialized.FindProperty("_glow").objectReferenceValue = glow;
            serialized.FindProperty("_ingredientIcon").objectReferenceValue = icon;
            serialized.FindProperty("_categoryLabel").objectReferenceValue = categoryLabel;
            serialized.FindProperty("_selectionLabel").objectReferenceValue = selectionLabel;
            SetObjectList(
                serialized.FindProperty("_glowFrames"),
                glowFrames.Cast<UnityEngine.Object>());
            serialized.FindProperty("_frameInterval").floatValue = 0.11f;
            serialized.FindProperty("_selectedRingColor").colorValue =
                Color.white;
            serialized.FindProperty("_availableRingColor").colorValue =
                Color.white;
            serialized.FindProperty("_lockedRingColor").colorValue =
                new Color32(255, 255, 255, 210);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            result.Add(view);
        }

        return result;
    }

    private static InventoryViewReferences ConfigureInventoryPanel(
        RectTransform inventory,
        TMP_FontAsset font,
        TMP_FontAsset boldFont)
    {
        RectTransform contentLayer = EnsureRectChild(inventory, "CocktailContent");
        SetStretch(contentLayer, 35f, 35f, 28f, 24f);
        contentLayer.SetAsLastSibling();

        TMP_Text stepTitle = EnsureText(contentLayer, "StepTitle", boldFont, 28f,
            FontStyles.Bold, TextAlignmentOptions.Left, PaleBlue);
        stepTitle.enableWordWrapping = false;
        SetRect(stepTitle.rectTransform, new Vector2(-100f, 190f), new Vector2(630f, 36f));

        TMP_Text stepBadge = EnsureStepBadge(contentLayer, font);

        TMP_Text instruction = EnsureText(contentLayer, "Instruction", font, 16f,
            FontStyles.Normal, TextAlignmentOptions.Left, new Color32(144, 168, 200, 255));
        instruction.enableWordWrapping = false;
        instruction.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(instruction.rectTransform, new Vector2(-80f, 154f), new Vector2(670f, 22f));

        RectTransform headerDivider = EnsureRectChild(contentLayer, "HeaderDivider");
        SetRect(headerDivider, new Vector2(0f, 134f), new Vector2(830f, 2f));
        Image headerDividerImage = GetOrAddComponent<Image>(headerDivider.gameObject);
        headerDividerImage.color = Bronze;
        headerDividerImage.raycastTarget = false;

        TMP_Text summary = EnsureText(contentLayer, "SelectionSummary", boldFont, 16f,
            FontStyles.Bold, TextAlignmentOptions.Center, new Color32(144, 168, 200, 255));
        summary.richText = false;
        summary.enableWordWrapping = false;
        summary.overflowMode = TextOverflowModes.Ellipsis;
        summary.rectTransform.anchorMin = summary.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        summary.rectTransform.pivot = new Vector2(0.5f, 0f);
        summary.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        summary.rectTransform.sizeDelta = new Vector2(128f, 24f);

        RectTransform footerDivider = EnsureRectChild(contentLayer, "FooterDivider");
        SetRect(footerDivider, new Vector2(0f, -154f), new Vector2(830f, 2f));
        Image footerDividerImage = GetOrAddComponent<Image>(footerDivider.gameObject);
        footerDividerImage.color = BronzeShadow;
        footerDividerImage.raycastTarget = false;

        RectTransform optionContent = EnsureRectChild(contentLayer, "OptionContent");
        SetRect(optionContent, new Vector2(0f, 10f), new Vector2(830f, 232f));
        ClearChildren(optionContent);
        GridLayoutGroup oldGrid = optionContent.GetComponent<GridLayoutGroup>();
        if (oldGrid != null)
        {
            UnityEngine.Object.DestroyImmediate(oldGrid);
        }

        CocktailOptionLayout optionLayout =
            GetOrAddComponent<CocktailOptionLayout>(optionContent.gameObject);
        var layoutSerialized = new SerializedObject(optionLayout);
        layoutSerialized.FindProperty("_cellSize").vector2Value = new Vector2(184f, 110f);
        layoutSerialized.FindProperty("_horizontalSpacing").floatValue = 16f;
        layoutSerialized.FindProperty("_verticalSpacing").floatValue = 12f;
        layoutSerialized.ApplyModifiedPropertiesWithoutUndo();

        var optionButtons = new List<CocktailIngredientButtonView>();
        for (int index = 0; index < 8; index++)
        {
            optionButtons.Add(EnsureIngredientButton(optionContent, index, font));
        }
        optionLayout.RefreshLayout();

        RectTransform reviewContent = EnsureRectChild(contentLayer, "ReviewContent");
        SetRect(reviewContent, new Vector2(0f, 10f), new Vector2(830f, 232f));
        ClearChildren(reviewContent);
        ReviewReferences review = ConfigureReviewOrResult(
            reviewContent,
            font,
            boldFont,
            false);

        RectTransform resultContent = EnsureRectChild(contentLayer, "ResultContent");
        SetRect(resultContent, new Vector2(0f, 10f), new Vector2(830f, 232f));
        ClearChildren(resultContent);
        ReviewReferences result = ConfigureReviewOrResult(
            resultContent,
            font,
            boldFont,
            true);

        Button back = EnsureFooterButton(contentLayer, "BackButton", "<  이전", -351f, 128f, font);
        Button skip = EnsureFooterButton(contentLayer, "SkipButton", "건너뛰기  >", 319f, 192f, font);
        Button reset = EnsureFooterButton(contentLayer, "ResetButton", "처음부터", 0f, 140f, font);
        PositionHeaderButton(reset);
        Button confirm = EnsureFooterButton(contentLayer, "ConfirmButton", "제조하기", 315f, 200f, boldFont);
        Button serve = EnsureFooterButton(contentLayer, "ServeButton", "손님에게 건네기", 315f, 200f, boldFont);

        return new InventoryViewReferences
        {
            StepTitle = stepTitle,
            StepBadge = stepBadge,
            Instruction = instruction,
            Summary = summary,
            OptionContent = optionContent.gameObject,
            OptionLayout = optionLayout,
            OptionButtons = optionButtons,
            ReviewContent = reviewContent.gameObject,
            Review = review,
            ResultContent = resultContent.gameObject,
            Result = result,
            BackButton = back,
            SkipButton = skip,
            ResetButton = reset,
            ConfirmButton = confirm,
            ServeButton = serve
        };
    }

    private static TMP_Text EnsureStepBadge(RectTransform parent, TMP_FontAsset font)
    {
        RectTransform badgeRoot = EnsureRectChild(parent, "StepBadgeFrame");
        SetRect(badgeRoot, new Vector2(250f, 190f), new Vector2(72f, 28f));
        Image outer = GetOrAddComponent<Image>(badgeRoot.gameObject);
        outer.color = BronzeShadow;
        outer.raycastTarget = false;

        RectTransform frameRect = EnsureRectChild(badgeRoot, "BronzeFrame");
        SetStretch(frameRect, 2f, 2f, 2f, 2f);
        Image frame = GetOrAddComponent<Image>(frameRect.gameObject);
        frame.color = Bronze;
        frame.raycastTarget = false;

        RectTransform fillRect = EnsureRectChild(frameRect, "Fill");
        SetStretch(fillRect, 2f, 2f, 2f, 2f);
        Image fill = GetOrAddComponent<Image>(fillRect.gameObject);
        fill.color = NavyPanel;
        fill.raycastTarget = false;

        TMP_Text label = EnsureText(badgeRoot, "StepBadge", font, 14f,
            FontStyles.Bold, TextAlignmentOptions.Center, BronzeHighlight);
        SetStretch(label.rectTransform, 4f, 4f, 2f, 2f);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.transform.SetAsLastSibling();
        return label;
    }

    private static CocktailIngredientButtonView EnsureIngredientButton(
        RectTransform parent,
        int index,
        TMP_FontAsset font)
    {
        RectTransform rect = EnsureRectChild(parent, "IngredientSlot" + (index + 1).ToString("00"));
        Image outerFrame = GetOrAddComponent<Image>(rect.gameObject);
        outerFrame.color = BronzeShadow;
        outerFrame.raycastTarget = true;

        RectTransform frameRect = EnsureRectChild(rect, "BronzeFrame");
        SetStretch(frameRect, 2f, 2f, 2f, 2f);
        Image frame = GetOrAddComponent<Image>(frameRect.gameObject);
        frame.color = Bronze;
        frame.raycastTarget = false;

        RectTransform fillRect = EnsureRectChild(rect, "Fill");
        SetStretch(fillRect, 5f, 5f, 5f, 5f);
        Image background = GetOrAddComponent<Image>(fillRect.gameObject);
        background.color = NavyPanel;
        background.raycastTarget = false;
        frameRect.SetAsFirstSibling();
        fillRect.SetSiblingIndex(1);

        Button button = GetOrAddComponent<Button>(rect.gameObject);
        button.targetGraphic = outerFrame;
        button.transition = Selectable.Transition.None;

        RectTransform iconRect = EnsureRectChild(rect, "Icon");
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -5f);
        iconRect.sizeDelta = new Vector2(152f, 76f);
        Image icon = GetOrAddComponent<Image>(iconRect.gameObject);
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        TMP_Text label = EnsureText(rect, "Label", font, 16f, FontStyles.Bold,
            TextAlignmentOptions.Center, PaleBlue);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        label.rectTransform.sizeDelta = new Vector2(-16f, 26f);

        RectTransform markerRect = EnsureRectChild(rect, "SelectedMarker");
        SetStretch(markerRect, 8f, 8f, 8f, 8f);
        ConfigurePixelCorners(markerRect, BronzeHighlight);

        CocktailIngredientButtonView view =
            GetOrAddComponent<CocktailIngredientButtonView>(rect.gameObject);
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_button").objectReferenceValue = button;
        serialized.FindProperty("_frame").objectReferenceValue = frame;
        serialized.FindProperty("_background").objectReferenceValue = background;
        serialized.FindProperty("_icon").objectReferenceValue = icon;
        serialized.FindProperty("_label").objectReferenceValue = label;
        serialized.FindProperty("_selectedMarker").objectReferenceValue = markerRect.gameObject;
        serialized.FindProperty("_normalColor").colorValue = NavyPanel;
        serialized.FindProperty("_hoverColor").colorValue = NavyHover;
        serialized.FindProperty("_selectedColor").colorValue = new Color32(4, 18, 45, 255);
        serialized.FindProperty("_normalFrameColor").colorValue = Bronze;
        serialized.FindProperty("_hoverFrameColor").colorValue = BronzeBright;
        serialized.FindProperty("_selectedFrameColor").colorValue = BronzeHighlight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
        return view;
    }

    private static void ConfigurePixelCorners(RectTransform root, Color color)
    {
        ClearChildren(root);
        CreatePixelCorner(root, "TopLeft", new Vector2(0f, 1f), color);
        CreatePixelCorner(root, "TopRight", new Vector2(1f, 1f), color);
        CreatePixelCorner(root, "BottomLeft", new Vector2(0f, 0f), color);
        CreatePixelCorner(root, "BottomRight", new Vector2(1f, 0f), color);
    }

    private static void CreatePixelCorner(
        RectTransform parent,
        string name,
        Vector2 anchor,
        Color color)
    {
        RectTransform horizontal = CreateUiObject(name + "H", parent).GetComponent<RectTransform>();
        horizontal.anchorMin = horizontal.anchorMax = anchor;
        horizontal.pivot = anchor;
        horizontal.anchoredPosition = Vector2.zero;
        horizontal.sizeDelta = new Vector2(12f, 4f);
        Image horizontalImage = horizontal.gameObject.AddComponent<Image>();
        horizontalImage.color = color;
        horizontalImage.raycastTarget = false;

        RectTransform vertical = CreateUiObject(name + "V", parent).GetComponent<RectTransform>();
        vertical.anchorMin = vertical.anchorMax = anchor;
        vertical.pivot = anchor;
        vertical.anchoredPosition = Vector2.zero;
        vertical.sizeDelta = new Vector2(4f, 12f);
        Image verticalImage = vertical.gameObject.AddComponent<Image>();
        verticalImage.color = color;
        verticalImage.raycastTarget = false;
    }

    private static ReviewReferences ConfigureReviewOrResult(
        RectTransform parent,
        TMP_FontAsset font,
        TMP_FontAsset boldFont,
        bool includeBadge)
    {
        RectTransform card = EnsureRectChild(parent, "Card");
        SetStretch(card, 10f, 10f, 8f, 8f);
        RectTransform previewFrame = EnsureRectChild(card, "PreviewFrame");
        SetRect(previewFrame, new Vector2(-301f, 0f), new Vector2(176f, 192f));
        Image previewOuter = GetOrAddComponent<Image>(previewFrame.gameObject);
        previewOuter.color = BronzeShadow;
        previewOuter.raycastTarget = false;
        RectTransform previewBorderRect = EnsureRectChild(previewFrame, "BronzeFrame");
        SetStretch(previewBorderRect, 2f, 2f, 2f, 2f);
        Image previewBorder = GetOrAddComponent<Image>(previewBorderRect.gameObject);
        previewBorder.color = Bronze;
        previewBorder.raycastTarget = false;
        RectTransform previewFillRect = EnsureRectChild(previewFrame, "Fill");
        SetStretch(previewFillRect, 6f, 6f, 6f, 6f);
        Image previewFill = GetOrAddComponent<Image>(previewFillRect.gameObject);
        previewFill.color = NavyPanel;
        previewFill.raycastTarget = false;

        RectTransform previewRect = EnsureRectChild(previewFrame, "CocktailImage");
        SetStretch(previewRect, 12f, 12f, 12f, 12f);
        Image preview = GetOrAddComponent<Image>(previewRect.gameObject);
        preview.preserveAspect = true;
        preview.raycastTarget = false;

        RectTransform divider = EnsureRectChild(card, "PixelDivider");
        SetRect(divider, new Vector2(-196f, 0f), new Vector2(4f, 192f));
        Image dividerImage = GetOrAddComponent<Image>(divider.gameObject);
        dividerImage.color = Bronze;
        dividerImage.raycastTarget = false;

        TMP_Text name = EnsureText(card, "CocktailName", boldFont, 24f,
            FontStyles.Bold, TextAlignmentOptions.Left, PaleBlue);
        name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        name.rectTransform.pivot = new Vector2(0f, 0.5f);
        name.rectTransform.anchoredPosition = new Vector2(-176f, 68f);
        name.rectTransform.sizeDelta = new Vector2(560f, 36f);

        RectTransform titleRule = EnsureRectChild(card, "TitleRule");
        SetRect(titleRule, new Vector2(104f, 44f), new Vector2(560f, 2f));
        Image titleRuleImage = GetOrAddComponent<Image>(titleRule.gameObject);
        titleRuleImage.color = Bronze;
        titleRuleImage.raycastTarget = false;

        TMP_Text badge = null;
        if (includeBadge)
        {
            badge = EnsureText(card, "ResultBadge", font, 14f, FontStyles.Bold,
                TextAlignmentOptions.Left, BronzeHighlight);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            badge.rectTransform.pivot = new Vector2(0f, 0.5f);
            badge.rectTransform.anchoredPosition = new Vector2(-174f, 26f);
            badge.rectTransform.sizeDelta = new Vector2(548f, 24f);
        }

        TMP_Text description = EnsureText(card, "Description", font, 18f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(176, 192, 216, 255));
        description.enableWordWrapping = true;
        description.rectTransform.anchorMin = description.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        description.rectTransform.pivot = new Vector2(0f, 0.5f);
        description.rectTransform.anchoredPosition = new Vector2(
            -176f,
            includeBadge ? -42f : -24f);
        description.rectTransform.sizeDelta = new Vector2(
            560f,
            includeBadge ? 96f : 132f);

        return new ReviewReferences
        {
            Image = preview,
            Name = name,
            Badge = badge,
            Description = description
        };
    }

    private static Button EnsureFooterButton(
        RectTransform parent,
        string name,
        string labelText,
        float x,
        float width,
        TMP_FontAsset font)
    {
        RectTransform rect = EnsureRectChild(parent, name);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 4f);
        rect.sizeDelta = new Vector2(Mathf.Round(width / 4f) * 4f, 44f);

        Outline oldOutline = rect.GetComponent<Outline>();
        if (oldOutline != null)
        {
            UnityEngine.Object.DestroyImmediate(oldOutline);
        }
        Image image = GetOrAddComponent<Image>(rect.gameObject);
        image.color = BronzeShadow;
        image.raycastTarget = true;
        RectTransform frameRect = EnsureRectChild(rect, "BronzeFrame");
        SetStretch(frameRect, 2f, 2f, 2f, 2f);
        Image frame = GetOrAddComponent<Image>(frameRect.gameObject);
        bool isPrimary = name == "ConfirmButton" || name == "ServeButton";
        frame.color = isPrimary ? BronzeBright : Bronze;
        frame.raycastTarget = false;
        RectTransform fillRect = EnsureRectChild(frameRect, "Fill");
        SetStretch(fillRect, 3f, 3f, 3f, 3f);
        Image fill = GetOrAddComponent<Image>(fillRect.gameObject);
        fill.color = NavyPanel;
        fill.raycastTarget = false;
        frameRect.SetAsFirstSibling();
        Button button = GetOrAddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        TMP_Text label = EnsureText(rect, "Label", font, 16f, FontStyles.Bold,
            TextAlignmentOptions.Center, PaleBlue);
        SetStretch(label.rectTransform, 2f, 2f, 1f, 1f);
        label.text = labelText;
        label.raycastTarget = false;
        label.transform.SetAsLastSibling();
        return button;
    }

    private static void PositionHeaderButton(Button button)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-2f, -2f);
        rect.sizeDelta = new Vector2(140f, 36f);

        TMP_Text label = rect.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.fontSize = 14f;
            label.color = new Color32(176, 192, 216, 255);
        }
    }

    private static void ConfigureController(
        CocktailCastingController controller,
        GameObject uiRoot,
        GameObject overlay,
        CanvasGroup backgroundCanvasGroup,
        CanvasGroup canvasGroup,
        RectTransform revealMask,
        RectTransform panelShell,
        IReadOnlyList<CocktailStepNodeView> nodes,
        InventoryViewReferences view)
    {
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("_uiRoot").objectReferenceValue = uiRoot;
        serialized.FindProperty("_backgroundOverlay").objectReferenceValue = overlay;
        serialized.FindProperty("_backgroundCanvasGroup").objectReferenceValue = backgroundCanvasGroup;
        serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        serialized.FindProperty("_revealMask").objectReferenceValue = revealMask;
        serialized.FindProperty("_panelShell").objectReferenceValue = panelShell;
        serialized.FindProperty("_stepTitle").objectReferenceValue = view.StepTitle;
        serialized.FindProperty("_stepBadge").objectReferenceValue = view.StepBadge;
        serialized.FindProperty("_instruction").objectReferenceValue = view.Instruction;
        serialized.FindProperty("_selectionSummary").objectReferenceValue = view.Summary;
        serialized.FindProperty("_optionContent").objectReferenceValue = view.OptionContent;
        serialized.FindProperty("_optionLayout").objectReferenceValue = view.OptionLayout;
        SetObjectList(
            serialized.FindProperty("_optionButtons"),
            view.OptionButtons.Cast<UnityEngine.Object>());
        SetObjectList(serialized.FindProperty("_stepNodes"), nodes.Cast<UnityEngine.Object>());
        serialized.FindProperty("_reviewContent").objectReferenceValue = view.ReviewContent;
        serialized.FindProperty("_reviewImage").objectReferenceValue = view.Review.Image;
        serialized.FindProperty("_reviewName").objectReferenceValue = view.Review.Name;
        serialized.FindProperty("_reviewDescription").objectReferenceValue = view.Review.Description;
        serialized.FindProperty("_resultContent").objectReferenceValue = view.ResultContent;
        serialized.FindProperty("_resultImage").objectReferenceValue = view.Result.Image;
        serialized.FindProperty("_resultName").objectReferenceValue = view.Result.Name;
        serialized.FindProperty("_resultBadge").objectReferenceValue = view.Result.Badge;
        serialized.FindProperty("_resultDescription").objectReferenceValue = view.Result.Description;
        serialized.FindProperty("_backButton").objectReferenceValue = view.BackButton;
        serialized.FindProperty("_skipButton").objectReferenceValue = view.SkipButton;
        serialized.FindProperty("_resetButton").objectReferenceValue = view.ResetButton;
        serialized.FindProperty("_confirmButton").objectReferenceValue = view.ConfirmButton;
        serialized.FindProperty("_serveButton").objectReferenceValue = view.ServeButton;
        serialized.FindProperty("_startDelay").floatValue = 0.32f;
        serialized.FindProperty("_panelLeadDelay").floatValue = 0.08f;
        serialized.FindProperty("_backdropFadeInDuration").floatValue = 0.24f;
        serialized.FindProperty("_backdropTargetAlpha").floatValue = 0.92f;
        serialized.FindProperty("_openTravelDuration").floatValue = 0.82f;
        serialized.FindProperty("_openSettleDuration").floatValue = 0.18f;
        serialized.FindProperty("_openOvershootPixels").floatValue = 24f;
        serialized.FindProperty("_closeAnticipationDuration").floatValue = 0.15f;
        serialized.FindProperty("_closeAnticipationPixels").floatValue = 18f;
        serialized.FindProperty("_closeTravelDuration").floatValue = 0.72f;
        serialized.FindProperty("_backdropFadeOutDuration").floatValue = 0.30f;
        serialized.FindProperty("_openingCastingDialogueId").intValue = 1001;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void ConnectDialogueStart(
        GameObject gameScope,
        CocktailCastingController controller)
    {
        DialogueSceneEventBindings bindings =
            gameScope.GetComponent<DialogueSceneEventBindings>()
            ?? throw new InvalidOperationException(
                "GameScope requires DialogueSceneEventBindings before Cocktail setup.");
        FieldInfo field = typeof(DialogueSceneEventBindings).GetField(
            "_onGameStartRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent startEvent = field?.GetValue(bindings) as UnityEvent;
        if (startEvent == null)
        {
            throw new InvalidOperationException(
                "DialogueSceneEventBindings._onGameStartRequested could not be resolved.");
        }

        bool alreadyConnected = false;
        for (int index = 0; index < startEvent.GetPersistentEventCount(); index++)
        {
            if (startEvent.GetPersistentTarget(index) == controller &&
                startEvent.GetPersistentMethodName(index) == nameof(CocktailCastingController.RequestStartCasting))
            {
                alreadyConnected = true;
                break;
            }
        }

        if (!alreadyConnected)
        {
            UnityEventTools.AddPersistentListener(startEvent, controller.RequestStartCasting);
        }

        EditorUtility.SetDirty(bindings);
    }

    private static GameObject FindCocktailOverlay(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if ((root.name == "Background" || root.name == "CocktailBackground") &&
                root.GetComponent<Canvas>() != null)
            {
                return root;
            }
        }

        throw new InvalidOperationException(
            "Play scene requires the separate UI Background Canvas used for cocktail dimming.");
    }

    private static void CreateConnector(
        RectTransform parent,
        Vector2 from,
        Vector2 to,
        int index)
    {
        Vector2 difference = to - from;
        Vector2 direction = difference.normalized;
        Vector2 start = from + direction * 58f;
        Vector2 end = to - direction * 58f;
        float visibleLength = Vector2.Distance(start, end);
        int pixelCount = Mathf.Max(2, Mathf.RoundToInt(visibleLength / 12f));

        RectTransform connector = CreateUiObject(
            "ConstellationLine" + (index + 1).ToString("00"), parent).GetComponent<RectTransform>();
        SetStretch(connector);
        for (int pixel = 0; pixel <= pixelCount; pixel++)
        {
            float t = pixel / (float)pixelCount;
            Vector2 position = Vector2.Lerp(start, end, t);
            position.x = Mathf.Round(position.x);
            position.y = Mathf.Round(position.y);
            RectTransform dot = CreateUiObject("Pixel" + pixel.ToString("00"), connector)
                .GetComponent<RectTransform>();
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = position;
            float size = pixel == 0 || pixel == pixelCount || pixel % 5 == 0 ? 8f : 6f;
            dot.sizeDelta = new Vector2(size, size);
            Image image = dot.gameObject.AddComponent<Image>();
            image.color = pixel % 4 == 0 ? StarBlueBright : StarBlue;
            image.raycastTarget = false;
        }
    }

    private static void ConfigurePanelTextureImporters()
    {
        string[] paths =
        {
            "Assets/02. Scenes/Play/Arts/Panel/Bar_Panel_Back.png",
            "Assets/02. Scenes/Play/Arts/Panel/Bar_Panel_Drawer.png",
            "Assets/02. Scenes/Play/Arts/Panel/Bar_Panel_OutLine.png",
            "Assets/02. Scenes/Play/Arts/Panel/Bar_Casting_UI_Back.png",
            "Assets/02. Scenes/Play/Arts/Panel/Bar_Casting_UI_OutLine.png",
            "Assets/02. Scenes/Play/Arts/Panel/Bar_NotGlowing.png",
            GlowPath,
            "Assets/01. Total/Arts/믹서/Sparkling_Water.png"
        };

        foreach (string path in paths)
        {
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
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rect = EnsureRectChild(parent, name);
        TMP_Text text = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static RectTransform EnsureRectChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;
            if (existingRect == null)
            {
                throw new InvalidOperationException($"UI child '{name}' is not a RectTransform.");
            }

            return existingRect;
        }

        return CreateUiObject(name, parent).GetComponent<RectTransform>();
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }
    }

    private static void DisableDecorativeRaycasts(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            image.raycastTarget = false;
        }
    }

    private static RectTransform FindChildBySpriteName(Transform root, string namePart)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite != null && image.sprite.name.Contains(namePart))
            {
                return image.rectTransform;
            }
        }

        return null;
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
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(
        RectTransform rect,
        float left = 0f,
        float right = 0f,
        float top = 0f,
        float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static void SetStringList(
        SerializedProperty property,
        IReadOnlyList<string> values)
    {
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).stringValue = values[index];
        }
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

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private sealed class InventoryViewReferences
    {
        public TMP_Text StepTitle;
        public TMP_Text StepBadge;
        public TMP_Text Instruction;
        public TMP_Text Summary;
        public GameObject OptionContent;
        public CocktailOptionLayout OptionLayout;
        public List<CocktailIngredientButtonView> OptionButtons;
        public GameObject ReviewContent;
        public ReviewReferences Review;
        public GameObject ResultContent;
        public ReviewReferences Result;
        public Button BackButton;
        public Button SkipButton;
        public Button ResetButton;
        public Button ConfirmButton;
        public Button ServeButton;
    }

    private sealed class ReviewReferences
    {
        public Image Image;
        public TMP_Text Name;
        public TMP_Text Badge;
        public TMP_Text Description;
    }
}
#endif
