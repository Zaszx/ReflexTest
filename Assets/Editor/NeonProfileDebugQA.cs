using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editor-only profile/debug control regression route using the isolated QA save.</summary>
public static class NeonProfileDebugQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null || !NeonPresentationQAProfile.IsActive) throw new InvalidOperationException("Profile debug QA requires an isolated GameManager session.");
        if (!gm.EditorProfileReady) throw new InvalidOperationException("Editor profile debug APIs are not ready.");
        NeonSaveService service = Field<NeonSaveService>(gm, "saveService");
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        if (service == null || service.DirectoryPath != NeonPresentationQAProfile.DirectoryPath) throw new InvalidOperationException("Profile debug QA storage is not isolated.");
        data.profile.coins = 250; data.profile.lastBankedRunId = "qa-previous-banked-run";
        SetTiers(data.profile, 1); service.Save(data, gm.gameConfig);
        check(gm.EditorBankedCoins == 250, "Debug profile starts at 250 coins");
        for (int i = 0; i < 4; i++) check(gm.EditorUpgradeTier((UpgradeId)i) == 1, "Debug profile starts tier 1");
        gm.ShowMainMenu(); yield return Wait(gm, "MainMenu", check);
        gm.StartNewRun(); yield return Wait(gm, "Playing", check);
        ActiveRunData live = Field<ActiveRunData>(gm, "sessionRun");
        if (live == null) throw new InvalidOperationException("Debug profile QA could not create a real run.");
        live.currentHealth = 2; live.currentReserveSeconds = 7.25f; live.levelState.objectiveProgress = 1;
        gm.ReturnToMainMenu(); yield return Wait(gm, "MainMenu", check);
        string runBefore = JsonUtility.ToJson(data.activeRun);
        gm.OpenUpgradeShop(); yield return Wait(gm, "Shop", check);
        check(!string.IsNullOrEmpty(gm.EditorDebugAddCoins()) && gm.EditorBankedCoins == 1250, "Debug add coins reaches 1250");
        check(!string.IsNullOrEmpty(gm.EditorDebugAddCoins()) && gm.EditorBankedCoins == 2250, "Debug add coins reaches 2250");
        var ui = gm.GetComponent<RogueliteUIController>();
        check(Field<TMP_Text>(ui, "wallet").text == 2250.ToString("N0"), "Debug grant immediately refreshes the shop wallet");
        check(Field<TMP_Text>(ui, "menuWallet").text == 2250.ToString("N0"), "Debug grant immediately refreshes the main menu wallet");
        data = Field<SaveEnvelopeData>(gm, "saveData");
        check(Field<RogueliteUIController>(gm, "rogueliteUI") != null, "UI remains available during profile debug route");
        check(service.Load(gm.gameConfig).profile.coins == 2250, "Added coins persist to the isolated envelope");
        check(JsonUtility.ToJson(data.activeRun) == runBefore, "Adding coins leaves the active run unchanged");
        string runBeforeReset = JsonUtility.ToJson(data.activeRun); string marker = data.profile.lastBankedRunId;
        check(!string.IsNullOrEmpty(gm.EditorDebugResetUpgrades()), "Debug reset reports completion");
        check(gm.EditorBankedCoins == 2250, "Reset preserves banked coins");
        for (int i = 0; i < 4; i++) check(gm.EditorUpgradeTier((UpgradeId)i) == 0, "Reset clears profile tier");
        data = Field<SaveEnvelopeData>(gm, "saveData");
        check(JsonUtility.ToJson(data.activeRun) == runBeforeReset, "Reset leaves the complete active run unchanged");
        check(data.profile.lastBankedRunId == marker, "Reset preserves bank marker");
        SaveEnvelopeData loaded = service.Load(gm.gameConfig);
        foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            check(UpgradeCatalog.GetTier(loaded.profile, id) == 0, "Reset persists tier zero: " + id);
        check(JsonUtility.ToJson(loaded.activeRun) == runBeforeReset, "Reset preserves the complete active run on disk");
        check(loaded.profile.legacyMigrationComplete && loaded.profile.lastBankedRunId == marker, "Reset preserves saved migration and bank metadata");
        string resetProfile = JsonUtility.ToJson(data.profile);
        check(!string.IsNullOrEmpty(gm.EditorDebugResetUpgrades()) && gm.EditorUpgradeTier(UpgradeId.MaximumHealth) == 0, "Repeated reset is idempotent");
        check(JsonUtility.ToJson(data.profile) == resetProfile, "Repeated reset leaves the whole profile unchanged");
        yield return Capture(capture, "01-debug-shop-reset");

        gm.ReturnToMainMenu(); yield return Wait(gm, "MainMenu", check);
        data.activeRun = null; service.Save(data, gm.gameConfig); gm.ShowMainMenu(); yield return Wait(gm, "MainMenu", check);
        gm.StartNewRun(); yield return Wait(gm, "Playing", check);
        gm.enabled = false;
        ActiveRunData fresh = Field<ActiveRunData>(gm, "sessionRun");
        check(fresh != null && fresh.currentLevelIndex == 0, "Fresh debug run starts at Level 1");
        check(fresh != null && fresh.upgrades.maximumHealthTier == 0 && fresh.upgrades.startingReserveTier == 0 && fresh.upgrades.gridStabilizerTier == 0 && fresh.upgrades.reverseResistanceTier == 0, "Fresh run receives reset profile upgrades");
        check(gm.campaign.GetLevel(0).timeLimit == 16 && fresh != null && fresh.levelState.normalTimeRemaining > 0 && fresh.levelState.normalTimeRemaining <= 16,
            "Fresh run uses the halved 16-second Level 1 timer");
        check(fresh.upgrades.maxHealth == gm.gameConfig.baseHealth && fresh.upgrades.startingReserveSeconds == gm.gameConfig.baseStartingReserveSeconds &&
            fresh.upgrades.gridStabilizerMultiplier == 1 && fresh.upgrades.reverseCooldownBonusSeconds == 0, "Fresh run uses all unupgraded base effects");
        yield return Capture(capture, "02-debug-new-run");
        string resources = JsonUtility.ToJson(new ResourceSnapshot(fresh));
        data.profile.coins = long.MaxValue - 500; service.Save(data, gm.gameConfig);
        string runBeforeOverflow = JsonUtility.ToJson(fresh);
        check(!string.IsNullOrEmpty(gm.EditorDebugAddCoins()) && gm.EditorBankedCoins == long.MaxValue, "Debug add coins saturates at long maximum");
        data = Field<SaveEnvelopeData>(gm, "saveData"); fresh = Field<ActiveRunData>(gm, "sessionRun");
        check(JsonUtility.ToJson(new ResourceSnapshot(fresh)) == resources, "Coin overflow leaves run resources unchanged");
        check(JsonUtility.ToJson(fresh) == runBeforeOverflow, "Coin overflow leaves the active run unchanged");
        gm.ReturnToMainMenu(); yield return Wait(gm, "MainMenu", check);
        gm.enabled = true;
        gm.StartDebugLevel(5); yield return Wait(gm, "Playing", check);
        ActiveRunData early = Field<ActiveRunData>(gm, "sessionRun");
        check(gm.campaign.GetLevel(5).gridSize == 4, "Early debug fixture selects the configured 4x4 level");
        check(Field<List<GameSquare>>(gm, "instantiatedSquares").Count == 16, "Early debug fixture creates 16 cells");
        check(Mathf.Abs(gm.campaign.GetLevel(5).timeLimit - 16f) < .01f && early != null && early.levelState.normalTimeRemaining <= 16f, "Early 4x4 fixture keeps its configured 16 second limit");
        yield return Capture(capture, "03-early-4x4");
    }

    [Serializable] private sealed class ResourceSnapshot
    { public int health; public float reserve; public long pending; public ResourceSnapshot(ActiveRunData r){health=r.currentHealth;reserve=r.currentReserveSeconds;pending=r.pendingCoins;} }
    private static void SetTiers(PlayerProfileData p, int tier) { p.maximumHealthTier=p.startingReserveTier=p.gridStabilizerTier=p.reverseResistanceTier=tier; }
    private static IEnumerator Capture(Func<string, IEnumerator> c,string label){if(c!=null){var r=c(label);if(r!=null)yield return r;}}
    private static IEnumerator Wait(GameManager gm,string state,Action<bool,string> check){float end=Time.realtimeSinceStartup+4f;while(Time.realtimeSinceStartup<end){if(Field<object>(gm,"state").ToString()==state&&Ready(gm,state)){check(true,"Flow reaches "+state+" and settles");yield break;}yield return null;}check(false,"Flow reaches "+state+" within 4 seconds");throw new InvalidOperationException("Profile debug QA timed out waiting for "+state);}
    private static bool Ready(GameManager gm,string state)
    { var ui=gm.GetComponent<RogueliteUIController>(); if(state=="Playing")return ui!=null&&ui.IsGameplayReady; if(state=="Shop")return ui!=null&&ui.IsShopVisible&&Motion(Field<GameObject>(ui,"shopPanel")); if(state=="MainMenu")return Motion(gm.mainMenuPanel); return true; }
    private static bool Motion(GameObject panel){var m=panel==null?null:panel.GetComponent<NeonScreenMotion>();return m==null?panel!=null&&panel.activeInHierarchy:m.IsReady&&m.Opacity>=.999f;}
    private static T Field<T>(object target,string name) where T:class { FieldInfo f=target.GetType().GetField(name,Hidden);if(f==null)throw new MissingFieldException(target.GetType().Name,name);return (T)f.GetValue(target); }
}
