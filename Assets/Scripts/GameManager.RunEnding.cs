using UnityEngine;

public sealed partial class GameManager
{
    private bool runEndingActive;
    private float runEndingElapsed, runEndingDuration;
    private int runEndingToken;
    private string displayedCommittedRunId;

    private static bool IsDepletionFailure(string reason) =>
        reason == "HEALTH DEPLETED" || reason == "RESERVE DEPLETED";

    private void BeginRunEndingPresentation(RunSummaryData result, bool practice = false,
        int practiceLevel = 0, int practiceCorrect = 0)
    {
        // Logic has already terminated. Retain the current hierarchy and its
        // exact visible motion pose; only decorative colors/opacity may change.
        state = FlowState.RunEnding;
        runEndingActive = true;
        runEndingToken = presentationToken;
        runEndingElapsed = 0f;
        runEndingDuration = Mathf.Max(0f, NeonMotion.T.terminalTransitionDuration);
        if (introCoroutine != null) { StopCoroutine(introCoroutine); introCoroutine = null; }
        feedbackController.ResetImmediate();
        foreach (GameSquare cell in instantiatedSquares)
            if (cell != null) cell.BeginTerminalPresentation();
        bool reserveFailure = result.reason == "RESERVE DEPLETED";
        if (reserveFailure) StopDamageFlash();
        rogueliteUI.BeginTerminalHudFeedback(reserveFailure);
        SetSquareAnimationsPaused(applicationSuspended);
        rogueliteUI.BeginRunEnding(result, practice, practiceLevel, practiceCorrect);
        RenderRunEndingPresentation(0f);
        if (runEndingDuration <= 0f) AdvanceRunEndingPresentation(0f);
    }

    private void AdvanceRunEndingPresentation(float delta)
    {
        if (!runEndingActive || state != FlowState.RunEnding || applicationSuspended) return;
        if (runEndingToken != presentationToken) { CancelRunEndingPresentation(); return; }
        runEndingElapsed += Mathf.Max(0f, delta);
        float progress = runEndingDuration <= 0f ? 1f : Mathf.Clamp01(runEndingElapsed / runEndingDuration);
        RenderRunEndingPresentation(progress);
        if (progress < 1f) return;

        // Presentation completion cannot save, bank, deduct or advance anything.
        rogueliteUI.CompleteRunEnding();
        runEndingActive = false;
        state = FlowState.RunSummary;
        StopDamageFlash();
        foreach (GameSquare cell in instantiatedSquares)
            if (cell != null) cell.EndTerminalPresentation();
        rogueliteUI.EndTerminalHudFeedback();
    }

    private void RenderRunEndingPresentation(float progress)
    {
        float start = Mathf.Clamp01(NeonMotion.T.terminalGridShutdownStartNormalized);
        float end = Mathf.Max(start, Mathf.Clamp01(NeonMotion.T.terminalGridShutdownEndNormalized));
        float shutdown = end <= start ? (progress >= end ? 1f : 0f) : Mathf.InverseLerp(start, end, progress);
        shutdown = NeonMotion.Ease(shutdown);
        foreach (GameSquare cell in instantiatedSquares)
            if (cell != null) cell.SetTerminalShutdown(shutdown);
        rogueliteUI.RenderTerminalHudShutdown(shutdown);
        rogueliteUI.RenderRunEnding(progress);
    }

    private void CancelRunEndingPresentation()
    {
        if (!runEndingActive && (rogueliteUI == null || !rogueliteUI.IsRunEndingActive)) return;
        runEndingActive = false;
        runEndingToken = -1;
        // Remove the outgoing panel before settling its decorative children.
        rogueliteUI.CancelRunEnding();
        StopDamageFlash();
        foreach (GameSquare cell in instantiatedSquares)
            if (cell != null) cell.EndTerminalPresentation();
        rogueliteUI.EndTerminalHudFeedback();
    }

    private bool RestoreCommittedRunReport()
    {
        var snapshot = saveData?.lastRunResult;
        if (HasRealRun || snapshot == null || !snapshot.reportPending) return false;
        sessionRun = null;
        activeLevel = null;
        isDebugSession = false;
        terminalRequested = true;
        state = FlowState.RunSummary;
        displayedCommittedRunId = snapshot.runId;
        if (snapshot.campaignCompleted) ConfigureSuccessButton("UPGRADES", OpenUpgradeShop, true);
        else ConfigureFailPrimary("UPGRADES", OpenUpgradeShop);
        rogueliteUI.ShowCommittedRunReportImmediate(snapshot.ToSummary());
        return true;
    }

    private void AcknowledgeCommittedRunReport()
    {
        if (string.IsNullOrEmpty(displayedCommittedRunId)) return;
        var snapshot = saveData?.lastRunResult;
        if (snapshot != null && snapshot.runId == displayedCommittedRunId && snapshot.reportPending)
        {
            snapshot.reportPending = false;
            SaveEnvelopeCritical();
        }
        displayedCommittedRunId = null;
    }

    private void OnDestroy()
    {
        runEndingActive = false;
        runEndingToken = -1;
        if (Instance == this) Instance = null;
    }
}
