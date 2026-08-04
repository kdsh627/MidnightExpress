using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds forward motion to the title scene while the train itself stays fixed.
/// Background layers scroll left at different speeds and wrap seamlessly.
/// </summary>
public sealed class TitleParallaxMotion : MonoBehaviour
{
    [Header("Scene reference")]
    [SerializeField] private Camera titleCamera;

    [Header("Material-scrolled repeating layers")]
    [Tooltip("Each renderer keeps its transform fixed. Its repeat material is moved only with a UV offset.")]
    [SerializeField] private List<ScrollingLayer> scrollingLayers = new List<ScrollingLayer>();

    [Header("Speed ramp")]
    [Min(0.01f)] [SerializeField] private float speedRampDuration = 3.5f;
    [Range(0f, 1f)] [SerializeField] private float speedRampStart = 0.35f;

    [Header("Speed streaks")]
    [Range(0, 32)] [SerializeField] private int streakCount = 9;
    [Range(4, 64)] [SerializeField] private int streakTextureWidth = 16;
    [SerializeField] private int streakSortingOrder = 6;
    [SerializeField] private Color streakColorA = new Color(0.35f, 0.85f, 1f, 0.16f);
    [SerializeField] private Color streakColorB = new Color(1f, 0.32f, 0.75f, 0.13f);
    [SerializeField] private Vector2 streakLengthRange = new Vector2(0.18f, 0.58f);
    [SerializeField] private Vector2 streakSpeedRange = new Vector2(10f, 16f);
    [SerializeField] private Vector2 initialStreakCooldownRange = new Vector2(0f, 1.6f);
    [SerializeField] private Vector2 streakCooldownRange = new Vector2(0.45f, 1.8f);
    [Range(0f, 1f)] [SerializeField] private float upperStreakChance = 0.55f;
    [SerializeField] private Vector2 upperStreakYRange = new Vector2(0.65f, 3.25f);
    [SerializeField] private Vector2 lowerStreakYRange = new Vector2(-3.2f, -2f);
    [SerializeField] private Vector2 streakThicknessRange = new Vector2(0.007f, 0.013f);
    [Min(1f)] [SerializeField] private float streakPixelsPerUnit = 100f;
    [SerializeField] private int randomSeed = 1977;

    [Serializable]
    private sealed class ScrollingLayer
    {
        [Tooltip("Inspector label only.")]
        public string Name;
        [Tooltip("The SpriteRenderer stays fixed while its material UV scrolls.")]
        public SpriteRenderer Renderer;
        [Tooltip("A material using MidnightExpress/Title/Repeat Scroll Sprite Lit.")]
        public Material Material;
        [Min(0f)]
        [Tooltip("Visual movement speed in world units per second.")]
        public float Speed;
        [Min(0.01f)]
        [Tooltip("Horizontal texture repetitions across the renderer. Usually 1.")]
        public float TilingX = 1f;

        [NonSerialized] public Material StartMaterial;
        [NonSerialized] public MaterialPropertyBlock StartPropertyBlock;
        [NonSerialized] public MaterialPropertyBlock RuntimePropertyBlock;
        [NonSerialized] public float RepeatWorldWidth;
        [NonSerialized] public float MirroredUvDirection = 1f;
        [NonSerialized] public float Offset;
        [NonSerialized] public bool Initialized;
    }

    private sealed class SpeedStreak
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public float LogicalX;
        public float Length;
        public float Speed;
        public float Cooldown;
    }

    private readonly List<SpeedStreak> streaks = new List<SpeedStreak>();
    private static readonly int TilingXId = Shader.PropertyToID("_TilingX");
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

    private System.Random random;

    private Texture2D streakTexture;
    private Sprite streakSprite;
    private bool runtimeInitialized;

    private void OnEnable()
    {
        if (!Application.isPlaying || runtimeInitialized)
        {
            return;
        }

        CleanupOrphanedSpeedStreaks();
        random = new System.Random(randomSeed);
        titleCamera ??= FindComponentInScene<Camera>("Main Camera");
        if (titleCamera == null)
        {
            enabled = false;
            return;
        }

        SetupScrollingLayers();
        CreateSpeedStreaks();
        runtimeInitialized = true;
    }

    private void Update()
    {
        if (!runtimeInitialized)
        {
            return;
        }

        var deltaTime = Time.deltaTime;
        var ramp = Mathf.SmoothStep(
            speedRampStart,
            1f,
            Mathf.Clamp01(Time.timeSinceLevelLoad / speedRampDuration));

        for (var i = 0; i < scrollingLayers.Count; i++)
        {
            TickScrollingLayer(scrollingLayers[i], deltaTime, ramp);
        }

        TickSpeedStreaks(deltaTime, ramp);
    }

    private void SetupScrollingLayers()
    {
        for (var i = 0; i < scrollingLayers.Count; i++)
        {
            var layer = scrollingLayers[i];
            if (layer == null || layer.Renderer == null || layer.Renderer.sprite == null)
            {
                continue;
            }

            var repeatMaterial = layer.Material != null
                ? layer.Material
                : layer.Renderer.sharedMaterial;
            if (repeatMaterial == null ||
                !repeatMaterial.HasProperty(TilingXId) ||
                !repeatMaterial.HasProperty(ScrollOffsetId))
            {
                Debug.LogWarning(
                    $"Title parallax layer '{layer.Name}' needs a repeat-scroll material.",
                    layer.Renderer);
                continue;
            }

            layer.StartMaterial = layer.Renderer.sharedMaterial;
            layer.StartPropertyBlock = new MaterialPropertyBlock();
            layer.Renderer.GetPropertyBlock(layer.StartPropertyBlock);

            layer.RuntimePropertyBlock = new MaterialPropertyBlock();
            layer.Renderer.GetPropertyBlock(layer.RuntimePropertyBlock);
            layer.Renderer.sharedMaterial = repeatMaterial;

            var signedScaleX = layer.Renderer.transform.lossyScale.x;
            var scaleX = Mathf.Max(0.0001f, Mathf.Abs(signedScaleX));
            layer.RepeatWorldWidth = Mathf.Max(
                0.01f,
                layer.Renderer.sprite.bounds.size.x * scaleX);
            // A negative X scale mirrors geometry, which reverses the apparent
            // UV scroll direction. Compensate so every positive Speed moves
            // the layer in the same screen-space direction.
            layer.MirroredUvDirection = signedScaleX < 0f ? -1f : 1f;
            layer.Offset = 0f;

            layer.RuntimePropertyBlock.SetFloat(
                TilingXId,
                Mathf.Max(0.01f, layer.TilingX));
            layer.RuntimePropertyBlock.SetFloat(ScrollOffsetId, 0f);
            layer.Renderer.SetPropertyBlock(layer.RuntimePropertyBlock);
            layer.Initialized = true;
        }
    }

    private static void TickScrollingLayer(
        ScrollingLayer layer,
        float deltaTime,
        float ramp)
    {
        if (layer == null ||
            !layer.Initialized ||
            layer.Renderer == null ||
            layer.RuntimePropertyBlock == null)
        {
            return;
        }

        var tilingX = Mathf.Max(0.01f, layer.TilingX);
        var uvPerWorldUnit = tilingX / layer.RepeatWorldWidth;
        layer.Offset = Mathf.Repeat(
            layer.Offset +
            layer.Speed *
            layer.MirroredUvDirection *
            ramp *
            deltaTime *
            uvPerWorldUnit,
            1f);

        layer.RuntimePropertyBlock.SetFloat(TilingXId, tilingX);
        layer.RuntimePropertyBlock.SetFloat(ScrollOffsetId, layer.Offset);
        layer.Renderer.SetPropertyBlock(layer.RuntimePropertyBlock);
    }

    private void CreateSpeedStreaks()
    {
        streakTexture = new Texture2D(streakTextureWidth, 1, TextureFormat.RGBA32, false);
        streakTexture.name = "TitleSpeedStreak";
        streakTexture.hideFlags = HideFlags.DontSave;
        streakTexture.filterMode = FilterMode.Point;

        for (var x = 0; x < streakTextureWidth; x++)
        {
            var normalized = x / (streakTextureWidth - 1f);
            var edgeFade = Mathf.Sin(normalized * Mathf.PI);
            streakTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, edgeFade));
        }

        streakTexture.Apply(false, true);
        streakSprite = Sprite.Create(
            streakTexture,
            new Rect(0f, 0f, streakTextureWidth, 1f),
            new Vector2(0.5f, 0.5f),
            streakTextureWidth);
        streakSprite.hideFlags = HideFlags.DontSave;

        for (var i = 0; i < streakCount; i++)
        {
            var streakObject = new GameObject("SpeedStreak_" + i);
            streakObject.hideFlags = HideFlags.DontSave;
            streakObject.transform.SetParent(transform, false);

            var renderer = streakObject.AddComponent<SpriteRenderer>();
            renderer.sprite = streakSprite;
            renderer.sortingOrder = streakSortingOrder;
            renderer.color = i % 2 == 0
                ? streakColorA
                : streakColorB;

            var streak = new SpeedStreak
            {
                Transform = streakObject.transform,
                Renderer = renderer,
                Cooldown = RandomRange(initialStreakCooldownRange)
            };

            streaks.Add(streak);
            RespawnStreak(streak, true);
        }
    }

    private void TickSpeedStreaks(float deltaTime, float ramp)
    {
        var cameraHalfWidth = titleCamera.orthographicSize * titleCamera.aspect;
        var cameraLeft = titleCamera.transform.position.x - cameraHalfWidth;
        var cameraRight = titleCamera.transform.position.x + cameraHalfWidth;

        for (var i = 0; i < streaks.Count; i++)
        {
            var streak = streaks[i];

            if (streak.Cooldown > 0f)
            {
                streak.Cooldown -= deltaTime;
                streak.Renderer.enabled = false;
                continue;
            }

            if (!streak.Renderer.enabled)
            {
                streak.Renderer.enabled = true;
                streak.LogicalX = cameraRight + streak.Length;
            }

            streak.LogicalX -= streak.Speed * ramp * deltaTime;

            if (streak.LogicalX + streak.Length < cameraLeft)
            {
                streak.Cooldown = RandomRange(streakCooldownRange);
                streak.Renderer.enabled = false;
                RespawnStreak(streak, false);
                continue;
            }

            var position = streak.Transform.position;
            position.x = SnapToPixel(streak.LogicalX, streakPixelsPerUnit);
            streak.Transform.position = position;
        }
    }

    private void RespawnStreak(SpeedStreak streak, bool scatterAcrossScreen)
    {
        var cameraHalfWidth = titleCamera.orthographicSize * titleCamera.aspect;
        var cameraLeft = titleCamera.transform.position.x - cameraHalfWidth;
        var cameraRight = titleCamera.transform.position.x + cameraHalfWidth;

        streak.Length = RandomRange(streakLengthRange);
        streak.Speed = RandomRange(streakSpeedRange);
        streak.LogicalX = scatterAcrossScreen
            ? RandomRange(cameraLeft, cameraRight)
            : cameraRight + streak.Length;

        var y = random.NextDouble() < upperStreakChance
            ? RandomRange(upperStreakYRange)
            : RandomRange(lowerStreakYRange);
        var thickness = RandomRange(streakThicknessRange);
        streak.Transform.position = new Vector3(streak.LogicalX, y, 0f);
        streak.Transform.localScale = new Vector3(streak.Length, thickness * streakTextureWidth, 1f);
    }

    private void OnDisable()
    {
        RestoreScrollingLayers();
        CleanupSpeedStreaks();
        runtimeInitialized = false;
    }

    private void OnDestroy()
    {
        RestoreScrollingLayers();
        CleanupSpeedStreaks();
    }

    private void RestoreScrollingLayers()
    {
        for (var i = 0; i < scrollingLayers.Count; i++)
        {
            var layer = scrollingLayers[i];
            if (layer == null || !layer.Initialized)
            {
                continue;
            }

            if (layer.Renderer != null)
            {
                layer.Renderer.sharedMaterial = layer.StartMaterial;
                layer.Renderer.SetPropertyBlock(layer.StartPropertyBlock);
            }

            layer.StartMaterial = null;
            layer.StartPropertyBlock = null;
            layer.RuntimePropertyBlock = null;
            layer.RepeatWorldWidth = 0f;
            layer.MirroredUvDirection = 1f;
            layer.Offset = 0f;
            layer.Initialized = false;
        }
    }

    private void CleanupSpeedStreaks()
    {
        for (var i = streaks.Count - 1; i >= 0; i--)
        {
            if (streaks[i]?.Transform != null)
            {
                DestroyRuntimeObject(streaks[i].Transform.gameObject);
            }
        }

        streaks.Clear();

        if (streakSprite != null)
        {
            DestroyRuntimeObject(streakSprite);
            streakSprite = null;
        }

        if (streakTexture != null)
        {
            DestroyRuntimeObject(streakTexture);
            streakTexture = null;
        }
    }

    private void CleanupOrphanedSpeedStreaks()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("SpeedStreak_", StringComparison.Ordinal))
            {
                DestroyOrphanImmediately(child.gameObject);
            }
        }
    }

    private static void DestroyOrphanImmediately(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        DestroyImmediate(target);
#else
        Destroy(target);
#endif
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying || !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DestroyImmediate(target);
            return;
        }
#endif

        Destroy(target);
    }

    private float RandomRange(float minimum, float maximum)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private float RandomRange(Vector2 range)
    {
        return RandomRange(range.x, range.y);
    }

    private static float SnapToPixel(float value, float pixelsPerUnit)
    {
        if (pixelsPerUnit <= 0f)
        {
            return value;
        }

        return Mathf.Round(value * pixelsPerUnit) / pixelsPerUnit;
    }

    private T FindComponentInScene<T>(string objectName) where T : Component
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var sceneTransform in root.GetComponentsInChildren<Transform>(true))
            {
                if (sceneTransform.name == objectName && sceneTransform.TryGetComponent<T>(out var component))
                {
                    return component;
                }
            }
        }

        return null;
    }
}
