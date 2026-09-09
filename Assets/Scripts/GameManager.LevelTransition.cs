using UnityEngine;
using UnityEngine.UI;

public sealed partial class GameManager
{
    private bool levelTransitionActive;
    private float levelTransitionElapsed, levelTransitionDuration;
    private int levelTransitionResumedFrame = -1;
    private int transitionInputReadyFrame = -1;
    private Color transitionCellFrom, transitionCellTo, transitionTargetFrom, transitionTargetTo;
    private bool transitionReusesCells;
    private CanvasGroup incomingGridGroup;
    private Vector3 transitionGridPosition, transitionGridScale;
    private Quaternion transitionGridRotation;

    private void BeginNextLevelTransition(int completedLevelNumber)
    {
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
        SetSquareAnimationsPaused(true);

        // Commit the prepared next-level checkpoint before presentation starts.
        // No reward, progression or PRNG work is deferred to animation completion.
        sessionRun.currentLevelIndex++;
        InitializeCurrentLevelState();
        SaveRealRunCritical();
        state = FlowState.LevelTransition;
        levelTransitionActive = true;
        levelTransitionElapsed = 0f;
        levelTransitionDuration = Mathf.Max(0f, NeonMotion.T.levelTransitionDuration);
        levelTransitionResumedFrame = -1;
        transitionCellTo = GameSquare.CellPalette(activeLevel.cellColor);
        transitionTargetTo = NeonTheme.LevelTarget(activeLevel.outlineColor);
        transitionReusesCells = previous.gridSize == activeLevel.gridSize;

        if (!transitionReusesCells)
            PrepareGridResize(previous.gridSize, activeLevel.gridSize, oldSide);

        ConfigureGridHierarchyAndSize();
        RefitTransitionLayout();
        var sequence = sessionRun.levelState;
        for (int i = 0; i < instantiatedSquares.Count; i++)
        {
            var role = i == sequence.smallIndex ? GameSquare.LitSize.Small :
                i == sequence.mediumIndex ? GameSquare.LitSize.Medium :
                i == sequence.largeIndex ? GameSquare.LitSize.Large : GameSquare.LitSize.None;
            instantiatedSquares[i].BeginLevelPresentation(role, activeLevel.smallScale, activeLevel.mediumScale, activeLevel.fullScale);
        }
        ApplyGridMotionState(false, 0f);
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
        rogueliteUI.BeginLevelTransition(completedLevelNumber, activeLevel, campaign.LevelCount, sessionRun, isDebugSession, sourceNormalTime);
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
        FinishLevelTransitionPresentation();
        // No second introduction, screen animation, queued input or reward callback.
        terminalRequested = false;
        transitionInputReadyFrame = Time.frameCount;
        state = FlowState.Playing;
        UpdateGameplayUI();
    }

    private void RenderLevelTransition(float progress)
    {
        float eased = NeonMotion.Ease(progress, NeonMotion.T.levelTransitionEasing);
        Color cellColor = Color.Lerp(transitionCellFrom, transitionCellTo, eased);
        Color targetColor = Color.Lerp(transitionTargetFrom, transitionTargetTo, eased);
        foreach (GameSquare cell in instantiatedSquares)
        {
            cell.SetBasePalette(cellColor, targetColor);
            cell.RenderLevelPresentation(eased);
        }
        foreach (GameSquare cell in retiredGridCells) cell.SetBasePalette(cellColor, targetColor);
        float pose = transitionReusesCells ? eased : RenderGridResize(progress);
        gridContentRoot.localPosition = Vector3.Lerp(transitionGridPosition, Vector3.zero, pose);
        gridContentRoot.localRotation = Quaternion.Slerp(transitionGridRotation, Quaternion.identity, pose);
        gridContentRoot.localScale = Vector3.Lerp(transitionGridScale, Vector3.one, pose);
        if (!transitionReusesCells) ContainResizingGrid();
        incomingGridGroup.alpha = 1f;
        rogueliteUI.RenderLevelTransition(progress, eased);
    }

    private void FinishLevelTransitionPresentation()
    {
        foreach (GameSquare cell in instantiatedSquares)
        {
            cell.EndLevelPresentation();
            cell.SetAnimationsPaused(applicationSuspended || state == FlowState.Settings);
        }
        if (gridContentRoot != null)
        {
            gridContentRoot.localPosition = Vector3.zero;
            gridContentRoot.localRotation = Quaternion.identity;
            gridContentRoot.localScale = Vector3.one;
        }
        if (incomingGridGroup != null)
        {
            incomingGridGroup.alpha = 1f;
            incomingGridGroup.blocksRaycasts = true;
            incomingGridGroup.interactable = true;
        }
        FinishGridResize();
        levelTransitionActive = false;
        rogueliteUI.EndLevelTransition();
    }

    private void CancelLevelTransitionPresentation()
    {
        if (!levelTransitionActive) return;
        presentationToken++;
        RenderLevelTransition(1f);
        FinishLevelTransitionPresentation();
    }
}
