using UnityEngine;

/// <summary>Cosmetic motion only. None of these values participate in game rules or saves.</summary>
[CreateAssetMenu(menuName = "NeonReflex/Motion Settings")]
public sealed class NeonMotionSettings : ScriptableObject
{
    public enum BoundedEase { Linear, SmoothStep, OutCubic }
    public BoundedEase easing = BoundedEase.OutCubic;

    [Header("Target roles / seconds")]
    [Min(0)] public float targetRoleDuration = .11f;
    [Min(0)] public float targetAppearDuration = .10f;
    [Min(0)] public float targetExitDuration = .09f;
    [Min(0)] public float targetRoleRetargetDuration = .065f;
    [Range(.5f, 1)] public float targetSpawnScale = .82f;
    [Range(0, 1)] public float targetSpawnAlpha = .65f;
    [Range(0, .5f)] public float targetExitAlpha = .35f;
    [Range(0, .2f)] public float targetExitContraction = .08f;
    [Range(.01f, .4f)] public float targetRoleGap = .06f;

    [Header("Accepted input and resources / seconds")]
    [Min(0)] public float correctCellDuration = .16f;
    [Min(0)] public float wrongCellDuration = .20f;
    [Range(0, .5f)] public float correctCellTint = .22f;
    [Range(0, .5f)] public float wrongCellTint = .32f;
    [Min(0)] public float healthDuration = .20f;
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
        float t = Mathf.Clamp01(value);
        switch (T.easing)
        {
            case NeonMotionSettings.BoundedEase.Linear: return t;
            case NeonMotionSettings.BoundedEase.SmoothStep: return t * t * (3 - 2 * t);
            default: float remaining = 1 - t; return 1 - remaining * remaining * remaining;
        }
    }
}
