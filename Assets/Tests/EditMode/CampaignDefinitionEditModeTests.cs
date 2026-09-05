using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CampaignDefinitionEditModeTests
{
    [Test]
    public void InvalidCampaignIsRejectedBeforeGameplayCanStart()
    {
        CampaignDefinition invalid = ScriptableObject.CreateInstance<CampaignDefinition>();
        LevelData onlyLevel = ScriptableObject.CreateInstance<LevelData>();
        try
        {
            invalid.levels.Add(onlyLevel);
            CampaignValidationReport report = CampaignValidation.Validate(invalid);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.errors, Does.Contain("Campaign must contain at least 100 configured levels for this release."));

            invalid.levels.Add(null);
            report = CampaignValidation.Validate(invalid);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.errors.Exists(error => error.Contains("missing asset")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(onlyLevel);
            Object.DestroyImmediate(invalid);
        }
    }

    [Test]
    public void ActualCampaignHasOrderedIntroductionsAndOnlyCombinesIntroducedModifiers()
    {
        CampaignDefinition campaign = Resources.Load<CampaignDefinition>("NeonReflexCampaign");
        Assert.That(campaign, Is.Not.Null, "Resources/NeonReflexCampaign must be the campaign used at runtime.");
        CampaignValidationReport report = CampaignValidation.Validate(campaign);
        Assert.That(report.errors, Is.Empty, string.Join("\n", report.errors));
        Assert.That(campaign.LevelCount, Is.GreaterThanOrEqualTo(100));
        Assert.That(campaign.GetLevel(campaign.LevelCount - 1), Is.Not.Null, "Final level must be list-driven rather than a fixed index.");

        var ids = new HashSet<string>();
        var numbers = new HashSet<int>();
        bool reverseIntroduced = false;
        bool rotationIntroduced = false;
        bool scaleIntroduced = false;
        bool movementIntroduced = false;
        for (int i = 0; i < campaign.LevelCount; i++)
        {
            LevelData level = campaign.GetLevel(i);
            Assert.That(level, Is.Not.Null, "Missing level at campaign index " + i);
            Assert.That(ids.Add(level.stableId), Is.True, "Duplicate level id at " + i);
            Assert.That(numbers.Add(level.levelNumber), Is.True, "Duplicate level number at " + i);
            Assert.That(level.levelNumber, Is.EqualTo(i + 1));
            Assert.That(level.gridSize * level.gridSize, Is.GreaterThanOrEqualTo(3));
            Assert.That(level.requiredCorrectClicks, Is.GreaterThan(0));
            Assert.That(level.timeLimit, Is.GreaterThan(0f));
            Assert.That(level.completionCoinReward, Is.GreaterThan(0));

            bool rotation = !Mathf.Approximately(level.rotateSpeed, 0f);
            if (i >= 5)
            {
                int activeModifierCount = (level.reverseEnabled ? 1 : 0) + (rotation ? 1 : 0) + (level.scaleEnabled ? 1 : 0) + (level.movementEnabled ? 1 : 0);
                if (activeModifierCount > 1)
                {
                    Assert.That(level.reverseEnabled, Is.False.Or.EqualTo(reverseIntroduced));
                    Assert.That(rotation, Is.False.Or.EqualTo(rotationIntroduced));
                    Assert.That(level.scaleEnabled, Is.False.Or.EqualTo(scaleIntroduced));
                    Assert.That(level.movementEnabled, Is.False.Or.EqualTo(movementIntroduced));
                }
            }
            reverseIntroduced |= level.reverseEnabled;
            rotationIntroduced |= rotation;
            scaleIntroduced |= level.scaleEnabled;
            movementIntroduced |= level.movementEnabled;
        }

        Assert.That(campaign.GetLevel(0).reverseEnabled, Is.False);
        Assert.That(campaign.GetLevel(0).rotateSpeed, Is.Zero);
        Assert.That(campaign.GetLevel(0).scaleEnabled, Is.False);
        Assert.That(campaign.GetLevel(0).movementEnabled, Is.False);
        Assert.That(campaign.GetLevel(1).reverseEnabled, Is.True);
        Assert.That(campaign.GetLevel(2).rotateSpeed, Is.Not.Zero);
        Assert.That(campaign.GetLevel(3).scaleEnabled, Is.True);
        Assert.That(campaign.GetLevel(4).movementEnabled, Is.True);
    }
}
