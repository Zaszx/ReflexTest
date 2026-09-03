using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "NeonReflex/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("Level Configuration")]
    public int levelNumber = 1;
    public int gridSize = 3; // n x n grid
    public int requiredCorrectClicks = 10; // x correct clicks
    public float timeLimit = 30f; // t seconds time limit

    [Header("Gameplay Modifiers")]
    [Tooltip("Allows the global Reverse mechanic to trigger during this level.")]
    public bool reverseEnabled;

    [Tooltip("Grid rotation speed in degrees per second. Set to 0 to disable rotation.")]
    public float rotateSpeed;

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
}
