using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "NeonReflex/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("Level Configuration")]
    [Tooltip("Stable persistence identifier. Do not change after shipping a level.")]
    public string stableId = "neon-reflex-level-001";
    public int levelNumber = 1;
    public int gridSize = 3; // n x n grid
    public int requiredCorrectClicks = 10; // x correct clicks
    public float timeLimit = 30f; // t seconds time limit
    [Min(0)] public long completionCoinReward = 10;

    [Header("Gameplay Modifiers")]
    [Tooltip("Allows the global Reverse mechanic to trigger during this level.")]
    public bool reverseEnabled;

    [Tooltip("Grid rotation speed in degrees per second. Set to 0 to disable rotation.")]
    public float rotateSpeed;

    [Header("Scale Modifier")]
    public bool scaleEnabled;
    [Min(0.01f)] public float minimumGridScale = 0.9f;
    [Min(0.01f)] public float maximumGridScale = 1f;
    [Tooltip("Seconds for a complete minimum-to-maximum-to-minimum cycle.")]
    [Min(0.1f)] public float scaleCycleDuration = 6f;

    [Header("Movement Modifier")]
    public bool movementEnabled;
    [Tooltip("Fraction of the shortest gameplay dimension travelled per second.")]
    [Min(0f)] public float movementSpeedNormalized = 0.035f;
    [Tooltip("Extra normalized padding kept between transformed grid corners and bounds.")]
    [Range(0f, 0.2f)] public float movementTravelPaddingNormalized = 0.03f;

    [Header("Visual Theme (Neon)")]
    public Color backgroundColor = new Color(0.043f, 0.043f, 0.086f); // #0B0B16
    public Color cellColor = new Color(0.102f, 0.102f, 0.18f); // #1A1A2E
    public Color outlineColor = new Color(0.0f, 0.96f, 1.0f); // #00F5FF (Neon Cyan)
    public Color textPrimaryColor = Color.white;
    public Color textSecondaryColor = new Color(0.5f, 0.5f, 0.7f); // Muted blue-grey

    [Header("Outline Scales")]
    public float smallScale = 0.4f;
    public float mediumScale = 0.7f;
    public float fullScale = 1.0f;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(stableId))
            stableId = $"neon-reflex-level-{Mathf.Max(1, levelNumber):000}";

        levelNumber = Mathf.Max(1, levelNumber);
        gridSize = Mathf.Max(2, gridSize);
        requiredCorrectClicks = Mathf.Max(1, requiredCorrectClicks);
        timeLimit = Mathf.Max(0.1f, timeLimit);
        completionCoinReward = System.Math.Max(0L, completionCoinReward);
        minimumGridScale = Mathf.Max(0.01f, minimumGridScale);
        maximumGridScale = Mathf.Max(minimumGridScale, maximumGridScale);
        scaleCycleDuration = Mathf.Max(0.1f, scaleCycleDuration);
        movementSpeedNormalized = Mathf.Max(0f, movementSpeedNormalized);
        movementTravelPaddingNormalized = Mathf.Clamp(movementTravelPaddingNormalized, 0f, 0.2f);
        smallScale = Mathf.Clamp(smallScale, 0.05f, 1f);
        mediumScale = Mathf.Clamp(mediumScale, smallScale, 1f);
        fullScale = Mathf.Max(mediumScale, fullScale);
    }
}
