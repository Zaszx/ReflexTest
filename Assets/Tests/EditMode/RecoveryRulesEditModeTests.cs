using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class RecoveryRulesEditModeTests
{
    private RecoverySettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<RecoverySettings>();
        settings.reboundDurationSeconds = 1f;
        settings.reboundMotionMultiplier = .5f;
        settings.reboundRecoveryBlendSeconds = .2f;
        settings.heartSpawnChanceAfterCorrect = 1f;
        settings.firstHeartEligibilityDelaySeconds = 5f;
        settings.heartSpawnCooldownSeconds = 15f;
        settings.maxHeartsPerLevel = 2;
        settings.heartEntranceSeconds = .08f;
        settings.heartExitSeconds = .15f;
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);

    [Test]
    public void ReboundHoldsOnlyItsExactActiveSliceAndDoesNotStack()
    {
        var rebound = new ReboundStateData();
        Assert.That(ReboundRules.TryActivate(rebound, true, true, settings), Is.True);
        Assert.That(ReboundRules.TryActivate(rebound, true, true, settings), Is.False);
        Assert.That(ReboundRules.Tick(rebound, .4f, settings).resourceDrainSeconds, Is.Zero);

        ReboundTickResult boundary = ReboundRules.Tick(rebound, .7f, settings);
        Assert.That(boundary.ended, Is.True);
        Assert.That(boundary.resourceDrainSeconds, Is.EqualTo(.1f).Within(.0001f));
        Assert.That(ReboundRules.MotionMultiplier(rebound, settings), Is.GreaterThan(.5f).And.LessThan(1f));
        ReboundRules.Tick(rebound, .2f, settings);
        Assert.That(ReboundRules.MotionMultiplier(rebound, settings), Is.EqualTo(1f).Within(.0001f));
    }

    [Test]
    public void CorrectTapEndsTimerHoldImmediatelyButPreservesMotionRecovery()
    {
        var rebound = new ReboundStateData();
        ReboundRules.TryActivate(rebound, true, true, settings);
        Assert.That(ReboundRules.EndOnCorrect(rebound, settings), Is.True);
        Assert.That(rebound.active, Is.False);
        Assert.That(ReboundRules.Tick(rebound, .05f, settings).resourceDrainSeconds, Is.EqualTo(.05f).Within(.0001f));
        Assert.That(ReboundRules.MotionMultiplier(rebound, settings), Is.GreaterThan(.5f).And.LessThan(1f));
    }

    [Test]
    public void HeartRequiresActiveGameplayEligibilityAndRetainsReservationThroughExit()
    {
        var heart = new HeartStateData();
        float cooldown = 0f;
        int spawned = 0;
        var random = new DeterministicRandom(123);
        var targets = new List<int> { 0, 1, 2 };

        Assert.That(HeartRules.TrySpawnAfterCorrect(heart, ref cooldown, ref spawned, true, 2, 3, 4, targets, ref random, settings), Is.True);
        HeartRules.SetVisibleLifetime(heart, .75f);
        int cell = heart.cellIndex;
        Assert.That(targets.Contains(cell), Is.False);
        Assert.That(heart.IsActionable, Is.True, "Hearts are collectible once their entrance is presented.");
        Assert.That(HeartRules.TryCollect(heart, cell, settings), Is.True);
        Assert.That(heart.phase, Is.EqualTo(HeartPhase.Retiring));
        Assert.That(HeartRules.ConsumesCellTap(heart, cell), Is.True);
        Assert.That(HeartRules.TryCollect(heart, cell, settings), Is.False, "Retiring visuals consume taps without granting a second heal.");
        Assert.That(HeartRules.Tick(heart, ref cooldown, .15f, settings), Is.EqualTo(HeartTickResult.Retired));
        Assert.That(heart.IsReserved, Is.False);
    }

    [Test]
    public void HeartCannotSpawnWhenTargetsFillAllCellsAndHeartRandomStreamIsStable()
    {
        var heart = new HeartStateData();
        float cooldown = 0f;
        int spawned = 0;
        var random = new DeterministicRandom(77);
        uint before = random.State;
        Assert.That(HeartRules.TrySpawnAfterCorrect(heart, ref cooldown, ref spawned, true, 1, 3, 3,
            new List<int> { 0, 1, 2 }, ref random, settings), Is.False);
        Assert.That(random.State, Is.EqualTo(before), "A capacity rejection must not perturb the heart stream.");
        Assert.That(HeartRules.DeriveLevelRandomState(99, 4), Is.EqualTo(HeartRules.DeriveLevelRandomState(99, 4)));
        Assert.That(HeartRules.DeriveLevelRandomState(99, 4), Is.Not.EqualTo(HeartRules.DeriveLevelRandomState(99, 5)));
    }

    [Test]
    public void RecoveryUpgradesCapturePermanentOwnershipAndTierLifetimeForNewRuns()
    {
        GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
        try
        {
            UpgradeCatalog.EnsureDefaults(config);
            Assert.That(config.reboundUpgrade.tiers, Has.Count.EqualTo(1));
            Assert.That(config.reboundUpgrade.tiers[0].cost, Is.EqualTo(120));
            CollectionAssert.AreEqual(new long[] { 80, 125, 185, 260, 350, 455 },
                config.healingUpgrade.tiers.ConvertAll(tier => tier.cost));

            var profile = PlayerProfileData.CreateDefault();
            profile.reboundOwned = true;
            profile.healingTier = 3;
            UpgradeSnapshotData snapshot = UpgradeCatalog.CaptureSnapshot(config, profile);

            Assert.That(snapshot.reboundOwned, Is.True);
            Assert.That(snapshot.healingTier, Is.EqualTo(3));
            Assert.That(snapshot.healingLifetimeSeconds, Is.EqualTo(1.05f).Within(.0001f));
            Assert.That(snapshot.reboundDurationSeconds, Is.EqualTo(RecoverySettingsProvider.Current.reboundDurationSeconds).Within(.0001f));
            Assert.That(snapshot.reboundMotionMultiplier, Is.EqualTo(RecoverySettingsProvider.Current.reboundMotionMultiplier).Within(.0001f));
            Assert.That(snapshot.reboundRecoveryBlendSeconds, Is.EqualTo(RecoverySettingsProvider.Current.reboundRecoveryBlendSeconds).Within(.0001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    [Test]
    public void ActiveRecoveryStateRoundTripsWithoutRefreshingAHeartOrRebound()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NeonRecoveryRules_" + Guid.NewGuid().ToString("N"));
        var config = ScriptableObject.CreateInstance<GameConfig>();
        try
        {
            var service = new NeonSaveService(directory);
            var envelope = SaveEnvelopeData.CreateDefault();
            envelope.activeRun = new ActiveRunData
            {
                runId = "recovery-round-trip",
                runSeed = 418,
                currentHealth = 2,
                currentReserveSeconds = 8f,
                upgrades = new UpgradeSnapshotData
                {
                    maxHealth = 3, reboundOwned = true, healingTier = 2, healingLifetimeSeconds = .9f,
                    reboundDurationSeconds = 1f, reboundMotionMultiplier = .5f, reboundRecoveryBlendSeconds = .2f
                },
                levelState = new ActiveLevelStateData
                {
                    normalTimeRemaining = 4f,
                    targetIndices = new[] { 0, 1, 2 },
                    rebound = new ReboundStateData { active = true, activeRemainingSeconds = .6f },
                    heart = new HeartStateData { cellIndex = 4, phase = HeartPhase.Retiring, phaseRemainingSeconds = .1f },
                    heartSpawnCooldownRemaining = 9f,
                    heartsSpawnedThisLevel = 1
                }
            };
            service.Save(envelope, config);

            ActiveLevelStateData restored = service.Load(config).activeRun.levelState;
            Assert.That(restored.rebound.active, Is.True);
            Assert.That(restored.rebound.activeRemainingSeconds, Is.EqualTo(.6f).Within(.0001f));
            Assert.That(restored.heart.cellIndex, Is.EqualTo(4));
            Assert.That(restored.heart.phase, Is.EqualTo(HeartPhase.Retiring));
            Assert.That(restored.heart.phaseRemainingSeconds, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(restored.heartsSpawnedThisLevel, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
