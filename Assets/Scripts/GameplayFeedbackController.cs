using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayFeedbackController : MonoBehaviour
{
    private static readonly Color ReversePink = new Color(1f, 0.08f, 0.48f, 1f);
    private static readonly Color ReversePinkSoft = new Color(1f, 0.08f, 0.48f, 0.18f);

    private RectTransform screenRoot;
    private RectTransform visualRoot;
    private Image dimOverlay;

    private RectTransform announcementRect;
    private CanvasGroup announcementGroup;
    private TMP_Text announcementText;
    private TMP_Text instructionText;

    private RectTransform badgeRect;
    private CanvasGroup badgeGroup;
    private TMP_Text badgeText;

    private Vector2 screenBasePosition;
    private Quaternion screenBaseRotation;
    private Vector2 badgeBasePosition;

    private Coroutine transitionCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine badgePulseCoroutine;
    private Coroutine badgeReactionCoroutine;
    private Coroutine badgeMistakeCoroutine;
    private bool presentationPaused;

    private float PresentationDelta => presentationPaused ? 0f : Time.unscaledDeltaTime;

    public void SetPresentationPaused(bool paused)
    {
        presentationPaused = paused;
    }

    public void RestoreReverseActive()
    {
        if (visualRoot == null) return;
        StopPresentationCoroutines();
        visualRoot.gameObject.SetActive(true);
        dimOverlay.gameObject.SetActive(false);
        announcementRect.gameObject.SetActive(false);
        badgeRect.gameObject.SetActive(true);
        badgeRect.anchoredPosition = badgeBasePosition;
        badgeRect.localScale = Vector3.one;
        badgeGroup.alpha = 1f;
        badgePulseCoroutine = StartCoroutine(BadgePulseRoutine());
    }

    public void Initialize(RectTransform gameplayRoot)
    {
        if (visualRoot != null || gameplayRoot == null) return;

        screenRoot = gameplayRoot;
        screenBasePosition = screenRoot.anchoredPosition;
        screenBaseRotation = screenRoot.localRotation;

        visualRoot = CreateRect("GameplayFeedback", screenRoot);
        StretchToParent(visualRoot);
        visualRoot.SetAsLastSibling();

        dimOverlay = CreateImage("ReverseDim", visualRoot, Color.clear);
        StretchToParent(dimOverlay.rectTransform);

        announcementRect = CreateRect("ReverseAnnouncement", visualRoot);
        SetCenteredRect(announcementRect, new Vector2(920f, 240f), Vector2.zero);
        announcementGroup = announcementRect.gameObject.AddComponent<CanvasGroup>();

        announcementText = CreateText("Title", announcementRect, 112f, ReversePink);
        SetCenteredRect(announcementText.rectTransform, new Vector2(920f, 145f), new Vector2(0f, 35f));
        announcementText.fontStyle = FontStyles.Bold;
        Shadow titleShadow = announcementText.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0.35f, 0f, 0.18f, 0.9f);
        titleShadow.effectDistance = new Vector2(7f, -7f);

        instructionText = CreateText("Instruction", announcementRect, 38f, Color.white);
        SetCenteredRect(instructionText.rectTransform, new Vector2(800f, 65f), new Vector2(0f, -72f));
        instructionText.fontStyle = FontStyles.Bold;
        instructionText.characterSpacing = 5f;

        badgeRect = CreateRect("ReverseBadge", visualRoot);
        badgeRect.anchorMin = new Vector2(0.5f, 0.735f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.735f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.sizeDelta = new Vector2(380f, 70f);
        badgeRect.anchoredPosition = Vector2.zero;
        badgeBasePosition = badgeRect.anchoredPosition;

        Image badgeBackground = badgeRect.gameObject.AddComponent<Image>();
        badgeBackground.color = new Color(0.12f, 0.01f, 0.07f, 0.9f);
        badgeBackground.raycastTarget = false;
        Outline badgeOutline = badgeRect.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(1f, 0.08f, 0.48f, 0.8f);
        badgeOutline.effectDistance = new Vector2(3f, -3f);

        badgeGroup = badgeRect.gameObject.AddComponent<CanvasGroup>();
        badgeText = CreateText("Text", badgeRect, 40f, ReversePink);
        StretchToParent(badgeText.rectTransform);
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.characterSpacing = 4f;
        badgeText.text = "REVERSE";

        visualRoot.gameObject.SetActive(false);
    }

    public void PlayReverseEntrance(GameSquare smallestTarget, Action onComplete)
    {
        if (visualRoot == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopPresentationCoroutines();
        visualRoot.gameObject.SetActive(true);
        visualRoot.SetAsLastSibling();
        transitionCoroutine = StartCoroutine(ReverseEntranceRoutine(smallestTarget, onComplete));
    }

    public void PlayReverseExit(Action onComplete)
    {
        if (visualRoot == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopPresentationCoroutines();
        visualRoot.gameObject.SetActive(true);
        transitionCoroutine = StartCoroutine(ReverseExitRoutine(onComplete));
    }

    public void PlayReverseCorrect(GameSquare newLargeTarget)
    {
        if (visualRoot == null || !badgeRect.gameObject.activeInHierarchy) return;

        if (badgeReactionCoroutine != null) StopCoroutine(badgeReactionCoroutine);
        badgeReactionCoroutine = StartCoroutine(BadgePopRoutine());
        if (newLargeTarget != null) newLargeTarget.PlayAttentionPulse(0.16f);
    }

    public void PlayReverseMistake()
    {
        if (visualRoot == null || !badgeRect.gameObject.activeInHierarchy) return;

        if (badgeMistakeCoroutine != null) StopCoroutine(badgeMistakeCoroutine);
        badgeMistakeCoroutine = StartCoroutine(BadgeMistakeRoutine());
    }

    public void ResetImmediate()
    {
        StopAllCoroutines();
        transitionCoroutine = null;
        shakeCoroutine = null;
        badgePulseCoroutine = null;
        badgeReactionCoroutine = null;
        badgeMistakeCoroutine = null;

        if (screenRoot != null)
        {
            screenRoot.anchoredPosition = screenBasePosition;
            screenRoot.localRotation = screenBaseRotation;
        }

        if (badgeRect != null)
        {
            badgeRect.anchoredPosition = badgeBasePosition;
            badgeRect.localScale = Vector3.one;
        }

        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }

    private IEnumerator ReverseEntranceRoutine(GameSquare smallestTarget, Action onComplete)
    {
        screenBasePosition = screenRoot.anchoredPosition;
        screenBaseRotation = screenRoot.localRotation;

        dimOverlay.gameObject.SetActive(true);
        dimOverlay.color = Color.clear;
        announcementRect.gameObject.SetActive(true);
        badgeRect.gameObject.SetActive(false);
        announcementText.text = "REVERSE!";
        instructionText.text = string.Empty;
        announcementGroup.alpha = 0f;
        announcementRect.anchoredPosition = Vector2.zero;
        announcementRect.localScale = Vector3.one * 0.4f;
        announcementRect.localRotation = Quaternion.Euler(0f, 0f, -6f);

        const float anticipationDuration = 0.08f;
        float elapsed = 0f;
        while (elapsed < anticipationDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / anticipationDuration);
            dimOverlay.color = Color.Lerp(Color.clear, new Color(0.02f, 0f, 0.025f, 0.18f), t);
            yield return null;
        }

        instructionText.text = "TAP THE SMALLEST";
        shakeCoroutine = StartCoroutine(ScreenShakeRoutine(0.24f, 9f, 0.7f));
        TriggerHaptic();

        const float impactDuration = 0.28f;
        elapsed = 0f;
        while (elapsed < impactDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / impactDuration);
            float eased = EaseOutBack(t);
            announcementGroup.alpha = Mathf.Clamp01(t * 4f);
            announcementRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, eased);
            announcementRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-6f, 0f, eased));
            dimOverlay.color = Color.Lerp(new Color(0.02f, 0f, 0.025f, 0.18f), ReversePinkSoft, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        announcementRect.localScale = Vector3.one;
        announcementRect.localRotation = Quaternion.identity;
        if (smallestTarget != null) smallestTarget.PlayAttentionPulse();

        yield return WaitPresentationTime(0.22f);

        Canvas.ForceUpdateCanvases();
        badgeRect.gameObject.SetActive(true);
        badgeGroup.alpha = 0f;
        badgeRect.localScale = Vector3.one * 0.82f;
        Vector3 startPosition = announcementRect.localPosition;
        Vector3 badgePosition = badgeRect.localPosition;

        const float settleDuration = 0.18f;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float smooth = t * t * (3f - 2f * t);
            announcementRect.localPosition = Vector3.Lerp(startPosition, badgePosition, smooth);
            announcementRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, smooth);
            announcementGroup.alpha = 1f - smooth;
            badgeGroup.alpha = smooth;
            badgeRect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, smooth);
            dimOverlay.color = Color.Lerp(new Color(0.02f, 0f, 0.025f, 0.18f), Color.clear, smooth);
            yield return null;
        }

        announcementRect.gameObject.SetActive(false);
        dimOverlay.gameObject.SetActive(false);
        badgeGroup.alpha = 1f;
        badgeRect.localScale = Vector3.one;
        badgePulseCoroutine = StartCoroutine(BadgePulseRoutine());
        transitionCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator ReverseExitRoutine(Action onComplete)
    {
        if (badgePulseCoroutine != null) StopCoroutine(badgePulseCoroutine);
        badgePulseCoroutine = null;
        badgeGroup.alpha = 1f;
        badgeRect.gameObject.SetActive(true);

        float elapsed = 0f;
        const float collapseDuration = 0.18f;
        while (elapsed < collapseDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / collapseDuration);
            float x = t < 0.35f ? Mathf.Lerp(1f, 1.14f, t / 0.35f) : Mathf.Lerp(1.14f, 0f, (t - 0.35f) / 0.65f);
            badgeRect.localScale = new Vector3(x, Mathf.Lerp(1f, 0.86f, t), 1f);
            badgeGroup.alpha = 1f - Mathf.Clamp01((t - 0.65f) / 0.35f);
            yield return null;
        }
        badgeRect.gameObject.SetActive(false);

        announcementRect.gameObject.SetActive(true);
        announcementRect.anchoredPosition = Vector2.zero;
        announcementRect.localRotation = Quaternion.identity;
        announcementRect.localScale = Vector3.one * 0.78f;
        announcementText.text = "NORMAL";
        announcementText.color = Color.white;
        instructionText.text = string.Empty;
        announcementGroup.alpha = 0f;

        elapsed = 0f;
        const float normalInDuration = 0.12f;
        while (elapsed < normalInDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / normalInDuration);
            announcementGroup.alpha = t;
            announcementRect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1f, EaseOutBack(t));
            yield return null;
        }

        yield return WaitPresentationTime(0.13f);

        elapsed = 0f;
        const float normalOutDuration = 0.16f;
        while (elapsed < normalOutDuration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / normalOutDuration);
            announcementGroup.alpha = 1f - t;
            announcementRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, t);
            yield return null;
        }

        announcementText.color = ReversePink;
        announcementRect.gameObject.SetActive(false);
        visualRoot.gameObject.SetActive(false);
        transitionCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator ScreenShakeRoutine(float duration, float maxOffset, float maxAngle)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            float strength = (1f - t) * (1f - t);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * maxOffset * strength;
            screenRoot.anchoredPosition = screenBasePosition + offset;
            float angle = Mathf.Sin(t * Mathf.PI * 8f) * maxAngle * strength;
            screenRoot.localRotation = screenBaseRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        screenRoot.anchoredPosition = screenBasePosition;
        screenRoot.localRotation = screenBaseRotation;
        shakeCoroutine = null;
    }

    private IEnumerator BadgePulseRoutine()
    {
        while (true)
        {
            float elapsed = 0f;
            const float duration = 1.25f;
            while (elapsed < duration)
            {
                elapsed += PresentationDelta;
                float pulse = (Mathf.Sin((elapsed / duration) * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
                badgeGroup.alpha = Mathf.Lerp(0.78f, 1f, pulse);
                yield return null;
            }
        }
    }

    private IEnumerator BadgePopRoutine()
    {
        float elapsed = 0f;
        const float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.4f ? Mathf.Lerp(1f, 1.12f, t / 0.4f) : Mathf.Lerp(1.12f, 1f, (t - 0.4f) / 0.6f);
            badgeRect.localScale = Vector3.one * scale;
            yield return null;
        }
        badgeRect.localScale = Vector3.one;
        badgeReactionCoroutine = null;
    }

    private IEnumerator BadgeMistakeRoutine()
    {
        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += PresentationDelta;
            float t = Mathf.Clamp01(elapsed / duration);
            float offset = Mathf.Sin(t * Mathf.PI * 6f) * 18f * (1f - t);
            badgeRect.anchoredPosition = badgeBasePosition + Vector2.right * offset;
            yield return null;
        }
        badgeRect.anchoredPosition = badgeBasePosition;
        badgeMistakeCoroutine = null;
    }

    private void StopPresentationCoroutines()
    {
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        if (badgePulseCoroutine != null) StopCoroutine(badgePulseCoroutine);
        if (badgeReactionCoroutine != null) StopCoroutine(badgeReactionCoroutine);
        if (badgeMistakeCoroutine != null) StopCoroutine(badgeMistakeCoroutine);

        transitionCoroutine = null;
        shakeCoroutine = null;
        badgePulseCoroutine = null;
        badgeReactionCoroutine = null;
        badgeMistakeCoroutine = null;
        screenRoot.anchoredPosition = screenBasePosition;
        screenRoot.localRotation = screenBaseRotation;
        badgeRect.anchoredPosition = badgeBasePosition;
        badgeRect.localScale = Vector3.one;
    }

    private IEnumerator WaitPresentationTime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += PresentationDelta;
            yield return null;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = string.Empty;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void TriggerHaptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (PlayerPrefs.GetInt("HapticsEnabled", 1) == 1) Handheld.Vibrate();
#endif
    }
}
