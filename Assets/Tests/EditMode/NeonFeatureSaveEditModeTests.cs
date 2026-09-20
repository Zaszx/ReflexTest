using NUnit.Framework;
using UnityEngine;

public sealed class NeonFeatureSaveEditModeTests
{
    [Test]
    public void LegacyThreeIndexSaveMigratesWithoutAddingPurchasedEffects()
    {
        const string old = "{\"saveVersion\":2,\"profile\":{\"saveVersion\":2,\"coins\":271},\"activeRun\":{\"runId\":\"legacy\",\"runSaveVersion\":2,\"currentHealth\":3,\"upgrades\":{\"maxHealth\":3},\"levelState\":{\"smallIndex\":1,\"mediumIndex\":5,\"largeIndex\":7,\"normalTimeRemaining\":4.5}}}";
        var data = JsonUtility.FromJson<SaveEnvelopeData>(old);
        NeonSaveService.Normalize(data);
        CollectionAssert.AreEqual(new[] { 1, 5, 7 }, data.activeRun.levelState.targetIndices);
        Assert.That(data.activeRun.levelState.effectiveOutlineCount, Is.EqualTo(3));
        Assert.That(data.profile.coins, Is.EqualTo(271));
        Assert.That(data.activeRun.upgrades.reboundOwned, Is.False);
        Assert.That(data.activeRun.upgrades.healingTier, Is.Zero);
        Assert.That(data.activeRun.levelState.heart.IsReserved, Is.False);
        Assert.That(data.activeRun.levelState.enemies.initialized, Is.False);
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void SaveNormalizationPreservesActiveCountAndTargetEndpoints(int count)
    {
        var data = SaveEnvelopeData.CreateDefault();
        data.activeRun = new ActiveRunData();
        var targets = new int[count]; for (int i = 0; i < count; i++) targets[i] = i;
        data.activeRun.levelState.targetIndices = targets;
        NeonSaveService.Normalize(data);
        Assert.That(data.activeRun.levelState.effectiveOutlineCount, Is.EqualTo(count));
        Assert.That(data.activeRun.levelState.largeIndex, Is.EqualTo(count - 1));
        CollectionAssert.AreEqual(targets, data.activeRun.levelState.targetIndices);
    }

    [Test]
    public void AuthoredCountsAndEnemiesRespectIntroductions()
    {
        var campaign = Resources.Load<CampaignDefinition>("NeonReflexCampaign");
        for (int i = 0; i < 5; i++) Assert.That(campaign.GetLevel(i).outlineCount, Is.EqualTo(2));
        Assert.That(campaign.GetLevel(5).outlineCount, Is.EqualTo(3));
        Assert.That(campaign.GetLevel(14).outlineCount, Is.EqualTo(4));
        Assert.That(campaign.GetLevel(34).outlineCount, Is.EqualTo(5));
        for (int i = 0; i < 24; i++) Assert.That(campaign.GetLevel(i).enemies.enabled, Is.False);
        Assert.That(campaign.GetLevel(24).enemies.enabled, Is.True);
        Assert.That(campaign.GetLevel(34).enemies.enabled, Is.False);
    }
}
