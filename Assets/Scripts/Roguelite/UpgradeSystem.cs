using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeId { MaximumHealth, StartingReserve, GridStabilizer, ReverseResistance }
[Serializable] public sealed class UpgradeTierDefinition { public long cost; public float effectValue; }
[Serializable] public sealed class UpgradeDefinitionData { public string stableId; public string displayName; [TextArea] public string description; public List<UpgradeTierDefinition> tiers = new List<UpgradeTierDefinition>(); }

public static class UpgradeDefaults
{
    private static UpgradeDefinitionData Create(string id, string name, string description, float[] values, long[] costs)
    {
        var definition = new UpgradeDefinitionData { stableId = id, displayName = name, description = description };
        for (int i = 0; i < values.Length; i++) definition.tiers.Add(new UpgradeTierDefinition { effectValue = values[i], cost = costs[i] });
        return definition;
    }
    public static UpgradeDefinitionData MaximumHealth() => Create("maximum-health", "Maximum Health", "Adds permanent run-start health.", new[] { 4f,5f,6f,7f,8f,9f,10f,11f,12f,13f,14f,15f,16f,17f,18f,19f,20f }, new long[] { 30,45,65,90,120,155,195,240,290,345,405,470,540,615,695,780,870 });
    public static UpgradeDefinitionData StartingReserve() => Create("starting-reserve", "Starting Reserve", "Increases reserve time for new runs.", new[] {20f,25f,30f,35f,40f,45f}, new long[] {40,70,110,165,235,320});
    public static UpgradeDefinitionData GridStabilizer() => Create("grid-stabilizer", "Grid Stabilizer", "Slows rotation, scale and movement.", new[] {.90f,.80f,.70f,.60f,.50f}, new long[] {60,100,155,225,310});
    public static UpgradeDefinitionData ReverseResistance() => Create("reverse-resistance", "Reverse Resistance", "Adds post-Reverse cooldown.", new[] {2f,4f,6f,8f,10f}, new long[] {70,115,175,250,340});
}

public static class UpgradeCatalog
{
    public static void EnsureDefaults(GameConfig config)
    {
        if (config == null) return;
        if (config.maximumHealthUpgrade == null) config.maximumHealthUpgrade = UpgradeDefaults.MaximumHealth();
        if (config.startingReserveUpgrade == null) config.startingReserveUpgrade = UpgradeDefaults.StartingReserve();
        if (config.gridStabilizerUpgrade == null) config.gridStabilizerUpgrade = UpgradeDefaults.GridStabilizer();
        if (config.reverseResistanceUpgrade == null) config.reverseResistanceUpgrade = UpgradeDefaults.ReverseResistance();
    }
    public static UpgradeDefinitionData Get(GameConfig config, UpgradeId id)
    {
        if (config == null) return null; EnsureDefaults(config);
        switch (id) { case UpgradeId.MaximumHealth: return config.maximumHealthUpgrade; case UpgradeId.StartingReserve: return config.startingReserveUpgrade; case UpgradeId.GridStabilizer: return config.gridStabilizerUpgrade; default: return config.reverseResistanceUpgrade; }
    }
    public static int GetTier(PlayerProfileData profile, UpgradeId id) { if (profile == null) return 0; switch(id) { case UpgradeId.MaximumHealth:return profile.maximumHealthTier; case UpgradeId.StartingReserve:return profile.startingReserveTier; case UpgradeId.GridStabilizer:return profile.gridStabilizerTier; default:return profile.reverseResistanceTier; } }
    public static void SetTier(PlayerProfileData profile, UpgradeId id, int value) { if(profile==null)return; switch(id) {case UpgradeId.MaximumHealth:profile.maximumHealthTier=value;break;case UpgradeId.StartingReserve:profile.startingReserveTier=value;break;case UpgradeId.GridStabilizer:profile.gridStabilizerTier=value;break;default:profile.reverseResistanceTier=value;break;} }
    public static int ClampTier(GameConfig config, UpgradeId id, int tier) => Mathf.Clamp(tier, 0, Get(config, id)?.tiers?.Count ?? 0);
    public static UpgradeTierDefinition CurrentTier(GameConfig config, PlayerProfileData profile, UpgradeId id) { var d=Get(config,id); int tier=ClampTier(config,id,GetTier(profile,id)); return tier > 0 ? d.tiers[tier-1] : null; }
    public static UpgradeTierDefinition NextTier(GameConfig config, PlayerProfileData profile, UpgradeId id) { var d=Get(config,id); int tier=ClampTier(config,id,GetTier(profile,id)); return d != null && tier < d.tiers.Count ? d.tiers[tier] : null; }
    public static UpgradeSnapshotData CaptureSnapshot(GameConfig config, PlayerProfileData profile)
    {
        EnsureDefaults(config); profile = profile ?? PlayerProfileData.CreateDefault();
        int healthCap = Mathf.Clamp(config.maximumHealthCap, 3, 20);
        var snapshot = new UpgradeSnapshotData { maximumHealthTier=ClampTier(config,UpgradeId.MaximumHealth,profile.maximumHealthTier), startingReserveTier=ClampTier(config,UpgradeId.StartingReserve,profile.startingReserveTier), gridStabilizerTier=ClampTier(config,UpgradeId.GridStabilizer,profile.gridStabilizerTier), reverseResistanceTier=ClampTier(config,UpgradeId.ReverseResistance,profile.reverseResistanceTier), maxHealth=Mathf.Clamp(config.baseHealth, 1, healthCap), startingReserveSeconds=Mathf.Max(0f,config.baseStartingReserveSeconds), gridStabilizerMultiplier=1f };
        var h=CurrentTier(config,profile,UpgradeId.MaximumHealth); if(h!=null) snapshot.maxHealth=Mathf.Clamp(Mathf.RoundToInt(h.effectValue),1,healthCap);
        var r=CurrentTier(config,profile,UpgradeId.StartingReserve); if(r!=null) snapshot.startingReserveSeconds=Mathf.Max(0f,r.effectValue);
        var s=CurrentTier(config,profile,UpgradeId.GridStabilizer); if(s!=null) snapshot.gridStabilizerMultiplier=Mathf.Clamp(s.effectValue,.01f,1f);
        var rr=CurrentTier(config,profile,UpgradeId.ReverseResistance); if(rr!=null) snapshot.reverseCooldownBonusSeconds=Mathf.Max(0f,rr.effectValue);
        return snapshot;
    }
    public static bool TryPurchase(GameConfig config, PlayerProfileData profile, UpgradeId id, bool hasActiveRun, out string reason)
    {
        reason=DisabledReason(config,profile,id,hasActiveRun); if(!string.IsNullOrEmpty(reason)) return false;
        var next=NextTier(config,profile,id); if(next==null || next.cost<0 || profile.coins<next.cost || profile.coins-next.cost<0) { reason="Insufficient coins."; return false; }
        int current = ClampTier(config, id, GetTier(profile, id));
        profile.coins-=next.cost; SetTier(profile,id,current + 1); return true;
    }
    public static string DisabledReason(GameConfig config, PlayerProfileData profile, UpgradeId id, bool hasActiveRun)
    {
        if(hasActiveRun)return "Finish or abandon the active run before purchasing upgrades."; var next=NextTier(config,profile,id); if(next==null)return "Maximum tier reached."; if(next.cost<0)return "Invalid upgrade data."; if(profile==null || profile.coins<next.cost)return "Insufficient coins."; return string.Empty;
    }
}
