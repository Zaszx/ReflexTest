using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ReboundStateData
{
    public bool active;
    public float activeRemainingSeconds;
    public float motionRecoveryRemainingSeconds;
}

public readonly struct ReboundTickResult
{
    public readonly float resourceDrainSeconds;
    public readonly bool ended;
    public ReboundTickResult(float resourceDrainSeconds, bool ended)
    { this.resourceDrainSeconds = Mathf.Max(0f, resourceDrainSeconds); this.ended = ended; }
}

/// <summary>Pure Rebound state machine. Its active clock is deliberately independent of the resource clock.</summary>
public static class ReboundRules
{
    public static bool TryActivate(ReboundStateData state, bool owned, bool nonFatalWrongGridTap, RecoverySettings settings = null)
    {
        if (state == null || !owned || !nonFatalWrongGridTap || state.active) return false;
        settings = settings ?? RecoverySettingsProvider.Current;
        state.active = true;
        state.activeRemainingSeconds = Mathf.Max(0f, settings.reboundDurationSeconds);
        state.motionRecoveryRemainingSeconds = 0f;
        if (state.activeRemainingSeconds <= 0f) End(state, settings);
        return true;
    }

    /// <summary>Ends the timer hold now; only motion may blend afterwards.</summary>
    public static bool EndOnCorrect(ReboundStateData state, RecoverySettings settings = null)
    {
        if (state == null || !state.active) return false;
        End(state, settings ?? RecoverySettingsProvider.Current);
        return true;
    }

    public static void Cancel(ReboundStateData state)
    {
        if (state == null) return;
        state.active = false;
        state.activeRemainingSeconds = 0f;
        state.motionRecoveryRemainingSeconds = 0f;
    }

    /// <summary>
    /// Advances with active-gameplay time. resourceDrainSeconds is the exact slice that should be passed to GameplayTimerRules.Tick.
    /// A frame crossing expiry drains its overshoot immediately, so Rebound never grants an extra frame.
    /// </summary>
    public static ReboundTickResult Tick(ReboundStateData state, float activeGameplayDelta, RecoverySettings settings = null)
    {
        if (state == null || activeGameplayDelta <= 0f || float.IsNaN(activeGameplayDelta)) return new ReboundTickResult(Mathf.Max(0f, activeGameplayDelta), false);
        settings = settings ?? RecoverySettingsProvider.Current;
        float delta = Mathf.Max(0f, activeGameplayDelta);
        bool ended = false;
        float resourceDrain = delta;
        float recoveryDelta = delta;
        if (state.active)
        {
            float held = Mathf.Min(delta, Mathf.Max(0f, state.activeRemainingSeconds));
            state.activeRemainingSeconds = Mathf.Max(0f, state.activeRemainingSeconds - delta);
            resourceDrain = delta - held;
            if (state.activeRemainingSeconds <= 0f)
            {
                End(state, settings);
                ended = true;
            }
            // Recovery blending starts at the active-window boundary. It must
            // not consume the held portion of an expiry-crossing frame.
            recoveryDelta = resourceDrain;
        }
        if (state.motionRecoveryRemainingSeconds > 0f)
            state.motionRecoveryRemainingSeconds = Mathf.Max(0f, state.motionRecoveryRemainingSeconds - recoveryDelta);
        return new ReboundTickResult(resourceDrain, ended);
    }

    public static float MotionMultiplier(ReboundStateData state, RecoverySettings settings = null)
    {
        settings = settings ?? RecoverySettingsProvider.Current;
        float slowed = Mathf.Clamp(settings.reboundMotionMultiplier, .01f, 1f);
        if (state != null && state.active) return slowed;
        float blend = Mathf.Max(0f, settings.reboundRecoveryBlendSeconds);
        if (state == null || state.motionRecoveryRemainingSeconds <= 0f || blend <= 0f) return 1f;
        float elapsed = 1f - Mathf.Clamp01(state.motionRecoveryRemainingSeconds / blend);
        return Mathf.Lerp(slowed, 1f, Mathf.SmoothStep(0f, 1f, elapsed));
    }

    private static void End(ReboundStateData state, RecoverySettings settings)
    {
        state.active = false;
        state.activeRemainingSeconds = 0f;
        state.motionRecoveryRemainingSeconds = Mathf.Max(0f, settings.reboundRecoveryBlendSeconds);
    }
}

public enum HeartPhase { None, Entering, Available, Retiring }

[Serializable]
public sealed class HeartStateData
{
    public int cellIndex = -1;
    public HeartPhase phase;
    public float phaseRemainingSeconds;
    public float visibleRemainingSeconds;
    public bool IsReserved => cellIndex >= 0 && phase != HeartPhase.None;
    public bool IsActionable => cellIndex >= 0 && (phase == HeartPhase.Entering || phase == HeartPhase.Available);
}

public enum HeartTickResult { None, BecameAvailable, Expired, Retired }

/// <summary>Pure deterministic heart availability and reservation rules. A level owns its separate heart RNG stream.</summary>
public static class HeartRules
{
    public static uint DeriveLevelRandomState(int runSeed, int levelIndex)
    {
        uint value = unchecked((uint)runSeed) ^ 0xA511E9B3u ^ unchecked((uint)levelIndex * 0x9E3779B9u);
        value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
        return value == 0 ? 1u : value;
    }

    public static void BeginLevel(HeartStateData heart, ref float spawnCooldownSeconds, ref int spawnedThisLevel, RecoverySettings settings = null)
    {
        if (heart != null) Clear(heart);
        settings = settings ?? RecoverySettingsProvider.Current;
        spawnCooldownSeconds = Mathf.Max(0f, settings.firstHeartEligibilityDelaySeconds);
        spawnedThisLevel = 0;
    }

    public static HeartTickResult Tick(HeartStateData heart, ref float spawnCooldownSeconds, float activeGameplayDelta, RecoverySettings settings = null)
    {
        if (heart == null || activeGameplayDelta <= 0f || float.IsNaN(activeGameplayDelta)) return HeartTickResult.None;
        settings = settings ?? RecoverySettingsProvider.Current;
        float remaining = Mathf.Max(0f, activeGameplayDelta);
        spawnCooldownSeconds = Mathf.Max(0f, spawnCooldownSeconds - remaining);
        while (remaining > 0f && heart.phase != HeartPhase.None)
        {
            float phase = Mathf.Max(0f, heart.phaseRemainingSeconds);
            if (phase > remaining)
            {
                heart.phaseRemainingSeconds = phase - remaining;
                if (heart.phase == HeartPhase.Available) heart.visibleRemainingSeconds = heart.phaseRemainingSeconds;
                return HeartTickResult.None;
            }
            remaining -= phase;
            if (heart.phase == HeartPhase.Entering)
            {
                heart.phase = HeartPhase.Available;
                heart.phaseRemainingSeconds = Mathf.Max(0f, heart.visibleRemainingSeconds);
                if (remaining <= 0f) return HeartTickResult.BecameAvailable;
            }
            else if (heart.phase == HeartPhase.Available)
            {
                Retire(heart, settings);
                if (remaining <= 0f) return HeartTickResult.Expired;
            }
            else
            {
                Clear(heart);
                return HeartTickResult.Retired;
            }
        }
        return HeartTickResult.None;
    }

    public static bool TrySpawnAfterCorrect(HeartStateData heart, ref float spawnCooldownSeconds, ref int spawnedThisLevel,
        bool healingOwned, int currentHealth, int maximumHealth, int cellCount, ICollection<int> activeTargetCells,
        ref DeterministicRandom random, RecoverySettings settings = null)
    {
        settings = settings ?? RecoverySettingsProvider.Current;
        if (heart == null || !healingOwned || heart.IsReserved || currentHealth >= maximumHealth || spawnCooldownSeconds > 0f ||
            spawnedThisLevel >= Mathf.Max(1, settings.maxHeartsPerLevel) || cellCount <= 0 || activeTargetCells == null) return false;
        var occupied = new HashSet<int>();
        foreach (int cell in activeTargetCells) if (cell >= 0 && cell < cellCount) occupied.Add(cell);
        // The reserved heart plus all active targets must fit. The subsequently consumed target cell is a valid replacement.
        if (occupied.Count >= cellCount || random.NextFloat01() >= Mathf.Clamp01(settings.heartSpawnChanceAfterCorrect)) return false;
        int choices = cellCount - occupied.Count;
        int pick = random.Range(0, choices);
        int chosen = -1;
        for (int i = 0; i < cellCount; i++) if (!occupied.Contains(i) && pick-- == 0) { chosen = i; break; }
        if (chosen < 0) return false;
        heart.cellIndex = chosen;
        heart.phase = HeartPhase.Entering;
        heart.phaseRemainingSeconds = Mathf.Max(0f, settings.heartEntranceSeconds);
        heart.visibleRemainingSeconds = 0f; // Caller assigns the captured tier lifetime below.
        spawnCooldownSeconds = Mathf.Max(0f, settings.heartSpawnCooldownSeconds);
        spawnedThisLevel++;
        return true;
    }

    public static void SetVisibleLifetime(HeartStateData heart, float visibleLifetimeSeconds)
    {
        if (heart == null || !heart.IsReserved) return;
        heart.visibleRemainingSeconds = Mathf.Max(0f, visibleLifetimeSeconds);
        if (heart.phase == HeartPhase.Available) heart.phaseRemainingSeconds = heart.visibleRemainingSeconds;
    }

    /// <summary>Returns true only for an actionable heart; a retiring heart still consumes its cell tap but yields false.</summary>
    public static bool TryCollect(HeartStateData heart, int tappedCell, RecoverySettings settings = null)
    {
        if (heart == null || !heart.IsReserved || heart.cellIndex != tappedCell) return false;
        if (!heart.IsActionable) return false;
        Retire(heart, settings ?? RecoverySettingsProvider.Current);
        return true;
    }

    public static bool ConsumesCellTap(HeartStateData heart, int tappedCell) => heart != null && heart.IsReserved && heart.cellIndex == tappedCell;

    public static void Retire(HeartStateData heart, RecoverySettings settings = null)
    {
        if (heart == null || !heart.IsReserved) return;
        settings = settings ?? RecoverySettingsProvider.Current;
        heart.phase = HeartPhase.Retiring;
        heart.phaseRemainingSeconds = Mathf.Max(0f, settings.heartExitSeconds);
        heart.visibleRemainingSeconds = 0f;
        if (heart.phaseRemainingSeconds <= 0f) Clear(heart);
    }

    public static void Clear(HeartStateData heart)
    {
        if (heart == null) return;
        heart.cellIndex = -1;
        heart.phase = HeartPhase.None;
        heart.phaseRemainingSeconds = 0f;
        heart.visibleRemainingSeconds = 0f;
    }
}
