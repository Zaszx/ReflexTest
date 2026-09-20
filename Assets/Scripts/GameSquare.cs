using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Immediate logical target roles with owned, interruptible visual motion.
/// Only presentation children move; the cell and gameplay transforms never do.
/// </summary>
public partial class GameSquare : MonoBehaviour, IPointerDownHandler
{
    [Header("UI Components")]
    public Image bgImage;
    public Image outlineImage;

    public enum LitSize { None, Small, Medium, Large }

    [Header("Current State")]
    public int gridX;
    public int gridY;
    public LitSize currentLitSize = LitSize.None;
    public System.Action<GameSquare> onClicked;
    /// <summary>Optional shared input resolver. Returning false consumes this pointer before cell gameplay runs.</summary>
    public Func<PointerEventData, bool> pointerGate;

    private PrecisionCellFrame targetFrame;
    private PrecisionCellFrame retiringFrame;
    private PrecisionCellFrame errorFrame;
    private PrecisionCellFrame successFrame;
    private Color baseCellColor, targetColor;
    private Color feedbackStart;
    private float smallScale = .4f, mediumScale = .7f, fullScale = 1f;
    private int currentTargetRank = -1, targetRankCount = 3;
    private float renderedScale, renderedAlpha;
    private float roleStartScale, roleStartAlpha, roleTargetScale, roleElapsed, roleDuration;
    private float exitStartScale, exitStartAlpha, exitElapsed, exitDuration, exitContraction;
    private readonly List<RetiringVisual> additionalRetiringVisuals = new List<RetiringVisual>();
    private readonly List<PrecisionCellFrame> retiringPool = new List<PrecisionCellFrame>();
    private float feedbackElapsed, feedbackDuration;
    private float errorElapsed, errorDuration, terminalShutdown;
    private float successElapsed, successDuration;
    private Color terminalRetiringColor;
    private bool targetAnimating, exitActive, feedbackActive, errorActive, successActive, terminalPresentation;
    private bool consumedForNextAssignment, animationsPaused;
    private bool levelPresentationActive;
    private float levelOutgoingScale, levelOutgoingAlpha;

    public float RenderedScale => renderedScale;
    public float RenderedAlpha => renderedAlpha;
    public float RetiringAlpha => retiringFrame != null && exitActive ? retiringFrame.color.a : 0f;
    public bool IsTargetAnimating => targetAnimating;
    public bool HasRetiringVisual => exitActive || additionalRetiringVisuals.Count > 0;
    public int CurrentTargetRank => currentTargetRank;

    /// <summary>
    /// Copies the four corners of the currently visible target perimeter into
    /// <paramref name="corners" in world space. Call this before consuming or
    /// reassigning the target; it deliberately reports the rendered transform,
    /// including any grid motion, rotation, and target-role scale.
    /// </summary>
    public bool TryGetRenderedOutlineCorners(Vector3[] corners)
    {
        if (corners == null || corners.Length < 4 || outlineImage == null ||
            currentTargetRank < 0 || renderedScale <= 0f || renderedAlpha <= 0f ||
            !outlineImage.gameObject.activeInHierarchy)
            return false;

        outlineImage.rectTransform.GetWorldCorners(corners);
        return true;
    }

    public void SetAnimationsPaused(bool paused) => animationsPaused = paused;

    /// <summary>
    /// Freezes this cell's role at its currently rendered pose for an outgoing
    /// run presentation. It never settles or changes the logical target role.
    /// Damage feedback remains independently eligible to finish.
    /// </summary>
    public void BeginTerminalPresentation()
    {
        terminalPresentation = true;
        targetAnimating = false;
        // Keep an accepted target's outgoing mesh exactly where it was. The
        // terminal shutdown owns only its opacity, not its role or transform.
        if (retiringFrame != null && exitActive) terminalRetiringColor = retiringFrame.color;
    }

    /// <summary>Applies a cosmetic shutdown multiplier without moving the cell or its hitbox.</summary>
    public void SetTerminalShutdown(float progress)
    {
        terminalShutdown = Mathf.Clamp01(progress);
        ApplyCurrentAppearance();
    }

    /// <summary>Returns this reusable cell to normal cosmetic ownership.</summary>
    public void EndTerminalPresentation()
    {
        terminalPresentation = false;
        terminalShutdown = 0f;
        if (retiringFrame != null && exitActive) retiringFrame.color = terminalRetiringColor;
        ApplyCurrentAppearance();
    }

    public static Color CellPalette(Color levelColor)
    {
        Color result = Color.Lerp(NeonTheme.Hex("0C202D"), levelColor, .035f);
        result.a = 1f;
        return result;
    }

    // The campaign flow owns this palette during its single transition timeline.
    public void SetBasePalette(Color fill, Color outline)
    {
        baseCellColor = fill;
        targetColor = outline;
        feedbackActive = false;
        ClearErrorLayer();
        ClearSuccessLayer();
        ApplyCurrentAppearance();
        if (targetFrame != null)
        {
            Color tint = outline * Mathf.Lerp(1f, NeonMotion.T.terminalGridDimMultiplier, terminalShutdown);
            tint.a = renderedAlpha * (1f - terminalShutdown);
            targetFrame.color = tint;
        }
        if (levelPresentationActive && retiringFrame != null)
        {
            Color tint = outline; tint.a = retiringFrame.color.a;
            retiringFrame.color = tint;
        }
    }

    public void BeginLevelPresentation(LitSize nextRole, float small, float medium, float full)
    {
        BeginLevelPresentationRank(LegacyRank(nextRole), 3, small, medium, full);
    }

    public void BeginLevelPresentationRank(int rank, int count, float small, float medium, float full)
    {
        levelOutgoingScale = renderedScale;
        levelOutgoingAlpha = renderedAlpha;
        NormalizePresentation(false);
        targetAnimating = false;
        levelPresentationActive = true;
        animationsPaused = true;
        smallScale = small; mediumScale = medium; fullScale = full;
        targetRankCount = Mathf.Clamp(count, GridSequenceRules.MinimumOutlineCount, GridSequenceRules.MaximumOutlineCount);
        currentTargetRank = Mathf.Clamp(rank, -1, targetRankCount - 1);
        currentLitSize = LegacySize(currentTargetRank, targetRankCount);
        retiringFrame.CornerFraction = 1f;
        retiringFrame.rectTransform.localScale = Vector3.one * levelOutgoingScale;
        retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, levelOutgoingScale);
        RenderLevelPresentation(0f);
    }

    public void RenderLevelPresentation(float progress)
    {
        if (!levelPresentationActive) return;
        float t = Mathf.Clamp01(progress);
        // The transition owner reserves the middle phase for grid morphing:
        // old targets are gone before it starts, and incoming ranks grow only
        // after the cells have settled into their destination layout.
        float outgoingT = NeonMotion.Ease(Mathf.Clamp01(t / .18f));
        float incomingT = NeonMotion.Ease(Mathf.Clamp01((t - .72f) / .28f));
        Color outgoing = targetColor; outgoing.a = levelOutgoingAlpha * (1f - outgoingT);
        retiringFrame.color = outgoing;
        retiringFrame.rectTransform.localScale = Vector3.one * (levelOutgoingScale * (1f - outgoingT));
        retiringFrame.gameObject.SetActive(levelOutgoingScale > 0f && outgoing.a > 0f);
        float incomingScale = GetCurrentScale();
        RenderTarget(incomingScale * incomingT, currentTargetRank < 0 ? 0f : incomingT);
    }

    public void EndLevelPresentation()
    {
        levelPresentationActive = false;
        if (retiringFrame != null) retiringFrame.CornerFraction = .22f;
        NormalizePresentation();
    }

    public void Setup(int x, int y, Sprite solidSprite, Sprite outlineSprite,
        Color cellColor, Color outlineColor, float small, float medium, float full)
    {
        gridX = x;
        gridY = y;
        smallScale = small;
        mediumScale = medium;
        fullScale = full;
        currentLitSize = LitSize.None;
        currentTargetRank = -1;
        targetRankCount = 3;
        baseCellColor = CellPalette(cellColor);
        targetColor = NeonTheme.LevelTarget(outlineColor);
        targetColor.a = 1f;
        bgImage.sprite = null;
        bgImage.color = baseCellColor;
        bgImage.raycastTarget = true;
        NeonGridFill.Apply(bgImage);
        outlineImage.enabled = false;
        outlineImage.raycastTarget = false;
        outlineImage.color = targetColor;

        // Cell fills already define the grid. A separate subpixel boundary on
        // every cell caused visible flicker during grid motion.
        if (targetFrame == null)
            targetFrame = CreateFrame("PrecisionOutline", outlineImage.rectTransform);
        targetFrame.color = targetColor;
        if (retiringFrame == null)
        {
            // One reusable exit mesh per cell, independent of its active role.
            retiringFrame = CreateFrame("RetiringTarget", bgImage.rectTransform);
            retiringFrame.transform.SetAsFirstSibling();
            retiringFrame.CornerFraction = .22f;
            // Rapidly reusing a cell must not allow a stale exit to alter its
            // new role, while the pool stays bounded for long sessions.
            for (int i = 0; i < 3; i++)
            {
                PrecisionCellFrame pooled = CreateFrame("RetiringTargetPool", bgImage.rectTransform);
                pooled.transform.SetAsFirstSibling();
                pooled.CornerFraction = .22f;
                pooled.gameObject.SetActive(false);
                retiringPool.Add(pooled);
            }
        }
        if (errorFrame == null)
        {
            // A permanent inner frame keeps wrong-tap feedback local while
            // leaving the role-defining outline and hit surface untouched.
            errorFrame = CreateFrame("DamageOutline", bgImage.rectTransform);
            errorFrame.transform.SetAsLastSibling();
            errorFrame.rectTransform.anchorMin = new Vector2(.10f, .10f);
            errorFrame.rectTransform.anchorMax = new Vector2(.90f, .90f);
            errorFrame.CornerFraction = .82f;
        }
        errorFrame.gameObject.SetActive(false);
        if (successFrame == null)
        {
            // Success is an independent acknowledgement layer. It never owns,
            // recolors, or resizes either role-defining target frame.
            successFrame = CreateFrame("SuccessOutline", bgImage.rectTransform);
            successFrame.transform.SetAsLastSibling();
            successFrame.rectTransform.anchorMin = new Vector2(.16f, .16f);
            successFrame.rectTransform.anchorMax = new Vector2(.84f, .84f);
            // Tiny corner ticks acknowledge a hit without resembling another
            // actionable Small or retired target outline.
            successFrame.CornerFraction = .08f;
            successFrame.LineWidth = NeonTheme.T.targetStrokeWidth;
        }
        successFrame.gameObject.SetActive(false);
        NormalizePresentation();
    }

    /// <summary>Assign each cell once from the latest authoritative snapshot.</summary>
    public void SetLitSize(LitSize size, bool animate)
    {
        SetTargetRank(LegacyRank(size), 3, smallScale, mediumScale, fullScale, animate);
    }

    /// <summary>Assign a target rank from an authoritative 2-5 target sequence.</summary>
    public void SetTargetRank(int rank, int count, float small, float medium, float full, bool animate)
    {
        int clampedCount = Mathf.Clamp(count, GridSequenceRules.MinimumOutlineCount, GridSequenceRules.MaximumOutlineCount);
        int nextRank = Mathf.Clamp(rank, -1, clampedCount - 1);
        int previousRank = currentTargetRank;
        bool wasConsumed = consumedForNextAssignment;
        consumedForNextAssignment = false;
        smallScale = small; mediumScale = medium; fullScale = full;
        targetRankCount = clampedCount;
        currentTargetRank = nextRank; // Input and the model never wait for a tween.
        currentLitSize = LegacySize(nextRank, clampedCount);
        if (outlineImage == null) return;

        if (!animate || !gameObject.activeInHierarchy)
        {
            CancelExit();
            SettleTarget();
            return;
        }
        if (nextRank < 0)
        {
            if (!wasConsumed && previousRank >= 0) BeginRetiringVisual(false);
            SettleTarget();
            return;
        }
        if (previousRank == nextRank && !wasConsumed) return;

        bool appearing = previousRank < 0 || wasConsumed;
        bool interrupted = targetAnimating && !appearing;
        roleTargetScale = GetCurrentScale();
        roleDuration = Mathf.Max(0f, appearing ? NeonMotion.T.targetAppearDuration
            : interrupted ? Mathf.Min(NeonMotion.T.targetRoleDuration, NeonMotion.T.targetRoleRetargetDuration)
            : NeonMotion.T.targetRoleDuration);
        if (roleDuration <= 0f)
        {
            SettleTarget();
            return;
        }

        float start = renderedScale;
        if (appearing)
        {
            // A new smallest target starts within its own rank cue.
            start = nextRank == 0 && !NeonTheme.ReducedEffects
                ? roleTargetScale * Mathf.Clamp(NeonMotion.T.targetSpawnScale, .1f, 1f)
                : roleTargetScale;
            renderedAlpha = Mathf.Clamp01(NeonMotion.T.targetSpawnAlpha);
        }
        // Reassignment may need a small immediate semantic nudge. All remaining
        // interpolation stays in this role's disjoint band, including rapid taps.
        roleStartScale = TargetRoleBands.ClampStartRank(nextRank, clampedCount, start, smallScale, mediumScale,
            fullScale, NeonMotion.T.targetRoleGap);
        roleStartAlpha = Mathf.Clamp01(renderedAlpha);
        roleElapsed = 0f;
        targetAnimating = true;
        RenderTarget(roleStartScale, roleStartAlpha);
    }

    /// <summary>
    /// After a successful logical commit, capture the consumed visual before the
    /// new snapshot is assigned. Reuse of this cell cannot affect the exit mesh.
    /// </summary>
    public void PlayConsumedFeedback(bool reverse = false)
    {
        if (outlineImage == null || currentTargetRank < 0) return;
        BeginRetiringVisual(reverse);
        consumedForNextAssignment = true;
        targetAnimating = false;
        RenderTarget(0f, 0f);
    }

    /// <summary>Local acknowledgement only; called after a validated tap.</summary>
    public void PlayTapFeedback(bool correct)
    {
        if (bgImage == null || !gameObject.activeInHierarchy) return;
        float intensity = correct ? NeonMotion.T.correctCellTint : NeonMotion.T.wrongCellTint;
        if (NeonTheme.ReducedEffects) intensity *= NeonMotion.T.reducedEffectsStrength;
        Color accent = correct ? NeonMotion.T.successAccent : NeonMotion.T.damageAccent;
        Color impact = Color.Lerp(baseCellColor, accent, Mathf.Clamp01(intensity));
        // Merge repeated feedback from its current visible value, with no queue.
        feedbackStart = Color.Lerp(bgImage.color, impact, .9f);
        feedbackDuration = Mathf.Max(0f, correct ? NeonMotion.T.correctCellDuration : NeonMotion.T.wrongCellDuration);
        feedbackElapsed = 0f;
        feedbackActive = feedbackDuration > 0f;
        if (correct) BeginSuccessLayer(); else BeginErrorLayer();
        bgImage.color = feedbackActive ? feedbackStart : CurrentBaseColor();
    }

    /// <summary>Explicit name for terminal callers; equivalent to a wrong tap cosmetic only.</summary>
    public void PlayDamageFeedback() => PlayTapFeedback(false);

    // Compatibility only: target-directed pulses would reveal the answer.
    public void PlayAttentionPulse(float delay = 0f) { }

    private void Update() => AdvancePresentation(NeonMotion.Delta(animationsPaused));

    /// <summary>Explicit stepping also supports deterministic lifecycle tests.</summary>
    public void AdvancePresentation(float delta)
    {
        if (animationsPaused || delta <= 0f || !gameObject.activeInHierarchy) return;
        if (targetAnimating && !terminalPresentation)
        {
            roleElapsed += delta;
            float t = Mathf.Clamp01(roleElapsed / roleDuration);
            float eased = NeonMotion.Ease(t);
            RenderTarget(Mathf.Lerp(roleStartScale, roleTargetScale, eased), Mathf.Lerp(roleStartAlpha, 1f, eased));
            if (t >= 1f) SettleTarget();
        }
        if (!terminalPresentation)
        {
            for (int i = additionalRetiringVisuals.Count - 1; i >= 0; i--)
            {
                RetiringVisual visual = additionalRetiringVisuals[i];
                visual.elapsed += delta;
                float t = Mathf.Clamp01(visual.elapsed / visual.duration);
                float eased = NeonMotion.Ease(t);
                float scale = visual.startScale * (1f - visual.contraction * eased);
                visual.frame.rectTransform.localScale = Vector3.one * scale;
                visual.frame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, scale);
                visual.frame.Dissolve = visual.contraction < 0f ? t : 0f;
                Color color = NeonTheme.T.Primary;
                color.a = visual.startAlpha * (1f - eased);
                visual.frame.color = color;
                if (t >= 1f) { visual.frame.gameObject.SetActive(false); additionalRetiringVisuals.RemoveAt(i); }
                else additionalRetiringVisuals[i] = visual;
            }
        }
        if (exitActive && !terminalPresentation)
        {
            exitElapsed += delta;
            float t = Mathf.Clamp01(exitElapsed / exitDuration);
            float eased = NeonMotion.Ease(t);
            float scale = exitStartScale * (1f - exitContraction * eased);
            retiringFrame.rectTransform.localScale = Vector3.one * scale;
            retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, scale);
            retiringFrame.Dissolve = exitContraction < 0f ? t : 0f;
            Color color = NeonTheme.T.Primary;
            color.a = exitStartAlpha * (1f - eased);
            retiringFrame.color = color;
            if (t >= 1f) CancelExit();
        }
        if (feedbackActive)
        {
            feedbackElapsed += delta;
            float t = Mathf.Clamp01(feedbackElapsed / feedbackDuration);
            float eased = NeonMotion.Ease(t);
            bgImage.color = Color.Lerp(feedbackStart, CurrentBaseColor(), eased);
            if (t >= 1f) feedbackActive = false;
        }
        if (errorActive)
        {
            errorElapsed += delta;
            float t = NeonMotionAccent.Fraction(errorElapsed, errorDuration);
            Color tint = NeonMotion.T.damageAccent;
            tint.a = (1f - NeonMotion.Ease(t)) * NeonMotion.T.wrongCellOutlineOpacity * NeonMotionAccent.Strength;
            if (terminalPresentation) tint.a *= 1f - terminalShutdown;
            errorFrame.color = tint;
            if (t >= 1f) ClearErrorLayer();
        }
        if (successActive)
        {
            successElapsed += delta;
            float t = NeonMotionAccent.Fraction(successElapsed, successDuration);
            Color tint = NeonMotion.T.successAccent;
            tint.a = (1f - NeonMotion.Ease(t)) * NeonMotion.T.correctCellTint * NeonMotionAccent.Strength;
            if (terminalPresentation) tint.a *= 1f - terminalShutdown;
            successFrame.color = tint;
            if (t >= 1f) ClearSuccessLayer();
        }
    }

    /// <summary>
    /// Clear transient cosmetics without changing game state. By default the
    /// latest role settles exactly; false leaves that role's current tween owned.
    /// </summary>
    public void NormalizePresentation(bool settleTargets = true)
    {
        consumedForNextAssignment = false;
        feedbackActive = false;
        ClearErrorLayer();
        ClearSuccessLayer();
        CancelExit();
        if (bgImage != null) bgImage.color = CurrentBaseColor();
        if (settleTargets) SettleTarget();
    }

    private void OnDisable() => NormalizePresentation();

    private void BeginRetiringVisual(bool reverse)
    {
        if (retiringFrame == null || renderedScale <= 0f || renderedAlpha <= 0f) return;
        if (exitActive)
        {
            // Preserve the old layer when a cell becomes a target and is
            // consumed again before its prior effect settles.
            PrecisionCellFrame nextFrame = TakeRetiringFrame();
            retiringFrame.gameObject.name = "RetiringTargetPool";
            nextFrame.gameObject.name = "RetiringTarget";
            additionalRetiringVisuals.Add(new RetiringVisual
            {
                frame = retiringFrame, startScale = exitStartScale, startAlpha = exitStartAlpha,
                elapsed = exitElapsed, duration = exitDuration, contraction = exitContraction
            });
            retiringFrame = nextFrame;
        }
        exitDuration = Mathf.Max(0f, NeonMotion.T.targetExitDuration);
        if (exitDuration <= 0f)
        {
            CancelExit();
            return;
        }
        exitStartScale = renderedScale;
        // Capture the active target's current pose before its independent
        // retiring mesh starts eroding; do not pop it to a dimmed version.
        exitStartAlpha = renderedAlpha;
        if (NeonTheme.ReducedEffects) exitStartAlpha *= Mathf.Clamp01(NeonMotion.T.reducedEffectsStrength);
        // Normal taps grow slightly while dissolving. Reverse taps instead
        // contract the consumed smallest rank all the way to zero.
        exitContraction = NeonTheme.ReducedEffects ? 0f : reverse
            ? 1f
            : -Mathf.Clamp(NeonMotion.T.targetExitContraction, .08f, .12f);
        exitElapsed = 0f;
        exitActive = true;
        retiringFrame.rectTransform.localScale = Vector3.one * exitStartScale;
        retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, exitStartScale);
        retiringFrame.Dissolve = 0f;
        Color tint = NeonTheme.T.Primary;
        tint.a = exitStartAlpha;
        retiringFrame.color = tint;
        retiringFrame.gameObject.SetActive(true);
    }

    private void CancelExit()
    {
        exitActive = false;
        if (retiringFrame != null) { retiringFrame.Dissolve = 0f; retiringFrame.gameObject.SetActive(false); }
        for (int i = 0; i < additionalRetiringVisuals.Count; i++)
            if (additionalRetiringVisuals[i].frame != null) { additionalRetiringVisuals[i].frame.Dissolve = 0f; additionalRetiringVisuals[i].frame.gameObject.SetActive(false); }
        for (int i = 0; i < retiringPool.Count; i++)
            if (retiringPool[i] != null) { retiringPool[i].Dissolve = 0f; retiringPool[i].gameObject.SetActive(false); }
        additionalRetiringVisuals.Clear();
    }

    private PrecisionCellFrame TakeRetiringFrame()
    {
        for (int i = 0; i < retiringPool.Count; i++)
            if (!retiringPool[i].gameObject.activeSelf) return retiringPool[i];

        // At the bounded cap, recycle the oldest retiring graphic. It is
        // decorative and noninteractive, so this cannot change game state.
        RetiringVisual oldest = additionalRetiringVisuals[0];
        additionalRetiringVisuals.RemoveAt(0);
        oldest.frame.gameObject.SetActive(false);
        return oldest.frame;
    }

    private void SettleTarget()
    {
        targetAnimating = false;
        float scale = GetCurrentScale();
        RenderTarget(scale, currentTargetRank < 0 ? 0f : 1f);
    }

    private void RenderTarget(float scale, float alpha)
    {
        renderedScale = scale;
        renderedAlpha = alpha;
        if (outlineImage == null) return;
        outlineImage.transform.localScale = Vector3.one * scale;
        outlineImage.gameObject.SetActive(scale > 0f && alpha > 0f);
        if (targetFrame != null)
        {
            targetFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, scale);
            Color tint = targetColor * Mathf.Lerp(1f, NeonMotion.T.terminalGridDimMultiplier, terminalShutdown);
            tint.a = alpha * (1f - terminalShutdown);
            targetFrame.color = tint;
        }
    }

    private float GetCurrentScale()
    {
        return TargetRoleBands.ScaleForRank(currentTargetRank, targetRankCount, smallScale, mediumScale, fullScale);
    }

    private static int LegacyRank(LitSize size)
    {
        switch (size)
        {
            case LitSize.Small: return 0;
            case LitSize.Medium: return 1;
            case LitSize.Large: return 2;
            default: return -1;
        }
    }

    private static LitSize LegacySize(int rank, int count)
    {
        if (rank < 0) return LitSize.None;
        if (rank == 0) return LitSize.Small;
        return rank == count - 1 ? LitSize.Large : LitSize.Medium;
    }

    private Color CurrentBaseColor()
    {
        return Color.Lerp(baseCellColor, NeonTheme.T.Background, terminalShutdown * (1f - NeonMotion.T.terminalGridDimMultiplier));
    }

    private void ApplyCurrentAppearance()
    {
        if (bgImage != null && !feedbackActive) bgImage.color = CurrentBaseColor();
        RenderTarget(renderedScale, renderedAlpha);
        if (terminalPresentation && retiringFrame != null && exitActive)
        {
            Color tint = terminalRetiringColor;
            tint.a *= 1f - terminalShutdown;
            retiringFrame.color = tint;
        }
    }

    private void BeginErrorLayer()
    {
        if (errorFrame == null) return;
        errorDuration = Mathf.Max(0f, NeonMotion.T.wrongCellDuration);
        errorElapsed = 0f;
        errorActive = errorDuration > 0f;
        if (!errorActive) { ClearErrorLayer(); return; }
        errorFrame.gameObject.SetActive(true);
        Color tint = NeonMotion.T.damageAccent;
        tint.a = NeonMotion.T.wrongCellOutlineOpacity * NeonMotionAccent.Strength;
        errorFrame.color = tint;
    }

    private void ClearErrorLayer()
    {
        errorActive = false;
        if (errorFrame != null) errorFrame.gameObject.SetActive(false);
    }

    private void BeginSuccessLayer()
    {
        if (successFrame == null) return;
        successDuration = Mathf.Max(0f, NeonMotion.T.correctCellDuration);
        successElapsed = 0f;
        successActive = successDuration > 0f;
        if (!successActive) { ClearSuccessLayer(); return; }
        successFrame.gameObject.SetActive(true);
        Color tint = NeonMotion.T.successAccent;
        tint.a = NeonMotion.T.correctCellTint * NeonMotionAccent.Strength;
        successFrame.color = tint;
    }

    private void ClearSuccessLayer()
    {
        successActive = false;
        if (successFrame != null) successFrame.gameObject.SetActive(false);
    }

    private static PrecisionCellFrame CreateFrame(string name, RectTransform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PrecisionCellFrame));
        child.transform.SetParent(parent, false);
        PrecisionCellFrame frame = child.GetComponent<PrecisionCellFrame>();
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        frame.raycastTarget = false;
        return frame;
    }

    private struct RetiringVisual
    {
        public PrecisionCellFrame frame;
        public float startScale, startAlpha, elapsed, duration, contraction;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerGate == null || pointerGate(eventData)) onClicked?.Invoke(this);
    }
}

/// <summary>Disjoint size bands preserve the gameplay language during a morph.</summary>
public static class TargetRoleBands
{
    public static float ClampStart(GameSquare.LitSize role, float value, float small, float medium, float large, float relativeGap)
    {
        float gap = Mathf.Max(0f, Mathf.Min(medium - small, large - medium)) * Mathf.Clamp(relativeGap, .001f, .45f);
        float lowerSplit = (small + medium) * .5f;
        float upperSplit = (medium + large) * .5f;
        switch (role)
        {
            case GameSquare.LitSize.Small: return Mathf.Clamp(value, Mathf.Min(small, small * .1f), lowerSplit - gap);
            case GameSquare.LitSize.Medium: return Mathf.Clamp(value, lowerSplit + gap, upperSplit - gap);
            case GameSquare.LitSize.Large: return Mathf.Clamp(value, upperSplit + gap, large);
            default: return 0f;
        }
    }

    public static float ScaleForRank(int rank, int count, float small, float medium, float full)
    {
        if (rank < 0 || count < GridSequenceRules.MinimumOutlineCount) return 0f;
        int clampedCount = Mathf.Clamp(count, GridSequenceRules.MinimumOutlineCount, GridSequenceRules.MaximumOutlineCount);
        int clampedRank = Mathf.Clamp(rank, 0, clampedCount - 1);
        if (clampedCount == 3)
            return clampedRank == 0 ? small : clampedRank == 1 ? medium : full;
        float progress = clampedRank / (float)(clampedCount - 1);
        return progress <= .5f
            ? Mathf.Lerp(small, medium, progress * 2f)
            : Mathf.Lerp(medium, full, (progress - .5f) * 2f);
    }

    public static float ClampStartRank(int rank, int count, float value, float small, float medium, float full, float relativeGap)
    {
        if (rank < 0) return 0f;
        int clampedCount = Mathf.Clamp(count, GridSequenceRules.MinimumOutlineCount, GridSequenceRules.MaximumOutlineCount);
        int clampedRank = Mathf.Clamp(rank, 0, clampedCount - 1);
        float target = ScaleForRank(clampedRank, clampedCount, small, medium, full);
        if (clampedCount == 2)
        {
            float split = (small + full) * .5f;
            float gap = (full - small) * Mathf.Clamp(relativeGap, .001f, .45f);
            return clampedRank == 0
                ? Mathf.Clamp(value, small * .1f, split - gap)
                : Mathf.Clamp(value, split + gap, full);
        }

        float lower = clampedRank == 0 ? small : (ScaleForRank(clampedRank - 1, clampedCount, small, medium, full) + target) * .5f;
        float upper = clampedRank == clampedCount - 1 ? full : (target + ScaleForRank(clampedRank + 1, clampedCount, small, medium, full)) * .5f;
        float localGap = Mathf.Max(0f, upper - lower) * Mathf.Clamp(relativeGap, .001f, .45f);
        return Mathf.Clamp(value, lower + (clampedRank == 0 ? 0f : localGap), upper - (clampedRank == clampedCount - 1 ? 0f : localGap));
    }
}

/// <summary>A native UI frame; short corner segments distinguish retiring effects.</summary>
public sealed class PrecisionCellFrame : MaskableGraphic
{
    public override Material defaultMaterial => NeonGridRendering.Material ?? base.defaultMaterial;

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        NeonGridRendering.EnsureChannels(canvas);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        NeonGridRendering.EnsureChannels(canvas);
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        NeonGridRendering.EnsureChannels(canvas);
    }

    private float lineWidth = 4.5f;
    private float cornerFraction = 1f;
    private float dissolve;
    public float LineWidth
    {
        get => lineWidth;
        set { if (Mathf.Approximately(lineWidth, value)) return; lineWidth = value; SetVerticesDirty(); }
    }
    public float CornerFraction
    {
        get => cornerFraction;
        set { if (Mathf.Approximately(cornerFraction, value)) return; cornerFraction = value; SetVerticesDirty(); }
    }
    public float Dissolve
    {
        get => dissolve;
        set { value = Mathf.Clamp01(value); if (Mathf.Approximately(dissolve, value)) return; dissolve = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (NeonGridRendering.Material != null)
        {
            NeonGridRendering.Populate(mesh, rectTransform.rect, color, Mathf.Max(0, lineWidth), cornerFraction, dissolve);
            return;
        }
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float width = Mathf.Min(LineWidth, Mathf.Min(rect.width, rect.height) * .5f);
        if (width <= 0f) return;
        if (cornerFraction >= .5f)
        {
            AddBar(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + width);
            AddBar(mesh, rect.xMin, rect.yMax - width, rect.xMax, rect.yMax);
            AddBar(mesh, rect.xMin, rect.yMin + width, rect.xMin + width, rect.yMax - width);
            AddBar(mesh, rect.xMax - width, rect.yMin + width, rect.xMax, rect.yMax - width);
            return;
        }
        float xSegment = Mathf.Max(width, rect.width * cornerFraction);
        float ySegment = Mathf.Max(width, rect.height * cornerFraction);
        AddBar(mesh, rect.xMin, rect.yMin, rect.xMin + xSegment, rect.yMin + width);
        AddBar(mesh, rect.xMax - xSegment, rect.yMin, rect.xMax, rect.yMin + width);
        AddBar(mesh, rect.xMin, rect.yMax - width, rect.xMin + xSegment, rect.yMax);
        AddBar(mesh, rect.xMax - xSegment, rect.yMax - width, rect.xMax, rect.yMax);
        AddBar(mesh, rect.xMin, rect.yMin + width, rect.xMin + width, rect.yMin + ySegment);
        AddBar(mesh, rect.xMin, rect.yMax - ySegment, rect.xMin + width, rect.yMax - width);
        AddBar(mesh, rect.xMax - width, rect.yMin + width, rect.xMax, rect.yMin + ySegment);
        AddBar(mesh, rect.xMax - width, rect.yMax - ySegment, rect.xMax, rect.yMax - width);
    }

    private void AddBar(VertexHelper mesh, float left, float bottom, float right, float top)
    {
        int start = mesh.currentVertCount;
        Color32 tint = color;
        mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
        mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
