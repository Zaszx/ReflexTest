using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameManager
{
    [Header("First-time player practice")]
    public NeonOnboardingSettings onboardingSettings;

    private enum PracticeExit { StartRun, ReturnToOrigin, Home }
    private sealed class PracticeSession
    {
        public OnboardingStep step;
        public bool replay;
        public bool skipped;
        public PracticeExit exit;
        public int correctInSection;
        public float demoElapsed;
        public FlowState origin;
        public ActiveRunData originalRun;
        public LevelData originalLevel;
        public bool originalDebug;
        public bool originalTerminal;
        public DeterministicRandom originalRandom;
    }

    private PracticeSession onboarding;
    private readonly List<LevelData> practiceLevels = new List<LevelData>();
    private int onboardingReadyFrame = -1;
    public bool IsOnboardingActive => onboarding != null;
    public OnboardingStep CurrentOnboardingStep => onboarding?.step ?? OnboardingStep.None;
    private int PracticeGoal => 1 + Mathf.Max(1, onboardingSettings.followCorrectTaps) + Mathf.Max(1, onboardingSettings.healthCorrectTaps);
    private bool OnboardingCanInteract => IsOnboardingActive && state == FlowState.Onboarding &&
        !applicationSuspended && !levelTransitionActive && !rogueliteUI.IsSettingsVisible &&
        rogueliteUI.IsGameplayReady && rogueliteUI.OnboardingPresentationReady && Time.frameCount != onboardingReadyFrame;

    public void BeginOnboardingReplay()
    {
        if (IsOnboardingActive || state != FlowState.Settings || settingsClosing || runEndingActive) return;
        BeginOnboarding(true);
    }

    private void BeginOnboarding(bool replay)
    {
        if (IsOnboardingActive) return;
        if (onboardingSettings == null) onboardingSettings = Resources.Load<NeonOnboardingSettings>("NeonOnboardingSettings");
        if (onboardingSettings == null)
        {
            onboardingSettings = ScriptableObject.CreateInstance<NeonOnboardingSettings>();
            onboardingSettings.hideFlags = HideFlags.HideAndDontSave;
        }
        SaveRealRunCritical();
        CancelLevelTransitionPresentation();
        CancelSettingsDismissal();
        if (introCoroutine != null) { StopCoroutine(introCoroutine); introCoroutine = null; }
        presentationToken++;
        StopDamageFlash();
        feedbackController.ResetImmediate();
        NormalizeTargetPresentation();
        rogueliteUI.NormalizeGameplayFeedback();
        onboarding = new PracticeSession
        {
            replay = replay,
            origin = state == FlowState.Settings ? stateBeforeSettings : state,
            originalRun = sessionRun,
            originalLevel = activeLevel,
            originalDebug = isDebugSession,
            originalTerminal = terminalRequested,
            originalRandom = random
        };
        rogueliteUI.OnboardingFadeDuration = Mathf.Max(.08f, onboardingSettings.instructionFadeSeconds);
        rogueliteUI.BeginOnboardingPresentation(AdvanceOnboarding, SkipOnboarding);
        ConfigureGameplayPanels();
        if (onboarding.originalRun != null && onboarding.originalLevel != null && gridContentRoot != null)
        {
            BeginPreparedLevelTransition(0, CreatePracticeState);
        }
        else
        {
            CreatePracticeState();
            ConfigureGridHierarchyAndSize();
            BuildGrid();
            RepairOrRestoreSequence();
            ApplyGridMotionState(false, 0);
            ApplyLevelTheme();
            state = FlowState.Onboarding;
        }
        SetSquareAnimationsPaused(applicationSuspended || levelTransitionActive);
        feedbackController.SetPresentationPaused(applicationSuspended);
        SetOnboardingStep(OnboardingStep.FirstTap);
    }

    private LevelData CreatePracticeLevel(int number)
    {
        var level = Instantiate(campaign.GetLevel(0));
        level.name = "Guided practice " + number;
        level.hideFlags = HideFlags.HideAndDontSave;
        level.stableId = "onboarding-practice-" + number;
        level.levelNumber = number;
        level.gridSize = 3;
        level.requiredCorrectClicks = PracticeGoal;
        level.timeLimit = Mathf.Max(.1f, onboardingSettings.levelSeconds);
        level.rotateSpeed = 0;
        level.scaleEnabled = level.movementEnabled = level.reverseEnabled = false;
        level.movementTravelPaddingNormalized = 0;
        level.completionCoinReward = 0;
        practiceLevels.Add(level);
        return level;
    }

    private void CreatePracticeState()
    {
        rogueliteUI.SetOnboardingLayout(true);
        activeLevel = CreatePracticeLevel(1);
        random = new DeterministicRandom(41721);
        sessionRun = new ActiveRunData
        {
            runId = "isolated-onboarding",
            runSeed = 41721,
            currentLevelId = activeLevel.stableId,
            currentHealth = 3,
            currentReserveSeconds = Mathf.Max(3, onboardingSettings.reserveSeconds),
            upgrades = new UpgradeSnapshotData { maxHealth = 3, startingReserveSeconds = Mathf.Max(3, onboardingSettings.reserveSeconds) },
            levelState = new ActiveLevelStateData
            {
                normalTimeRemaining = activeLevel.timeLimit,
                // The first target is intentionally near the instruction pointer.
                largeIndex = 0, smallIndex = 1, mediumIndex = 4
            }
        };
        StoreRandom();
        isDebugSession = false;
        terminalRequested = false;
    }

    private void TickOnboarding()
    {
        float delta = NeonMotion.Delta(applicationSuspended || state == FlowState.Settings);
        rogueliteUI.AdvanceOnboardingPresentation(delta);
        if (applicationSuspended || state == FlowState.Settings) return;
        if (levelTransitionActive)
        {
            AdvanceLevelTransition(NeonMotion.TransitionDelta());
            return;
        }
        if (onboarding.step == OnboardingStep.Returning)
        {
            FinishReplayReturn();
            return;
        }
        if (onboarding.step == OnboardingStep.Exiting)
        {
            if (rogueliteUI.OnboardingPresentationHidden && !rogueliteUI.IsSettingsVisible) FinishOnboardingExit();
            return;
        }
        if (!OnboardingCanInteract) return;
        if (onboarding.step == OnboardingStep.NextLevel)
        {
            SetOnboardingStep(OnboardingStep.CarrySummary);
        }
        else if (onboarding.step == OnboardingStep.TimeCountdown)
        {
            if (!sessionRun.levelState.reserveActive)
                GameplayTimerRules.Tick(sessionRun, Mathf.Min(delta, sessionRun.levelState.normalTimeRemaining));
            else
            {
                onboarding.demoElapsed += delta;
                if (onboarding.demoElapsed >= Mathf.Max(.1f, onboardingSettings.zeroTimeHoldSeconds)) SetOnboardingStep(OnboardingStep.ReserveIntro);
            }
            UpdateGameplayUI();
        }
        else if (onboarding.step == OnboardingStep.ReserveCountdown)
        {
            float duration = Mathf.Min(Mathf.Max(.1f, onboardingSettings.reserveDrainSeconds), sessionRun.upgrades.startingReserveSeconds - 1f);
            float tick = Mathf.Min(delta, Mathf.Max(0, duration - onboarding.demoElapsed));
            GameplayTimerRules.Tick(sessionRun, tick);
            onboarding.demoElapsed += tick;
            UpdateGameplayUI();
            if (onboarding.demoElapsed >= duration) SetOnboardingStep(OnboardingStep.ReserveCarry);
        }
    }

    private void OnPracticeSquareClicked(GameSquare square)
    {
        if (!OnboardingCanInteract || square == null) return;
        OnboardingStep step = onboarding.step;
        if (step != OnboardingStep.FirstTap && step != OnboardingStep.FollowSequence && step != OnboardingStep.HealthPractice) return;
        int index = square.gridY * activeLevel.gridSize + square.gridX;
        if (!IsValidSquareIndex(index) || instantiatedSquares[index] != square) return;
        if (index == sessionRun.levelState.largeIndex) HandleCorrectTap();
        else if (step == OnboardingStep.HealthPractice) HandleMistake(square);
        // Before health is introduced, a wrong tap has no feedback or side effect.
    }

    private void OnPracticeCorrectTap()
    {
        onboarding.correctInSection++;
        switch (onboarding.step)
        {
            case OnboardingStep.FirstTap:
                SetOnboardingStep(OnboardingStep.FollowSequence);
                break;
            case OnboardingStep.FollowSequence:
                if (onboarding.correctInSection >= Mathf.Max(1, onboardingSettings.followCorrectTaps)) SetOnboardingStep(OnboardingStep.HealthIntro);
                break;
            case OnboardingStep.HealthPractice:
                if (onboarding.correctInSection >= Mathf.Max(1, onboardingSettings.healthCorrectTaps)) SetOnboardingStep(OnboardingStep.TimeIntro);
                break;
        }
    }

    public void AdvanceOnboarding()
    {
        if (!OnboardingCanInteract) return;
        switch (onboarding.step)
        {
            case OnboardingStep.HealthIntro:
            case OnboardingStep.HealthRetry:
                sessionRun.currentHealth = 3;
                sessionRun.levelState.objectiveProgress = 1 + Mathf.Max(1, onboardingSettings.followCorrectTaps);
                SetOnboardingStep(OnboardingStep.HealthPractice);
                break;
            case OnboardingStep.TimeIntro: SetOnboardingStep(OnboardingStep.TimeCountdown); break;
            case OnboardingStep.ReserveIntro: SetOnboardingStep(OnboardingStep.ReserveCountdown); break;
            case OnboardingStep.ReserveCarry:
                SetOnboardingStep(OnboardingStep.NextLevel);
                BeginPreparedLevelTransition(1, () =>
                {
                    activeLevel = CreatePracticeLevel(2);
                    sessionRun.currentLevelIndex = 1;
                    sessionRun.currentLevelId = activeLevel.stableId;
                    sessionRun.levelState = new ActiveLevelStateData
                    {
                        normalTimeRemaining = activeLevel.timeLimit,
                        smallIndex = 1, mediumIndex = 4, largeIndex = 0
                    };
                });
                break;
            case OnboardingStep.CarrySummary: SetOnboardingStep(OnboardingStep.Ready); break;
            case OnboardingStep.Ready: RequestOnboardingExit(false, false); break;
        }
    }

    private void SetOnboardingStep(OnboardingStep step)
    {
        if (!IsOnboardingActive || onboarding.step == OnboardingStep.Exiting || onboarding.step == OnboardingStep.Returning) return;
        onboarding.step = step;
        onboarding.correctInSection = 0;
        onboarding.demoElapsed = 0;
        onboardingReadyFrame = Time.frameCount;
        // The timer demonstration represents an unfinished tap goal.
        if (step == OnboardingStep.TimeIntro) sessionRun.levelState.objectiveProgress = 0;
        var prompt = onboardingSettings.Prompt(step);
        int section = step == OnboardingStep.FirstTap ? 1 : step == OnboardingStep.FollowSequence ? 2 :
            step <= OnboardingStep.HealthRetry ? 3 : step < OnboardingStep.Ready ? 4 : 5;
        string body = prompt.body, action = prompt.action;
        if (step == OnboardingStep.Ready && onboarding.replay)
        {
            body = onboarding.originalRun != null ? "Return to your paused run." : "";
            action = onboarding.originalRun != null ? "RETURN TO RUN" : "START";
        }
        rogueliteUI.ShowOnboardingInstruction(prompt.title, body, action, $"PRACTICE  {section} / 5", step == OnboardingStep.Ready);
        rogueliteUI.SetOnboardingHudVisibility(step >= OnboardingStep.HealthIntro, step >= OnboardingStep.TimeIntro);
        RectTransform focus = null;
        Color tint = NeonTheme.Hex("59EFFF");
        if (step == OnboardingStep.FirstTap && IsValidSquareIndex(sessionRun.levelState.largeIndex))
            focus = instantiatedSquares[sessionRun.levelState.largeIndex].transform as RectTransform;
        else if (step == OnboardingStep.HealthIntro || step == OnboardingStep.HealthRetry)
        { focus = gameplayPanel.transform.Find("SafeContent/HealthPanel") as RectTransform; tint = NeonTheme.Hex("ACF35F"); }
        else if (step >= OnboardingStep.TimeIntro && step <= OnboardingStep.CarrySummary)
        { focus = gameplayPanel.transform.Find("SafeContent/TimePanel") as RectTransform; tint = NeonTheme.Hex("FFD05B"); }
        rogueliteUI.SetOnboardingFocus(focus, tint, step == OnboardingStep.FirstTap);
        UpdateGameplayUI();
    }

    public void SkipOnboarding() => RequestOnboardingExit(true, false);

    private void RequestOnboardingExit(bool skipped, bool home)
    {
        if (!IsOnboardingActive || onboarding.step == OnboardingStep.Exiting || onboarding.step == OnboardingStep.Returning) return;
        onboarding.skipped = skipped;
        onboarding.exit = home ? PracticeExit.Home : onboarding.replay ? PracticeExit.ReturnToOrigin : PracticeExit.StartRun;
        onboarding.step = OnboardingStep.Exiting;
        CancelLevelTransitionPresentation();
        if (state == FlowState.LevelTransition) state = FlowState.Onboarding;
        if (state == FlowState.Settings && stateBeforeSettings == FlowState.LevelTransition) stateBeforeSettings = FlowState.Onboarding;
        rogueliteUI.EndOnboardingPresentation();
        if (state == FlowState.Settings) CloseSettings();
    }

    private void FinishOnboardingExit()
    {
        PracticeSession completed = onboarding;
        StopDamageFlash();
        feedbackController.ResetImmediate();
        if (completed.exit == PracticeExit.StartRun)
        {
            // Commit the onboarding outcome and new real run together, after all practice callbacks are disabled.
            BeginPreparedLevelTransition(0, () =>
            {
                onboarding = null;
                rogueliteUI.ResetOnboardingPresentation();
                OnboardingProfileRules.RecordOutcome(saveData.profile, completed.skipped);
                CreateRealRunState();
            });
            ReleasePracticeLevels();
            return;
        }
        if (completed.exit == PracticeExit.ReturnToOrigin && completed.originalRun != null && completed.originalLevel != null)
        {
            completed.step = OnboardingStep.Returning;
            BeginPreparedLevelTransition(-1, () =>
            {
                rogueliteUI.ResetOnboardingPresentation();
                RestorePracticeOrigin(completed);
            });
            return;
        }
        onboarding = null;
        rogueliteUI.ResetOnboardingPresentation();
        RestorePracticeOrigin(completed);
        ReleasePracticeLevels();
        if (completed.exit == PracticeExit.Home)
        {
            ReturnToMainMenu();
            return;
        }
        state = completed.origin;
        if (state == FlowState.Shop) OpenUpgradeShop();
        else
        {
            rogueliteUI.ShowBaseScreen(state == FlowState.RunSummary ? failPanel : mainMenuPanel);
            if (state != FlowState.RunSummary) { state = FlowState.MainMenu; RefreshMainMenu(); }
        }
        OpenSettings();
    }

    private void RestorePracticeOrigin(PracticeSession context)
    {
        sessionRun = context.originalRun;
        activeLevel = context.originalLevel;
        isDebugSession = context.originalDebug;
        terminalRequested = context.originalTerminal;
        random = context.originalRandom;
    }

    private void FinishReplayReturn()
    {
        onboarding = null;
        ReleasePracticeLevels();
        state = FlowState.Playing;
        if (sessionRun.levelState.reverseActive) feedbackController.RestoreReverseActive();
        OpenSettings();
    }

    private void ReleasePracticeLevels()
    {
        foreach (var level in practiceLevels) if (level != null) Destroy(level);
        practiceLevels.Clear();
    }

}
