using UnityEngine;
using UnityEngine.UI;

public sealed partial class GameManager
{
    private bool levelTransitionActive;
    private float levelTransitionElapsed, levelTransitionDuration;
    private int levelTransitionResumedFrame = -1;
    private Color transitionCellFrom, transitionCellTo, transitionTargetFrom, transitionTargetTo;
    private bool transitionReusesCells;
    private CanvasGroup incomingGridGroup;
    private Vector3 transitionGridPosition, transitionGridScale;
    private Quaternion transitionGridRotation;
    private LevelTransitionAnnouncementSelector announcementSelector;

    private void ResetAnnouncementSelection()
    {
        announcementSelector = new LevelTransitionAnnouncementSelector(
            Resources.Load<LevelTransitionAnnouncementSettings>("LevelTransitionAnnouncementSettings"));
        var boundary = announcementSelector.FindBoundary(campaign);
        if (boundary.kind == MotionChallengeKind.Combined)
            Debug.LogWarning($"Level {boundary.enteringLevelNumber} introduces multiple motion modifiers together; using one combined grid challenge.");
    }

    private void BeginNextLevelTransition(int completedLevelNumber)
    {
        if (announcementSelector == null) ResetAnnouncementSelection();
        var announcement = announcementSelector.Select(campaign, completedLevelNumber, !isDebugSession && !IsOnboardingActive);
        string subline = announcement.Subline;
        LevelData incoming = campaign.GetLevel(sessionRun.currentLevelIndex + 1);
        if (incoming != null && incoming.enemies != null && incoming.enemies.enabled && !IsOnboardingActive)
        {
            bool introduced = false;
            for (int i = 0; i <= sessionRun.currentLevelIndex; i++) introduced |= campaign.GetLevel(i)?.enemies?.enabled == true;
            if (!introduced) subline = "RED SQUARES APPROACH · TAP TO DESTROY";
        }
        BeginPreparedLevelTransition(completedLevelNumber, () =>
        {
            sessionRun.currentLevelIndex++;
            InitializeCurrentLevelState();
        }, subline, NeonMotion.T.levelTransitionDuration);
    }

    // Campaign, guided next-level demo, and practice handoff share the same cell/pose morph.
    private void BeginPreparedLevelTransition(int completedLevelNumber, System.Action prepareDestination,
        string announcementSubline = null, float duration = -1f)
    {
        // Settle cosmetic recoil before sampling the authoritative grid pose.
        StopDamageFlash();
        LevelData previous = activeLevel;
        float sourceNormalTime = sessionRun.levelState.reserveActive ? 0f : sessionRun.levelState.normalTimeRemaining;
        transitionCellFrom = GameSquare.CellPalette(previous.cellColor);
        transitionTargetFrom = NeonTheme.LevelTarget(previous.outlineColor);
        Vector3 oldPosition = rotationScaleRoot.position;
        Quaternion oldRotation = rotationScaleRoot.rotation;
        Vector3 oldScale = rotationScaleRoot.lossyScale;
        float oldSide = baseGridSide;
        StopDamageFlash();
        feedbackController.ResetImmediate();
        // Replay handoffs borrow the original run; they must not consume its
        // saved pickup, recovery window or enemies.
        if (completedLevelNumber > 0) EndGameplayFeatures();
        else HideGameplayFeatureViews();
        SetSquareAnimationsPaused(true);

        // Commit the prepared next-level checkpoint before presentation starts.
        // No reward, progression or PRNG work is deferred to animation completion.
        prepareDestination();
        SaveRealRunCritical();
        state = FlowState.LevelTransition;
        levelTransitionActive = true;
        levelTransitionElapsed = 0f;
        levelTransitionDuration = Mathf.Max(0f, duration < 0 ? NeonMotion.T.practiceTransitionDuration : duration);
        levelTransitionResumedFrame = -1;
        transitionCellTo = GameSquare.CellPalette(activeLevel.cellColor);
        transitionTargetTo = NeonTheme.LevelTarget(activeLevel.outlineColor);
        transitionReusesCells = previous.gridSize == activeLevel.gridSize;

        if (!transitionReusesCells)
            PrepareGridResize(previous.gridSize, activeLevel.gridSize, oldSide);

        ConfigureGridHierarchyAndSize();
        RefitTransitionLayout();
        var sequence = ReadSequence(sessionRun.levelState);
        for (int i = 0; i < instantiatedSquares.Count; i++)
        {
            instantiatedSquares[i].BeginLevelPresentationRank(System.Array.IndexOf(sequence.targets, i), sequence.Count,
                activeLevel.smallScale, activeLevel.mediumScale, activeLevel.fullScale);
        }
        foreach (GameSquare cell in retiredGridCells)
            cell.BeginLevelPresentationRank(-1, 3, previous.smallScale, previous.mediumScale, previous.fullScale);
        ApplyGridMotionState(false, 0f);
        ConfigureGameplayFeatures();
        // Fitting can canonically clamp motion bounds; checkpoint that exact state.
        SaveRealRunCritical();
        SetSquareAnimationsPaused(true);
        incomingGridGroup = gridContentRoot.GetComponent<CanvasGroup>();
        if (incomingGridGroup == null) incomingGridGroup = gridContentRoot.gameObject.AddComponent<CanvasGroup>();
        incomingGridGroup.blocksRaycasts = false;
        incomingGridGroup.interactable = false;
        transitionGridPosition = rotationScaleRoot.InverseTransformPoint(oldPosition);
        transitionGridRotation = Quaternion.Inverse(rotationScaleRoot.rotation) * oldRotation;
        Vector3 destinationScale = rotationScaleRoot.lossyScale;
        float fitRatio = oldSide / Mathf.Max(1f, baseGridSide);
        transitionGridScale = new Vector3(oldScale.x / destinationScale.x * fitRatio,
            oldScale.y / destinationScale.y * fitRatio, 1f);
        rogueliteUI.BeginLevelTransition(completedLevelNumber, activeLevel, campaign.LevelCount, sessionRun, isDebugSession,
            sourceNormalTime, announcementSubline, levelTransitionDuration);
        RenderLevelTransition(0f);
        // Even a zero-duration configuration settles only through the flow owner.
    }

    private void RefitTransitionLayout()
    {
        int size = Mathf.Max(2, activeLevel.gridSize);
        float spacing = baseGridSide * .025f;
        GridLayoutGroup layout = gridContentRoot.GetComponent<GridLayoutGroup>();
        layout.constraintCount = size;
        layout.cellSize = Vector2.one * Mathf.Max(1f, (baseGridSide - spacing * (size - 1)) / size);
        layout.spacing = Vector2.one * spacing;
        Canvas.ForceUpdateCanvases();
    }

    private void AdvanceLevelTransition(float delta)
    {
        if (!levelTransitionActive || state != FlowState.LevelTransition || applicationSuspended ||
            Time.frameCount == levelTransitionResumedFrame) return;
        levelTransitionElapsed += Mathf.Max(0f, delta);
        float progress = levelTransitionDuration <= 0 ? 1f : Mathf.Clamp01(levelTransitionElapsed / levelTransitionDuration);
        RenderLevelTransition(progress);
        if (progress < 1f) return;
        if (!FinishLevelTransitionPresentation(true))
        {
            // Newly created Graphics may not have a raycast depth until Unity
            // registers their final geometry. Keep the announcement visible
            // while waiting for that condition, never an arbitrary frame delay.
            rogueliteUI.RenderLevelTransition(.88f, 1f);
            return;
        }
        // No second introduction, screen animation, queued input or reward callback.
        terminalRequested = false;
        state = IsOnboardingActive ? FlowState.Onboarding : FlowState.Playing;
        claimedPointers.Clear();
        claimedPointerFrame = -1;
        rogueliteUI.RefreshInputLayers();
        UpdateGameplayUI();
    }

    private void RenderLevelTransition(float progress)
    {
        float prepareDuration = Mathf.Min(levelTransitionDuration, Mathf.Max(0f, NeonMotion.T.levelTransitionPreparationDuration));
        // Even zero-duration transitions first expose the captured source pose;
        // their flow-owner advance then settles the prepared endpoint.
        float prepareProgress = progress <= 0 ? 0f : prepareDuration <= 0 ? 1f :
            Mathf.Clamp01(progress * levelTransitionDuration / prepareDuration);
        float eased = NeonMotion.Ease(prepareProgress, NeonMotion.T.levelTransitionEasing);
        Color cellColor = Color.Lerp(transitionCellFrom, transitionCellTo, eased);
        Color targetColor = Color.Lerp(transitionTargetFrom, transitionTargetTo, eased);
        foreach (GameSquare cell in instantiatedSquares)
        {
            cell.SetBasePalette(cellColor, targetColor);
            cell.RenderLevelPresentation(prepareProgress);
        }
        foreach (GameSquare cell in retiredGridCells)
        { cell.SetBasePalette(cellColor, targetColor); cell.RenderLevelPresentation(prepareProgress); }
        float morph = Mathf.InverseLerp(.18f, .72f, prepareProgress);
        float pose = transitionReusesCells ? NeonMotion.Ease(morph, NeonMotion.T.levelTransitionEasing) : RenderGridResize(morph);
        gridContentRoot.localPosition = Vector3.Lerp(transitionGridPosition, Vector3.zero, pose);
        gridContentRoot.localRotation = Quaternion.Slerp(transitionGridRotation, Quaternion.identity, pose);
        gridContentRoot.localScale = Vector3.Lerp(transitionGridScale, Vector3.one, pose);
        if (!transitionReusesCells) ContainResizingGrid();
        incomingGridGroup.alpha = 1f;
        rogueliteUI.RenderLevelTransition(progress, eased);
    }

    private bool FinishLevelTransitionPresentation(bool requireInputReady = false)
    {
        foreach (GameSquare cell in instantiatedSquares)
        {
            cell.EndLevelPresentation();
            cell.SetAnimationsPaused(true);
        }
        if (gridContentRoot != null)
        {
            gridContentRoot.localPosition = Vector3.zero;
            gridContentRoot.localRotation = Quaternion.identity;
            gridContentRoot.localScale = Vector3.one;
        }
        FinishGridResize();
        if (incomingGridGroup != null) incomingGridGroup.alpha = 1f;
        Canvas.ForceUpdateCanvases();
        if (requireInputReady)
        {
            foreach (GameSquare cell in instantiatedSquares)
                if (cell.bgImage.depth < 0 || cell.bgImage.canvasRenderer.cull) return false;
        }
        foreach (GameSquare cell in instantiatedSquares)
            cell.SetAnimationsPaused(applicationSuspended || state == FlowState.Settings);
        if (incomingGridGroup != null)
        {
            incomingGridGroup.alpha = 1f;
            incomingGridGroup.blocksRaycasts = true;
            incomingGridGroup.interactable = true;
        }
        levelTransitionActive = false;
        rogueliteUI.EndLevelTransition();
        return true;
    }

    private void CancelLevelTransitionPresentation()
    {
        if (!levelTransitionActive) return;
        presentationToken++;
        RenderLevelTransition(1f);
        FinishLevelTransitionPresentation();
    }
}
