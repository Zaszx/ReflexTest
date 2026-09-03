using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "NeonReflex/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Debug Tools")]
    [Tooltip("Shows the level selector in the Editor and Development Builds only. Release builds always exclude it.")]
    public bool enableDebugLevelSelect = true;

    [Header("Reverse Modifier")]
    [Min(1)] public int reverseMinCorrectClicks = 3;
    [Min(1)] public int reverseMaxCorrectClicks = 6;
    [Min(0f)] public float reverseCooldownSeconds = 8f;
    [Range(0f, 1f)] public float reverseTriggerChancePerCorrectClick = 0.25f;
}
