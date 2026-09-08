#if UNITY_EDITOR
using System;
using UnityEngine;

// Entire API is absent from player assemblies, including development builds.
public sealed partial class GameManager
{
    public bool EditorProfileReady => Application.isPlaying && saveData?.profile != null && saveService != null && rogueliteUI != null;
    public long EditorBankedCoins => saveData?.profile?.coins ?? 0;
    public int EditorUpgradeTier(UpgradeId id) => UpgradeCatalog.GetTier(saveData?.profile, id);

    public string EditorDebugAddCoins()
    {
        if (!EditorProfileReady) return "Enter Play Mode and wait for the game to initialize.";
        long previous = saveData.profile.coins;
        saveData.profile.coins = RunEconomyRules.SaturatingAdd(previous, 1000);
        return SaveEditorProfileChange($"Added {saveData.profile.coins - previous:N0} banked coins");
    }

    public string EditorDebugResetUpgrades()
    {
        if (!EditorProfileReady) return "Enter Play Mode and wait for the game to initialize.";
        foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            UpgradeCatalog.SetTier(saveData.profile, id, 0);
        // Run upgrades are immutable starting snapshots; resetting the permanent
        // profile must not change health, reserve, or motion halfway through a run.
        return SaveEditorProfileChange("All permanent upgrades reset to tier 0");
    }

    private string SaveEditorProfileChange(string message)
    {
        SaveEnvelopeCritical();
        RefreshMainMenu();
        rogueliteUI.RefreshShop(gameConfig, saveData.profile, HasRealRun);
        return message + (saveDirty ? ". Save pending; see the Console." : ". Saved.");
    }
}
#endif
