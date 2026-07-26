using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Turns the split Start ticket artwork into a button, tears it at the
/// perforation, then loads the configured game scene.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class StartTicketButton : MonoBehaviour
{
    [Header("Ticket pieces")]
    [SerializeField] private RectTransform leftPiece;
    [SerializeField] private RectTransform rightPiece;
    [SerializeField] private RectTransform startLabel;

    [Header("Timing")]
    [Min(0.01f)] [SerializeField] private float anticipationDuration = 0.1f;
    [Min(0.01f)] [SerializeField] private float tearDuration = 0.32f;
    [Min(0.01f)] [SerializeField] private float exitDuration = 0.28f;
    [Min(0f)] [SerializeField] private float previewResetDelay = 0.18f;

    [Header("Anticipation")]
    [SerializeField] private Vector2 anticipationScale = new Vector2(1.025f, 0.975f);
    [Min(0f)] [SerializeField] private float anticipationShakeDistance = 2f;
    [Min(0f)] [SerializeField] private float anticipationShakeFrequency = 5f;

    [Header("Tear")]
    [SerializeField] private Vector2 leftTearOffset = new Vector2(-38f, -8f);
    [SerializeField] private Vector2 rightTearOffset = new Vector2(62f, 9f);
    [Min(1f)] [SerializeField] private float tearEasePower = 3f;
    [SerializeField] private float leftTearAngle = 8f;
    [SerializeField] private float rightTearAngle = -5f;

    [Header("Exit")]
    [SerializeField] private Vector2 leftExitOffset = new Vector2(-150f, -25f);
    [SerializeField] private Vector2 rightExitOffset = new Vector2(220f, 28f);
    [SerializeField] private float leftExitAngle = 16f;
    [SerializeField] private float rightExitAngle = -10f;
    [Range(0f, 1f)] [SerializeField] private float fadeStart = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float fadeEnd = 0.85f;

    [Header("Game start")]
    [Tooltip("Optional scene path or scene name. When empty, build index 1 is used.")]
    [SerializeField] private string targetScene;

    private Button button;
    private Graphic leftGraphic;
    private Graphic rightGraphic;
    private Graphic labelGraphic;
    private Vector3 ticketStartScale;
    private Vector2 leftStartPosition;
    private Vector2 rightStartPosition;
    private Vector2 labelStartPosition;
    private Quaternion leftStartRotation;
    private Quaternion rightStartRotation;
    private Quaternion labelStartRotation;
    private Color leftStartColor;
    private Color rightStartColor;
    private Color labelStartColor;
    private bool isPlaying;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = rightGraphic;
        button.onClick.AddListener(PlayAndStartGame);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayAndStartGame);
        }
    }

    public void PlayAndStartGame()
    {
        BeginTear(true);
    }

    /// <summary>
    /// MCP/editor preview entry point. Plays the full tear without changing scenes.
    /// </summary>
    public void PlayPreview()
    {
        BeginTear(false);
    }

    public void ResetTicket()
    {
        StopAllCoroutines();
        isPlaying = false;

        transform.localScale = ticketStartScale;
        leftPiece.anchoredPosition = leftStartPosition;
        rightPiece.anchoredPosition = rightStartPosition;
        startLabel.anchoredPosition = labelStartPosition;
        leftPiece.localRotation = leftStartRotation;
        rightPiece.localRotation = rightStartRotation;
        startLabel.localRotation = labelStartRotation;

        SetGraphicAlpha(leftGraphic, leftStartColor, 1f);
        SetGraphicAlpha(rightGraphic, rightStartColor, 1f);
        SetGraphicAlpha(labelGraphic, labelStartColor, 1f);

        if (button != null)
        {
            button.interactable = true;
        }
    }

    private void BeginTear(bool startGameAfterTear)
    {
        if (isPlaying || leftPiece == null || rightPiece == null || startLabel == null)
        {
            return;
        }

        isPlaying = true;
        button.interactable = false;
        StartCoroutine(TearRoutine(startGameAfterTear));
    }

    private IEnumerator TearRoutine(bool startGameAfterTear)
    {
        yield return AnimateAnticipation();
        yield return AnimateTear();
        yield return AnimateExit();

        if (startGameAfterTear && TryLoadGameScene())
        {
            yield break;
        }

        yield return new WaitForSecondsRealtime(previewResetDelay);
        ResetTicket();
    }

    private IEnumerator AnimateAnticipation()
    {
        var elapsed = 0f;

        while (elapsed < anticipationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / anticipationDuration);
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            var shake = Mathf.Sin(progress * Mathf.PI * anticipationShakeFrequency) *
                        (1f - progress) *
                        anticipationShakeDistance;

            transform.localScale = Vector3.Lerp(
                ticketStartScale,
                new Vector3(
                    ticketStartScale.x * anticipationScale.x,
                    ticketStartScale.y * anticipationScale.y,
                    ticketStartScale.z),
                eased);

            leftPiece.anchoredPosition = leftStartPosition + Vector2.left * shake;
            rightPiece.anchoredPosition = rightStartPosition + Vector2.right * shake;
            startLabel.anchoredPosition = labelStartPosition + Vector2.right * shake;
            yield return null;
        }
    }

    private IEnumerator AnimateTear()
    {
        var elapsed = 0f;
        while (elapsed < tearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / tearDuration);
            var eased = 1f - Mathf.Pow(1f - progress, tearEasePower);
            var leftAngle = Mathf.Lerp(0f, leftTearAngle, eased);
            var rightAngle = Mathf.Lerp(0f, rightTearAngle, eased);

            transform.localScale = Vector3.Lerp(
                new Vector3(
                    ticketStartScale.x * anticipationScale.x,
                    ticketStartScale.y * anticipationScale.y,
                    ticketStartScale.z),
                ticketStartScale,
                eased);

            leftPiece.anchoredPosition = leftStartPosition + leftTearOffset * eased;
            rightPiece.anchoredPosition = rightStartPosition + rightTearOffset * eased;
            leftPiece.localRotation = leftStartRotation * Quaternion.Euler(0f, 0f, leftAngle);
            rightPiece.localRotation = rightStartRotation * Quaternion.Euler(0f, 0f, rightAngle);
            ApplyLabelPose(rightTearOffset * eased, rightAngle);
            yield return null;
        }
    }

    private IEnumerator AnimateExit()
    {
        var elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / exitDuration);
            var eased = progress * progress;
            var fadeProgress = Mathf.InverseLerp(
                Mathf.Min(fadeStart, fadeEnd),
                Mathf.Max(fadeStart + 0.001f, fadeEnd),
                progress);
            var alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            var leftOffset = Vector2.Lerp(leftTearOffset, leftExitOffset, eased);
            var rightOffset = Vector2.Lerp(rightTearOffset, rightExitOffset, eased);
            var leftAngle = Mathf.Lerp(leftTearAngle, leftExitAngle, eased);
            var rightAngle = Mathf.Lerp(rightTearAngle, rightExitAngle, eased);

            leftPiece.anchoredPosition = leftStartPosition + leftOffset;
            rightPiece.anchoredPosition = rightStartPosition + rightOffset;
            leftPiece.localRotation = leftStartRotation * Quaternion.Euler(0f, 0f, leftAngle);
            rightPiece.localRotation = rightStartRotation * Quaternion.Euler(0f, 0f, rightAngle);
            ApplyLabelPose(rightOffset, rightAngle);

            SetGraphicAlpha(leftGraphic, leftStartColor, alpha);
            SetGraphicAlpha(rightGraphic, rightStartColor, alpha);
            SetGraphicAlpha(labelGraphic, labelStartColor, alpha);
            yield return null;
        }
    }

    private bool TryLoadGameScene()
    {
        if (!string.IsNullOrWhiteSpace(targetScene) &&
            Application.CanStreamedLevelBeLoaded(targetScene))
        {
            SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
            return true;
        }

        if (SceneManager.sceneCountInBuildSettings > 1 &&
            Application.CanStreamedLevelBeLoaded(1))
        {
            SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
            return true;
        }

        Debug.LogWarning(
            "[StartTicket] Game scene is not available. Add it after Title in Build Settings " +
            "or assign its path to StartTicketButton.targetScene.");
        return false;
    }

    private void ApplyLabelPose(Vector2 rightOffset, float rightAngle)
    {
        var rotatedLabelOffset = Rotate(labelStartPosition - rightStartPosition, rightAngle);
        startLabel.anchoredPosition = rightStartPosition + rightOffset + rotatedLabelOffset;
        startLabel.localRotation = labelStartRotation * Quaternion.Euler(0f, 0f, rightAngle);
    }

    private void ResolveReferences()
    {
        if (leftPiece == null)
        {
            leftPiece = transform.Find("Image") as RectTransform;
        }

        if (rightPiece == null)
        {
            rightPiece = transform.Find("Image (1)") as RectTransform;
        }

        if (startLabel == null)
        {
            startLabel = transform.Find("Text (TMP)") as RectTransform;
        }

        leftGraphic = leftPiece != null ? leftPiece.GetComponent<Graphic>() : null;
        rightGraphic = rightPiece != null ? rightPiece.GetComponent<Graphic>() : null;
        labelGraphic = startLabel != null ? startLabel.GetComponent<Graphic>() : null;
    }

    private void CacheInitialState()
    {
        ticketStartScale = transform.localScale;
        leftStartPosition = leftPiece.anchoredPosition;
        rightStartPosition = rightPiece.anchoredPosition;
        labelStartPosition = startLabel.anchoredPosition;
        leftStartRotation = leftPiece.localRotation;
        rightStartRotation = rightPiece.localRotation;
        labelStartRotation = startLabel.localRotation;
        leftStartColor = leftGraphic.color;
        rightStartColor = rightGraphic.color;
        labelStartColor = labelGraphic.color;
    }

    private static void SetGraphicAlpha(Graphic graphic, Color baseColor, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        baseColor.a *= alpha;
        graphic.color = baseColor;
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var cosine = Mathf.Cos(radians);
        var sine = Mathf.Sin(radians);
        return new Vector2(
            value.x * cosine - value.y * sine,
            value.x * sine + value.y * cosine);
    }
}
