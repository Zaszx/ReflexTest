using UnityEngine;

public sealed partial class RogueliteUIController
{
    private TerminalFreshInputGate terminalInputGate;
    private RunSummaryData terminalSummary;
    private bool terminalTimelineActive;
    private bool terminalPresentationReady;
    private float terminalGameplayOpacity = 1;
    private GameObject terminalReportPanel;

    public bool IsRunEndingActive => terminalTimelineActive;
    public bool IsRunEndingPresentationReady => terminalPresentationReady;
    public bool IsRunReportInputReady => terminalInputGate != null && terminalInputGate.Ready;
    public bool IsRunReportAwaitingRelease => terminalInputGate != null && terminalInputGate.AwaitingRelease;

    /// <summary>
    /// Starts presentation from copied values after GameManager has already committed
    /// the terminal outcome. This method never reads or mutates the active run.
    /// </summary>
    public void BeginRunEnding(RunSummaryData summary, bool practice = false,
        int practiceLevel = 0, int practiceCorrect = 0)
    {
        RunSummaryData copied = CopyRunSummary(summary);
        CancelRunEnding();
        terminalSummary = copied;
        terminalTimelineActive = true;
        terminalPresentationReady = false;
        terminalReportPanel = gm.failPanel;

        PopulateTerminalReport(copied, practice, practiceLevel, practiceCorrect);
        EnsureTerminalInputGate().BeginGate();

        NeonScreenMotion gameplayMotion = ScreenMotion(gm.gameplayPanel);
        NeonScreenMotion reportMotion = ScreenMotion(gm.failPanel);
        terminalGameplayOpacity = gameplayMotion == null ? 1 : gameplayMotion.Opacity;
        gameplayMotion?.BeginExternalControl(true, true);
        reportMotion?.BeginExternalControl(true, false);
        reportMotion?.SetExternalOpacity(0);

        failView.motion.BeginExternalTimeline();
        failView.motion.RenderExternalTimeline(0, 0, 0, NeonMotion.T.terminalReportTravel);
        gm.failPanel.transform.SetAsLastSibling();
        RefreshInputLayers();
    }

    /// <summary>Renders an idempotent frame of the GameManager-owned terminal clock.</summary>
    public void RenderRunEnding(float normalized)
    {
        if (!terminalTimelineActive) return;
        float t = Mathf.Clamp01(normalized);
        NeonMotionSettings settings = NeonMotion.T;

        float report = Stage(t, settings.terminalReportRevealStartNormalized,
            settings.terminalReportRevealEndNormalized);
        float details = Stage(t, settings.terminalReportDetailsStartNormalized,
            settings.terminalReportDetailsEndNormalized);
        float actions = Stage(t, settings.terminalReportActionsStartNormalized,
            settings.terminalReportActionsEndNormalized);

        // Grid colors own the power-down cue. The gameplay screen itself remains
        // visible in its final pose until the incoming opaque report covers it.
        ScreenMotion(gm.gameplayPanel)?.SetExternalOpacity(terminalGameplayOpacity);
        ScreenMotion(gm.failPanel)?.SetExternalOpacity(NeonMotion.Ease(report));
        failView.motion.RenderExternalTimeline(report, details, actions, settings.terminalReportTravel);
    }

    public void CompleteRunEnding()
    {
        if (!terminalTimelineActive) return;
        RenderRunEnding(1);
        terminalTimelineActive = false;
        terminalPresentationReady = true;

        failView.motion.CompleteExternalTimeline();
        ScreenMotion(gm.gameplayPanel)?.EndExternalControl(false);
        ScreenMotion(gm.failPanel)?.EndExternalControl(true);
        EnsureTerminalInputGate().MarkPresentationReady();
        RefreshInputLayers();
    }

    /// <summary>
    /// Cancels only terminal presentation ownership. Generic navigation calls this
    /// before choosing its destination, preventing hidden panels or stale renders.
    /// </summary>
    public void CancelRunEnding()
    {
        bool hadTerminalOwnership = terminalTimelineActive ||
            (terminalInputGate != null && terminalInputGate.Active);
        terminalTimelineActive = false;
        terminalPresentationReady = false;
        terminalSummary = null;
        if (!hadTerminalOwnership) return;

        // Remove both externally-owned layers before normalizing child motion, so
        // cancellation cannot expose a one-frame pose or opacity snap.
        if (gm != null)
        {
            ScreenMotion(gm.gameplayPanel)?.EndExternalControl(false, true);
            ScreenMotion(gm.failPanel)?.EndExternalControl(false);
        }
        failView?.motion?.CancelExternalTimeline();
        if (terminalInputGate != null) terminalInputGate.CancelGate();
        terminalReportPanel = null;
    }

    /// <summary>Restores a committed report directly, without replaying terminal damage.</summary>
    public void ShowCommittedRunReportImmediate(RunSummaryData summary)
    {
        RunSummaryData copied = CopyRunSummary(summary);
        CancelRunEnding();
        if (copied.campaignCompleted)
        {
            // Reuse the established campaign-complete value binding, then settle
            // its optional entrance in the same frame.
            ShowRunSummary(copied);
            successView.motion.NormalizeImmediate();
            terminalReportPanel = gm.successPanel;
        }
        else
        {
            PopulateTerminalReport(copied, false, 0, 0);
            failView.motion.CancelExternalTimeline();
            terminalReportPanel = gm.failPanel;
        }
        terminalSummary = copied;

        SetScreenImmediate(gm.mainMenuPanel, false);
        SetScreenImmediate(gm.gameplayPanel, false);
        SetScreenImmediate(gm.successPanel, copied.campaignCompleted);
        SetScreenImmediate(gm.settingsPanel, false);
        SetScreenImmediate(gm.failPanel, !copied.campaignCompleted);
        terminalReportPanel.transform.SetAsLastSibling();
        terminalPresentationReady = true;
        EnsureTerminalInputGate().BeginGate();
        terminalInputGate.MarkPresentationReady();
        RefreshInputLayers();
    }

    /// <summary>Hook used by the controller's centralized input-layer calculation.</summary>
    public bool TerminalAllowsInput(GameObject panel)
    {
        if (terminalInputGate == null || !terminalInputGate.Active) return true;
        terminalInputGate.SetPaused(IsSettingsVisible);
        if (panel == gm.gameplayPanel) return false;
        if (panel == terminalReportPanel) return terminalInputGate.Ready;
        return true;
    }

    private void PopulateTerminalReport(RunSummaryData summary, bool practice,
        int practiceLevel, int practiceCorrect)
    {
        ResultView view = failView;
        string reason = summary.reason ?? string.Empty;
        SetReportCause(view, reason, practice);
        if (practice)
        {
            view.eyebrow.text = "SANDBOX / PRACTICE REPORT";
            view.amountLabel.text = "PRACTICE / NO REWARDS";
            view.amount.text = "NO COINS BANKED";
            view.amount.fontSizeMax = 48;
            view.amount.color = ReportIce;
            view.leftLabel.text = "LEVEL PRACTICED";
            view.leftValue.text = Mathf.Max(0, practiceLevel).ToString("00");
            view.rightLabel.text = "CORRECT TARGETS";
            view.rightValue.text = Mathf.Max(0, practiceCorrect).ToString("00");
            view.footer.text = "No coins, upgrades or saved run state changed.";
            return;
        }

        view.eyebrow.text = "CAMPAIGN / RUN REPORT";
        view.amountLabel.text = "COINS EARNED / BANKED";
        view.amount.text = $"+{summary.totalEarned:N0}";
        view.amount.fontSizeMax = 150;
        view.amount.color = ReportLime;
        view.leftLabel.text = "LEVEL REACHED";
        view.leftValue.text = summary.highestLevelEntered.ToString("00");
        view.rightLabel.text = "LEVELS COMPLETED";
        view.rightValue.text = summary.levelsCompleted.ToString("00");
        view.footer.text = $"Permanent balance: <color=#D7EBF9><b>{summary.newWalletBalance:N0}</b></color> coins";
    }

    private TerminalFreshInputGate EnsureTerminalInputGate()
    {
        if (terminalInputGate == null)
        {
            terminalInputGate = GetComponent<TerminalFreshInputGate>();
            if (terminalInputGate == null) terminalInputGate = gameObject.AddComponent<TerminalFreshInputGate>();
            terminalInputGate.BecameReady += RefreshInputLayers;
        }
        return terminalInputGate;
    }

    private NeonScreenMotion ScreenMotion(GameObject panel)
    {
        return panel != null && screens.TryGetValue(panel, out NeonScreenMotion motion) ? motion : null;
    }

    private void SetScreenImmediate(GameObject panel, bool show)
    {
        ScreenMotion(panel)?.SetImmediate(show);
    }

    private static RunSummaryData CopyRunSummary(RunSummaryData source)
    {
        if (source == null) source = new RunSummaryData();
        return new RunSummaryData
        {
            reason = source.reason ?? string.Empty,
            highestLevelEntered = source.highestLevelEntered,
            levelsCompleted = source.levelsCompleted,
            runLevelRewards = source.runLevelRewards,
            completionBonus = source.completionBonus,
            totalEarned = source.totalEarned,
            newWalletBalance = source.newWalletBalance,
            campaignCompleted = source.campaignCompleted
        };
    }

    private static float Stage(float value, float start, float end)
    {
        start = Mathf.Clamp01(start);
        end = Mathf.Clamp01(end);
        if (end <= start) return value >= end ? 1 : 0;
        return Mathf.Clamp01((value - start) / (end - start));
    }
}
