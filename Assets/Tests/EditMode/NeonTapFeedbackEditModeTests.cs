using NUnit.Framework;
using UnityEngine;

public sealed class NeonTapFeedbackEditModeTests
{
    [Test]
    public void SharedEdgePriorityNeverLetsSuccessOrDamageOverwriteTerminal()
    {
        Assert.IsTrue(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.None, TapFeedbackPriority.Success));
        Assert.IsTrue(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.Success, TapFeedbackPriority.Damage));
        Assert.IsTrue(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.Damage, TapFeedbackPriority.Damage));
        Assert.IsFalse(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.Damage, TapFeedbackPriority.Success));
        Assert.IsFalse(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.Terminal, TapFeedbackPriority.Damage));
        Assert.IsFalse(GameplayFeedbackController.CanReplaceTapEdge(TapFeedbackPriority.Terminal, TapFeedbackPriority.Success));
    }

    [Test]
    public void RecoilRefreshStartsAtCurrentOffsetStaysBoundedAndReturnsExactlyToRest()
    {
        const float limit = .004f;
        float start = 0;
        for (int hit = 0; hit < 20; hit++)
        {
            Assert.AreEqual(start, GameplayFeedbackController.EvaluateDamageRecoil(0, start, limit));
            for (int i = 0; i <= 100; i++)
                Assert.LessOrEqual(Mathf.Abs(GameplayFeedbackController.EvaluateDamageRecoil(i / 100f, start, limit)), limit);
            start = GameplayFeedbackController.EvaluateDamageRecoil(.08f, start, limit);
        }
        Assert.AreEqual(0, GameplayFeedbackController.EvaluateDamageRecoil(1, start, limit));
        Assert.AreEqual(0, GameplayFeedbackController.EvaluateDamageRecoil(2, start, limit));
    }

    [Test]
    public void RecoilAlternatesHorizontallyAndZeroAmplitudeHasNoOffset()
    {
        Assert.Greater(GameplayFeedbackController.EvaluateDamageRecoil(.08f, 0, .004f), 0);
        Assert.Less(GameplayFeedbackController.EvaluateDamageRecoil(.25f, 0, .004f), 0);
        Assert.AreEqual(0, GameplayFeedbackController.EvaluateDamageRecoil(.08f, .004f, 0));
    }
}
