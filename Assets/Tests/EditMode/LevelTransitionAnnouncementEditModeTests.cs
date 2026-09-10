using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LevelTransitionAnnouncementEditModeTests
{
    private LevelTransitionAnnouncementSettings settings;
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<LevelTransitionAnnouncementSettings>();
        settings.motivationLines = new List<string>
        {
            "Good job!", "Keep going!", "Nicely done!", "Keep it up!", "Stay sharp!",
            "You’ve got this!", "Keep the rhythm!", "On to the next!", "Stay focused!", "Next challenge!"
        };
        settings.entranceDuration = .2f;
        settings.holdDuration = 1.4f;
        settings.exitDuration = .2f;
        created.Add(settings);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--) Object.DestroyImmediate(created[i]);
    }

    [Test]
    public void OrdinaryLinesUseEveryConfiguredEntryBeforeRepeatingAndAvoidBoundaryRepeat()
    {
        CampaignDefinition campaign = Campaign(12);
        LevelTransitionAnnouncementSelector selector = new LevelTransitionAnnouncementSelector(settings, 73);
        var firstBag = new HashSet<string>();
        string previous = null;
        for (int i = 1; i <= 20; i++)
        {
            LevelTransitionAnnouncement selected = selector.Select(campaign, i, true);
            Assert.That(selected.IsMotionChallenge, Is.False);
            Assert.That(selected.Subline, Is.Not.EqualTo(previous));
            if (i <= settings.motivationLines.Count) firstBag.Add(selected.Subline);
            previous = selected.Subline;
        }
        Assert.That(firstBag.SetEquals(settings.motivationLines), Is.True);
    }

    [Test]
    public void SpecialChallengeIsAtEarliestValidMotionBoundaryAndDoesNotConsumeTheBag()
    {
        CampaignDefinition campaign = Campaign(4);
        campaign.GetLevel(1).reverseEnabled = true;
        campaign.GetLevel(2).rotateSpeed = 70f;
        campaign.GetLevel(3).scaleEnabled = true;
        campaign.GetLevel(3).minimumGridScale = .8f;
        campaign.GetLevel(3).maximumGridScale = 1f;
        campaign.GetLevel(3).scaleCycleDuration = 3f;

        LevelTransitionAnnouncementSelector selector = new LevelTransitionAnnouncementSelector(settings, 19);
        MotionChallengeBoundary boundary = selector.FindBoundary(campaign);
        Assert.That(boundary.completedLevelNumber, Is.EqualTo(2));
        Assert.That(boundary.enteringLevelNumber, Is.EqualTo(3));
        Assert.That(boundary.kind, Is.EqualTo(MotionChallengeKind.Rotation));

        LevelTransitionAnnouncement special = selector.Select(campaign, 2, true);
        Assert.That(special.IsMotionChallenge, Is.True);
        Assert.That(special.Subline, Is.EqualTo("Can you keep up as it rotates?"));
        Assert.That(settings.PhaseDuration, Is.EqualTo(1.8f).Within(.0001f));

        LevelTransitionAnnouncement afterSpecial = selector.Select(campaign, 3, true);
        LevelTransitionAnnouncementSelector untouched = new LevelTransitionAnnouncementSelector(settings, 19);
        LevelTransitionAnnouncement firstOrdinary = untouched.Select(Campaign(4), 3, true);
        Assert.That(afterSpecial.Subline, Is.EqualTo(firstOrdinary.Subline), "The special line must not dequeue an ordinary motivation.");
    }

    [Test]
    public void ValidityAndEdgeCasesChooseOneSafeChallengeOrNone()
    {
        CampaignDefinition moveFirst = Campaign(3);
        moveFirst.GetLevel(1).movementEnabled = true;
        moveFirst.GetLevel(1).movementSpeedNormalized = .03f;
        Assert.That(new LevelTransitionAnnouncementSelector(settings, 1).FindBoundary(moveFirst).kind, Is.EqualTo(MotionChallengeKind.Move));

        CampaignDefinition scaleFirst = Campaign(3);
        scaleFirst.GetLevel(1).scaleEnabled = true;
        scaleFirst.GetLevel(1).minimumGridScale = .8f;
        scaleFirst.GetLevel(1).maximumGridScale = 1.05f;
        scaleFirst.GetLevel(1).scaleCycleDuration = 4f;
        Assert.That(new LevelTransitionAnnouncementSelector(settings, 1).FindBoundary(scaleFirst).kind, Is.EqualTo(MotionChallengeKind.Scale));

        CampaignDefinition combined = Campaign(3);
        combined.GetLevel(1).rotateSpeed = 60f;
        combined.GetLevel(1).movementEnabled = true;
        combined.GetLevel(1).movementSpeedNormalized = .03f;
        MotionChallengeBoundary combinedBoundary = new LevelTransitionAnnouncementSelector(settings, 1).FindBoundary(combined);
        Assert.That(combinedBoundary.kind, Is.EqualTo(MotionChallengeKind.Combined));
        Assert.That(new LevelTransitionAnnouncementSelector(settings, 1).Select(combined, 1, true).Subline,
            Is.EqualTo("Can you keep up with the changing grid?"));

        CampaignDefinition stationary = Campaign(3);
        stationary.GetLevel(1).movementEnabled = true;
        stationary.GetLevel(1).movementSpeedNormalized = 0f;
        stationary.GetLevel(2).scaleEnabled = true;
        stationary.GetLevel(2).minimumGridScale = 1f;
        stationary.GetLevel(2).maximumGridScale = 1f;
        Assert.That(new LevelTransitionAnnouncementSelector(settings, 1).FindBoundary(stationary).Exists, Is.False);

        CampaignDefinition motionAtFirstLevel = Campaign(3);
        motionAtFirstLevel.GetLevel(0).rotateSpeed = 60f;
        Assert.That(new LevelTransitionAnnouncementSelector(settings, 1).FindBoundary(motionAtFirstLevel).Exists, Is.False);
    }

    [Test]
    public void NonCampaignEntriesHaveNoMotivationAndDoNotAdvanceThePresentationBag()
    {
        CampaignDefinition campaign = Campaign(4);
        LevelTransitionAnnouncementSelector selector = new LevelTransitionAnnouncementSelector(settings, 442);
        LevelTransitionAnnouncement nonCampaign = selector.Select(campaign, 3, false);
        Assert.That(nonCampaign.HasSubline, Is.False);

        LevelTransitionAnnouncement afterNonCampaign = selector.Select(campaign, 3, true);
        LevelTransitionAnnouncementSelector untouched = new LevelTransitionAnnouncementSelector(settings, 442);
        LevelTransitionAnnouncement expected = untouched.Select(campaign, 3, true);
        Assert.That(afterNonCampaign.Subline, Is.EqualTo(expected.Subline));
    }

    [Test]
    public void CampaignReorderingMovesTheSelectedBoundaryAndNewRunsRepeatIt()
    {
        CampaignDefinition campaign = Campaign(4);
        campaign.GetLevel(2).rotateSpeed = 60;
        campaign.GetLevel(3).movementEnabled = true;
        campaign.GetLevel(3).movementSpeedNormalized = .1f;
        var selector = new LevelTransitionAnnouncementSelector(settings, 1);
        Assert.AreEqual(MotionChallengeKind.Rotation, selector.FindBoundary(campaign).kind);
        LevelData movement = campaign.levels[3];
        campaign.levels.RemoveAt(3);
        campaign.levels.Insert(1, movement);
        Assert.AreEqual(MotionChallengeKind.Move, selector.FindBoundary(campaign).kind);
        Assert.AreEqual(1, selector.FindBoundary(campaign).completedLevelNumber);
        Assert.AreEqual(settings.moveChallenge, selector.Select(campaign, 1, true).Subline);
        Assert.AreEqual(settings.moveChallenge, new LevelTransitionAnnouncementSelector(settings, 2).Select(campaign, 1, true).Subline);
    }

    [Test]
    public void AuthoredCampaignChallengeIsAfterLevelTwoBeforeRotationLevelThree()
    {
        CampaignDefinition campaign = Resources.Load<CampaignDefinition>("NeonReflexCampaign");
        Assert.That(campaign, Is.Not.Null);
        MotionChallengeBoundary boundary = new LevelTransitionAnnouncementSelector(settings, 4).FindBoundary(campaign);
        Assert.That(boundary.completedLevelNumber, Is.EqualTo(2));
        Assert.That(boundary.enteringLevelNumber, Is.EqualTo(3));
        Assert.That(boundary.kind, Is.EqualTo(MotionChallengeKind.Rotation));
    }

    private CampaignDefinition Campaign(int levelCount)
    {
        CampaignDefinition campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
        created.Add(campaign);
        for (int i = 0; i < levelCount; i++)
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            level.levelNumber = i + 1;
            level.stableId = "announcement-test-" + i;
            level.rotateSpeed = 0f;
            level.scaleEnabled = false;
            level.movementEnabled = false;
            campaign.levels.Add(level);
            created.Add(level);
        }
        return campaign;
    }
}
