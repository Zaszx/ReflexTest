using UnityEngine;

/// <summary>Cosmetic motion only. None of these values participate in game rules or saves.</summary>
[CreateAssetMenu(menuName = "NeonReflex/Motion Settings")]
public sealed class NeonMotionSettings : ScriptableObject
{
    public enum BoundedEase { Linear, SmoothStep, OutCubic }
    public BoundedEase easing = BoundedEase.OutCubic;

    [Header("In-place campaign transition")]
    [Min(0)] public float levelTransitionDuration = 1.8f;
    [Min(0)] public float levelTransitionPreparationDuration = 1f;
    [Min(0)] public float practiceTransitionDuration = 1f;
    public BoundedEase levelTransitionEasing = BoundedEase.SmoothStep;

    [Header("Run-ending presentation / seconds and normalized timeline")]
    [Min(0)] public float terminalTransitionDuration = .9f;
    [Range(0, 1)] public float terminalGridShutdownStartNormalized = .13f;
    [Range(0, 1)] public float terminalGridShutdownEndNormalized = .62f;
    [Range(0, 1)] public float terminalReportRevealStartNormalized = .33f;
    [Range(0, 1)] public float terminalReportRevealEndNormalized = .78f;
    [Range(0, 1)] public float terminalReportDetailsStartNormalized = .61f;
    [Range(0, 1)] public float terminalReportDetailsEndNormalized = .89f;
    [Range(0, 1)] public float terminalReportActionsStartNormalized = .72f;
    [Range(0, 1)] public float terminalReportActionsEndNormalized = 1f;
    [Min(0)] public float terminalReportTravel = 18f;

    [Header("Target roles / seconds")]
    [Min(0)] public float targetRoleDuration = .11f;
    [Min(0)] public float targetAppearDuration = .10f;
    [Min(0)] public float targetExitDuration = .12f;
    [Min(0)] public float targetRoleRetargetDuration = .065f;
    [Range(.5f, 1)] public float targetSpawnScale = .82f;
    [Range(0, 1)] public float targetSpawnAlpha = .65f;
    [Range(0, .5f)] public float targetExitAlpha = .35f;
    [Range(0, .2f)] public float targetExitContraction = .08f;
    [Range(.01f, .4f)] public float targetRoleGap = .06f;

    [Header("Accepted input and resources / seconds")]
    [Min(0)] public float correctCellDuration = .16f;
    [Min(0)] public float wrongCellDuration = .16f;
    [Range(0, .5f)] public float correctCellTint = .30f;
    [Range(0, .5f)] public float wrongCellTint = .45f;
    [Range(0, 1)] public float wrongCellOutlineOpacity = .95f;
    [Range(0, 1)] public float damageAccentOpacity = .82f;
    public Color damageAccent = new Color(1f, .16f, .22f, 1f);
    public Color successAccent = new Color(.35f, 1f, .48f, 1f);
    [Min(0)] public float healthDuration = .25f;
    [Range(.5f, 1f)] public float healthDamageCompression = .91f;
    [Min(0)] public float floatingDamageDuration = .26f;
    [Range(0, 14)] public float floatingDamageTravel = 14f;
    [Min(0)] public float damageAggregationWindow = .18f;
    [Range(0, 1)] public float lostHealthGhostOpacity = .65f;
    [Min(0)] public float reserveExhaustedDuration = .22f;
    [Range(0, 1)] public float reserveFailureAccentOpacity = .9f;
    public Color reserveFailureAccent = new Color(1f, .796f, .459f, 1f);
    [Min(0)] public float damageVignetteDuration = .24f;
    [Range(0, 1)] public float damageVignetteIntensity = .48f;
    [Min(0)] public float successVignetteDuration = .17f;
    [Range(0, 1)] public float successVignetteIntensity = .18f;
    [Range(.04f, .3f)] public float vignetteEdgeWidth = .12f;
    [Range(.12f, .18f)] public float damageShakeDuration = .15f;
    [Range(0, .008f)] public float damageShakeViewportFraction = .004f;
    public bool successParticlesEnabled = false;
    [Range(0, 8)] public int successParticleCount = 6;
    [Range(.15f, .25f)] public float successParticleDuration = .22f;
    [Range(0, .05f)] public float successParticleTravel = .02f;
    [Range(0, 1)] public float terminalGridDimMultiplier = .22f;
    [Min(0)] public float progressDuration = .12f;
    [Min(0)] public float resourceDuration = .22f;
    [Min(0)] public float purchaseDuration = .28f;
    [Min(0)] public float pressDuration = .06f;
    [Min(0)] public float releaseDuration = .12f;
    public Vector2 pressOffset = new Vector2(4, -2);

    [Header("Navigation / seconds and reference-canvas pixels")]
    [Min(0)] public float screenDuration = .22f;
    [Min(0)] public float screenExitDuration = .16f;
    [Min(0)] public float modalDuration = .18f;
    [Min(0)] public float modalExitDuration = .14f;
    [Range(1, 6)] public float modalBackdropLead = 4;
    [Min(0)] public float resultDuration = .32f;
    [Min(0)] public float resultStagger = .045f;
    [Min(0)] public float screenTravel = 22;
    [Min(0)] public float modalTravel = 16;
    [Min(0)] public float resultTravel = 16;

    [Header("Existing Reverse windows remain 0.76s / 0.59s")]
    [Min(0)] public float reverseEnterFade = .14f;
    [Min(0)] public float reverseExitFade = .18f;
    [Min(0)] public float ruleTravel = 16;
    [Header("Reduced effects")]
    [Range(0, 1)] public float reducedEffectsStrength = .45f;
}

/// <summary>Shared bounded easing and a presentation clock, independent of simulation time.</summary>
public static class NeonMotion
{
    private static NeonMotionSettings settings;
    private static bool applicationSuspended;
    private static int resumedFrame = -1;
    public static NeonMotionSettings T
    {
        get
        {
            if (settings == null) settings = Resources.Load<NeonMotionSettings>("NeonMotionSettings");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<NeonMotionSettings>();
                settings.hideFlags = HideFlags.DontSave;
            }
            return settings;
        }
    }
    public static bool ApplicationSuspended => applicationSuspended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClock()
    {
        settings = null;
        applicationSuspended = false;
        resumedFrame = -1;
    }

    public static void SetApplicationSuspended(bool suspended)
    {
        if (applicationSuspended && !suspended) resumedFrame = Time.frameCount;
        applicationSuspended = suspended;
    }

    public static float Delta(bool paused = false)
    {
        // Cosmetic tracks may settle over a bounded active frame step.
        return Mathf.Min(TransitionDelta(paused), .1f);
    }

    public static float TransitionDelta(bool paused = false)
    {
        if (paused || applicationSuspended || Time.frameCount == resumedFrame) return 0;
        // Existing gameplay locks must count all active elapsed time. Clamping
        // a foreground hitch here would prolong the intro/Reverse interruption.
        return Mathf.Max(0, Time.unscaledDeltaTime);
    }

    public static float Ease(float value)
    {
        return Ease(value, T.easing);
    }

    public static float Ease(float value, NeonMotionSettings.BoundedEase easing)
    {
        float t = Mathf.Clamp01(value);
        switch (easing)
        {
            case NeonMotionSettings.BoundedEase.Linear: return t;
            case NeonMotionSettings.BoundedEase.SmoothStep: return t * t * (3 - 2 * t);
            default: float remaining = 1 - t; return 1 - remaining * remaining * remaining;
        }
    }
}
