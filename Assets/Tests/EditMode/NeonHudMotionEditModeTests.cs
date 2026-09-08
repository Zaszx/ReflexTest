using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class NeonHudMotionEditModeTests
{
    private GameObject root;
    private NeonHudMotion hud;
    private Image primaryHealth, objective, timer;
    private NeonMotionSettings originalSettings, isolatedSettings;
    private static readonly FieldInfo SettingsField = typeof(NeonMotion).GetField("settings", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        originalSettings = NeonMotion.T;
        isolatedSettings = Object.Instantiate(originalSettings);
        isolatedSettings.hideFlags = HideFlags.HideAndDontSave;
        SettingsField.SetValue(null, isolatedSettings);
        NeonMotion.T.healthDuration = .2f;
        NeonMotion.T.progressDuration = .12f;
        NeonMotion.T.resourceDuration = .22f;
        NeonMotion.T.reducedEffectsStrength = 1f;
        root = new GameObject("HUD fixture", typeof(RectTransform));
        primaryHealth = Track("Health");
        objective = Track("Objective");
        timer = Track("Timer");
        primaryHealth.rectTransform.anchorMax = new Vector2(.8f, 1f);
        timer.rectTransform.anchorMax = new Vector2(.47f, 1f);
        hud = root.AddComponent<NeonHudMotion>();
        hud.Initialize(primaryHealth, objective, null, null, null, null, timer);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        SettingsField.SetValue(null, originalSettings);
        Object.DestroyImmediate(isolatedSettings);
    }

    [Test]
    public void RapidDamageMergesOnlyLostHealthWithoutWritingPrimaryOrTimerFills()
    {
        hud.SetState(5, 5, 0, 10, false);
        hud.SetState(4, 5, 1, 10, false);
        hud.SetState(3, 5, 2, 10, false);
        Image ghost = primaryHealth.transform.parent.Find("LostHealth").GetComponent<Image>();
        Assert.That(ghost.rectTransform.anchorMin.x, Is.EqualTo(.6f).Within(.0001f));
        Assert.That(ghost.rectTransform.anchorMax.x, Is.EqualTo(1f));
        Assert.That(ghost.color.a, Is.GreaterThan(0f));
        Assert.That(primaryHealth.rectTransform.anchorMax.x, Is.EqualTo(.8f));
        Assert.That(timer.rectTransform.anchorMax.x, Is.EqualTo(.47f));
        Assert.That(ghost.raycastTarget, Is.False);
        int objectCount = root.GetComponentsInChildren<RectTransform>(true).Length;
        for (int i = 0; i < 100; i++) hud.SetState(3, 5, 2, 10, false);
        Assert.That(root.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(objectCount));
    }

    [Test]
    public void HiddenSnapshotsBecomeBaselineWithoutReplayingDamageOnReactivation()
    {
        hud.SetState(5, 5, 0, 10, false);
        root.SetActive(false);
        hud.SetState(4, 5, 2, 10, false);
        hud.SetState(2, 5, 6, 10, true);
        root.SetActive(true);
        hud.SetState(2, 5, 6, 10, true);
        Image ghost = primaryHealth.transform.parent.Find("LostHealth").GetComponent<Image>();
        Assert.That(ghost.color.a, Is.Zero);
        Assert.That(objective.rectTransform.anchorMax.x, Is.EqualTo(.6f).Within(.0001f));
        Assert.That(primaryHealth.rectTransform.anchorMax.x, Is.EqualTo(.8f));
        Assert.That(timer.rectTransform.anchorMax.x, Is.EqualTo(.47f));
    }

    [Test]
    public void ZeroDurationFeedbackSettlesLatestObjectiveAndLeavesNoHealthGhost()
    {
        NeonMotion.T.healthDuration = 0f;
        NeonMotion.T.progressDuration = 0f;
        NeonMotion.T.resourceDuration = 0f;
        hud.SetState(5, 5, 0, 10, false);
        hud.SetState(1, 5, 7, 10, true);
        Image ghost = primaryHealth.transform.parent.Find("LostHealth").GetComponent<Image>();
        Assert.That(ghost.color.a, Is.Zero);
        Assert.That(objective.rectTransform.anchorMax.x, Is.EqualTo(.7f).Within(.0001f));
        hud.NormalizeImmediate();
        hud.SetState(1, 5, 7, 10, true);
        Assert.That(ghost.color.a, Is.Zero, "A restored snapshot must not replay damage.");
        Assert.That(objective.rectTransform.anchorMax.x, Is.EqualTo(.7f).Within(.0001f));
    }

    private Image Track(string name)
    {
        var track = new GameObject(name, typeof(RectTransform));
        track.transform.SetParent(root.transform, false);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        var image = fill.GetComponent<Image>();
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        return image;
    }
}
