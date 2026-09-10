using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rule-change presentation in the existing authorized input-lock windows.
/// This layer never moves the gameplay root, grid, or any target.
/// </summary>
public partial class GameplayFeedbackController : MonoBehaviour
{
    public const float ReverseEntranceDuration = 0.76f;
    public const float ReverseExitDuration = 0.59f;

    private RectTransform visualRoot;
    private Image dimOverlay;
    private RectTransform announcementRect;
    private CanvasGroup announcementGroup;
    private TMP_Text eyebrowText;
    private TMP_Text announcementText;
    private TMP_Text instructionText;
    private Image topRule;
    private Image bottomRule;
    private Image[] brackets;
    private Coroutine transitionCoroutine;
    private bool presentationPaused;
    private int transitionVersion;

    private float PresentationDelta => NeonMotion.TransitionDelta(presentationPaused);

    public void SetPresentationPaused(bool paused)
    {
        presentationPaused = paused;
    }

    public void Initialize(RectTransform gameplayRoot)
    {
        if (visualRoot != null || gameplayRoot == null) return;
        visualRoot = CreateRect("GameplayFeedback", gameplayRoot);
        StretchToParent(visualRoot);
        visualRoot.SetAsLastSibling();
        CanvasGroup layer = visualRoot.gameObject.AddComponent<CanvasGroup>();
        layer.blocksRaycasts = false;
        layer.interactable = false;

        dimOverlay = CreateImage("RuleDim", visualRoot, Color.clear);
        StretchToParent(dimOverlay.rectTransform);

        announcementRect = CreateRect("RuleAnnouncement", visualRoot);
        announcementRect.anchorMin = new Vector2(0.06f, 0.5f);
        announcementRect.anchorMax = new Vector2(0.94f, 0.5f);
        announcementRect.sizeDelta = new Vector2(0f, 304f);
        announcementRect.anchoredPosition = Vector2.zero;
        announcementGroup = announcementRect.gameObject.AddComponent<CanvasGroup>();
        announcementGroup.blocksRaycasts = false;
        announcementGroup.interactable = false;

        Image plate = CreateImage("InkPlate", announcementRect, NeonTheme.T.Background);
        StretchToParent(plate.rectTransform);
        eyebrowText = CreateText("Eyebrow", announcementRect, 23f, NeonTheme.T.Muted);
        PlaceText(eyebrowText.rectTransform, 40f, 99f, 36f);
        eyebrowText.characterSpacing = 6f;

        announcementText = CreateText("Title", announcementRect, 86f, NeonTheme.T.Reverse);
        PlaceText(announcementText.rectTransform, 28f, 19f, 112f);
        announcementText.fontStyle = FontStyles.Bold;
        announcementText.characterSpacing = 3f;

        instructionText = CreateText("Instruction", announcementRect, 36f, NeonTheme.T.Text);
        PlaceText(instructionText.rectTransform, 28f, -75f, 64f);

        topRule = CreateImage("TopRule", announcementRect, NeonTheme.T.Reverse);
        SetRule(topRule.rectTransform, 1f);
        bottomRule = CreateImage("BottomRule", announcementRect, NeonTheme.T.Reverse);
        SetRule(bottomRule.rectTransform, 0f);

        brackets = new Image[4];
        for (int i = 0; i < brackets.Length; i++)
        {
            bool right = (i & 1) != 0;
            bool top = i < 2;
            brackets[i] = CreateImage("Bracket" + i, announcementRect, NeonTheme.T.Reverse);
            RectTransform bracket = brackets[i].rectTransform;
            bracket.anchorMin = bracket.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            bracket.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            bracket.sizeDelta = new Vector2(3f, 32f);
            bracket.anchoredPosition = Vector2.zero;
        }
        visualRoot.gameObject.SetActive(false);
    }

    public void RestoreReverseActive()
    {
        // The persistent, upright rule indicator belongs to the HUD. Restoring
        // a saved Reverse state must not replay its pause or add a second badge.
        ResetImmediate();
    }

    public void PlayReverseEntrance(GameSquare smallestTarget, Action onComplete)
    {
        BeginTransition(true, onComplete);
    }

    public void PlayReverseExit(Action onComplete)
    {
        BeginTransition(false, onComplete);
    }

    // Existing callers remain valid. Cell-local feedback is dispatched by the
    // validated tap path, so these hooks never identify or animate an answer.
    public void PlayReverseCorrect(GameSquare newLargeTarget) { }
    public void PlayReverseMistake() { }

    public void ResetImmediate()
    {
        StopPresentation();
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        ResetTapFeedback();
    }

    private void BeginTransition(bool reverse, Action onComplete)
    {
        if (visualRoot == null)
        {
            onComplete?.Invoke();
            return;
        }
        StopPresentation();
        visualRoot.gameObject.SetActive(true);
        visualRoot.SetAsLastSibling();
        Color accent = reverse ? NeonTheme.T.Reverse : NeonTheme.T.Primary;
        eyebrowText.text = reverse ? "RULE SHIFT" : "RULE RESTORED";
        announcementText.text = reverse ? "REVERSE" : "STANDARD";
        announcementText.color = accent;
        instructionText.text = reverse ? "Smallest outline" : "Largest outline";
        topRule.color = bottomRule.color = accent;
        for (int i = 0; i < brackets.Length; i++) brackets[i].color = accent;
        announcementRect.gameObject.SetActive(true);
        announcementGroup.alpha = 0f;
        dimOverlay.color = Color.clear;
        transitionCoroutine = StartCoroutine(TransitionRoutine(reverse, transitionVersion, onComplete));
    }

    private IEnumerator TransitionRoutine(bool reverse, int version, Action onComplete)
    {
        // Use one continuous timeline so phase boundaries cannot accumulate
        // extra frame delays. Reduced effects preserves the exact same window.
        float duration = reverse ? ReverseEntranceDuration : ReverseExitDuration;
        float inDuration = Mathf.Clamp(NeonMotion.T.reverseEnterFade, 0, duration * .4f);
        float outDuration = Mathf.Clamp(NeonMotion.T.reverseExitFade, 0, duration * .4f);
        float elapsed = 0f;
        bool hapticTriggered = false;
        while (elapsed < duration)
        {
            elapsed += PresentationDelta;
            float enter = inDuration <= 0 ? 1 : Mathf.Clamp01(elapsed / inDuration);
            float leave = outDuration <= 0 ? (elapsed >= duration ? 1 : 0) : Mathf.Clamp01((elapsed - (duration - outDuration)) / outDuration);
            float alpha = NeonMotion.Ease(enter) * (1f - NeonMotion.Ease(leave));
            announcementGroup.alpha = alpha;
            Color dim = NeonTheme.T.Background;
            dim.a = alpha * (NeonTheme.ReducedEffects ? 0.46f : 0.62f);
            dimOverlay.color = dim;
            announcementRect.anchoredPosition = NeonTheme.ReducedEffects
                ? Vector2.zero
                : new Vector2(0f, Mathf.Lerp(NeonMotion.T.ruleTravel, 0f, NeonMotion.Ease(enter)) - leave * NeonMotion.T.ruleTravel * .5f);
            if (reverse && !hapticTriggered && elapsed >= 0.08f)
            {
                hapticTriggered = true;
                TriggerHaptic();
            }
            yield return null;
        }

        if (version != transitionVersion) yield break;
        transitionCoroutine = null;
        announcementGroup.alpha = 0f;
        announcementRect.anchoredPosition = Vector2.zero;
        visualRoot.gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private void StopPresentation()
    {
        transitionVersion++;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = null;
        if (announcementRect != null) announcementRect.anchoredPosition = Vector2.zero;
        if (announcementGroup != null) announcementGroup.alpha = 0f;
        if (dimOverlay != null) dimOverlay.color = Color.clear;
    }

    private void OnDisable()
    {
        ResetImmediate();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image result = rect.gameObject.AddComponent<Image>();
        result.color = color;
        result.raycastTarget = false;
        return result;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TMP_Text result = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (NeonTheme.T.font != null) result.font = NeonTheme.T.font;
        result.text = string.Empty;
        result.fontSize = size;
        result.color = color;
        result.alignment = TextAlignmentOptions.Center;
        result.raycastTarget = false;
        result.textWrappingMode = TextWrappingModes.NoWrap;
        result.overflowMode = TextOverflowModes.Overflow;
        return result;
    }

    private static void PlaceText(RectTransform rect, float inset, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(-inset * 2f, height);
        rect.anchoredPosition = new Vector2(0f, y);
    }

    private static void SetRule(RectTransform rect, float y)
    {
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y);
        rect.pivot = new Vector2(0.5f, y);
        rect.sizeDelta = new Vector2(0f, 3f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void TriggerHaptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (PlayerPrefs.GetInt("HapticsEnabled", 1) == 1) Handheld.Vibrate();
#endif
    }
}
