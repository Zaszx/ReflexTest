using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class NeonReflexRulesEditModeTests
{
    private GameConfig config;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<GameConfig>();
        UpgradeCatalog.EnsureDefaults(config);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void SequenceInitializeAndAdvancesPreserveDistinctCellsAndExactMappings()
    {
        var random = new DeterministicRandom(731);
        for (int i = 0; i < 32; i++)
        {
            Assert.That(GridSequenceRules.Initialize(9, ref random, out GridSequenceState initial), Is.True);
            Assert.That(GridSequenceRules.Validate(initial, 9), Is.True);

            GridSequenceState normal = initial;
            Assert.That(GridSequenceRules.AdvanceNormal(ref normal, 9, ref random), Is.True);
            Assert.That(GridSequenceRules.Validate(normal, 9), Is.True);
            Assert.That(normal.medium, Is.EqualTo(initial.small));
            Assert.That(normal.large, Is.EqualTo(initial.medium));
            Assert.That(normal.small, Is.Not.EqualTo(normal.medium));
            Assert.That(normal.small, Is.Not.EqualTo(normal.large));

            GridSequenceState reverse = initial;
            Assert.That(GridSequenceRules.AdvanceReverse(ref reverse, 9, ref random), Is.True);
            Assert.That(GridSequenceRules.Validate(reverse, 9), Is.True);
            Assert.That(reverse.small, Is.EqualTo(initial.medium));
            Assert.That(reverse.medium, Is.EqualTo(initial.large));
            Assert.That(reverse.large, Is.Not.EqualTo(reverse.small));
            Assert.That(reverse.large, Is.Not.EqualTo(reverse.medium));
        }
    }

    [Test]
    public void InvalidSequenceCannotAdvance()
    {
        var random = new DeterministicRandom(1);
        GridSequenceState invalid = new GridSequenceState(1, 1, 2);
        Assert.That(GridSequenceRules.AdvanceNormal(ref invalid, 9, ref random), Is.False);
        Assert.That(GridSequenceRules.AdvanceReverse(ref invalid, 9, ref random), Is.False);
    }

    [Test]
    public void TimerDrainsNormalThenCarriesOvershootIntoReserve()
    {
        ActiveRunData run = CreateRun(normal: 5f, reserve: 10f);
        Assert.That(GameplayTimerRules.Tick(run, 2f), Is.EqualTo(GameplayTimerTransition.None));
        Assert.That(run.levelState.normalTimeRemaining, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(run.currentReserveSeconds, Is.EqualTo(10f));

        Assert.That(GameplayTimerRules.Tick(run, 4.25f), Is.EqualTo(GameplayTimerTransition.EnteredReserve));
        Assert.That(run.levelState.normalTimeRemaining, Is.Zero);
        Assert.That(run.levelState.reserveActive, Is.True);
        Assert.That(run.currentReserveSeconds, Is.EqualTo(8.75f).Within(0.0001f));
    }

    [Test]
    public void TimerExactBoundaryAndReserveDepletionAreClamped()
    {
        ActiveRunData run = CreateRun(normal: 2f, reserve: 3f);
        Assert.That(GameplayTimerRules.Tick(run, 2f), Is.EqualTo(GameplayTimerTransition.EnteredReserve));
        Assert.That(run.currentReserveSeconds, Is.EqualTo(3f));
        Assert.That(GameplayTimerRules.Tick(run, 3.5f), Is.EqualTo(GameplayTimerTransition.ReserveDepleted));
        Assert.That(run.currentReserveSeconds, Is.Zero);
    }

    [Test]
    public void TimerNoOpDeltaAndNewLevelStateDoNotAlterPersistentReserve()
    {
        ActiveRunData run = CreateRun(normal: 3f, reserve: 11f);
        run.currentHealth = 2;
        Assert.That(GameplayTimerRules.Tick(run, 0f), Is.EqualTo(GameplayTimerTransition.None));
        Assert.That(GameplayTimerRules.Tick(run, float.NaN), Is.EqualTo(GameplayTimerTransition.None));
        Assert.That(run.levelState.normalTimeRemaining, Is.EqualTo(3f));
        Assert.That(run.currentReserveSeconds, Is.EqualTo(11f));
        Assert.That(run.currentHealth, Is.EqualTo(2));

        run.levelState = new ActiveLevelStateData { normalTimeRemaining = 20f };
        Assert.That(run.levelState.normalTimeRemaining, Is.EqualTo(20f));
        Assert.That(run.currentReserveSeconds, Is.EqualTo(11f));
        Assert.That(run.currentHealth, Is.EqualTo(2));
    }

    [Test]
    public void UpgradesUseBaseValuesCapsExactCostsAndPurchaseGuards()
    {
        PlayerProfileData profile = PlayerProfileData.CreateDefault();
        UpgradeSnapshotData baseSnapshot = UpgradeCatalog.CaptureSnapshot(config, profile);
        Assert.That(baseSnapshot.maxHealth, Is.EqualTo(3));
        Assert.That(baseSnapshot.startingReserveSeconds, Is.EqualTo(15f));
        Assert.That(config.maximumHealthCap, Is.EqualTo(20));
        Assert.That(UpgradeCatalog.NextTier(config, profile, UpgradeId.MaximumHealth).cost, Is.EqualTo(30));
        Assert.That(UpgradeCatalog.NextTier(config, profile, UpgradeId.StartingReserve).cost, Is.EqualTo(40));

        Assert.That(UpgradeCatalog.TryPurchase(config, profile, UpgradeId.MaximumHealth, false, out string insufficient), Is.False);
        Assert.That(insufficient, Is.EqualTo("Insufficient coins."));
        profile.coins = 30;
        Assert.That(UpgradeCatalog.TryPurchase(config, profile, UpgradeId.MaximumHealth, false, out string purchased), Is.True, purchased);
        Assert.That(profile.coins, Is.Zero);
        Assert.That(profile.maximumHealthTier, Is.EqualTo(1));
        Assert.That(UpgradeCatalog.CaptureSnapshot(config, profile).maxHealth, Is.EqualTo(4));

        profile.coins = 1000;
        long coinsBeforeActiveRunAttempt = profile.coins;
        Assert.That(UpgradeCatalog.TryPurchase(config, profile, UpgradeId.StartingReserve, true, out string activeRun), Is.False);
        Assert.That(activeRun, Is.EqualTo("Finish or abandon the active run before purchasing upgrades."));
        Assert.That(profile.coins, Is.EqualTo(coinsBeforeActiveRunAttempt));

        profile.maximumHealthTier = config.maximumHealthUpgrade.tiers.Count;
        Assert.That(UpgradeCatalog.TryPurchase(config, profile, UpgradeId.MaximumHealth, false, out string maximum), Is.False);
        Assert.That(maximum, Is.EqualTo("Maximum tier reached."));
        Assert.That(UpgradeCatalog.CaptureSnapshot(config, profile).maxHealth, Is.EqualTo(20));
    }

    [Test]
    public void UpgradeSnapshotIsImmutableAndStabilizerNeverZerosNonzeroMotion()
    {
        PlayerProfileData profile = PlayerProfileData.CreateDefault();
        profile.gridStabilizerTier = 5;
        profile.reverseResistanceTier = 5;
        UpgradeSnapshotData captured = UpgradeCatalog.CaptureSnapshot(config, profile);
        profile.gridStabilizerTier = 0;
        profile.reverseResistanceTier = 0;

        Assert.That(captured.gridStabilizerMultiplier, Is.EqualTo(0.5f));
        Assert.That(captured.reverseCooldownBonusSeconds, Is.EqualTo(10f));
        Assert.That(120f * captured.gridStabilizerMultiplier, Is.EqualTo(60f));
        Assert.That(-120f * captured.gridStabilizerMultiplier, Is.EqualTo(-60f));
        Assert.That(0.035f * captured.gridStabilizerMultiplier, Is.GreaterThan(0f));
        Assert.That(1f * captured.gridStabilizerMultiplier, Is.GreaterThan(0f));
    }

    [Test]
    public void RandomRestoresExactlyAndValidDirectionMeetsAxisThreshold()
    {
        var random = new DeterministicRandom(98765);
        random.NextUInt();
        uint saved = random.State;
        uint expectedNext = random.NextUInt();
        var restored = new DeterministicRandom(NeonSaveService.ParseRandomState(NeonSaveService.EncodeRandomState(saved)));
        Assert.That(restored.NextUInt(), Is.EqualTo(expectedNext));

        var directionRandom = new DeterministicRandom(17);
        for (int i = 0; i < 64; i++)
        {
            Vector2 direction = directionRandom.NextValidUnitDirection(.4f);
            Assert.That(Mathf.Abs(direction.x), Is.GreaterThanOrEqualTo(.4f));
            Assert.That(Mathf.Abs(direction.y), Is.GreaterThanOrEqualTo(.4f));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.0001f));
        }
    }

    [Test]
    public void MotionUsesSignedExtentsContinuousScaleAndAxisReflections()
    {
        Vector2 extents = GridMotionMath.TransformedAabbHalfExtents(10f, 1f, 45f);
        Assert.That(extents.x, Is.EqualTo(Mathf.Sqrt(50f)).Within(.0001f));
        Assert.That(extents.y, Is.EqualTo(Mathf.Sqrt(50f)).Within(.0001f));

        float phase = 0f;
        int direction = 1;
        Assert.That(GridMotionMath.AdvanceTrianglePhase(ref phase, ref direction, .8f, 1.2f, 1f, 1f), Is.EqualTo(1.2f).Within(.0001f));
        Assert.That(direction, Is.EqualTo(-1));
        Assert.That(GridMotionMath.AdvanceTrianglePhase(ref phase, ref direction, .8f, 1.2f, 1f, .25f), Is.EqualTo(1.1f).Within(.0001f));
        Assert.That(phase, Is.EqualTo(.75f).Within(.0001f));
        Assert.That(direction, Is.EqualTo(-1), "A saved descending phase must keep descending until the minimum endpoint.");

        phase = .6f;
        direction = -1;
        Assert.That(GridMotionMath.AdvanceTrianglePhase(ref phase, ref direction, .8f, 1.2f, 1f, .2f), Is.EqualTo(.96f).Within(.0001f));
        Assert.That(phase, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(direction, Is.EqualTo(-1));

        Vector2 position = new Vector2(.9f, 0f);
        Vector2 velocity = Vector2.right;
        GridMotionMath.AdvanceStableMovement(ref position, ref velocity, 1f, .2f, Vector2.one, Vector2.zero);
        Assert.That(position.x, Is.EqualTo(.9f).Within(.0001f));
        Assert.That(velocity.x, Is.LessThan(0f));

        position = new Vector2(0f, .9f);
        velocity = Vector2.up;
        GridMotionMath.AdvanceStableMovement(ref position, ref velocity, 1f, .2f, Vector2.one, Vector2.zero);
        Assert.That(position.y, Is.EqualTo(.9f).Within(.0001f));
        Assert.That(velocity.y, Is.LessThan(0f));

        position = new Vector2(.9f, .9f);
        velocity = Vector2.one.normalized;
        GridMotionMath.AdvanceStableMovement(ref position, ref velocity, 1f, .3f, Vector2.one, Vector2.zero);
        Assert.That(velocity.x, Is.LessThan(0f));
        Assert.That(velocity.y, Is.LessThan(0f));
        Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(1f));
        Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(1f));
    }

    [Test]
    public void MotionContainsImpossibleBoundsAndZeroDeltaPausesState()
    {
        Vector2 position = new Vector2(4f, -4f);
        Vector2 direction = new Vector2(-.6f, .8f);
        Vector2 clamped = GridMotionMath.ClampPosition(position, direction, Vector2.one, new Vector2(2f, 2f), out Vector2 corrected);
        Assert.That(clamped, Is.EqualTo(Vector2.zero));
        Assert.That(corrected.x, Is.GreaterThanOrEqualTo(0f));
        Assert.That(corrected.y, Is.GreaterThanOrEqualTo(0f));

        position = new Vector2(.2f, -.3f);
        direction = new Vector2(.6f, .8f);
        Vector2 beforePosition = position;
        Vector2 beforeDirection = direction;
        GridMotionMath.AdvanceStableMovement(ref position, ref direction, 3f, 0f, Vector2.one, Vector2.zero);
        Assert.That(position, Is.EqualTo(beforePosition));
        Assert.That(direction, Is.EqualTo(beforeDirection));
    }

    [Test]
    public void SaveRoundTripsExactRunStateAndNormalizesWithoutFabricatingCoins()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var service = new NeonSaveService(directory);
            var envelope = SaveEnvelopeData.CreateDefault();
            envelope.profile.coins = 73;
            envelope.activeRun = CreateRun(12.5f, 7.25f);
            envelope.activeRun.runId = "run-roundtrip";
            envelope.activeRun.randomState = NeonSaveService.EncodeRandomState(4294967295u);
            envelope.activeRun.levelState = new ActiveLevelStateData
            {
                normalTimeRemaining = 12.5f, reserveActive = true, objectiveProgress = 4,
                smallIndex = 1, mediumIndex = 5, largeIndex = 8, reverseActive = true,
                reverseCorrectTapsRemaining = 2, reverseCooldownRemaining = 3.5f,
                rotationAngle = -37f, scalePhase = .75f, scaleDirection = -1,
                movementPosition = new Vector2(.2f, -.3f), movementDirection = new Vector2(.6f, .8f)
            };
            service.Save(envelope, config);
            SaveEnvelopeData loaded = service.Load(config);
            Assert.That(loaded.profile.coins, Is.EqualTo(73));
            Assert.That(loaded.activeRun.randomState, Is.EqualTo("4294967295"));
            Assert.That(loaded.activeRun.levelState.smallIndex, Is.EqualTo(1));
            Assert.That(loaded.activeRun.levelState.mediumIndex, Is.EqualTo(5));
            Assert.That(loaded.activeRun.levelState.largeIndex, Is.EqualTo(8));
            Assert.That(loaded.activeRun.levelState.reverseActive, Is.True);
            Assert.That(loaded.activeRun.levelState.rotationAngle, Is.EqualTo(-37f));
            Assert.That(loaded.activeRun.levelState.scalePhase, Is.EqualTo(.75f));
            Assert.That(loaded.activeRun.levelState.movementPosition, Is.EqualTo(new Vector2(.2f, -.3f)));

            loaded.profile.coins = -99;
            loaded.activeRun.pendingCoins = -3;
            loaded.activeRun.currentReserveSeconds = float.NaN;
            NeonSaveService.Normalize(loaded, config);
            Assert.That(loaded.profile.coins, Is.Zero);
            Assert.That(loaded.activeRun.pendingCoins, Is.Zero);
            Assert.That(loaded.activeRun.currentReserveSeconds, Is.Zero);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void SaveRecoversLastGoodBackupAndClearedRunStaysCleared()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var service = new NeonSaveService(directory);
            var envelope = SaveEnvelopeData.CreateDefault();
            envelope.profile.coins = 10;
            service.Save(envelope, config);
            envelope.profile.coins = 20;
            service.Save(envelope, config);
            File.WriteAllText(service.PrimaryPath, "not valid json");
            Assert.That(service.Load(config).profile.coins, Is.EqualTo(10));

            envelope.activeRun = null;
            service.Save(envelope, config);
            Assert.That(service.Load(config).activeRun, Is.Null);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void SaveUsesBackupWhenPrimaryIsParseableButMissingRequiredEnvelopeData()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var service = new NeonSaveService(directory);
            var envelope = SaveEnvelopeData.CreateDefault();
            envelope.profile.coins = 41;
            service.Save(envelope, config);
            envelope.profile.coins = 99;
            service.Save(envelope, config);
            File.WriteAllText(service.PrimaryPath, "{}");

            // A parseable but incomplete envelope is corrupt data, not a new profile.
            Assert.That(service.Load(config).profile.coins, Is.EqualTo(41));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void LegacyProgressMigrationRunsOnceAndPreservesHaptics()
    {
        const string campaignKey = "CampaignLevel";
        const string timedKey = "TimedHighScore";
        const string speedKey = "SpeedBestTime";
        const string hapticsKey = "HapticsEnabled";
        bool hadCampaign = PlayerPrefs.HasKey(campaignKey);
        bool hadTimed = PlayerPrefs.HasKey(timedKey);
        bool hadSpeed = PlayerPrefs.HasKey(speedKey);
        bool hadHaptics = PlayerPrefs.HasKey(hapticsKey);
        int oldCampaign = PlayerPrefs.GetInt(campaignKey);
        int oldTimed = PlayerPrefs.GetInt(timedKey);
        float oldSpeed = PlayerPrefs.GetFloat(speedKey);
        int oldHaptics = PlayerPrefs.GetInt(hapticsKey, 1);
        string directory = CreateTemporaryDirectory();

        try
        {
            PlayerPrefs.SetInt(campaignKey, 4);
            PlayerPrefs.SetInt(timedKey, 99);
            PlayerPrefs.SetFloat(speedKey, 1.25f);
            PlayerPrefs.SetInt(hapticsKey, 0);

            var service = new NeonSaveService(directory);
            SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
            Assert.That(service.MigrateLegacyPlayerPrefs(envelope, config), Is.True);
            Assert.That(envelope.profile.legacyMigrationComplete, Is.True);
            Assert.That(PlayerPrefs.HasKey(campaignKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(timedKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(speedKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(hapticsKey), Is.Zero);

            PlayerPrefs.SetInt(campaignKey, 7);
            Assert.That(service.MigrateLegacyPlayerPrefs(envelope, config), Is.False);
            Assert.That(PlayerPrefs.GetInt(campaignKey), Is.EqualTo(7), "A completed migration must be a no-op on later launches.");
            Assert.That(PlayerPrefs.GetInt(hapticsKey), Is.Zero);
            Assert.That(service.Load(config).profile.legacyMigrationComplete, Is.True);
        }
        finally
        {
            RestoreIntPlayerPref(campaignKey, hadCampaign, oldCampaign);
            RestoreIntPlayerPref(timedKey, hadTimed, oldTimed);
            if (hadSpeed) PlayerPrefs.SetFloat(speedKey, oldSpeed); else PlayerPrefs.DeleteKey(speedKey);
            RestoreIntPlayerPref(hapticsKey, hadHaptics, oldHaptics);
            PlayerPrefs.Save();
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void RewardBankingUsesOnlyPendingCompletedRewardsAndIsIdempotent()
    {
        SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
        envelope.profile.coins = 7;
        envelope.activeRun = CreateRun(12f, 8f);
        envelope.activeRun.runId = "reward-run";
        envelope.activeRun.currentLevelIndex = 4;
        envelope.activeRun.levelsCompleted = 4;
        envelope.activeRun.pendingCoins = 123;

        Assert.That(RunEconomyRules.TryBankAndClearActiveRun(
            envelope, "reward-run", "HEALTH DEPLETED", false, 0, 100, out RunSummaryData summary), Is.True);
        Assert.That(summary.runLevelRewards, Is.EqualTo(123));
        Assert.That(summary.totalEarned, Is.EqualTo(123));
        Assert.That(summary.levelsCompleted, Is.EqualTo(4));
        Assert.That(envelope.profile.coins, Is.EqualTo(130));
        Assert.That(envelope.activeRun, Is.Null);

        Assert.That(RunEconomyRules.TryBankAndClearActiveRun(
            envelope, "reward-run", "HEALTH DEPLETED", false, 0, 100, out _), Is.False);
        Assert.That(envelope.profile.coins, Is.EqualTo(130));
    }

    [Test]
    public void CampaignBankingAddsSeparateBonusAndSaturatesCurrency()
    {
        SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
        envelope.profile.coins = long.MaxValue - 5;
        envelope.activeRun = CreateRun(0f, 2f);
        envelope.activeRun.runId = "victory-run";
        envelope.activeRun.pendingCoins = 20;

        Assert.That(RunEconomyRules.TryBankAndClearActiveRun(
            envelope, "victory-run", "CAMPAIGN COMPLETE", true, 500, 100, out RunSummaryData summary), Is.True);
        Assert.That(summary.runLevelRewards, Is.EqualTo(20));
        Assert.That(summary.completionBonus, Is.EqualTo(500));
        Assert.That(summary.totalEarned, Is.EqualTo(520));
        Assert.That(envelope.profile.coins, Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void BankedAndClearedRunCannotReappearOrBankAgainAfterReload()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var service = new NeonSaveService(directory);
            SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
            envelope.profile.coins = 11;
            envelope.activeRun = CreateRun(4f, 6f);
            envelope.activeRun.runId = "durable-terminal-run";
            envelope.activeRun.pendingCoins = 29;

            Assert.That(RunEconomyRules.TryBankAndClearActiveRun(
                envelope, "durable-terminal-run", "RUN ABANDONED", false, 0, 100, out _), Is.True);
            service.Save(envelope, config);

            SaveEnvelopeData restored = service.Load(config);
            Assert.That(restored.profile.coins, Is.EqualTo(40));
            Assert.That(restored.profile.lastBankedRunId, Is.EqualTo("durable-terminal-run"));
            Assert.That(restored.activeRun, Is.Null);
            Assert.That(RunEconomyRules.TryBankAndClearActiveRun(
                restored, "durable-terminal-run", "RUN ABANDONED", false, 0, 100, out _), Is.False);
            Assert.That(restored.profile.coins, Is.EqualTo(40));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static ActiveRunData CreateRun(float normal, float reserve)
    {
        return new ActiveRunData
        {
            currentHealth = 3,
            currentReserveSeconds = reserve,
            upgrades = new UpgradeSnapshotData { maxHealth = 3, startingReserveSeconds = 15f, gridStabilizerMultiplier = 1f },
            levelState = new ActiveLevelStateData { normalTimeRemaining = normal }
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NeonReflexEditModeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void RestoreIntPlayerPref(string key, bool existed, int value)
    {
        if (existed) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
    }
}
