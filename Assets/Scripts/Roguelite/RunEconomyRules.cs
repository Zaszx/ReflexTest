using System;
using UnityEngine;

/// <summary>Pure, transaction-oriented reward banking for one active run.</summary>
public static class RunEconomyRules
{
    public static bool TryBankAndClearActiveRun(
        SaveEnvelopeData envelope,
        string expectedRunId,
        string reason,
        bool campaignCompleted,
        long completionBonus,
        int campaignLevelCount,
        out RunSummaryData summary)
    {
        summary = null;
        if (envelope == null || envelope.profile == null || envelope.activeRun == null)
            return false;

        ActiveRunData run = envelope.activeRun;
        if (string.IsNullOrEmpty(expectedRunId) || !string.Equals(run.runId, expectedRunId, StringComparison.Ordinal))
            return false;

        long levelRewards = Math.Max(0L, run.pendingCoins);
        long bonus = Math.Max(0L, completionBonus);
        long totalEarned = SaturatingAdd(levelRewards, bonus);
        long balance = Math.Max(0L, envelope.profile.coins);
        if (!string.Equals(envelope.profile.lastBankedRunId, run.runId, StringComparison.Ordinal))
        {
            balance = SaturatingAdd(balance, totalEarned);
            envelope.profile.coins = balance;
            envelope.profile.lastBankedRunId = run.runId;
        }

        summary = new RunSummaryData
        {
            reason = reason ?? string.Empty,
            // Between levels the index already points at the next, unentered level.
            highestLevelEntered = Mathf.Clamp(run.currentLevelIndex + (run.betweenLevels ? 0 : 1), 1, Mathf.Max(1, campaignLevelCount)),
            levelsCompleted = Mathf.Max(0, run.levelsCompleted),
            runLevelRewards = levelRewards,
            completionBonus = bonus,
            totalEarned = totalEarned,
            newWalletBalance = balance,
            campaignCompleted = campaignCompleted
        };
        envelope.lastRunResult = RunResultSnapshotData.FromSummary(run.runId, summary);
        envelope.activeRun = null;
        return true;
    }

    public static long SaturatingAdd(long left, long right)
    {
        left = Math.Max(0L, left);
        right = Math.Max(0L, right);
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
