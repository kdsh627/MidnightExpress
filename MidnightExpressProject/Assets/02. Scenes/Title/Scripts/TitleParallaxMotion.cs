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

    [Header("Layer speeds")]
    [Min(0f)] [SerializeField] private float skySpeed = 0.08f;
    [Min(0f)] [SerializeField] private float cloudSpeed = 0.32f;
    [Min(0f)] [SerializeField] private float buildingSpeed = 1.35f;
    [Min(0f)] [SerializeField] private float railSpeed = 6.5f;
    [Min(0f)] [SerializeField] private float railSeamOverlap = 0.75f;
    [SerializeField] private Vector2 cloudRespawnGap = new Vector2(1.5f, 4f);

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

    private sealed class LoopingLayer
    {
        public Transform[] Items;
        public float[] LogicalX;
        public float Width;
        public float Speed;
        public float PixelsPerUnit;
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

    private readonly List<LoopingLayer> layers = new List<LoopingLayer>();
    private readonly List<SpeedStreak> streaks = new List<SpeedStreak>();
    private System.Random random;

    private Transform cloud;
    private float cloudLogicalX;
    private float cloudHalfWidth;
    private float cloudPixelsPerUnit = 100f;
    private Texture2D streakTexture;
    private Sprite streakSprite;

    private void Awake()
    {
        random = new System.Random(randomSeed);
        titleCamera ??= FindComponentInScene<Camera>("Main Camera");
        if (titleCamera == null)
        {
            enabled = false;
            return;
        }

        AddLoopingLayer("Sky", skySpeed);
        SetupCloud();
        AddLoopingLayer("Buildings", buildingSpeed);
        AddLoopingLayer("Rail", railSpeed, railSeamOverlap);
        CreateSpeedStreaks();
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        var ramp = Mathf.SmoothStep(
            speedRampStart,
            1f,
            Mathf.Clamp01(Time.timeSinceLevelLoad / speedRampDuration));

        for (var i = 0; i < layers.Count; i++)
        {
            TickLayer(layers[i], deltaTime, ramp);
        }

        TickCloud(deltaTime, ramp);
        TickSpeedStreaks(deltaTime, ramp);
    }

    private void AddLoopingLayer(string objectName, float speed, float seamOverlap = 0f)
    {
        var source = FindComponentInScene<SpriteRenderer>(objectName);
        if (source == null || source.sprite == null)
        {
            return;
        }

        var width = Mathf.Max(0.01f, source.bounds.size.x - seamOverlap);
        var cameraWidth = titleCamera.orthographicSize * titleCamera.aspect * 2f;
        var copyCount = Mathf.Max(3, Mathf.CeilToInt(cameraWidth / width) + 2);

        if (copyCount % 2 == 0)
        {
            copyCount++;
        }

        var layer = new LoopingLayer
        {
            Items = new Transform[copyCount],
            LogicalX = new float[copyCount],
            Width = width,
            Speed = speed,
            PixelsPerUnit = source.sprite.pixelsPerUnit
        };

        var middle = copyCount / 2;
        var sourcePosition = source.transform.position;

        for (var i = 0; i < copyCount; i++)
        {
            var offset = i - middle;
            Transform item;

            if (offset == 0)
            {
                item = source.transform;
            }
            else
            {
                var clone = Instantiate(source.gameObject, source.transform.parent);
                clone.name = objectName + "_Loop_" + offset;
                clone.hideFlags = HideFlags.DontSave;
                item = clone.transform;
            }

            var position = sourcePosition + Vector3.right * (width * offset);
            item.position = position;
            layer.Items[i] = item;
            layer.LogicalX[i] = position.x;
        }

        layers.Add(layer);
    }

    private void TickLayer(LoopingLayer layer, float deltaTime, float ramp)
    {
        var cameraLeft = titleCamera.transform.position.x - titleCamera.orthographicSize * titleCamera.aspect;
        var maxX = layer.LogicalX[0];

        for (var i = 1; i < layer.LogicalX.Length; i++)
        {
            maxX = Mathf.Max(maxX, layer.LogicalX[i]);
        }

        for (var i = 0; i < layer.Items.Length; i++)
        {
            layer.LogicalX[i] -= layer.Speed * ramp * deltaTime;

            if (layer.LogicalX[i] + layer.Width * 0.5f < cameraLeft)
            {
                layer.LogicalX[i] = maxX + layer.Width;
                maxX = layer.LogicalX[i];
            }

            var position = layer.Items[i].position;
            position.x = SnapToPixel(layer.LogicalX[i], layer.PixelsPerUnit);
            layer.Items[i].position = position;
        }
    }

    private void SetupCloud()
    {
        var renderer = FindComponentInScene<SpriteRenderer>("Cloud");
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        cloud = renderer.transform;
        cloudLogicalX = cloud.position.x;
        cloudHalfWidth = renderer.bounds.extents.x;
        cloudPixelsPerUnit = renderer.sprite.pixelsPerUnit;
    }

    private void TickCloud(float deltaTime, float ramp)
    {
        if (cloud == null)
        {
            return;
        }

        cloudLogicalX -= cloudSpeed * ramp * deltaTime;

        var cameraHalfWidth = titleCamera.orthographicSize * titleCamera.aspect;
        var cameraLeft = titleCamera.transform.position.x - cameraHalfWidth;
        var cameraRight = titleCamera.transform.position.x + cameraHalfWidth;

        if (cloudLogicalX + cloudHalfWidth < cameraLeft)
        {
            cloudLogicalX = cameraRight + cloudHalfWidth + RandomRange(cloudRespawnGap);
        }

        var position = cloud.position;
        position.x = SnapToPixel(cloudLogicalX, cloudPixelsPerUnit);
        cloud.position = position;
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

    private void OnDestroy()
    {
        if (streakSprite != null)
        {
            Destroy(streakSprite);
        }

        if (streakTexture != null)
        {
            Destroy(streakTexture);
        }
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
