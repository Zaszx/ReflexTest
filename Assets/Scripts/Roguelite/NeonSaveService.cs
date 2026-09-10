using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Owns one atomic JSON envelope so banking and clearing a run cannot diverge.</summary>
public sealed class NeonSaveService
{
    [Serializable]
    private sealed class ProfileOnlyEnvelope
    {
        public int saveVersion;
        public PlayerProfileData profile;
    }

    [Serializable]
    private sealed class ActiveRunEnvelope
    {
        public int saveVersion;
        public PlayerProfileData profile;
        public ActiveRunData activeRun;
    }

    public const string FileName = "neon_reflex_save.json";
    public string DirectoryPath { get; }
    public string PrimaryPath => Path.Combine(DirectoryPath, FileName);
    public string BackupPath => PrimaryPath + ".bak";
    public string TempPath => PrimaryPath + ".tmp";
    public NeonSaveService(string directory = null) { DirectoryPath = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory; }
    public SaveEnvelopeData Load(GameConfig config = null)
    {
        if (TryRead(PrimaryPath, out SaveEnvelopeData result)) { Normalize(result, config); return result; }
        if (TryRead(BackupPath, out result)) { Debug.LogWarning("Neon Reflex save primary was unavailable; restored the valid backup."); Normalize(result, config); return result; }
        return SaveEnvelopeData.CreateDefault();
    }
    public void Save(SaveEnvelopeData envelope, GameConfig config = null)
    {
        envelope = envelope ?? SaveEnvelopeData.CreateDefault(); Normalize(envelope, config);
        Directory.CreateDirectory(DirectoryPath);
        // Unity serializes null inline classes as default objects. Omit absent
        // run/result fields so loading cannot fabricate either record.
        string json = envelope.lastRunResult != null
            ? JsonUtility.ToJson(envelope, true)
            : envelope.activeRun == null
                ? JsonUtility.ToJson(new ProfileOnlyEnvelope { saveVersion = envelope.saveVersion, profile = envelope.profile }, true)
                : JsonUtility.ToJson(new ActiveRunEnvelope { saveVersion = envelope.saveVersion, profile = envelope.profile, activeRun = envelope.activeRun }, true);
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream)) { writer.Write(json); writer.Flush(); stream.Flush(true); }
        // Do not replace an already-good backup with a corrupt primary.
        if (File.Exists(PrimaryPath) && TryRead(PrimaryPath, out SaveEnvelopeData previous) && previous != null) File.Copy(PrimaryPath, BackupPath, true);
        if (File.Exists(PrimaryPath))
        {
            try { ReplaceAtomicallyWithRetry(); }
            catch (PlatformNotSupportedException) { File.Copy(TempPath, PrimaryPath, true); File.Delete(TempPath); }
        }
        else File.Move(TempPath, PrimaryPath);
    }

    private void ReplaceAtomicallyWithRetry()
    {
        // A transient Windows file-sharing conflict can reject an otherwise
        // valid replacement. Retain atomic replacement on every attempt; if
        // contention persists, preserve the primary and staged file for the
        // caller's existing dirty-save retry rather than copying over it.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Replace(TempPath, PrimaryPath, null);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                System.Threading.Thread.Sleep(8);
            }
        }
    }
    public bool MigrateLegacyPlayerPrefs(SaveEnvelopeData envelope, GameConfig config = null)
    {
        if (envelope == null) return false;
        if (envelope.profile == null) envelope.profile = PlayerProfileData.CreateDefault();
        if (envelope.profile.legacyMigrationComplete) return false;
        PlayerPrefs.DeleteKey("CampaignLevel"); PlayerPrefs.DeleteKey("TimedHighScore"); PlayerPrefs.DeleteKey("SpeedBestTime");
        envelope.profile.legacyMigrationComplete = true;
        Save(envelope, config); return true;
    }
    public static uint ParseRandomState(string encoded, uint fallback = 1u)
    {
        return uint.TryParse(encoded, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value) && value != 0 ? value : fallback;
    }
    public static string EncodeRandomState(uint state) => (state == 0 ? 1u : state).ToString(CultureInfo.InvariantCulture);
    private static bool TryRead(string path, out SaveEnvelopeData envelope)
    {
        envelope = null;
        try
        {
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            envelope = JsonUtility.FromJson<SaveEnvelopeData>(json);
            if (envelope != null &&
                (json.IndexOf("\"activeRun\"", StringComparison.Ordinal) < 0 || IsLegacyInlineNullRun(json, envelope.activeRun)))
                envelope.activeRun = null;
            if (envelope != null && IsMissingOrInlineNullResult(json))
                envelope.lastRunResult = null;
            return HasRequiredEnvelopeData(json, envelope);
        }
        catch (Exception exception) { Debug.LogWarning("Unable to read Neon Reflex save: " + exception.Message); return false; }
    }

    private static bool IsLegacyInlineNullRun(string json, ActiveRunData run)
    {
        if (run == null || !string.IsNullOrEmpty(run.runId))
            return false;
        // Accept only the exact empty object previously emitted by JsonUtility,
        // including explicit nested fields. A damaged real run must still fail
        // validation and recover its backup rather than silently disappear.
        if (json.IndexOf("\"runId\"", StringComparison.Ordinal) < 0 ||
            json.IndexOf("\"currentLevelId\"", StringComparison.Ordinal) < 0 ||
            json.IndexOf("\"upgrades\"", StringComparison.Ordinal) < 0 ||
            json.IndexOf("\"levelState\"", StringComparison.Ordinal) < 0)
            return false;
        return string.Equals(JsonUtility.ToJson(run), JsonUtility.ToJson(new ActiveRunData()), StringComparison.Ordinal);
    }

    // JsonUtility materializes a missing nested class as a default instance.
    // Older envelopes did not contain a terminal result, and an explicit null
    // reference may be emitted as `{}`, so restore the intended nullable state.
    private static bool IsMissingOrInlineNullResult(string json)
    {
        const string field = "\"lastRunResult\"";
        int name = json.IndexOf(field, StringComparison.Ordinal);
        if (name < 0) return true;
        int cursor = name + field.Length;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
        if (cursor >= json.Length || json[cursor] != ':') return false;
        cursor++;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
        if (cursor + 3 < json.Length && string.Compare(json, cursor, "null", 0, 4, StringComparison.Ordinal) == 0)
            return true;
        if (cursor >= json.Length || json[cursor] != '{') return false;
        cursor++;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
        return cursor < json.Length && json[cursor] == '}';
    }

    private static bool HasRequiredEnvelopeData(string json, SaveEnvelopeData envelope)
    {
        if (envelope == null || envelope.profile == null)
            return false;
        if (json.IndexOf("\"saveVersion\"", StringComparison.Ordinal) < 0 ||
            json.IndexOf("\"profile\"", StringComparison.Ordinal) < 0 ||
            envelope.saveVersion <= 0 || envelope.profile.saveVersion <= 0)
            return false;

        ActiveRunData run = envelope.activeRun;
        if (run == null)
            return true;
        if (string.IsNullOrEmpty(run.runId) || run.runSaveVersion <= 0 || run.upgrades == null)
            return false;
        return run.betweenLevels || run.levelState != null;
    }
    public static void Normalize(SaveEnvelopeData envelope, GameConfig config = null)
    {
        if(envelope==null)return; NormalizeLastRunResult(envelope.lastRunResult); envelope.saveVersion=Mathf.Max(1,envelope.saveVersion); if(envelope.profile==null)envelope.profile=PlayerProfileData.CreateDefault();
        var p=envelope.profile; p.saveVersion=Mathf.Max(1,p.saveVersion); p.coins=Math.Max(0,p.coins); p.lastBankedRunId=p.lastBankedRunId??string.Empty; if(!Enum.IsDefined(typeof(OnboardingStatus),p.onboardingStatus))p.onboardingStatus=OnboardingStatus.NeverSeen;
        if(config!=null) { UpgradeCatalog.EnsureDefaults(config); p.maximumHealthTier=UpgradeCatalog.ClampTier(config,UpgradeId.MaximumHealth,p.maximumHealthTier); p.startingReserveTier=UpgradeCatalog.ClampTier(config,UpgradeId.StartingReserve,p.startingReserveTier); p.gridStabilizerTier=UpgradeCatalog.ClampTier(config,UpgradeId.GridStabilizer,p.gridStabilizerTier); p.reverseResistanceTier=UpgradeCatalog.ClampTier(config,UpgradeId.ReverseResistance,p.reverseResistanceTier); }
        else { p.maximumHealthTier=Mathf.Max(0,p.maximumHealthTier); p.startingReserveTier=Mathf.Max(0,p.startingReserveTier); p.gridStabilizerTier=Mathf.Max(0,p.gridStabilizerTier); p.reverseResistanceTier=Mathf.Max(0,p.reverseResistanceTier); }
        var run=envelope.activeRun; if(run==null)return; run.runId=run.runId??string.Empty; run.runSaveVersion=Mathf.Max(1,run.runSaveVersion); run.currentLevelIndex=Mathf.Max(0,run.currentLevelIndex); run.currentHealth=Mathf.Max(0,run.currentHealth); run.currentReserveSeconds=FiniteNonNegative(run.currentReserveSeconds); run.pendingCoins=Math.Max(0,run.pendingCoins); run.levelsCompleted=Math.Max(0,run.levelsCompleted); run.currentLevelId=run.currentLevelId??string.Empty; run.randomState=EncodeRandomState(ParseRandomState(run.randomState)); if(run.upgrades==null)run.upgrades=new UpgradeSnapshotData(); if(run.levelState==null)run.levelState=new ActiveLevelStateData();
        var u=run.upgrades; u.maxHealth=Mathf.Clamp(u.maxHealth,1,20); run.currentHealth=Mathf.Clamp(run.currentHealth,0,u.maxHealth); u.startingReserveSeconds=FiniteNonNegative(u.startingReserveSeconds); u.gridStabilizerMultiplier=Mathf.Clamp(FiniteNonNegative(u.gridStabilizerMultiplier),.01f,1f); u.reverseCooldownBonusSeconds=FiniteNonNegative(u.reverseCooldownBonusSeconds);
        var l=run.levelState; l.normalTimeRemaining=FiniteNonNegative(l.normalTimeRemaining); l.reverseCooldownRemaining=FiniteNonNegative(l.reverseCooldownRemaining); l.objectiveProgress=Mathf.Max(0,l.objectiveProgress); l.reverseCorrectTapsRemaining=Mathf.Max(0,l.reverseCorrectTapsRemaining); l.smallIndex=Mathf.Max(-1,l.smallIndex);l.mediumIndex=Mathf.Max(-1,l.mediumIndex);l.largeIndex=Mathf.Max(-1,l.largeIndex); l.scalePhase=Mathf.Clamp01(FiniteNonNegative(l.scalePhase)); l.scaleDirection=l.scaleDirection<0?-1:1; if(!Finite(l.rotationAngle))l.rotationAngle=0; if(!Finite(l.movementPosition.x)||!Finite(l.movementPosition.y))l.movementPosition=Vector2.zero; if(!Finite(l.movementDirection.x)||!Finite(l.movementDirection.y)||l.movementDirection.sqrMagnitude<.0001f)l.movementDirection=Vector2.one.normalized;
    }
    private static void NormalizeLastRunResult(RunResultSnapshotData result)
    {
        if (result == null) return;
        result.runId = result.runId ?? string.Empty;
        result.reason = result.reason ?? string.Empty;
        result.highestLevelEntered = Mathf.Max(0, result.highestLevelEntered);
        result.levelsCompleted = Mathf.Max(0, result.levelsCompleted);
        result.runLevelRewards = Math.Max(0L, result.runLevelRewards);
        result.completionBonus = Math.Max(0L, result.completionBonus);
        result.totalEarned = Math.Max(0L, result.totalEarned);
        // This is the historic post-transaction balance, not the live wallet.
        result.newWalletBalance = Math.Max(0L, result.newWalletBalance);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float FiniteNonNegative(float value) => Finite(value) ? Mathf.Max(0,value) : 0f;
}
