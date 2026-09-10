using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

/// <summary>
/// Lightweight, presentation-only onboarding layer. GameManager owns practice
/// rules and advances this bounded clock, so no coroutine can outlive practice.
/// </summary>
public sealed partial class RogueliteUIController
{
    public float OnboardingFadeDuration { get; set; } = .18f;

    private RectTransform onboardingRoot, onboardingBanner, onboardingReadyCard, onboardingFocus;
    private CanvasGroup onboardingRootGroup, onboardingBannerGroup, onboardingReadyGroup, onboardingFocusGroup, onboardingPointerGroup;
    private CanvasGroup onboardingHealthGroup, onboardingTimeGroup;
    private TMP_Text onboardingTitle, onboardingBody, onboardingProgress, onboardingSkipLabel;
    private Button onboardingAction, onboardingSkip;
    private NeonOnboardingGraphic onboardingBannerArt, onboardingReadyArt, onboardingFocusArt;
    private NeonShape onboardingPointer, onboardingCheck;
    private Action onboardingAdvance, onboardingSkipAction;
    private string queuedTitle, queuedBody, queuedAction, queuedProgress;
    private bool queuedReady, hasQueuedPrompt, onboardingVisible, onboardingEnding;
    private float bannerAlpha, bannerTarget, readyAlpha, readyTarget, focusAlpha, focusTargetAlpha;
    private float healthAlpha = 1f, healthTarget = 1f, timeAlpha = 1f, timeTarget = 1f;
    private RectTransform onboardingFocusTarget, queuedFocusTarget;
    private Color focusTint = Color.white;
    private Color queuedFocusTint = Color.white;
    private bool pointerRequested, queuedPointer, hasQueuedFocus;
    private float focusPulse;
    private readonly Vector3[] onboardingFocusCorners = new Vector3[4];

    public bool OnboardingPresentationReady => !onboardingEnding && !hasQueuedPrompt &&
        (onboardingRootGroup == null || onboardingRootGroup.alpha >= .999f) &&
        IsOnboardingSettled(bannerAlpha, bannerTarget) && IsOnboardingSettled(readyAlpha, readyTarget) &&
        !hasQueuedFocus && IsOnboardingSettled(focusAlpha, focusTargetAlpha) &&
        IsOnboardingSettled(healthAlpha, healthTarget) &&
        IsOnboardingSettled(timeAlpha, timeTarget);

    public bool OnboardingPresentationHidden => onboardingRoot == null || !onboardingRoot.gameObject.activeSelf ||
        (onboardingEnding && onboardingRootGroup.alpha <= .001f &&
         IsOnboardingSettled(healthAlpha, healthTarget) && IsOnboardingSettled(timeAlpha, timeTarget));

    public void BeginOnboardingPresentation(Action advance, Action skip)
    {
        EnsureOnboardingPresentation();
        onboardingAdvance = advance;
        onboardingSkipAction = skip;
        onboardingVisible = true;
        onboardingEnding = false;
        hasQueuedPrompt = false;
        onboardingRoot.gameObject.SetActive(true);
        onboardingRoot.SetAsLastSibling();
        onboardingRootGroup.alpha = 0f;
        onboardingRootGroup.blocksRaycasts = true;
        onboardingRootGroup.interactable = true;
        bannerAlpha = bannerTarget = 0f;
        readyAlpha = readyTarget = 0f;
        focusAlpha = focusTargetAlpha = 0f;
        onboardingBanner.gameObject.SetActive(true);
        onboardingReadyCard.gameObject.SetActive(true);
        onboardingFocus.gameObject.SetActive(true);
        onboardingSkip.gameObject.SetActive(true);
        onboardingSkip.interactable = true;
        SetOnboardingFocus(null, PlayCyan, false);
        SetOnboardingHudVisibility(false, false, true);
        ApplyOnboardingAlphas();
    }

    public void ShowOnboardingInstruction(string title, string body, string actionLabel, string progress, bool ready = false)
    {
        EnsureOnboardingPresentation();
        if (!onboardingVisible || onboardingEnding)
            return;

        queuedTitle = title ?? string.Empty;
        queuedBody = body ?? string.Empty;
        queuedAction = actionLabel ?? string.Empty;
        queuedProgress = progress ?? string.Empty;
        queuedReady = ready;
        hasQueuedPrompt = true;
        bannerTarget = 0f;
        readyTarget = 0f;
    }

    public void SetOnboardingHudVisibility(bool healthVisible, bool timeVisible, bool immediate = false)
    {
        EnsureOnboardingPresentation();
        healthTarget = healthVisible ? 1f : 0f;
        timeTarget = timeVisible ? 1f : 0f;
        if (!immediate)
            return;
        healthAlpha = healthTarget;
        timeAlpha = timeTarget;
        ApplyOnboardingAlphas();
    }

    public void SetOnboardingFocus(RectTransform target, Color tint, bool pointer = false)
    {
        EnsureOnboardingPresentation();
        queuedFocusTarget = target;
        queuedFocusTint = tint;
        queuedPointer = pointer && target != null;
        hasQueuedFocus = true;
        if (focusAlpha <= .001f && focusTargetAlpha <= .001f)
            ApplyQueuedFocus();
        else
            focusTargetAlpha = 0f;
    }

    public void AdvanceOnboardingPresentation(float delta)
    {
        if (onboardingRoot == null || !onboardingRoot.gameObject.activeSelf)
            return;

        float speed = OnboardingDuration <= 0f ? float.PositiveInfinity : Mathf.Max(0f, delta) / OnboardingDuration;
        if (onboardingEnding)
        {
            onboardingRootGroup.alpha = MoveOnboarding(onboardingRootGroup.alpha, 0f, speed);
            healthAlpha = MoveOnboarding(healthAlpha, healthTarget, speed);
            timeAlpha = MoveOnboarding(timeAlpha, timeTarget, speed);
            onboardingRootGroup.blocksRaycasts = false;
            ApplyOnboardingAlphas();
            if (onboardingRootGroup.alpha <= .001f &&
                IsOnboardingSettled(healthAlpha, healthTarget) && IsOnboardingSettled(timeAlpha, timeTarget))
            {
                onboardingRoot.gameObject.SetActive(false);
                onboardingVisible = false;
            }
            return;
        }

        onboardingRootGroup.alpha = MoveOnboarding(onboardingRootGroup.alpha, 1f, speed);
        bannerAlpha = MoveOnboarding(bannerAlpha, bannerTarget, speed);
        readyAlpha = MoveOnboarding(readyAlpha, readyTarget, speed);
        focusAlpha = MoveOnboarding(focusAlpha, focusTargetAlpha, speed);
        healthAlpha = MoveOnboarding(healthAlpha, healthTarget, speed);
        timeAlpha = MoveOnboarding(timeAlpha, timeTarget, speed);

        if (hasQueuedPrompt && bannerAlpha <= .001f && readyAlpha <= .001f)
        {
            ApplyQueuedOnboardingPrompt();
            hasQueuedPrompt = false;
        }
        if (hasQueuedFocus && focusAlpha <= .001f && focusTargetAlpha <= .001f)
            ApplyQueuedFocus();

        focusPulse += Mathf.Max(0f, delta);
        UpdateOnboardingFocusGeometry();
        ApplyOnboardingAlphas();
    }

    public void EndOnboardingPresentation()
    {
        if (onboardingRoot == null || !onboardingRoot.gameObject.activeSelf)
            return;
        onboardingEnding = true;
        onboardingVisible = false;
        hasQueuedPrompt = false;
        onboardingSkip.interactable = false;
        onboardingRootGroup.blocksRaycasts = false;
        bannerTarget = readyTarget = focusTargetAlpha = 0f;
        healthTarget = timeTarget = 1f;
    }

    public void ResetOnboardingPresentation()
    {
        if (onboardingRoot != null)
        {
            onboardingRootGroup.alpha = 0f;
            onboardingRootGroup.blocksRaycasts = false;
            onboardingRoot.gameObject.SetActive(false);
        }
        onboardingVisible = onboardingEnding = hasQueuedPrompt = false;
        onboardingFocusTarget = null;
        pointerRequested = queuedPointer = hasQueuedFocus = false;
        onboardingAdvance = null;
        onboardingSkipAction = null;
        bannerAlpha = bannerTarget = readyAlpha = readyTarget = focusAlpha = focusTargetAlpha = 0f;
        SetOnboardingHudVisibility(true, true, true);
        SetOnboardingLayout(false);
    }

    public void SetOnboardingLayout(bool enabled)
    {
        if (gm == null || gm.gridContainer == null)
            return;
        RectTransform bounds = gm.gridContainer.parent as RectTransform;
        if (bounds != null)
            Fill(bounds, 22, 254, 22, enabled ? 700 : 490);
    }

    private float OnboardingDuration => NeonTheme.ReducedEffects ? .08f : Mathf.Max(.08f, OnboardingFadeDuration);
    private static float MoveOnboarding(float value, float target, float normalizedStep)
    {
        if (float.IsPositiveInfinity(normalizedStep))
            return target;
        return Mathf.MoveTowards(value, target, normalizedStep);
    }
    private static bool IsOnboardingSettled(float value, float target) => Mathf.Abs(value - target) <= .001f;

    private void ApplyQueuedOnboardingPrompt()
    {
        onboardingTitle.text = queuedTitle;
        onboardingBody.text = queuedBody;
        onboardingProgress.text = queuedProgress;
        bool isReady = queuedReady;
        onboardingBannerArt.kind = NeonOnboardingGraphic.Kind.ChamferPanel;
        onboardingReadyCard.gameObject.SetActive(true);
        onboardingAction.gameObject.SetActive(!string.IsNullOrWhiteSpace(queuedAction));
        ButtonText(onboardingAction, queuedAction);
        if (isReady)
        {
            onboardingTitle.transform.SetParent(onboardingReadyCard, false);
            onboardingBody.transform.SetParent(onboardingReadyCard, false);
            onboardingProgress.transform.SetParent(onboardingReadyCard, false);
            At(onboardingTitle.rectTransform, .5f, 1, 0f, -166f, 650f, 96f);
            onboardingTitle.enableAutoSizing = false;
            onboardingTitle.fontSize = 64f;
            onboardingTitle.textWrappingMode = TextWrappingModes.Normal;
            onboardingTitle.overflowMode = TextOverflowModes.Ellipsis;
            At(onboardingBody.rectTransform, .5f, 1, 0f, -248f, 620f, 76f);
            onboardingBody.enableAutoSizing = false;
            onboardingBody.fontSize = 29f;
            onboardingBody.textWrappingMode = TextWrappingModes.Normal;
            onboardingBody.overflowMode = TextOverflowModes.Ellipsis;
            onboardingProgress.gameObject.SetActive(false);
            onboardingAction.transform.SetParent(onboardingReadyCard, false);
            At((RectTransform)onboardingAction.transform, .5f, 0, 0, 74, 540, 112);
            var readyLabel = onboardingAction.GetComponentInChildren<TMP_Text>();
            Fill(readyLabel.rectTransform, 18, 0, 18, 0);
            readyLabel.color = PlayBackground;
            readyLabel.enableAutoSizing = false;
            readyLabel.fontSize = 44f;
            readyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            readyLabel.overflowMode = TextOverflowModes.Ellipsis;
            onboardingReadyArt.gameObject.SetActive(true);
            onboardingBanner.gameObject.SetActive(false);
            readyTarget = 1f;
        }
        else
        {
            onboardingTitle.transform.SetParent(onboardingBanner, false);
            onboardingBody.transform.SetParent(onboardingBanner, false);
            onboardingProgress.transform.SetParent(onboardingBanner, false);
            bool hasAction = !string.IsNullOrWhiteSpace(queuedAction);
            bool titleOnly = !hasAction && string.IsNullOrWhiteSpace(queuedBody);
            At(onboardingTitle.rectTransform, .5f, 1, hasAction ? -118f : 0f, hasAction ? -58f : titleOnly ? -110f : -59f,
                hasAction ? 650f : 820f, hasAction ? 84f : 92f);
            onboardingTitle.enableAutoSizing = true;
            onboardingTitle.fontSizeMin = 29f;
            onboardingTitle.fontSizeMax = 35f;
            onboardingTitle.fontSize = 35f;
            onboardingTitle.textWrappingMode = TextWrappingModes.Normal;
            onboardingTitle.overflowMode = TextOverflowModes.Ellipsis;
            At(onboardingBody.rectTransform, .5f, 1, hasAction ? -118f : 0f, hasAction ? -143f : -151f,
                hasAction ? 650f : 820f, hasAction ? 76f : 74f);
            onboardingBody.enableAutoSizing = true;
            onboardingBody.fontSizeMin = 22f;
            onboardingBody.fontSizeMax = 27f;
            onboardingBody.textWrappingMode = TextWrappingModes.Normal;
            onboardingBody.overflowMode = TextOverflowModes.Ellipsis;
            onboardingProgress.gameObject.SetActive(!string.IsNullOrWhiteSpace(queuedProgress));
            onboardingAction.transform.SetParent(onboardingBanner, false);
            At((RectTransform)onboardingAction.transform, 1, .5f, -125, 0, 206, 104);
            var actionLabel = onboardingAction.GetComponentInChildren<TMP_Text>();
            Fill(actionLabel.rectTransform, 12, 0, 12, 0);
            actionLabel.color = PlayBackground;
            actionLabel.enableAutoSizing = true;
            actionLabel.fontSizeMin = 22f;
            actionLabel.fontSizeMax = 27f;
            actionLabel.textWrappingMode = TextWrappingModes.Normal;
            actionLabel.overflowMode = TextOverflowModes.Ellipsis;
            onboardingBanner.gameObject.SetActive(true);
            onboardingReadyArt.gameObject.SetActive(false);
            bannerTarget = 1f;
        }
    }

    private void ApplyQueuedFocus()
    {
        onboardingFocusTarget = queuedFocusTarget;
        focusTint = queuedFocusTint;
        pointerRequested = queuedPointer;
        hasQueuedFocus = false;
        focusTargetAlpha = onboardingFocusTarget == null ? 0f : 1f;
        if (onboardingFocusTarget != null)
            UpdateOnboardingFocusGeometry();
    }

    private void ApplyOnboardingAlphas()
    {
        if (onboardingRoot == null)
            return;
        onboardingBannerGroup.alpha = bannerAlpha;
        onboardingReadyGroup.alpha = readyAlpha;
        onboardingFocusGroup.alpha = focusAlpha;
        onboardingHealthGroup.alpha = healthAlpha;
        onboardingTimeGroup.alpha = timeAlpha;
        onboardingBannerGroup.blocksRaycasts = bannerAlpha > .98f && onboardingAction.gameObject.activeSelf && !onboardingEnding;
        onboardingReadyGroup.blocksRaycasts = readyAlpha > .98f && onboardingAction.gameObject.activeSelf && !onboardingEnding;
        onboardingFocusGroup.blocksRaycasts = false;
        onboardingHealthGroup.blocksRaycasts = onboardingTimeGroup.blocksRaycasts = false;
        onboardingPointerGroup.alpha = pointerRequested ? focusAlpha : 0f;
        onboardingPointerGroup.blocksRaycasts = false;
        onboardingPointer.gameObject.SetActive(pointerRequested && focusAlpha > .01f);
        onboardingFocusArt.color = focusTint;
        onboardingPointer.color = focusTint;
        onboardingFocusArt.SetVerticesDirty();
    }

    private void EnsureOnboardingPresentation()
    {
        if (onboardingRoot != null)
            return;

        onboardingHealthGroup = EnsureCanvasGroup(play.Find("HealthPanel"));
        onboardingTimeGroup = EnsureCanvasGroup(play.Find("TimePanel"));
        onboardingRoot = Rect("OnboardingPresentation", play);
        Fill(onboardingRoot);
        onboardingRootGroup = onboardingRoot.gameObject.AddComponent<CanvasGroup>();
        onboardingRootGroup.blocksRaycasts = true;
        onboardingRootGroup.interactable = true;

        onboardingBanner = Rect("InstructionBanner", onboardingRoot);
        At(onboardingBanner, .5f, 1, 0, -590, 960, 220);
        onboardingBannerArt = onboardingBanner.gameObject.AddComponent<NeonOnboardingGraphic>();
        onboardingBannerArt.kind = NeonOnboardingGraphic.Kind.ChamferPanel;
        onboardingBannerArt.color = PlayCyan;
        onboardingBannerArt.raycastTarget = false;
        onboardingBannerGroup = onboardingBanner.gameObject.AddComponent<CanvasGroup>();
        onboardingBannerGroup.blocksRaycasts = false;

        onboardingTitle = Text("InstructionTitle", onboardingBanner, string.Empty, 35, PlayWhite, TextAlignmentOptions.Center);
        At(onboardingTitle.rectTransform, .5f, 1, -74, -45, 670, 48);
        onboardingTitle.fontStyle = FontStyles.Bold;
        onboardingBody = Text("InstructionBody", onboardingBanner, string.Empty, 25, PlayCyan, TextAlignmentOptions.Center);
        At(onboardingBody.rectTransform, .5f, 1, 0, -151, 820, 74);
        onboardingBody.textWrappingMode = TextWrappingModes.Normal;
        onboardingProgress = Text("InstructionProgress", onboardingBanner, string.Empty, 17, PlayMuted, TextAlignmentOptions.Left);
        At(onboardingProgress.rectTransform, 0, 0, 194, 24, 320, 30);
        onboardingAction = OnboardingActionButton(onboardingBanner, "InstructionAdvance");

        onboardingReadyCard = Rect("OnboardingReadyCard", onboardingRoot);
        At(onboardingReadyCard, .5f, .5f, 0, -45, 720, 480);
        onboardingReadyArt = onboardingReadyCard.gameObject.AddComponent<NeonOnboardingGraphic>();
        onboardingReadyArt.kind = NeonOnboardingGraphic.Kind.ChamferPanel;
        onboardingReadyArt.color = PlayCyan;
        onboardingReadyArt.raycastTarget = false;
        onboardingReadyGroup = onboardingReadyCard.gameObject.AddComponent<CanvasGroup>();
        onboardingReadyGroup.blocksRaycasts = false;
        var checkRing = Rect("ReadyCheckRing", onboardingReadyCard).gameObject.AddComponent<NeonOnboardingGraphic>();
        checkRing.kind = NeonOnboardingGraphic.Kind.Circle;
        checkRing.color = PlayCyan;
        checkRing.raycastTarget = false;
        At(checkRing.rectTransform, .5f, 1, 0, -91, 82, 82);
        onboardingCheck = Shape("ReadyCheck", onboardingReadyCard, NeonShape.Kind.Check, PlayCyan, 7);
        At(onboardingCheck.rectTransform, .5f, 1, 0, -91, 56, 56);

        onboardingSkip = Button("SkipPractice", onboardingRoot, "SKIP  ›");
        onboardingSkip.image.color = Color.clear;
        At((RectTransform)onboardingSkip.transform, 1, 1, -108, -42, 184, 68);
        var skipFrame = Rect("Frame", onboardingSkip.transform).gameObject.AddComponent<NeonOnboardingGraphic>();
        Fill(skipFrame.rectTransform);
        skipFrame.transform.SetAsFirstSibling();
        skipFrame.kind = NeonOnboardingGraphic.Kind.ChamferFrame;
        skipFrame.color = PlayCyan;
        skipFrame.raycastTarget = false;
        onboardingSkipLabel = onboardingSkip.GetComponentInChildren<TMP_Text>();
        onboardingSkipLabel.color = PlayCyan;
        onboardingSkipLabel.fontSize = 27;
        onboardingSkipLabel.characterSpacing = 4;
        onboardingSkip.onClick.AddListener(RequestOnboardingSkip);

        onboardingFocus = Rect("OnboardingFocus", onboardingRoot);
        onboardingFocus.anchorMin = onboardingFocus.anchorMax = new Vector2(.5f, .5f);
        onboardingFocus.pivot = new Vector2(.5f, .5f);
        onboardingFocusArt = onboardingFocus.gameObject.AddComponent<NeonOnboardingGraphic>();
        onboardingFocusArt.kind = NeonOnboardingGraphic.Kind.Brackets;
        onboardingFocusArt.color = PlayCyan;
        onboardingFocusArt.raycastTarget = false;
        onboardingFocusGroup = onboardingFocus.gameObject.AddComponent<CanvasGroup>();
        onboardingFocusGroup.blocksRaycasts = false;
        onboardingPointer = Shape("FirstTapPointer", onboardingRoot, NeonShape.Kind.Arrow, PlayCyan, 6);
        At(onboardingPointer.rectTransform, .5f, .5f, 0, 0, 96, 96);
        onboardingPointer.raycastTarget = false;
        onboardingPointerGroup = onboardingPointer.gameObject.AddComponent<CanvasGroup>();
        onboardingPointerGroup.blocksRaycasts = false;
        onboardingPointer.gameObject.SetActive(false);

        onboardingRoot.gameObject.SetActive(false);
    }

    private Button OnboardingActionButton(Transform parent, string name)
    {
        var button = Button(name, parent, "CONTINUE", true);
        button.image.color = Color.clear;
        var art = Rect("Frame", button.transform).gameObject.AddComponent<NeonOnboardingGraphic>();
        Fill(art.rectTransform);
        art.transform.SetAsFirstSibling();
        art.kind = NeonOnboardingGraphic.Kind.LimeButton;
        art.color = PlayLime;
        art.raycastTarget = false;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.enableAutoSizing = true;
        label.fontSizeMin = 22;
        label.fontSizeMax = 27;
        label.fontSize = 27;
        label.characterSpacing = 3;
        label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => onboardingAdvance?.Invoke());
        return button;
    }

    private static CanvasGroup EnsureCanvasGroup(Transform target)
    {
        if (target == null)
            return null;
        var group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    private void RequestOnboardingSkip()
    {
        if (onboardingEnding)
            return;
        EndOnboardingPresentation();
        onboardingSkipAction?.Invoke();
    }

    private void UpdateOnboardingFocusGeometry()
    {
        if (onboardingFocusTarget == null || onboardingRoot == null)
            return;
        onboardingFocusTarget.GetWorldCorners(onboardingFocusCorners);
        Canvas renderCanvas = canvas == null ? null : canvas.GetComponent<Canvas>();
        Camera camera = renderCanvas != null && renderCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? renderCanvas.worldCamera : null;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < onboardingFocusCorners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, onboardingFocusCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(onboardingRoot, screen, camera, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Vector2 size = max - min;
        onboardingFocus.anchoredPosition = (min + max) * .5f;
        onboardingFocus.sizeDelta = size + Vector2.one * 28f;
        onboardingFocus.localScale = NeonTheme.ReducedEffects ? Vector3.one :
            Vector3.one * (1f + Mathf.Sin(focusPulse * 5f) * .012f);
        float pointerHalf = onboardingPointer.rectTransform.rect.width * .5f;
        Rect rootRect = onboardingRoot.rect;
        onboardingPointer.rectTransform.anchoredPosition = new Vector2(
            Mathf.Clamp((min.x + max.x) * .5f, rootRect.xMin + pointerHalf, rootRect.xMax - pointerHalf),
            Mathf.Clamp(max.y - 10f, rootRect.yMin + pointerHalf, rootRect.yMax - pointerHalf));
        onboardingPointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
    }
}
