#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bounded real-runtime verification for Rebound and Healing. The caller owns
/// the isolated NeonPresentationQA session; this helper never changes authored
/// campaign data, assets, PlayerPrefs, or a normal player save.
/// </summary>
public static class NeonRecoveryFeatureQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null || check == null)
            throw new ArgumentNullException(gm == null ? nameof(gm) : nameof(check));
        NeonSaveService saves = Field<NeonSaveService>(gm, "saveService");
        if (!NeonPresentationQAProfile.IsActive || saves == null || saves.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Recovery QA requires NeonPresentationQAProfile isolated storage.");

        bool wasEnabled = gm.enabled;
        try
        {
            yield return StartMotionFixture(gm);
            yield return VerifyRebound(gm, capture, check);
            yield return VerifyHeartLifecycle(gm, capture, check);
            yield return VerifySettingsFreeze(gm, check);
            yield return VerifySavedRecoveryRestore(gm, check);
            yield return CaptureBottomShopCards(gm, capture);
        }
        finally
        {
            if (gm != null)
            {
                gm.enabled = true;
                gm.ReturnToMainMenu();
                gm.enabled = wasEnabled;
            }
        }
    }

    private static IEnumerator StartMotionFixture(GameManager gm)
    {
        int levelIndex = FindMotionLevel(gm.campaign);
        if (levelIndex < 0)
            throw new InvalidOperationException("Recovery QA requires one authored moving, rotating, or scaling campaign level.");
        gm.enabled = true;
        gm.ReturnToMainMenu();
        gm.StartDebugLevel(levelIndex);
        yield return WaitForFlow(gm, "Playing");
        gm.enabled = false;

        ActiveRunData run = RunData(gm);
        run.currentHealth = Mathf.Max(6, run.upgrades.maxHealth);
        run.upgrades.maxHealth = run.currentHealth;
        run.upgrades.reboundOwned = true;
        run.upgrades.healingTier = 1;
        run.upgrades.healingLifetimeSeconds = .75f;
        run.upgrades.reboundDurationSeconds = 1f;
        run.upgrades.reboundMotionMultiplier = .5f;
        run.upgrades.reboundRecoveryBlendSeconds = .2f;
        run.levelState.normalTimeRemaining = 10f;
        run.levelState.reserveActive = false;
        run.currentReserveSeconds = 8f;
        ReboundRules.Cancel(run.levelState.rebound);
        HeartRules.Clear(run.levelState.heart);
        Invoke(gm, "ConfigureGameplayFeatures");
        Invoke(gm, "UpdateGameplayUI");
    }

    private static IEnumerator VerifyRebound(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        ActiveRunData run = RunData(gm);
        ActiveLevelStateData level = run.levelState;
        int wrong = WrongCell(gm, -1);
        int health = run.currentHealth;
        float normal = level.normalTimeRemaining;
        float activeBefore;

        Tap(gm, wrong);
        yield return null; // Separate accepted taps into distinct pointer frames.
        check(level.rebound.active && run.currentHealth == health - 1 && Mathf.Approximately(level.normalTimeRemaining, normal),
            "Owned Rebound starts once after an actual nonfatal wrong-grid tap and holds normal time immediately.");

        activeBefore = level.rebound.activeRemainingSeconds;
        Tap(gm, WrongCell(gm, -1));
        yield return null;
        check(run.currentHealth == health - 2 && Mathf.Approximately(level.rebound.activeRemainingSeconds, activeBefore),
            "A second actual wrong tap still damages health without extending or stacking Rebound.");

        float angle = level.rotationAngle;
        Vector2 position = level.movementPosition;
        float scale = level.scalePhase;
        Invoke(gm, "TickActiveGameplay", .2f);
        check(Mathf.Approximately(level.normalTimeRemaining, normal) && HasHalfMotion(gm, angle, position, scale, .2f),
            "While Rebound holds time, the authored grid motion advances at half its otherwise effective rate.");
        NeonHudMotion hud = gm.gameplayPanel.GetComponentInChildren<NeonHudMotion>(true);
        yield return new WaitForSecondsRealtime(.06f);
        hud?.SetPaused(true);
        if (capture != null) yield return capture("recovery-01-rebound-health");
        hud?.SetPaused(false);

        level.rebound.active = true;
        level.rebound.activeRemainingSeconds = .01f;
        level.rebound.motionRecoveryRemainingSeconds = 0f;
        normal = level.normalTimeRemaining;
        Invoke(gm, "TickActiveGameplay", .016f);
        check(!level.rebound.active && Mathf.Abs(level.normalTimeRemaining - (normal - .006f)) < .001f,
            "Rebound expiry drains only the exact .006-second overshoot rather than granting a free frame.");

        level.reserveActive = true;
        level.normalTimeRemaining = 0f;
        run.currentReserveSeconds = 5f;
        ReboundRules.Cancel(level.rebound);
        int reserveHealth = run.currentHealth;
        Tap(gm, WrongCell(gm, -1));
        yield return null;
        float reserve = run.currentReserveSeconds;
        Invoke(gm, "TickActiveGameplay", .25f);
        check(level.rebound.active && run.currentHealth == reserveHealth - 1 && Mathf.Approximately(run.currentReserveSeconds, reserve),
            "The same wrong-tap trigger holds selected reserve drain without switching the timer mode.");

        int objective = level.objectiveProgress;
        level.objectiveProgress = -100; // Prevent this QA tap from completing its authored objective.
        Tap(gm, CorrectCell(run));
        yield return null;
        check(!level.rebound.active && level.rebound.motionRecoveryRemainingSeconds > 0f && run.currentReserveSeconds <= reserve,
            "An accepted correct outline ends Rebound's timer hold immediately while retaining only the motion recovery blend.");
        level.objectiveProgress = objective;
    }

    private static IEnumerator VerifyHeartLifecycle(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        ActiveRunData run = RunData(gm);
        ActiveLevelStateData level = run.levelState;
        ReboundRules.Cancel(level.rebound);
        level.reserveActive = false;
        level.normalTimeRemaining = 10f;
        run.currentHealth = Mathf.Max(1, run.upgrades.maxHealth - 1);
        int cell = WrongCell(gm, -1);
        int beforeProgress = level.objectiveProgress;
        int beforeHealth = run.currentHealth;
        level.heart = new HeartStateData
        {
            cellIndex = cell,
            phase = HeartPhase.Entering,
            phaseRemainingSeconds = .08f,
            visibleRemainingSeconds = .75f
        };
        Invoke(gm, "RenderHeart");
        check(level.heart.IsReserved && !ContainsTarget(level, cell),
            "An entering heart reserves an unoccupied grid cell before it becomes fully visible.");
        Invoke(gm, "TickActiveGameplay", .04f);
        if (capture != null) yield return capture("recovery-02-heart-entering");

        Invoke(gm, "TickActiveGameplay", .04f);
        check(level.heart.phase == HeartPhase.Available && level.heart.IsActionable,
            "Heart entrance advances to an explicitly available, actionable state on active gameplay time.");

        Tap(gm, cell);
        yield return null;
        check(run.currentHealth == beforeHealth + 1 && level.objectiveProgress == beforeProgress && level.heart.phase == HeartPhase.Retiring,
            "An available heart tap heals once and neither advances nor damages the outline sequence.");

        Invoke(gm, "TickActiveGameplay", .15f);
        check(!level.heart.IsReserved, "Heart reservation releases only after its visible collection exit completes.");

        cell = WrongCell(gm, -1);
        level.heart = new HeartStateData { cellIndex = cell, phase = HeartPhase.Available, phaseRemainingSeconds = .01f, visibleRemainingSeconds = .01f };
        Invoke(gm, "RenderHeart");
        Invoke(gm, "TickActiveGameplay", .01f);
        check(level.heart.phase == HeartPhase.Retiring && HeartRules.ConsumesCellTap(level.heart, cell),
            "Expiry enters a retiring state that still reserves and consumes its cell tap.");
        int noLeakHealth = run.currentHealth;
        beforeProgress = level.objectiveProgress;
        Tap(gm, cell);
        yield return null;
        check(run.currentHealth == noLeakHealth && level.objectiveProgress == beforeProgress,
            "A retiring heart visual consumes its tap without a second heal, target progress, or wrong-tap damage leak.");
        if (capture != null) yield return capture("recovery-03-heart-retiring");
        Invoke(gm, "TickActiveGameplay", .15f);
        check(!level.heart.IsReserved, "Expired heart reservation eventually releases after its bounded exit.");
    }

    private static IEnumerator VerifySettingsFreeze(GameManager gm, Action<bool, string> check)
    {
        ActiveRunData run = RunData(gm);
        run.levelState.rebound.active = true;
        run.levelState.rebound.activeRemainingSeconds = .5f;
        run.levelState.heart = new HeartStateData { cellIndex = WrongCell(gm, -1), phase = HeartPhase.Available, phaseRemainingSeconds = .5f, visibleRemainingSeconds = .5f };
        float rebound = run.levelState.rebound.activeRemainingSeconds;
        float heart = run.levelState.heart.phaseRemainingSeconds;
        gm.enabled = true;
        gm.OpenSettings();
        yield return null;
        gm.enabled = false;
        Invoke(gm, "TickActiveGameplay", .25f);
        check(Mathf.Approximately(run.levelState.rebound.activeRemainingSeconds, rebound) && Mathf.Approximately(run.levelState.heart.phaseRemainingSeconds, heart),
            "Settings pause freezes Rebound and heart active-gameplay clocks without catch-up.");
        gm.enabled = true;
        gm.CloseSettings();
        yield return WaitForFlow(gm, "Playing");
        gm.enabled = false;
    }

    private static IEnumerator VerifySavedRecoveryRestore(GameManager gm, Action<bool, string> check)
    {
        gm.enabled = true;
        gm.ReturnToMainMenu();
        yield return null;
        SaveEnvelopeData envelope = Field<SaveEnvelopeData>(gm, "saveData");
        envelope.activeRun = null;
        envelope.lastRunResult = null;
        envelope.profile.hasStartedRealRun = true;
        envelope.profile.onboardingStatus = OnboardingStatus.Completed;
        gm.StartNewRun();
        yield return WaitForFlow(gm, "Playing");
        gm.enabled = false;

        ActiveRunData run = RunData(gm);
        run.upgrades.reboundOwned = true;
        run.upgrades.reboundDurationSeconds = 1f;
        run.upgrades.reboundMotionMultiplier = .5f;
        run.upgrades.reboundRecoveryBlendSeconds = .2f;
        run.upgrades.healingTier = 1;
        run.upgrades.healingLifetimeSeconds = .75f;
        // Long enough that the presentation handoff itself cannot consume the
        // fixture state before Continue reaches its ready frame.
        run.levelState.rebound = new ReboundStateData { active = true, activeRemainingSeconds = 5f };
        int cell = WrongCell(gm, -1);
        run.levelState.heart = new HeartStateData { cellIndex = cell, phase = HeartPhase.Retiring, phaseRemainingSeconds = 5f };
        Invoke(gm, "SaveRealRunCritical");
        gm.enabled = true;
        gm.ReturnToMainMenu();
        yield return null;
        gm.ContinueRun();
        yield return WaitForFlow(gm, "Playing");

        ActiveLevelStateData restored = RunData(gm).levelState;
        check(restored.rebound.active && restored.rebound.activeRemainingSeconds > 4.5f &&
              restored.heart.IsReserved && restored.heart.cellIndex == cell && restored.heart.phase == HeartPhase.Retiring && restored.heart.phaseRemainingSeconds > 4.5f,
            "Save then Continue preserves active Rebound and a retiring reserved heart without replaying either reward.");

        // A replay must suspend this exact live checkpoint and put a clean
        // practice fixture in front of it. This exercises the replay return
        // path while both recovery states are still materially active.
        ActiveRunData original = RunData(gm);
        original.levelState.rebound = new ReboundStateData { active = true, activeRemainingSeconds = 15f };
        original.levelState.heart = new HeartStateData { cellIndex = WrongCell(gm, -1), phase = HeartPhase.Retiring, phaseRemainingSeconds = 15f };
        Invoke(gm, "RenderHeart");
        gm.OpenSettings();
        yield return WaitForFlow(gm, "Settings");
        string frozenLevelState = JsonUtility.ToJson(original.levelState);
        gm.BeginOnboardingReplay();
        yield return WaitForOnboardingFirstTap(gm);

        ActiveRunData practice = RunData(gm);
        LevelData practiceLevel = Field<LevelData>(gm, "activeLevel");
        bool cleanPractice = !practice.upgrades.reboundOwned && practice.upgrades.healingTier == 0 &&
            (practice.levelState.rebound == null || !practice.levelState.rebound.active) &&
            (practice.levelState.heart == null || !practice.levelState.heart.IsReserved) &&
            (practiceLevel.enemies == null || !practiceLevel.enemies.enabled);
        check(cleanPractice,
            "Settings replay first-tap practice has no Rebound, Healing heart, or enemy state from the frozen run.");

        gm.SkipOnboarding();
        yield return WaitForFlow(gm, "Settings");
        ActiveLevelStateData replayRestored = RunData(gm).levelState;
        check(JsonUtility.ToJson(replayRestored) == frozenLevelState && replayRestored.rebound.active &&
              replayRestored.heart.IsReserved && replayRestored.heart.phase == HeartPhase.Retiring,
            "Skipping Settings replay restores the frozen Rebound and retiring-heart level state byte-for-byte.");
        gm.enabled = false;
    }

    private static IEnumerator CaptureBottomShopCards(GameManager gm, Func<string, IEnumerator> capture)
    {
        if (capture == null) yield break;
        gm.enabled = true;
        gm.ReturnToMainMenu();
        yield return WaitForFlow(gm, "MainMenu");
        gm.OpenUpgradeShop();
        yield return WaitForFlow(gm, "Shop");

        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        GameObject shopPanel = Field<GameObject>(ui, "shopPanel");
        UnityEngine.UI.ScrollRect scroll = shopPanel == null ? null : shopPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (scroll != null)
        {
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
        yield return capture("recovery-04-shop-rebound-healing");
    }

    private static int FindMotionLevel(CampaignDefinition campaign)
    {
        if (campaign == null) return -1;
        for (int i = 0; i < campaign.LevelCount; i++)
        {
            LevelData level = campaign.GetLevel(i);
            if (level != null && (!Mathf.Approximately(level.rotateSpeed, 0f) || level.movementEnabled || level.scaleEnabled)) return i;
        }
        return -1;
    }

    private static bool HasHalfMotion(GameManager gm, float angleBefore, Vector2 positionBefore, float scaleBefore, float delta)
    {
        ActiveRunData run = RunData(gm);
        LevelData level = Field<LevelData>(gm, "activeLevel");
        float stabilizer = Mathf.Clamp(run.upgrades.gridStabilizerMultiplier, .01f, 1f);
        if (!Mathf.Approximately(level.rotateSpeed, 0f))
        {
            float expected = level.rotateSpeed * stabilizer * delta * .5f;
            return Mathf.Abs(Mathf.DeltaAngle(angleBefore, run.levelState.rotationAngle) - expected) < .01f;
        }
        if (level.movementEnabled)
            return (run.levelState.movementPosition - positionBefore).sqrMagnitude > .0000001f;
        return !Mathf.Approximately(run.levelState.scalePhase, scaleBefore);
    }

    private static int CorrectCell(ActiveRunData run) => run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex;
    private static int WrongCell(GameManager gm, int excluded)
    {
        ActiveRunData run = RunData(gm);
        int correct = CorrectCell(run);
        List<GameSquare> squares = Field<List<GameSquare>>(gm, "instantiatedSquares");
        for (int i = 0; i < squares.Count; i++)
            if (i != correct && i != excluded && !ContainsTarget(run.levelState, i)) return i;
        for (int i = 0; i < squares.Count; i++) if (i != correct && i != excluded) return i;
        throw new InvalidOperationException("Fixture has no selectable non-target cell.");
    }

    private static bool ContainsTarget(ActiveLevelStateData level, int cell)
    {
        if (level.targetIndices != null)
            for (int i = 0; i < level.targetIndices.Length; i++) if (level.targetIndices[i] == cell) return true;
        return level.smallIndex == cell || level.mediumIndex == cell || level.largeIndex == cell;
    }

    private static void Tap(GameManager gm, int cell)
    {
        Field<List<GameSquare>>(gm, "instantiatedSquares")[cell].OnPointerDown(new PointerEventData(EventSystem.current));
        Canvas.ForceUpdateCanvases();
    }

    private static IEnumerator WaitForFlow(GameManager gm, string expected)
    {
        float until = Time.realtimeSinceStartup + 4f;
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        while ((Flow(gm) != expected || (expected == "Playing" && (ui == null || !ui.IsGameplayReady))) && Time.realtimeSinceStartup < until)
            yield return null;
        if (Flow(gm) != expected || (expected == "Playing" && (ui == null || !ui.IsGameplayReady)))
            throw new InvalidOperationException("Timed out waiting for " + expected + "; got " + Flow(gm));
        yield return null;
    }

    private static IEnumerator WaitForOnboardingFirstTap(GameManager gm)
    {
        float until = Time.realtimeSinceStartup + 4f;
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        while ((!gm.IsOnboardingActive || gm.CurrentOnboardingStep != OnboardingStep.FirstTap || Flow(gm) != "Onboarding" ||
                ui == null || !ui.IsGameplayReady || !ui.OnboardingPresentationReady) && Time.realtimeSinceStartup < until)
            yield return null;
        if (!gm.IsOnboardingActive || gm.CurrentOnboardingStep != OnboardingStep.FirstTap || Flow(gm) != "Onboarding" ||
            ui == null || !ui.IsGameplayReady || !ui.OnboardingPresentationReady)
            throw new InvalidOperationException("Timed out waiting for onboarding first-tap practice to become ready.");
        yield return null;
    }

    private static ActiveRunData RunData(GameManager gm) => Field<ActiveRunData>(gm, "sessionRun");
    private static string Flow(GameManager gm) => Field<object>(gm, "state").ToString();
    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, Hidden);
        if (method == null) throw new MissingMethodException(target.GetType().Name, name);
        return method.Invoke(target, args);
    }
    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Hidden);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }
}
#endif
