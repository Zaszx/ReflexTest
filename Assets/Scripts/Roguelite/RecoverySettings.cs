using UnityEngine;

/// <summary>Authorable recovery tuning. Rules consume this asset without owning presentation or run flow.</summary>
[CreateAssetMenu(fileName = "RecoverySettings", menuName = "NeonReflex/Recovery Settings")]
public sealed class RecoverySettings : ScriptableObject
{
    [Header("Rebound")]
    [Min(0f)] public float reboundDurationSeconds = 1f;
    [Range(.01f, 1f)] public float reboundMotionMultiplier = .5f;
    [Min(0f)] public float reboundRecoveryBlendSeconds = .2f;

    [Header("Hearts")]
    [Range(0f, 1f)] public float heartSpawnChanceAfterCorrect = .03f;
    [Min(0f)] public float firstHeartEligibilityDelaySeconds = 5f;
    [Min(0f)] public float heartSpawnCooldownSeconds = 15f;
    [Min(1)] public int maxHeartsPerLevel = 2;
    [Min(0f)] public float heartEntranceSeconds = .08f;
    [Min(0f)] public float heartExitSeconds = .15f;

    [Header("Recovery HUD")]
    [Range(1f, 1.3f)] public float healthDamagePeakScale = 1.17f;
    [Range(0f, 15f)] public float healthDamageRotationDegrees = 5f;
    [Range(1f, 1.2f)] public float healthHealingPeakScale = 1.08f;
    [Range(1f, 1.12f)] public float reserveValuePulseScale = 1.08f;
    [Min(.1f)] public float reserveValuePulseCycleSeconds = .8f;

    private void OnValidate()
    {
        reboundDurationSeconds = Mathf.Max(0f, reboundDurationSeconds);
        reboundMotionMultiplier = Mathf.Clamp(reboundMotionMultiplier, .01f, 1f);
        reboundRecoveryBlendSeconds = Mathf.Max(0f, reboundRecoveryBlendSeconds);
        heartSpawnChanceAfterCorrect = Mathf.Clamp01(heartSpawnChanceAfterCorrect);
        firstHeartEligibilityDelaySeconds = Mathf.Max(0f, firstHeartEligibilityDelaySeconds);
        heartSpawnCooldownSeconds = Mathf.Max(0f, heartSpawnCooldownSeconds);
        maxHeartsPerLevel = Mathf.Max(1, maxHeartsPerLevel);
        heartEntranceSeconds = Mathf.Max(0f, heartEntranceSeconds);
        heartExitSeconds = Mathf.Max(0f, heartExitSeconds);
        healthDamagePeakScale = Mathf.Clamp(healthDamagePeakScale, 1f, 1.3f);
        healthDamageRotationDegrees = Mathf.Clamp(healthDamageRotationDegrees, 0f, 15f);
        healthHealingPeakScale = Mathf.Clamp(healthHealingPeakScale, 1f, 1.2f);
        reserveValuePulseScale = Mathf.Clamp(reserveValuePulseScale, 1f, 1.12f);
        reserveValuePulseCycleSeconds = Mathf.Max(.1f, reserveValuePulseCycleSeconds);
    }
}

public static class RecoverySettingsProvider
{
    private static RecoverySettings cached;
    public static RecoverySettings Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<RecoverySettings>("RecoverySettings");
            if (cached == null)
            {
                cached = ScriptableObject.CreateInstance<RecoverySettings>();
                cached.hideFlags = HideFlags.DontSave;
            }
            return cached;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => cached = null;
}
