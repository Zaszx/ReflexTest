using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class GameManager
{
    private RecoverySettings runRecoverySettings;
    private NeonUpgradeGraphic heartView;
    private NeonEnemyController enemyController;
    private EnemySettings enemySettings;
    private int claimedPointerFrame = -1;
    private readonly HashSet<int> claimedPointers = new HashSet<int>();
    private RecoverySettings RunRecoverySettings => runRecoverySettings != null ? runRecoverySettings : RecoverySettingsProvider.Current;
    private int ReservedHeartCell => !IsOnboardingActive && sessionRun?.levelState?.heart?.IsReserved == true
        ? sessionRun.levelState.heart.cellIndex : -1;

    private static GridSequenceState ReadSequence(ActiveLevelStateData level)
    {
        return level.targetIndices != null && level.targetIndices.Length >= 2
            ? new GridSequenceState(level.targetIndices)
            : new GridSequenceState(level.smallIndex, level.mediumIndex, level.largeIndex);
    }

    private static void CommitSequence(ActiveLevelStateData level, GridSequenceState sequence)
    {
        level.targetIndices = sequence.targets;
        level.effectiveOutlineCount = sequence.Count;
        level.smallIndex = sequence.small;
        level.mediumIndex = sequence.medium;
        level.largeIndex = sequence.large;
    }

    private void InitializeRecoveryLevel()
    {
        var l = sessionRun.levelState;
        HeartRules.BeginLevel(l.heart, ref l.heartSpawnCooldownRemaining, ref l.heartsSpawnedThisLevel);
        l.heartRandomState = NeonSaveService.EncodeRandomState(HeartRules.DeriveLevelRandomState(sessionRun.runSeed, sessionRun.currentLevelIndex));
        l.enemies.randomState = HeartRules.DeriveLevelRandomState(sessionRun.runSeed ^ 0x234A783, sessionRun.currentLevelIndex);
    }

    private void ConfigureGameplayFeatures()
    {
        if (runRecoverySettings != null) Destroy(runRecoverySettings);
        runRecoverySettings = Instantiate(RecoverySettingsProvider.Current);
        runRecoverySettings.hideFlags = HideFlags.DontSave;
        var snapshot = sessionRun.upgrades;
        if (snapshot.reboundOwned)
        {
            runRecoverySettings.reboundDurationSeconds = snapshot.reboundDurationSeconds;
            runRecoverySettings.reboundMotionMultiplier = snapshot.reboundMotionMultiplier;
            runRecoverySettings.reboundRecoveryBlendSeconds = snapshot.reboundRecoveryBlendSeconds;
        }
        if (enemyController == null) enemyController = gameObject.AddComponent<NeonEnemyController>();
        if (enemySettings == null) enemySettings = Resources.Load<EnemySettings>("EnemySettings");
        enemyController.Configure(sessionRun.levelState.enemies,
            IsOnboardingActive || activeLevel.levelNumber < EnemyRules.Resolve(enemySettings, activeLevel.enemies).minimumCampaignLevel
                ? null : activeLevel.enemies, enemySettings, boundsRoot, rotationScaleRoot,
            () => { if (SimulationIsActive) HandleMistake(null); },
            () => SimulationIsActive && rogueliteUI.IsGameplayReady && !rogueliteUI.IsSettingsVisible,
            EnemyRules.CreateStreamSeed(sessionRun.runSeed, sessionRun.currentLevelIndex), SaveRealRunCritical, activeLevel.levelNumber);
        ApplyGridMotionState(false, 0f);
        RenderHeart();
    }

    private void FitEnemyApproachCorridor(Vector2 arenaSize, float maximumScale, float rotationExtent)
    {
        if (IsOnboardingActive || activeLevel.enemies == null || !activeLevel.enemies.enabled) return;
        if (enemySettings == null) enemySettings = Resources.Load<EnemySettings>("EnemySettings");
        var tuning = EnemyRules.Resolve(enemySettings, activeLevel.enemies);
        if (activeLevel.levelNumber < tuning.minimumCampaignLevel) return;
        float shortest = Mathf.Max(1f, Mathf.Min(arenaSize.x, arenaSize.y));
        float travel = activeLevel.movementEnabled ? Mathf.Max(0f, gameConfig.movementTravelAllowanceNormalized) : 0f;
        // Leave a complete two-second corridor on at least one pair of allowed
        // arena edges. Candidate validation rejects the other, narrower edges.
        float edgeSpan = 0f;
        if ((tuning.spawnEdges & (EnemySpawnEdges.Left | EnemySpawnEdges.Right)) != 0) edgeSpan = arenaSize.x;
        if ((tuning.spawnEdges & (EnemySpawnEdges.Top | EnemySpawnEdges.Bottom)) != 0) edgeSpan = Mathf.Max(edgeSpan, arenaSize.y);
        float corridor = EnemyRules.GetEnemyApproachPaddingNormalized(tuning) + tuning.sizeNormalized * .5f;
        float side = (edgeSpan - 2f * shortest * (travel + corridor + .005f)) / Mathf.Max(.01f, maximumScale * rotationExtent);
        int count = Mathf.Max(2, activeLevel.gridSize);
        float minimumSide = gameConfig.minimumTouchTargetPixels * count / (1f - .025f * (count - 1));
        if (side >= minimumSide) baseGridSide = Mathf.Min(baseGridSide, side);
        else Debug.LogWarning($"Level {activeLevel.levelNumber}: enemy corridor cannot fit the configured readable grid. Unsafe spawn candidates will be skipped.");
    }

    private void UpdateEnemyMotionEnvelope(Vector2 translationRoom)
    {
        if (enemyController == null || activeLevel == null) return;
        float maxScale = activeLevel.scaleEnabled ? activeLevel.maximumGridScale : 1f;
        var maxHalf = GridMotionMath.TransformedAabbHalfExtents(baseGridSide, maxScale,
            Mathf.Approximately(activeLevel.rotateSpeed, 0f) ? 0f : 45f);
        // Full configured travel, not just the current rotated pose's spare room.
        float shortest = Mathf.Min(boundsRoot.rect.width, boundsRoot.rect.height);
        var envelope = activeLevel.movementEnabled ? Vector2.one * shortest * Mathf.Max(0f, gameConfig.movementTravelAllowanceNormalized) : Vector2.zero;
        enemyController.SetMotionEnvelope(maxHalf, envelope, activeLevel.rotateSpeed * Mathf.Clamp(sessionRun.upgrades.gridStabilizerMultiplier, .01f, 1f));
    }

    // The same active clock advances hazards and recovery. Only the resource clock
    // is held by Rebound; bounded steps also make fast moving-grid collisions safe.
    private void TickActiveGameplay(float delta)
    {
        float remaining = delta;
        bool enteredReserve = false;
        while (remaining > 0f && SimulationIsActive)
        {
            var l = sessionRun.levelState;
            float held = l.rebound.active ? Mathf.Max(0f, l.rebound.activeRemainingSeconds) : 0f;
            float available = CalculateAvailableGameplayDelta(remaining) + held;
            float step = Mathf.Min(remaining, Mathf.Min(1f / 90f, available));
            if (step <= 0f) { FailRun("RESERVE DEPLETED"); break; }
            float motion = ReboundRules.MotionMultiplier(l.rebound, RunRecoverySettings);
            var recovery = ReboundRules.Tick(l.rebound, step, RunRecoverySettings);
            GameplayTimerTransition timer = GameplayTimerRules.Tick(sessionRun, recovery.resourceDrainSeconds);
            AdvanceReverseCooldown(step);
            AdvanceGridMotion(step * motion);
            HeartRules.Tick(l.heart, ref l.heartSpawnCooldownRemaining, step, RunRecoverySettings);
            enemyController?.Tick(step);
            checkpointElapsed += step;
            remaining = Mathf.Max(0f, remaining - step);
            enteredReserve |= timer == GameplayTimerTransition.EnteredReserve;
            if (timer == GameplayTimerTransition.ReserveDepleted && SimulationIsActive)
            { UpdateGameplayUI(); FailRun("RESERVE DEPLETED"); break; }
        }
        if (!SimulationIsActive) return;
        RenderHeart();
        UpdateGameplayUI();
        saveDirty = true;
        if (enteredReserve || checkpointElapsed >= gameConfig.saveCheckpointIntervalSeconds)
            SaveRealRunCritical();
    }

    private bool GateGridPointer(PointerEventData pointer)
    {
        if (IsOnboardingActive) return OnboardingCanInteract;
        if (!SimulationIsActive || !rogueliteUI.IsGameplayReady || rogueliteUI.IsSettingsVisible) return false;
        if (claimedPointerFrame != Time.frameCount) { claimedPointerFrame = Time.frameCount; claimedPointers.Clear(); }
        if (pointer != null && !claimedPointers.Add(pointer.pointerId)) return false;
        if (pointer != null && enemyController != null && enemyController.TryConsumePointer(pointer))
        { SaveRealRunCritical(); return false; }
        return true;
    }

    private bool TryHandleHeartTap(int index)
    {
        var heart = sessionRun.levelState.heart;
        if (!HeartRules.ConsumesCellTap(heart, index)) return false;
        if (HeartRules.TryCollect(heart, index, RunRecoverySettings))
        {
            sessionRun.currentHealth = Mathf.Min(sessionRun.upgrades.maxHealth, sessionRun.currentHealth + 1);
            UpdateGameplayUI();
            rogueliteUI.PlayHealthHealingFeedback();
            SaveRealRunCritical();
        }
        RenderHeart();
        return true;
    }

    private void TrySpawnHeartAfterCorrect()
    {
        if (IsOnboardingActive) return;
        var l = sessionRun.levelState;
        var stream = new DeterministicRandom(NeonSaveService.ParseRandomState(l.heartRandomState));
        if (HeartRules.TrySpawnAfterCorrect(l.heart, ref l.heartSpawnCooldownRemaining, ref l.heartsSpawnedThisLevel,
            sessionRun.upgrades.healingTier > 0, sessionRun.currentHealth, sessionRun.upgrades.maxHealth,
            instantiatedSquares.Count, l.targetIndices, ref stream, RunRecoverySettings))
            HeartRules.SetVisibleLifetime(l.heart, sessionRun.upgrades.healingLifetimeSeconds);
        l.heartRandomState = NeonSaveService.EncodeRandomState(stream.State);
        RenderHeart();
    }

    private void RenderHeart()
    {
        var h = sessionRun?.levelState?.heart;
        if (IsOnboardingActive || h == null || !h.IsReserved || !IsValidSquareIndex(h.cellIndex))
        { if (heartView != null) heartView.gameObject.SetActive(false); return; }
        if (heartView == null)
        {
            var go = new GameObject("HealingHeart", typeof(RectTransform), typeof(CanvasRenderer));
            heartView = go.AddComponent<NeonUpgradeGraphic>();
            heartView.kind = NeonUpgradeGraphic.Kind.Health;
            heartView.raycastTarget = false;
        }
        heartView.gameObject.SetActive(true);
        RectTransform rect = heartView.rectTransform;
        rect.SetParent(instantiatedSquares[h.cellIndex].transform, false);
        rect.SetAsLastSibling();
        rect.anchorMin = Vector2.one * .26f;
        rect.anchorMax = Vector2.one * .74f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        float alpha = 1f;
        if (h.phase == HeartPhase.Entering)
            alpha = 1f - h.phaseRemainingSeconds / Mathf.Max(.001f, RunRecoverySettings.heartEntranceSeconds);
        else if (h.phase == HeartPhase.Retiring)
            alpha = h.phaseRemainingSeconds / Mathf.Max(.001f, RunRecoverySettings.heartExitSeconds);
        alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alpha));
        rect.localScale = Vector3.one * (NeonTheme.ReducedEffects ? 1f : Mathf.Lerp(.78f, 1f, alpha));
        heartView.color = new Color(.7f, 1f, .23f, alpha);
        heartView.SetVerticesDirty();
    }

    private void HideGameplayFeatureViews()
    {
        if (heartView != null) heartView.gameObject.SetActive(false);
        enemyController?.ResetVisuals();
        rogueliteUI?.SetReboundFeedback(false);
    }

    private void EndGameplayFeatures()
    {
        if (sessionRun?.levelState != null)
        {
            ReboundRules.Cancel(sessionRun.levelState.rebound);
            HeartRules.Clear(sessionRun.levelState.heart);
        }
        enemyController?.CancelLevel();
        HideGameplayFeatureViews();
    }
}
