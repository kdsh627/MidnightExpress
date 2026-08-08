using System;
using System.Collections.Generic;
using System.Linq;
using ExcelData;
using Febucci.UI;
using Febucci.UI.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class DialogueSceneSetupTool
{
    private const string PlayScenePath = "Assets/02. Scenes/Play/Play.unity";
    private const string BootstrapScenePath = "Assets/02. Scenes/Bootstrap/BootstrapScene.unity";
    private const string DialogueDataPath = "Assets/01. Total/Data/DialogueDataSO.asset";
    private const string CharacterPrefabPath = "Assets/01. Total/Prefabs/Dialogue/DialogueCharacter.prefab";
    private const string HoverOutlineShaderPath =
        "Assets/01. Total/Scripts/Dialogue/DialogueSpriteHoverOutline.shader";
    private const string BubbleBodySpritePath = "Assets/01. Total/Arts/대화/Bubble1.png";
    private const string BubbleBodySpriteName = "Bubble1_0";

    private static readonly ActorSetup[] ActorSetups =
    {
        new ActorSetup("Char", "리오", 1001),
        // Char (1) is the authored off-screen reserve actor. Keep the active right-side
        // actor as the protagonist so imported/dummy dialogue never targets an inactive TMP canvas.
        new ActorSetup("Char (1)", "캐릭터3", 0),
        new ActorSetup("Char (2)", "주인공", 1002)
    };

    [MenuItem("Tools/Dialogue/Setup Play Scene")]
    public static void SetupPlayScene()
    {
        try
        {
            Scene playScene = OpenPlaySceneSafely();
            EnsureDialogueDataAsset();
            List<DialogueActor> actors = ConfigureActors(playScene);
            ConvertActorsToSharedPrefab(actors);
            ConfigurePointerInput(playScene);
            ConfigureGameScope(playScene, actors);
            ValidatePlayScene(playScene, actors);

            EditorSceneManager.MarkSceneDirty(playScene);
            EditorSceneManager.SaveScene(playScene);
            AssignDialogueDataToBootstrap();
            EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Dialogue setup completed: Play actors, dynamic bubbles, TextAnimator, shared prefab, "
                + "GameScope services, and Bootstrap DialogueDataSO are connected.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static Scene OpenPlaySceneSafely()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == PlayScenePath)
        {
            return activeScene;
        }

        if (activeScene.IsValid() && activeScene.isDirty)
        {
            throw new InvalidOperationException(
                $"The active scene '{activeScene.path}' has unsaved changes. Save it before running dialogue setup.");
        }

        return EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);
    }

    private static DialogueDataSO EnsureDialogueDataAsset()
    {
        DialogueDataSO asset = AssetDatabase.LoadAssetAtPath<DialogueDataSO>(DialogueDataPath);
        if (asset != null)
        {
            asset.ValidateImportedData();
            return asset;
        }

        EnsureAssetFolder("Assets/01. Total/Data");
        asset = ScriptableObject.CreateInstance<DialogueDataSO>();
        asset.PreCastingDialogues = new List<PreCastingDialogueData>
        {
            new PreCastingDialogueData
            {
                ID = 1001,
                Name = "리오",
                NextID = 1002,
                EventType = DialogueEventType.Appeared,
                Turn = 0,
                Script = string.Empty
            },
            new PreCastingDialogueData
            {
                ID = 1002,
                Name = "주인공",
                NextID = -1,
                EventType = DialogueEventType.Script,
                Turn = 1,
                Script = "...어서 오세요."
            }
        };
        asset.CastingDialogues = new List<CastingDialogueData>
        {
            new CastingDialogueData
            {
                ID = 1001,
                Name = "리오",
                Script = "향이 날아가지 않도록 천천히 저어 줘."
            }
        };
        asset.ValidateImportedData();

        AssetDatabase.CreateAsset(asset, DialogueDataPath);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static List<DialogueActor> ConfigureActors(Scene scene)
    {
        var actors = new List<DialogueActor>(ActorSetups.Length);
        foreach (ActorSetup setup in ActorSetups)
        {
            GameObject actorObject = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == setup.GameObjectName);
            if (actorObject == null)
            {
                throw new InvalidOperationException(
                    $"Play scene requires a root GameObject named '{setup.GameObjectName}'.");
            }

            DialogueActor actor = GetOrAddComponent<DialogueActor>(actorObject);
            BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(actorObject);
            Transform body = actorObject.transform.Find("Body");
            SpriteRenderer actorRenderer = body != null
                ? body.GetComponentInChildren<SpriteRenderer>(true)
                : actorObject.GetComponent<SpriteRenderer>();
            if (actorRenderer != null && actorRenderer.sprite != null)
            {
                if (body != null)
                {
                    actorRenderer.sortingOrder = -1;
                }

                Vector3 bodyScale = body != null ? body.localScale : Vector3.one;
                Vector3 bodyPosition = body != null ? body.localPosition : Vector3.zero;
                Vector3 spriteSize = actorRenderer.sprite.bounds.size;
                Vector3 spriteCenter = actorRenderer.sprite.bounds.center;
                collider.size = new Vector2(
                    spriteSize.x * Mathf.Abs(bodyScale.x),
                    spriteSize.y * Mathf.Abs(bodyScale.y));
                collider.offset = new Vector2(
                    bodyPosition.x + spriteCenter.x * bodyScale.x,
                    bodyPosition.y + spriteCenter.y * bodyScale.y);
            }

            DialogueActorAppearance appearance = body != null
                ? ConfigureAppearance(actorObject, body)
                : null;
            DialogueActorHoverOutline hoverOutline = body != null
                ? ConfigureHoverOutline(actorObject, body)
                : null;

            Transform bubbleTransform = actorObject.transform.Find("Bubble");
            if (bubbleTransform == null)
            {
                throw new InvalidOperationException(
                    $"'{actorObject.name}' requires the child hierarchy Bubble/TextArea/Canvas/Text (TMP).");
            }

            DialogueBubbleView bubble = ConfigureBubble(bubbleTransform.gameObject);
            SetActorProperties(
                actor,
                setup.CharacterName,
                setup.PreCastingEventId,
                bubble,
                appearance,
                hoverOutline);
            bubble.gameObject.SetActive(false);
            actors.Add(actor);
        }

        return actors;
    }

    private static DialogueBubbleView ConfigureBubble(GameObject bubbleObject)
    {
        DialogueBubbleView bubble = GetOrAddComponent<DialogueBubbleView>(bubbleObject);
        SpriteRenderer tailRenderer = bubbleObject.GetComponent<SpriteRenderer>();
        Transform textAreaTransform = bubbleObject.transform.Find("TextArea");
        SpriteRenderer textAreaRenderer = textAreaTransform != null
            ? textAreaTransform.GetComponent<SpriteRenderer>()
            : null;
        Canvas canvas = bubbleObject.GetComponentInChildren<Canvas>(true);
        TMP_Text text = bubbleObject.GetComponentInChildren<TMP_Text>(true);

        if (tailRenderer == null || textAreaRenderer == null || canvas == null || text == null)
        {
            throw new InvalidOperationException(
                $"Dialogue bubble '{bubbleObject.name}' is missing a tail, TextArea, Canvas, or TMP text.");
        }

        Sprite bubbleBodySprite = AssetDatabase.LoadAllAssetsAtPath(BubbleBodySpritePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == BubbleBodySpriteName)
            ?? throw new InvalidOperationException(
                $"Cropped bubble sprite '{BubbleBodySpriteName}' was not found at '{BubbleBodySpritePath}'.");

        TextAnimator_TMP textAnimator = GetOrAddComponent<TextAnimator_TMP>(text.gameObject);
        TypewriterByCharacter typewriter = GetOrAddComponent<TypewriterByCharacter>(text.gameObject);

        textAreaTransform.localScale = Vector3.one;
        var textAreaPosition = textAreaTransform.localPosition;
        textAreaPosition.y = 0.25f;
        textAreaTransform.localPosition = textAreaPosition;
        textAreaRenderer.sprite = bubbleBodySprite;
        textAreaRenderer.drawMode = SpriteDrawMode.Sliced;
        textAreaRenderer.size = new Vector2(3.2f, 1.8f);
        textAreaRenderer.sortingOrder = 0;
        tailRenderer.sortingOrder = 1;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2;
        if (canvas.transform is RectTransform canvasRect)
        {
            var canvasPosition = canvasRect.localPosition;
            canvasPosition.x = 0f;
            canvasPosition.y = 0.9f;
            canvasRect.localPosition = canvasPosition;
        }
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.18f;
        text.fontSizeMax = 0.3f;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = string.Empty;
        textAnimator.DefaultAppearancesTags = Array.Empty<string>();
        typewriter.useTypeWriter = true;
        typewriter.startTypewriterMode = TypewriterCore.StartTypewriterMode.OnShowText;
        typewriter.waitForNormalChars = 0.06f;

        var serializedBubble = new SerializedObject(bubble);
        SetObjectReference(serializedBubble, "_tailRenderer", tailRenderer);
        SetObjectReference(serializedBubble, "_textAreaRenderer", textAreaRenderer);
        SetObjectReference(serializedBubble, "_canvas", canvas);
        SetObjectReference(serializedBubble, "_canvasRect", canvas.transform as RectTransform);
        SetObjectReference(serializedBubble, "_text", text);
        SetObjectReference(serializedBubble, "_textAnimator", textAnimator);
        SetObjectReference(serializedBubble, "_typewriter", typewriter);
        serializedBubble.FindProperty("_bodyBottomOffset").floatValue = 0.25f;
        serializedBubble.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(bubble);
        EditorUtility.SetDirty(tailRenderer);
        EditorUtility.SetDirty(textAreaRenderer);
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(text);
        EditorUtility.SetDirty(textAnimator);
        EditorUtility.SetDirty(typewriter);
        return bubble;
    }

    private static void SetActorProperties(
        DialogueActor actor,
        string characterName,
        int preCastingEventId,
        DialogueBubbleView bubble,
        DialogueActorAppearance appearance,
        DialogueActorHoverOutline hoverOutline)
    {
        var serializedActor = new SerializedObject(actor);
        serializedActor.FindProperty("_characterName").stringValue = characterName;
        serializedActor.FindProperty("_preCastingEventId").intValue = preCastingEventId;
        serializedActor.FindProperty("_bubble").objectReferenceValue = bubble;
        serializedActor.FindProperty("_appearance").objectReferenceValue = appearance;
        serializedActor.FindProperty("_hoverOutline").objectReferenceValue = hoverOutline;
        serializedActor.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actor);
    }

    private static DialogueActorAppearance ConfigureAppearance(GameObject actorObject, Transform body)
    {
        DialogueActorAppearance appearance = GetOrAddComponent<DialogueActorAppearance>(actorObject);
        SpriteRenderer[] renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
        var serializedAppearance = new SerializedObject(appearance);
        serializedAppearance.FindProperty("_body").objectReferenceValue = body;
        serializedAppearance.FindProperty("_startHidden").boolValue = true;
        SerializedProperty rendererList = serializedAppearance.FindProperty("_renderers");
        rendererList.arraySize = renderers.Length;
        for (int index = 0; index < renderers.Length; index++)
        {
            rendererList.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
        }

        serializedAppearance.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(appearance);
        return appearance;
    }

    private static DialogueActorHoverOutline ConfigureHoverOutline(
        GameObject actorObject,
        Transform body)
    {
        Shader outlineShader = AssetDatabase.LoadAssetAtPath<Shader>(HoverOutlineShaderPath)
            ?? throw new InvalidOperationException(
                $"Dialogue hover outline shader was not found at '{HoverOutlineShaderPath}'.");
        DialogueActorHoverOutline hoverOutline =
            GetOrAddComponent<DialogueActorHoverOutline>(actorObject);
        SpriteRenderer[] renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
        var serializedOutline = new SerializedObject(hoverOutline);
        serializedOutline.FindProperty("_body").objectReferenceValue = body;
        serializedOutline.FindProperty("_outlineShader").objectReferenceValue = outlineShader;
        SerializedProperty rendererList = serializedOutline.FindProperty("_renderers");
        rendererList.arraySize = renderers.Length;
        for (int index = 0; index < renderers.Length; index++)
        {
            rendererList.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
        }

        serializedOutline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hoverOutline);
        return hoverOutline;
    }

    private static void ConvertActorsToSharedPrefab(IReadOnlyList<DialogueActor> actors)
    {
        EnsureAssetFolder("Assets/01. Total/Prefabs/Dialogue");
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (prefabAsset == null)
        {
            prefabAsset = PrefabUtility.SaveAsPrefabAsset(actors[0].gameObject, CharacterPrefabPath);
        }

        if (prefabAsset == null)
        {
            throw new InvalidOperationException($"Could not create dialogue character prefab at '{CharacterPrefabPath}'.");
        }

        prefabAsset = UpdateSharedPrefabDefaults();

        var settings = new ConvertToPrefabInstanceSettings
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            recordPropertyOverridesOfMatches = true,
            changeRootNameToAssetName = false,
            logInfo = false
        };

        foreach (DialogueActor actor in actors)
        {
            string currentPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject);
            if (string.Equals(currentPrefabPath, CharacterPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(actor.gameObject))
            {
                throw new InvalidOperationException(
                    $"'{actor.name}' is already connected to another prefab. Unpack it before running dialogue setup.");
            }

            PrefabUtility.ConvertToPrefabInstance(
                actor.gameObject,
                prefabAsset,
                settings,
                InteractionMode.AutomatedAction);
        }
    }

    private static GameObject UpdateSharedPrefabDefaults()
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
        try
        {
            Transform bubbleTransform = prefabContents.transform.Find("Bubble");
            if (bubbleTransform == null)
            {
                throw new InvalidOperationException("DialogueCharacter prefab requires a Bubble child.");
            }

            ConfigureBubble(bubbleTransform.gameObject);
            bubbleTransform.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(prefabContents, CharacterPrefabPath)
                ?? throw new InvalidOperationException("DialogueCharacter prefab defaults could not be saved.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void ConfigureGameScope(Scene scene, IReadOnlyList<DialogueActor> actors)
    {
        GameScope gameScope = FindComponentInScene<GameScope>(scene)
            ?? throw new InvalidOperationException("Play scene requires a GameScope component.");

        DialogueActorRegistry registry = GetOrAddComponent<DialogueActorRegistry>(gameScope.gameObject);
        GetOrAddComponent<DialogueSceneController>(gameScope.gameObject);
        GetOrAddComponent<DialogueSceneEventBindings>(gameScope.gameObject);

        var serializedRegistry = new SerializedObject(registry);
        SerializedProperty actorList = serializedRegistry.FindProperty("_actors");
        actorList.arraySize = actors.Count;
        for (int index = 0; index < actors.Count; index++)
        {
            actorList.GetArrayElementAtIndex(index).objectReferenceValue = actors[index];
        }

        serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
    }

    private static void ConfigurePointerInput(Scene scene)
    {
        Camera mainCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        if (mainCamera == null)
        {
            throw new InvalidOperationException("Play scene requires a camera tagged MainCamera.");
        }

        GetOrAddComponent<Physics2DRaycaster>(mainCamera.gameObject);

        EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            Undo.DestroyObjectImmediate(legacyModule);
        }

        GetOrAddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        EditorUtility.SetDirty(mainCamera.gameObject);
        EditorUtility.SetDirty(eventSystem.gameObject);
    }

    private static void ValidatePlayScene(Scene scene, IReadOnlyList<DialogueActor> actors)
    {
        GameScope gameScope = FindComponentInScene<GameScope>(scene);
        if (gameScope == null
            || gameScope.GetComponent<DialogueActorRegistry>() == null
            || gameScope.GetComponent<DialogueSceneController>() == null
            || gameScope.GetComponent<DialogueSceneEventBindings>() == null)
        {
            throw new InvalidOperationException("GameScope dialogue components were not configured correctly.");
        }

        foreach (DialogueActor actor in actors)
        {
            actor.ValidateConfiguration();
            actor.Bubble.ValidateReferences();
            if (actor.GetComponent<Collider2D>() == null)
            {
                throw new InvalidOperationException($"Dialogue actor '{actor.name}' requires a Collider2D.");
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(actor.gameObject))
            {
                throw new InvalidOperationException($"Dialogue actor '{actor.name}' is not connected to a prefab.");
            }
        }
    }

    private static void AssignDialogueDataToBootstrap()
    {
        Scene bootstrapScene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        DialogueDataSO dialogueData = AssetDatabase.LoadAssetAtPath<DialogueDataSO>(DialogueDataPath)
            ?? throw new InvalidOperationException(
                $"Dialogue data asset could not be reloaded after opening BootstrapScene: '{DialogueDataPath}'.");
        BaseScope baseScope = FindComponentInScene<BaseScope>(bootstrapScene)
            ?? throw new InvalidOperationException("BootstrapScene requires a BaseScope component.");

        var serializedScope = new SerializedObject(baseScope);
        SerializedProperty dialogueProperty = serializedScope.FindProperty("_dialogueData")
            ?? throw new InvalidOperationException("BaseScope no longer contains the serialized '_dialogueData' field.");
        dialogueProperty.objectReferenceValue = dialogueData;
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        if (dialogueProperty.objectReferenceValue != dialogueData)
        {
            throw new InvalidOperationException("Bootstrap BaseScope rejected the DialogueDataSO reference.");
        }

        EditorUtility.SetDirty(baseScope);
        EditorSceneManager.MarkSceneDirty(bootstrapScene);
        if (!EditorSceneManager.SaveScene(bootstrapScene))
        {
            throw new InvalidOperationException("BootstrapScene could not be saved after assigning DialogueDataSO.");
        }
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
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on '{serializedObject.targetObject.name}'.");
        property.objectReferenceValue = value;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }

            current = next;
        }
    }

    private readonly struct ActorSetup
    {
        public ActorSetup(string gameObjectName, string characterName, int preCastingEventId)
        {
            GameObjectName = gameObjectName;
            CharacterName = characterName;
            PreCastingEventId = preCastingEventId;
        }

        public string GameObjectName { get; }
        public string CharacterName { get; }
        public int PreCastingEventId { get; }
    }
}
