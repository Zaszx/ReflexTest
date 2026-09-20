using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RecoveryHudLifecycleEditModeTests
{
    private GameObject root;
    private NeonHudMotion hud;
    private RectTransform healthVisual;
    private RectTransform timerVisual;
    private static readonly MethodInfo RenderDamageVisual = typeof(NeonHudMotion).GetMethod("RenderDamageVisual", BindingFlags.Instance | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Recovery HUD", typeof(RectTransform));
        Image health = Track("Health");
        Image objective = Track("Objective");
        Image timer = Track("Timer");
        healthVisual = Child("HealthVisual");
        timerVisual = Child("TimerVisual");
        TMP_Text healthText = NeonStyle.Text("HealthText", healthVisual, "", 23f, NeonTheme.T.Text);
        hud = root.AddComponent<NeonHudMotion>();
        hud.Initialize(health, objective, healthText, null, null, null, timer, null, healthVisual, timerVisual);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void HealthWrapperUsesBoundedDamagePoseAndAlwaysReturnsToLayoutBaseline()
    {
        hud.SetState(3, 3, 0, 3, false);
        hud.SetState(2, 3, 0, 3, false);
        hud.PlayHealthDamage();
        RenderDamageVisual.Invoke(hud, new object[] { .5f });
        Assert.That(healthVisual.localScale.x, Is.EqualTo(RecoverySettingsProvider.Current.healthDamagePeakScale).Within(.001f));
        Assert.That(healthVisual.localEulerAngles.z, Is.EqualTo(RecoverySettingsProvider.Current.healthDamageRotationDegrees).Within(.01f));

        hud.NormalizeImmediate();
        Assert.That(healthVisual.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Quaternion.Angle(healthVisual.localRotation, Quaternion.identity), Is.Zero.Within(.001f));
    }

    [Test]
    public void ReserveAndTerminalLifecycleNeverLeaveTheValueWrapperScaled()
    {
        hud.SetState(3, 3, 0, 3, true);
        timerVisual.localScale = Vector3.one * 1.08f;
        hud.BeginTerminalPresentation(false);
        Assert.That(timerVisual.localScale, Is.EqualTo(Vector3.one));

        timerVisual.localScale = Vector3.one * 1.08f;
        hud.NormalizeImmediate();
        Assert.That(timerVisual.localScale, Is.EqualTo(Vector3.one));
    }

    private RectTransform Child(string name)
    {
        var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        child.SetParent(root.transform, false);
        return child;
    }

    private Image Track(string name)
    {
        RectTransform track = Child(name);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(track, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        return fill;
    }
}
