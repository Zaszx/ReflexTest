using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class OnboardingProfileEditModeTests
{
    [Test]
    public void ShouldOfferOnlyForACompletelyFreshProfile()
    {
        SaveEnvelopeData fresh = SaveEnvelopeData.CreateDefault();
        fresh.profile.legacyMigrationComplete = true;
        Assert.That(OnboardingProfileRules.ShouldOffer(fresh), Is.True);

        fresh.profile.onboardingStatus = OnboardingStatus.Skipped;
        Assert.That(OnboardingProfileRules.ShouldOffer(fresh), Is.False);

        fresh.profile.onboardingStatus = OnboardingStatus.NeverSeen;
        fresh.profile.hasStartedRealRun = true;
        Assert.That(OnboardingProfileRules.ShouldOffer(fresh), Is.False);
    }

    [Test]
    public void PriorProgressionEvidenceExemptsOldSaves()
    {
        AssertProgressionExempts(envelope => envelope.activeRun = ActiveRunData.CreateDefault());
        AssertProgressionExempts(envelope => envelope.lastRunResult = new RunResultSnapshotData());
        AssertProgressionExempts(envelope => envelope.profile.lastBankedRunId = "previous-run");
        AssertProgressionExempts(envelope => envelope.profile.coins = 1);
        AssertProgressionExempts(envelope => envelope.profile.maximumHealthTier = 1);
        AssertProgressionExempts(envelope => envelope.profile.startingReserveTier = 1);
        AssertProgressionExempts(envelope => envelope.profile.gridStabilizerTier = 1);
        AssertProgressionExempts(envelope => envelope.profile.reverseResistanceTier = 1);
    }

    [Test]
    public void OutcomeRoundTripsAndSkippedReplayDoesNotDowngradeCompleted()
    {
        PlayerProfileData profile = PlayerProfileData.CreateDefault();
        OnboardingProfileRules.RecordOutcome(profile, skipped: false);
        OnboardingProfileRules.RecordOutcome(profile, skipped: true);
        OnboardingProfileRules.MarkStartedRealRun(profile);

        string directory = CreateTemporaryDirectory();
        try
        {
            SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
            envelope.profile = profile;
            var service = new NeonSaveService(directory);
            service.Save(envelope);
            PlayerProfileData restored = service.Load().profile;

            Assert.That(restored.onboardingStatus, Is.EqualTo(OnboardingStatus.Completed));
            Assert.That(restored.hasStartedRealRun, Is.True);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void RecordingOutcomeDoesNotChangeRealProgressionData()
    {
        SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
        envelope.profile.coins = 321;
        envelope.profile.maximumHealthTier = 2;
        envelope.profile.startingReserveTier = 3;
        envelope.profile.gridStabilizerTier = 1;
        envelope.profile.reverseResistanceTier = 4;
        envelope.activeRun = ActiveRunData.CreateDefault();
        envelope.activeRun.runId = "active-run";
        envelope.activeRun.currentHealth = 2;
        envelope.activeRun.pendingCoins = 99;
        string activeRunBefore = JsonUtility.ToJson(envelope.activeRun);

        OnboardingProfileRules.RecordOutcome(envelope.profile, skipped: false);

        Assert.That(envelope.profile.coins, Is.EqualTo(321));
        Assert.That(envelope.profile.maximumHealthTier, Is.EqualTo(2));
        Assert.That(envelope.profile.startingReserveTier, Is.EqualTo(3));
        Assert.That(envelope.profile.gridStabilizerTier, Is.EqualTo(1));
        Assert.That(envelope.profile.reverseResistanceTier, Is.EqualTo(4));
        Assert.That(JsonUtility.ToJson(envelope.activeRun), Is.EqualTo(activeRunBefore));
    }

    private static void AssertProgressionExempts(Action<SaveEnvelopeData> applyEvidence)
    {
        SaveEnvelopeData envelope = SaveEnvelopeData.CreateDefault();
        applyEvidence(envelope);
        Assert.That(OnboardingProfileRules.ShouldOffer(envelope), Is.False);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NeonReflexOnboardingTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
