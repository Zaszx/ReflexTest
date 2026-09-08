using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Immediate logical target roles with owned, interruptible visual motion.
/// Only presentation children move; the cell and gameplay transforms never do.
/// </summary>
public class GameSquare : MonoBehaviour, IPointerDownHandler
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

    private PrecisionCellFrame targetFrame;
    private PrecisionCellFrame retiringFrame;
    private Color baseCellColor, targetColor;
    private Color feedbackStart;
    private float smallScale = .4f, mediumScale = .7f, fullScale = 1f;
    private float renderedScale, renderedAlpha;
    private float roleStartScale, roleStartAlpha, roleTargetScale, roleElapsed, roleDuration;
    private float exitStartScale, exitStartAlpha, exitElapsed, exitDuration, exitContraction;
    private float feedbackElapsed, feedbackDuration;
    private bool targetAnimating, exitActive, feedbackActive;
    private bool consumedForNextAssignment, animationsPaused;
    private bool levelPresentationActive;
    private float levelOutgoingScale, levelOutgoingAlpha;

    public float RenderedScale => renderedScale;
    public float RenderedAlpha => renderedAlpha;
    public float RetiringAlpha => retiringFrame != null && exitActive ? retiringFrame.color.a : 0f;
    public bool IsTargetAnimating => targetAnimating;
    public bool HasRetiringVisual => exitActive;

    public void SetAnimationsPaused(bool paused) => animationsPaused = paused;

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
        bgImage.color = fill;
        if (targetFrame != null)
        {
            Color tint = outline; tint.a = renderedAlpha;
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
        levelOutgoingScale = renderedScale;
        levelOutgoingAlpha = renderedAlpha;
        NormalizePresentation(false);
        targetAnimating = false;
        levelPresentationActive = true;
        animationsPaused = true;
        smallScale = small; mediumScale = medium; fullScale = full;
        currentLitSize = nextRole;
        retiringFrame.CornerFraction = 1f;
        retiringFrame.rectTransform.localScale = Vector3.one * levelOutgoingScale;
        retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, levelOutgoingScale);
        RenderLevelPresentation(0f);
    }

    public void RenderLevelPresentation(float progress)
    {
        if (!levelPresentationActive) return;
        float t = Mathf.Clamp01(progress);
        Color outgoing = targetColor; outgoing.a = levelOutgoingAlpha * (1f - t);
        retiringFrame.color = outgoing;
        retiringFrame.gameObject.SetActive(levelOutgoingScale > 0f && outgoing.a > 0f);
        RenderTarget(GetScaleValue(currentLitSize), currentLitSize == LitSize.None ? 0f : t);
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
        }
        NormalizePresentation();
    }

    /// <summary>Assign each cell once from the latest authoritative snapshot.</summary>
    public void SetLitSize(LitSize size, bool animate)
    {
        LitSize previous = currentLitSize;
        bool wasConsumed = consumedForNextAssignment;
        consumedForNextAssignment = false;
        currentLitSize = size; // Input and the model never wait for a tween.
        if (outlineImage == null) return;

        if (!animate || !gameObject.activeInHierarchy)
        {
            CancelExit();
            SettleTarget();
            return;
        }
        if (size == LitSize.None)
        {
            if (!wasConsumed && previous != LitSize.None) BeginRetiringVisual();
            SettleTarget();
            return;
        }
        if (previous == size && !wasConsumed) return;

        bool appearing = previous == LitSize.None || wasConsumed;
        bool interrupted = targetAnimating && !appearing;
        roleTargetScale = GetScaleValue(size);
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
            // A new Large never travels through the Small/Medium size cues.
            start = size == LitSize.Small && !NeonTheme.ReducedEffects
                ? roleTargetScale * Mathf.Clamp(NeonMotion.T.targetSpawnScale, .1f, 1f)
                : roleTargetScale;
            renderedAlpha = Mathf.Clamp01(NeonMotion.T.targetSpawnAlpha);
        }
        // Reassignment may need a small immediate semantic nudge. All remaining
        // interpolation stays in this role's disjoint band, including rapid taps.
        roleStartScale = TargetRoleBands.ClampStart(size, start, smallScale, mediumScale,
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
    public void PlayConsumedFeedback()
    {
        if (outlineImage == null || currentLitSize == LitSize.None) return;
        BeginRetiringVisual();
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
        Color accent = correct ? NeonTheme.T.Primary : NeonTheme.T.Danger;
        Color impact = Color.Lerp(baseCellColor, accent, Mathf.Clamp01(intensity));
        // Merge repeated feedback from its current visible value, with no queue.
        feedbackStart = Color.Lerp(bgImage.color, impact, .9f);
        feedbackDuration = Mathf.Max(0f, correct ? NeonMotion.T.correctCellDuration : NeonMotion.T.wrongCellDuration);
        feedbackElapsed = 0f;
        feedbackActive = feedbackDuration > 0f;
        bgImage.color = feedbackActive ? feedbackStart : baseCellColor;
    }

    // Compatibility only: target-directed pulses would reveal the answer.
    public void PlayAttentionPulse(float delay = 0f) { }

    private void Update() => AdvancePresentation(NeonMotion.Delta(animationsPaused));

    /// <summary>Explicit stepping also supports deterministic lifecycle tests.</summary>
    public void AdvancePresentation(float delta)
    {
        if (animationsPaused || delta <= 0f || !gameObject.activeInHierarchy) return;
        if (targetAnimating)
        {
            roleElapsed += delta;
            float t = Mathf.Clamp01(roleElapsed / roleDuration);
            float eased = NeonMotion.Ease(t);
            RenderTarget(Mathf.Lerp(roleStartScale, roleTargetScale, eased), Mathf.Lerp(roleStartAlpha, 1f, eased));
            if (t >= 1f) SettleTarget();
        }
        if (exitActive)
        {
            exitElapsed += delta;
            float t = Mathf.Clamp01(exitElapsed / exitDuration);
            float eased = NeonMotion.Ease(t);
            float scale = exitStartScale * (1f - exitContraction * eased);
            retiringFrame.rectTransform.localScale = Vector3.one * scale;
            retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, scale);
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
            bgImage.color = Color.Lerp(feedbackStart, baseCellColor, eased);
            if (t >= 1f) feedbackActive = false;
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
        CancelExit();
        if (bgImage != null) bgImage.color = baseCellColor;
        if (settleTargets) SettleTarget();
    }

    private void OnDisable() => NormalizePresentation();

    private void BeginRetiringVisual()
    {
        if (retiringFrame == null || renderedScale <= 0f || renderedAlpha <= 0f) return;
        exitDuration = Mathf.Max(0f, NeonMotion.T.targetExitDuration);
        if (exitDuration <= 0f)
        {
            CancelExit();
            return;
        }
        exitStartScale = renderedScale;
        exitStartAlpha = Mathf.Clamp01(NeonMotion.T.targetExitAlpha) * renderedAlpha;
        if (NeonTheme.ReducedEffects) exitStartAlpha *= Mathf.Clamp01(NeonMotion.T.reducedEffectsStrength);
        exitContraction = NeonTheme.ReducedEffects ? 0f : Mathf.Clamp(NeonMotion.T.targetExitContraction, 0f, .2f);
        exitElapsed = 0f;
        exitActive = true;
        retiringFrame.rectTransform.localScale = Vector3.one * exitStartScale;
        retiringFrame.LineWidth = NeonTheme.T.targetStrokeWidth / Mathf.Max(.001f, exitStartScale);
        Color tint = NeonTheme.T.Primary;
        tint.a = exitStartAlpha;
        retiringFrame.color = tint;
        retiringFrame.gameObject.SetActive(true);
    }

    private void CancelExit()
    {
        exitActive = false;
        if (retiringFrame != null) retiringFrame.gameObject.SetActive(false);
    }

    private void SettleTarget()
    {
        targetAnimating = false;
        float scale = GetScaleValue(currentLitSize);
        RenderTarget(scale, currentLitSize == LitSize.None ? 0f : 1f);
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
            Color tint = targetColor;
            tint.a = alpha;
            targetFrame.color = tint;
        }
    }

    private float GetScaleValue(LitSize size)
    {
        switch (size)
        {
            case LitSize.Small: return smallScale;
            case LitSize.Medium: return mediumScale;
            case LitSize.Large: return fullScale;
            default: return 0f;
        }
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

    public void OnPointerDown(PointerEventData eventData) => onClicked?.Invoke(this);
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

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (NeonGridRendering.Material != null)
        {
            NeonGridRendering.Populate(mesh, rectTransform.rect, color, Mathf.Max(0, lineWidth), cornerFraction);
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
