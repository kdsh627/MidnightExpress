using UnityEngine;

/// <summary>
/// Plays the title scene opening: camera zoom-out, opening letterboxes,
/// and a stationary train-like shake on Trail.
/// </summary>
public sealed class TitleIntroCinematic : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Camera titleCamera;
    [SerializeField] private RectTransform blackTop;
    [SerializeField] private RectTransform blackBottom;
    [SerializeField] private Transform trail;

    [Header("Intro")]
    [Min(0.01f)] [SerializeField] private float introDuration = 4f;
    [Min(0.01f)] [SerializeField] private float zoomStart = 4f;
    [Min(0.01f)] [SerializeField] private float zoomEnd = 5f;

    [Header("Letterbox")]
    [SerializeField] private float blackTopStartY = 540f;
    [SerializeField] private float blackBottomStartY = -540f;
    [SerializeField] private float letterboxEndY;

    [Header("Train shake")]
    [Min(0f)] [SerializeField] private float jointCycleFrequency = 2.25f;
    [Min(0f)] [SerializeField] private float firstImpactSharpness = 14f;
    [Range(0f, 1f)] [SerializeField] private float secondImpactPhase = 0.28f;
    [Min(0f)] [SerializeField] private float secondImpactSharpness = 20f;
    [Min(0f)] [SerializeField] private float secondImpactStrength = 0.55f;
    [Min(0f)] [SerializeField] private float impactAmplitude = 0.016f;
    [Min(0f)] [SerializeField] private float lowSettleFrequency = 15f;
    [Min(0f)] [SerializeField] private float lowSettleAmplitude = 0.005f;
    [Min(0f)] [SerializeField] private float highSettleFrequency = 32f;
    [Min(0f)] [SerializeField] private float highSettleAmplitude = 0.002f;

    private Vector2 blackTopStart;
    private Vector2 blackBottomStart;
    private Vector3 trailStartPosition;
    private Quaternion trailStartRotation;
    private float elapsed;

    private void Awake()
    {
        titleCamera ??= FindComponentInScene<Camera>("Main Camera");
        blackTop ??= FindComponentInScene<RectTransform>("Black1");
        blackBottom ??= FindComponentInScene<RectTransform>("Black2");
        trail ??= FindComponentInScene<Transform>("Trail");

        if (titleCamera != null)
        {
            titleCamera.orthographicSize = zoomStart;
        }

        if (blackTop != null)
        {
            blackTop.anchoredPosition = new Vector2(blackTop.anchoredPosition.x, blackTopStartY);
            blackTopStart = blackTop.anchoredPosition;
        }

        if (blackBottom != null)
        {
            blackBottom.anchoredPosition = new Vector2(blackBottom.anchoredPosition.x, blackBottomStartY);
            blackBottomStart = blackBottom.anchoredPosition;
        }

        if (trail != null)
        {
            trailStartPosition = trail.localPosition;
            trailStartRotation = trail.localRotation;
        }
    }

    private void Update()
    {
        AnimateIntro();
        ShakeTrail();
    }

    private void AnimateIntro()
    {
        elapsed = Mathf.Min(elapsed + Time.deltaTime, introDuration);
        var progress = Mathf.SmoothStep(0f, 1f, elapsed / introDuration);

        if (titleCamera != null)
        {
            titleCamera.orthographicSize = Mathf.Lerp(zoomStart, zoomEnd, progress);
        }

        if (blackTop != null)
        {
            var target = new Vector2(blackTopStart.x, letterboxEndY);
            blackTop.anchoredPosition = Vector2.Lerp(blackTopStart, target, progress);
        }

        if (blackBottom != null)
        {
            var target = new Vector2(blackBottomStart.x, letterboxEndY);
            blackBottom.anchoredPosition = Vector2.Lerp(blackBottomStart, target, progress);
        }
    }

    private void ShakeTrail()
    {
        if (trail == null)
        {
            return;
        }

        var time = Time.time;

        // Rail joints read more naturally as a soft paired "ka-dunk" than
        // a single perfectly even impact.
        var cycle = Mathf.Repeat(time * jointCycleFrequency, 1f);
        var firstImpact = Mathf.Clamp01(1f - cycle * firstImpactSharpness);
        firstImpact *= firstImpact;

        var secondDistance = Mathf.Abs(cycle - secondImpactPhase);
        var secondImpact = Mathf.Clamp01(1f - secondDistance * secondImpactSharpness);
        secondImpact *= secondImpact;

        var impact = firstImpact + secondImpact * secondImpactStrength;
        var settle =
            Mathf.Sin(time * lowSettleFrequency) * lowSettleAmplitude +
            Mathf.Sin(time * highSettleFrequency) * highSettleAmplitude;
        var verticalJolt = settle + impact * impactAmplitude;

        // Keep X/Z and every rotation axis fixed; only the local Y position jolts.
        trail.localPosition = new Vector3(trailStartPosition.x, trailStartPosition.y + verticalJolt, trailStartPosition.z);
        trail.localRotation = trailStartRotation;
    }

    private T FindComponentInScene<T>(string objectName) where T : Component
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName && transform.TryGetComponent<T>(out var component))
                {
                    return component;
                }
            }
        }

        return null;
    }
}
