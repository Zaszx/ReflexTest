using System;
using UnityEngine;

[Serializable] public sealed class SaveEnvelopeData
{
    public int saveVersion = 2;
    public PlayerProfileData profile = PlayerProfileData.CreateDefault();
    public ActiveRunData activeRun;
    public RunResultSnapshotData lastRunResult;
    public static SaveEnvelopeData CreateDefault() => new SaveEnvelopeData();
}

[Serializable] public sealed class PlayerProfileData
{
    public int saveVersion = 2;
    public long coins;
    public int maximumHealthTier, startingReserveTier, gridStabilizerTier, reverseResistanceTier;
    public bool legacyMigrationComplete;
    public string lastBankedRunId = string.Empty;
    public OnboardingStatus onboardingStatus;
    public bool hasStartedRealRun;
    public static PlayerProfileData CreateDefault() => new PlayerProfileData();
}

/// <summary>Durable outcome of the optional first-run practice sequence.</summary>
public enum OnboardingStatus
{
    NeverSeen,
    Skipped,
    Completed
}

[Serializable] public sealed class UpgradeSnapshotData
{
    public int maximumHealthTier, startingReserveTier, gridStabilizerTier, reverseResistanceTier;
    public int maxHealth = 3;
    public float startingReserveSeconds = 15f;
    public float gridStabilizerMultiplier = 1f;
    public float reverseCooldownBonusSeconds;
}

[Serializable] public sealed class ActiveRunData
{
    public string runId = string.Empty;
    public int runSaveVersion = 2;
    public int runSeed;
    // Stored as decimal string to retain the exact unsigned 32-bit PRNG state in JsonUtility.
    public string randomState = "1";
    public int currentLevelIndex;
    public string currentLevelId = string.Empty;
    public int currentHealth;
    public float currentReserveSeconds;
    public long pendingCoins;
    public int levelsCompleted;
    public bool betweenLevels;
    public UpgradeSnapshotData upgrades = new UpgradeSnapshotData();
    public ActiveLevelStateData levelState = new ActiveLevelStateData();
    public static ActiveRunData CreateDefault() => new ActiveRunData();
}

[Serializable] public sealed class ActiveLevelStateData
{
    public float normalTimeRemaining;
    public bool reserveActive;
    public int objectiveProgress;
    public int smallIndex = -1, mediumIndex = -1, largeIndex = -1;
    public bool reverseActive;
    public int reverseCorrectTapsRemaining;
    public float reverseCooldownRemaining;
    public float rotationAngle;
    public float scalePhase;
    public int scaleDirection = 1;
    public Vector2 movementPosition;
    public Vector2 movementDirection = Vector2.one.normalized;
    public bool rewardGranted;
}

[Serializable] public sealed class RunSummaryData
{
    public string reason = string.Empty;
    public int highestLevelEntered;
    public int levelsCompleted;
    public long runLevelRewards;
    public long completionBonus;
    public long totalEarned;
    public long newWalletBalance;
    public bool campaignCompleted;
}

/// <summary>Durable copied terminal result; presentation never receives this instance directly.</summary>
[Serializable] public sealed class RunResultSnapshotData
{
    public string runId = string.Empty;
    public string reason = string.Empty;
    public int highestLevelEntered;
    public int levelsCompleted;
    public long runLevelRewards;
    public long completionBonus;
    public long totalEarned;
    public long newWalletBalance;
    public bool campaignCompleted;
    public bool reportPending;

    public static RunResultSnapshotData FromSummary(string runId, RunSummaryData summary)
    {
        if (summary == null) return null;
        return new RunResultSnapshotData
        {
            runId = runId ?? string.Empty,
            reason = summary.reason ?? string.Empty,
            highestLevelEntered = summary.highestLevelEntered,
            levelsCompleted = summary.levelsCompleted,
            runLevelRewards = summary.runLevelRewards,
            completionBonus = summary.completionBonus,
            totalEarned = summary.totalEarned,
            newWalletBalance = summary.newWalletBalance,
            campaignCompleted = summary.campaignCompleted,
            reportPending = true
        };
    }

    public RunSummaryData ToSummary()
    {
        return new RunSummaryData
        {
            reason = reason ?? string.Empty,
            highestLevelEntered = highestLevelEntered,
            levelsCompleted = levelsCompleted,
            runLevelRewards = runLevelRewards,
            completionBonus = completionBonus,
            totalEarned = totalEarned,
            newWalletBalance = newWalletBalance,
            campaignCompleted = campaignCompleted
        };
    }
}
