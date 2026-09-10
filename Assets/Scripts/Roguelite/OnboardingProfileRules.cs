using System;

/// <summary>Pure persistence rules for deciding whether the opening practice should be offered.</summary>
public static class OnboardingProfileRules
{
    public static bool ShouldOffer(SaveEnvelopeData envelope)
    {
        if (envelope == null || envelope.profile == null || envelope.profile.onboardingStatus != OnboardingStatus.NeverSeen)
            return false;

        PlayerProfileData profile = envelope.profile;
        return envelope.activeRun == null &&
               envelope.lastRunResult == null &&
               string.IsNullOrEmpty(profile.lastBankedRunId) &&
               profile.coins <= 0 &&
               profile.maximumHealthTier <= 0 &&
               profile.startingReserveTier <= 0 &&
               profile.gridStabilizerTier <= 0 &&
               profile.reverseResistanceTier <= 0 &&
               !profile.hasStartedRealRun;
    }

    public static void RecordOutcome(PlayerProfileData profile, bool skipped)
    {
        if (profile == null)
            return;

        // A later replay can be skipped without erasing proof that it was completed.
        if (skipped && profile.onboardingStatus == OnboardingStatus.Completed)
            return;

        profile.onboardingStatus = skipped ? OnboardingStatus.Skipped : OnboardingStatus.Completed;
    }

    public static void MarkStartedRealRun(PlayerProfileData profile)
    {
        if (profile != null)
            profile.hasStartedRealRun = true;
    }
}
