using System;
using UnityEngine;

[Flags]
public enum EnemySpawnEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Bottom = 4,
    Top = 8,
    All = Left | Right | Bottom | Top
}

/// <summary>Authorable global defaults. All distances are fractions of the arena's shortest side.</summary>
[CreateAssetMenu(fileName = "EnemySettings", menuName = "NeonReflex/Enemy Settings")]
public sealed class EnemySettings : ScriptableObject
{
    [Min(1)] public int minimumCampaignLevel = 25;
    [Min(0)] public float initialSpawnDelay = 3f;
    [Min(.01f)] public float spawnIntervalMinimum = 6f;
    [Min(.01f)] public float spawnIntervalMaximum = 9f;
    [Min(0)] public float movementSpeedNormalized = .08f;
    [Range(.02f, .2f)] public float sizeNormalized = .055f;
    [Range(1f, 2.5f)] public float tapTargetScale = 1.7f;
    [Range(1, 8)] public int maximumConcurrent = 2;
    public EnemySpawnEdges spawnEdges = EnemySpawnEdges.All;
    [Min(0)] public float minimumSpawnClearanceNormalized = .06f;
    [Min(.1f)] public float requiredTravelSeconds = 2f;
    [Min(.001f)] public float collisionSubstepNormalized = .012f;

    private void OnValidate()
    {
        minimumCampaignLevel = Mathf.Max(1, minimumCampaignLevel);
        initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
        spawnIntervalMinimum = Mathf.Max(.01f, spawnIntervalMinimum);
        spawnIntervalMaximum = Mathf.Max(spawnIntervalMinimum, spawnIntervalMaximum);
        movementSpeedNormalized = Mathf.Max(0f, movementSpeedNormalized);
        sizeNormalized = Mathf.Clamp(sizeNormalized, .02f, .2f);
        maximumConcurrent = Mathf.Clamp(maximumConcurrent, 1, 8);
        if (spawnEdges == EnemySpawnEdges.None) spawnEdges = EnemySpawnEdges.All;
        minimumSpawnClearanceNormalized = Mathf.Max(0f, minimumSpawnClearanceNormalized);
        requiredTravelSeconds = Mathf.Max(.1f, requiredTravelSeconds);
        collisionSubstepNormalized = Mathf.Max(.001f, collisionSubstepNormalized);
    }
}

/// <summary>Optional level settings. Disabled is always explicit so old levels stay enemy-free.</summary>
[Serializable]
public sealed class EnemyLevelSettings
{
    public bool enabled;
    [Tooltip("When disabled this level uses EnemySettings values.")]
    public bool overrideDefaults;
    [Min(1)] public int minimumCampaignLevel = 25;
    [Min(0)] public float initialSpawnDelay = 3f;
    [Min(.01f)] public float spawnIntervalMinimum = 6f;
    [Min(.01f)] public float spawnIntervalMaximum = 9f;
    [Min(0)] public float movementSpeedNormalized = .08f;
    [Range(.02f, .2f)] public float sizeNormalized = .055f;
    [Range(1, 8)] public int maximumConcurrent = 2;
    public EnemySpawnEdges spawnEdges = EnemySpawnEdges.All;
    [Min(0)] public float minimumSpawnClearanceNormalized = .06f;
    [Min(.1f)] public float requiredTravelSeconds = 2f;
}

[Serializable]
public struct EffectiveEnemySettings
{
    public bool enabled;
    public int minimumCampaignLevel;
    public float initialSpawnDelay, spawnIntervalMinimum, spawnIntervalMaximum;
    public float movementSpeedNormalized, sizeNormalized;
    public float tapTargetScale;
    public int maximumConcurrent;
    public EnemySpawnEdges spawnEdges;
    public float minimumSpawnClearanceNormalized, requiredTravelSeconds, collisionSubstepNormalized;
}
