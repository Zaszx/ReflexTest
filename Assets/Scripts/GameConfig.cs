using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "NeonReflex/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Save and Run Defaults")]
    [Min(1)] public int saveVersion = 2;
    [Min(1)] public int baseHealth = 3;
    [Range(3, 20)] public int maximumHealthCap = 20;
    [Min(0f)] public float baseStartingReserveSeconds = 15f;

    [Header("Debug Tools")]
    [Tooltip("Shows the level selector in the Editor and Development Builds only. Release builds always exclude it.")]
    public bool enableDebugLevelSelect = true;

    [Header("Reverse Modifier")]
    [Min(1)] public int reverseMinCorrectClicks = 3;
    [Min(1)] public int reverseMaxCorrectClicks = 6;
    [Min(0f)] public float reverseCooldownSeconds = 8f;
    [Range(0.01f, 1f)] public float reverseTriggerChancePerCorrectClick = 0.25f;

    [Header("Grid Motion and Bounds")]
    [Range(0f, 0.7f)] public float minimumMovementAxisComponent = 0.22f;
    [Range(0f, 0.2f)] public float gameplayBoundsPaddingNormalized = 0.025f;
    [Range(0.01f, 0.3f)] public float movementTravelAllowanceNormalized = 0.08f;
    [Min(32f)] public float minimumTouchTargetPixels = 64f;
    [Min(0.1f)] public float saveCheckpointIntervalSeconds = 1f;

    [Header("Permanent Upgrades (Provisional)")]
    public UpgradeDefinitionData maximumHealthUpgrade = UpgradeDefaults.MaximumHealth();
    public UpgradeDefinitionData startingReserveUpgrade = UpgradeDefaults.StartingReserve();
    public UpgradeDefinitionData gridStabilizerUpgrade = UpgradeDefaults.GridStabilizer();
    public UpgradeDefinitionData reverseResistanceUpgrade = UpgradeDefaults.ReverseResistance();

    private void OnValidate()
    {
        saveVersion = Mathf.Max(1, saveVersion);
        baseHealth = Mathf.Clamp(baseHealth, 1, 20);
        maximumHealthCap = Mathf.Clamp(maximumHealthCap, Mathf.Max(3, baseHealth), 20);
        baseStartingReserveSeconds = Mathf.Max(0f, baseStartingReserveSeconds);
        reverseMinCorrectClicks = Mathf.Max(1, reverseMinCorrectClicks);
        reverseMaxCorrectClicks = Mathf.Max(reverseMinCorrectClicks, reverseMaxCorrectClicks);
        reverseCooldownSeconds = Mathf.Max(0f, reverseCooldownSeconds);
        reverseTriggerChancePerCorrectClick = Mathf.Clamp(reverseTriggerChancePerCorrectClick, 0.01f, 1f);
        minimumMovementAxisComponent = Mathf.Clamp(minimumMovementAxisComponent, 0f, 0.7f);
        gameplayBoundsPaddingNormalized = Mathf.Clamp(gameplayBoundsPaddingNormalized, 0f, 0.2f);
        movementTravelAllowanceNormalized = Mathf.Clamp(movementTravelAllowanceNormalized, 0.01f, 0.3f);
        minimumTouchTargetPixels = Mathf.Max(32f, minimumTouchTargetPixels);
        saveCheckpointIntervalSeconds = Mathf.Max(0.1f, saveCheckpointIntervalSeconds);
        UpgradeCatalog.EnsureDefaults(this);
    }
}
