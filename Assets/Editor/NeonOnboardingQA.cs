#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Actual-runtime onboarding coverage, always using Presentation QA's isolated profile.</summary>
public static class NeonOnboardingQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly OnboardingStep[] SkippableSteps =
    {
        OnboardingStep.FirstTap, OnboardingStep.FollowSequence, OnboardingStep.HealthIntro,
        OnboardingStep.HealthPractice, OnboardingStep.HealthRetry, OnboardingStep.TimeIntro,
        OnboardingStep.TimeCountdown, OnboardingStep.ReserveIntro, OnboardingStep.ReserveCountdown,
        OnboardingStep.ReserveCarry, OnboardingStep.NextLevel, OnboardingStep.CarrySummary,
        OnboardingStep.Ready
    };

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check, bool full = true)
    {
        if (gm == null || !NeonPresentationQAProfile.IsActive)
            throw new InvalidOperationException("Onboarding QA requires an isolated runtime profile.");
        if (Field<NeonSaveService>(gm, "saveService").DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Onboarding QA refused a non-isolated profile.");

        NeonOnboardingSettings originalSettings = gm.onboardingSettings;
        bool captureMode = capture != null;
        bool reducedEffectsBefore = NeonTheme.ReducedEffects;
        bool ownsSettingsClone = false;
        if (!captureMode)
        {
            gm.onboardingSettings = originalSettings == null
                ? ScriptableObject.CreateInstance<NeonOnboardingSettings>()
                : UnityEngine.Object.Instantiate(originalSettings);
            gm.onboardingSettings.hideFlags = HideFlags.HideAndDontSave;
            gm.onboardingSettings.levelSeconds = .3f;
            gm.onboardingSettings.reserveDrainSeconds = .3f;
            gm.onboardingSettings.instructionFadeSeconds = .01f;
            ownsSettingsClone = true;
        }

        try
        {
            yield return PrepareFresh(gm);
            // Capture sessions defer loading the Resources asset until this first onboarding starts.
            if (originalSettings == null && captureMode)
                originalSettings = gm.onboardingSettings;
            yield return WaitStep(gm, OnboardingStep.FirstTap, check);
            CheckPresentation(gm, check, "opening instruction settles before input checks");
            string initialProfile = JsonUtility.ToJson(Field<SaveEnvelopeData>(gm, "saveData").profile);
            yield return CaptureAndValidate(gm, capture, check, "01-first-tap");

            ActiveRunData practice = Field<ActiveRunData>(gm, "sessionRun");
            int initialProgress = practice.levelState.objectiveProgress;
            string initialSequence = JsonUtility.ToJson(practice.levelState);
            int initialHealth = practice.currentHealth;
            string initialRandom = practice.randomState;
            int wrong = practice.levelState.mediumIndex;
            Tap(gm, wrong);
            yield return null;
            check(gm.CurrentOnboardingStep == OnboardingStep.FirstTap && practice.levelState.objectiveProgress == initialProgress,
                "Wrong target is ignored during the first-tap lesson.");
            check(JsonUtility.ToJson(practice.levelState) == initialSequence && practice.currentHealth == initialHealth && practice.randomState == initialRandom,
                "First-tap wrong input leaves sequence, health, and deterministic RNG untouched.");
            check(RoleAt(gm, practice.levelState.largeIndex) == GameSquare.LitSize.Large,
                "The first-tap target remains the actual rendered largest-outline role.");

            TapCorrect(gm);
            yield return WaitStep(gm, OnboardingStep.FollowSequence, check);
            yield return CaptureAndValidate(gm, capture, check, "02-follow-sequence");
            for (int i = 0; i < gm.onboardingSettings.followCorrectTaps; i++)
                TapCorrect(gm);
            yield return WaitStep(gm, OnboardingStep.HealthIntro, check);
            yield return CaptureAndValidate(gm, capture, check, "03-health");

            float heldTime = practice.levelState.normalTimeRemaining;
            yield return new WaitForSecondsRealtime(.12f);
            check(Mathf.Approximately(heldTime, practice.levelState.normalTimeRemaining),
                "Instruction acknowledgement holds the practice timer at its displayed value.");
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.HealthPractice, check);
            check(practice.currentHealth == 3, "Health practice begins with exactly three isolated health.");
            for (int i = 0; i < 3; i++) { TapWrong(gm); yield return null; }
            yield return WaitStep(gm, OnboardingStep.HealthRetry, check);
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.HealthPractice, check);
            check(practice.currentHealth == 3 && practice.levelState.objectiveProgress == 1 + gm.onboardingSettings.followCorrectTaps,
                "Health retry restores only its three-health practice segment.");
            for (int i = 0; i < gm.onboardingSettings.healthCorrectTaps; i++) TapCorrect(gm);
            yield return WaitStep(gm, OnboardingStep.TimeIntro, check);
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.TimeCountdown, check);
            yield return WaitStep(gm, OnboardingStep.ReserveIntro, check);
            yield return CaptureAndValidate(gm, capture, check, "04-reserve");

            float reserveBefore = practice.currentReserveSeconds;
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.ReserveCountdown, check);
            yield return WaitStep(gm, OnboardingStep.ReserveCarry, check);
            check(practice.currentReserveSeconds < reserveBefore, "Reserve visibly drains after normal time expires.");
            int healthBeforeNextLevel = practice.currentHealth;
            float reserveAfterDrain = practice.currentReserveSeconds;
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.NextLevel, check);
            yield return WaitStep(gm, OnboardingStep.CarrySummary, check);
            check(practice.currentHealth == healthBeforeNextLevel && Mathf.Approximately(practice.currentReserveSeconds, reserveAfterDrain),
                "Next-level demonstration keeps practice health and reduced reserve intact.");
            check(Mathf.Approximately(practice.levelState.normalTimeRemaining, Mathf.Max(.1f, gm.onboardingSettings.levelSeconds)),
                "The next practice level restores fresh normal time.");
            gm.AdvanceOnboarding();
            yield return WaitStep(gm, OnboardingStep.Ready, check);
            check(Field<SaveEnvelopeData>(gm, "saveData").activeRun == null && JsonUtility.ToJson(Field<SaveEnvelopeData>(gm, "saveData").profile) == initialProfile,
                "Practice has not changed the saved profile or created a real active run before exit.");
            yield return CaptureAndValidate(gm, capture, check, "05-ready");

            gm.AdvanceOnboarding();
            yield return WaitFlow(gm, "Playing", check);
            SaveEnvelopeData finished = Field<SaveEnvelopeData>(gm, "saveData");
            check(!gm.IsOnboardingActive && finished.profile.onboardingStatus == OnboardingStatus.Completed &&
                  finished.activeRun != null && finished.activeRun.currentLevelIndex == 0,
                "Completing onboarding persists Completed and starts real Level 1.");
            CheckRealRunResources(gm, check, "Completed onboarding hand-off");
            NeonSaveService service = Field<NeonSaveService>(gm, "saveService");
            SaveEnvelopeData reloaded = service.Load(gm.gameConfig);
            check(reloaded.profile.onboardingStatus == OnboardingStatus.Completed && reloaded.activeRun != null && reloaded.activeRun.currentLevelIndex == 0,
                "Completion survives a save-service reload with real Level 1 intact.");

            if (!full)
                yield break;

            // The five visual captures above use configured production timings. The remaining
            // exhaustive exit permutations use an unsaved runtime clone to stay focused.
            if (captureMode)
            {
                gm.onboardingSettings = UnityEngine.Object.Instantiate(originalSettings);
                gm.onboardingSettings.hideFlags = HideFlags.HideAndDontSave;
                gm.onboardingSettings.levelSeconds = .3f;
                gm.onboardingSettings.reserveDrainSeconds = .3f;
                gm.onboardingSettings.instructionFadeSeconds = .01f;
                ownsSettingsClone = true;
            }
            yield return VerifyExperiencedPlayerIsNotForced(gm, check);
            yield return VerifyReplayIsolation(gm, check);
            yield return VerifyPauseAndFocusFreeze(gm, check);
            yield return VerifySkipEveryReachableStep(gm, check);
            yield return VerifyMenuSettingsReplayWithoutSession(gm, check);
            yield return VerifyReducedEffectsInitialSkip(gm, check);
        }
        finally
        {
            if (ownsSettingsClone && gm.onboardingSettings != null)
                UnityEngine.Object.Destroy(gm.onboardingSettings);
            gm.onboardingSettings = originalSettings;
            NeonTheme.ReducedEffects = reducedEffectsBefore;
            if (gm != null) gm.ReturnToMainMenu();
        }
    }

    private static IEnumerator VerifyExperiencedPlayerIsNotForced(GameManager gm, Action<bool, string> check)
    {
        gm.ReturnToMainMenu();
        yield return WaitFlow(gm, "MainMenu", check);
        Field<SaveEnvelopeData>(gm, "saveData").activeRun = null;
        gm.StartNewRun();
        yield return WaitFlow(gm, "Playing", check);
        check(!gm.IsOnboardingActive, "A profile with prior real-run evidence starts normally without onboarding.");
    }

    private static IEnumerator VerifyReplayIsolation(GameManager gm, Action<bool, string> check)
    {
        // Restore a larger moving campaign checkpoint so replay exercises both the
        // shared resize transition and exact preservation of motion/PRNG state.
        gm.ReturnToMainMenu();
        yield return WaitFlow(gm, "MainMenu", check);
        int movingIndex = gm.campaign.levels.FindIndex(level => level.gridSize == 4 &&
            (level.movementEnabled || level.scaleEnabled || !Mathf.Approximately(level.rotateSpeed, 0)));
        if (movingIndex < 0) throw new InvalidOperationException("Expected a 4x4 campaign level with motion for replay coverage.");
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        data.activeRun.currentLevelIndex = movingIndex;
        data.activeRun.currentLevelId = gm.campaign.GetLevel(movingIndex).stableId;
        data.activeRun.betweenLevels = true;
        gm.ContinueRun();
        yield return WaitFlow(gm, "Playing", check);
        check(Field<List<GameSquare>>(gm, "instantiatedSquares").Count == 16,
            "Replay fixture runs a real 4x4 campaign grid with motion.");
        gm.OpenSettings();
        yield return WaitFlow(gm, "Settings", check);
        yield return null;
        string before = JsonUtility.ToJson(data);
        gm.BeginOnboardingReplay();
        yield return WaitStep(gm, OnboardingStep.FirstTap, check);
        gm.SkipOnboarding();
        yield return WaitFlow(gm, "Settings", check);
        check(JsonUtility.ToJson(data) == before, "Skipping settings replay preserves the complete active-run envelope byte-for-byte.");
        gm.CloseSettings();
        yield return WaitFlow(gm, "Playing", check);

        gm.OpenSettings();
        yield return WaitFlow(gm, "Settings", check);
        yield return null;
        before = JsonUtility.ToJson(data);
        gm.BeginOnboardingReplay();
        yield return Reach(gm, OnboardingStep.Ready, check);
        gm.AdvanceOnboarding();
        yield return WaitFlow(gm, "Settings", check);
        check(JsonUtility.ToJson(data) == before, "Completing settings replay preserves the complete active-run envelope byte-for-byte.");
        gm.CloseSettings();
        yield return WaitFlow(gm, "Playing", check);
    }

    private static IEnumerator VerifyPauseAndFocusFreeze(GameManager gm, Action<bool, string> check)
    {
        yield return PrepareFresh(gm);
        ActiveRunData run = Field<ActiveRunData>(gm, "sessionRun");
        string before = JsonUtility.ToJson(run);
        gm.OpenSettings();
        yield return null;
        Invoke(gm, "OnApplicationFocus", false);
        Invoke(gm, "OnApplicationPause", true);
        yield return new WaitForSecondsRealtime(.1f);
        check(JsonUtility.ToJson(run) == before, "Pause/background signals freeze onboarding practice state.");
        Invoke(gm, "OnApplicationPause", false);
        Invoke(gm, "OnApplicationFocus", true);
        gm.CloseSettings();
        yield return WaitStep(gm, OnboardingStep.FirstTap, check);
        gm.ReturnToMainMenu();
        yield return WaitFlow(gm, "MainMenu", check);
        check(Field<SaveEnvelopeData>(gm, "saveData").profile.onboardingStatus == OnboardingStatus.NeverSeen,
            "Home during opening practice returns to the menu without recording an outcome.");

        yield return VerifyBackgroundFreezeAt(gm, OnboardingStep.TimeCountdown, check);
        yield return VerifyBackgroundFreezeAt(gm, OnboardingStep.ReserveCountdown, check);
    }

    private static IEnumerator VerifyBackgroundFreezeAt(GameManager gm, OnboardingStep step, Action<bool, string> check)
    {
        yield return PrepareFresh(gm);
        yield return Reach(gm, step, check);
        ActiveRunData practice = Field<ActiveRunData>(gm, "sessionRun");
        string before = JsonUtility.ToJson(practice);
        Invoke(gm, "OnApplicationFocus", false);
        Invoke(gm, "OnApplicationPause", true);
        yield return new WaitForSecondsRealtime(.12f);
        check(JsonUtility.ToJson(practice) == before, "Pause/background freezes the " + step + " timer demonstration.");
        Invoke(gm, "OnApplicationPause", false);
        Invoke(gm, "OnApplicationFocus", true);
        gm.ReturnToMainMenu();
        yield return WaitFlow(gm, "MainMenu", check);
    }

    private static IEnumerator VerifySkipEveryReachableStep(GameManager gm, Action<bool, string> check)
    {
        yield return PrepareFresh(gm);
        gm.OpenSettings();
        yield return WaitFlow(gm, "Settings", check);
        gm.SkipOnboarding();
        gm.SkipOnboarding();
        yield return WaitFlow(gm, "Playing", check);
        check(Field<SaveEnvelopeData>(gm, "saveData").profile.onboardingStatus == OnboardingStatus.Skipped,
            "Skip from Settings closes its modal and records the initial skip once.");
        CheckRealRunResources(gm, check, "Settings skip hand-off");

        foreach (OnboardingStep target in SkippableSteps)
        {
            yield return PrepareFresh(gm);
            yield return Reach(gm, target, check);
            gm.SkipOnboarding();
            yield return WaitFlow(gm, "Playing", check);
            SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
            check(!gm.IsOnboardingActive && data.profile.onboardingStatus == OnboardingStatus.Skipped &&
                  data.activeRun != null && data.activeRun.currentLevelIndex == 0,
                "Skip from " + target + " cleanly starts real Level 1 once.");
            CheckRealRunResources(gm, check, "Skip from " + target);
            int level = data.activeRun.currentLevelIndex;
            gm.SkipOnboarding();
            yield return null;
            check(data.activeRun.currentLevelIndex == level, "Duplicate exit input after " + target + " cannot start a second run.");
        }
    }

    private static IEnumerator VerifyMenuSettingsReplayWithoutSession(GameManager gm, Action<bool, string> check)
    {
        gm.ReturnToMainMenu();
        yield return WaitFlow(gm, "MainMenu", check);
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        check(data.activeRun != null && Field<ActiveRunData>(gm, "sessionRun") == null,
            "Menu replay fixture retains the saved active run while the live session is absent.");
        gm.OpenSettings();
        yield return WaitFlow(gm, "Settings", check);
        string before = JsonUtility.ToJson(data);
        gm.BeginOnboardingReplay();
        yield return WaitStep(gm, OnboardingStep.FirstTap, check);
        gm.SkipOnboarding();
        yield return WaitFlow(gm, "Settings", check);
        check(JsonUtility.ToJson(data) == before && Field<object>(gm, "stateBeforeSettings").ToString() == "MainMenu",
            "Menu-settings replay skip returns to Settings over MainMenu without changing the saved envelope.");
        gm.CloseSettings();
        yield return WaitFlow(gm, "MainMenu", check);

        gm.OpenSettings();
        yield return WaitFlow(gm, "Settings", check);
        before = JsonUtility.ToJson(data);
        gm.BeginOnboardingReplay();
        yield return Reach(gm, OnboardingStep.Ready, check);
        gm.AdvanceOnboarding();
        yield return WaitFlow(gm, "Settings", check);
        check(JsonUtility.ToJson(data) == before && Field<object>(gm, "stateBeforeSettings").ToString() == "MainMenu",
            "Menu-settings replay completion returns to Settings over MainMenu without changing the saved envelope.");
        gm.CloseSettings();
        yield return WaitFlow(gm, "MainMenu", check);
    }

    private static IEnumerator VerifyReducedEffectsInitialSkip(GameManager gm, Action<bool, string> check)
    {
        NeonTheme.ReducedEffects = true;
        yield return PrepareFresh(gm);
        yield return WaitStep(gm, OnboardingStep.FirstTap, check);
        gm.SkipOnboarding();
        yield return WaitFlow(gm, "Playing", check);
        check(Field<SaveEnvelopeData>(gm, "saveData").profile.onboardingStatus == OnboardingStatus.Skipped,
            "Reduced-effects initial skip still records one durable outcome.");
        CheckRealRunResources(gm, check, "Reduced-effects skip hand-off");
    }

    private static IEnumerator PrepareFresh(GameManager gm)
    {
        if (gm.IsOnboardingActive) gm.SkipOnboarding();
        gm.ReturnToMainMenu();
        yield return null;
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        data.activeRun = null;
        data.lastRunResult = null;
        data.profile = PlayerProfileData.CreateDefault();
        data.profile.legacyMigrationComplete = true;
        Field<NeonSaveService>(gm, "saveService").Save(data, gm.gameConfig);
        gm.ShowMainMenu();
        yield return null;
        gm.StartNewRun();
        yield return null;
    }

    private static IEnumerator Reach(GameManager gm, OnboardingStep target, Action<bool, string> check)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (gm.CurrentOnboardingStep != target && Time.realtimeSinceStartup < deadline)
        {
            OnboardingStep step = gm.CurrentOnboardingStep;
            if (step == OnboardingStep.FirstTap || step == OnboardingStep.FollowSequence || step == OnboardingStep.HealthPractice)
            {
                yield return WaitForInteraction(gm, step);
                if (step == OnboardingStep.HealthPractice && target == OnboardingStep.HealthRetry)
                {
                    TapWrong(gm); TapWrong(gm); TapWrong(gm);
                }
                else
                    TapCorrect(gm);
            }
            else if (step == OnboardingStep.HealthRetry || step == OnboardingStep.HealthIntro || step == OnboardingStep.TimeIntro ||
                     step == OnboardingStep.ReserveIntro || step == OnboardingStep.ReserveCarry || step == OnboardingStep.CarrySummary)
            {
                yield return WaitForInteraction(gm, step);
                gm.AdvanceOnboarding();
            }
            else if (step == OnboardingStep.TimeCountdown || step == OnboardingStep.ReserveCountdown || step == OnboardingStep.NextLevel)
                yield return null;
            else
                throw new InvalidOperationException("Cannot reach " + target + " from " + step + ".");
            yield return null;
        }
        if (gm.CurrentOnboardingStep != target)
            throw new InvalidOperationException("Onboarding reach timeout: " + target + ".");
        yield return WaitStep(gm, target, check);
    }

    private static void TapCorrect(GameManager gm)
    {
        ActiveRunData run = Field<ActiveRunData>(gm, "sessionRun");
        Tap(gm, run.levelState.largeIndex);
    }

    private static void TapWrong(GameManager gm)
    {
        ActiveRunData run = Field<ActiveRunData>(gm, "sessionRun");
        Tap(gm, run.levelState.mediumIndex);
    }

    private static void Tap(GameManager gm, int index)
    {
        List<GameSquare> squares = Field<List<GameSquare>>(gm, "instantiatedSquares");
        if (index < 0 || index >= squares.Count) throw new InvalidOperationException("Onboarding target index is invalid.");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, squares[index].transform.position) };
        ExecuteEvents.Execute(squares[index].gameObject, pointer, ExecuteEvents.pointerDownHandler);
    }

    private static GameSquare.LitSize RoleAt(GameManager gm, int index) => Field<List<GameSquare>>(gm, "instantiatedSquares")[index].currentLitSize;
    private static void CheckPresentation(GameManager gm, Action<bool, string> check, string label)
    {
        var ui = gm.GetComponent<RogueliteUIController>();
        check(ui != null && ui.OnboardingPresentationReady, label);
    }
    private static IEnumerator Capture(Func<string, IEnumerator> capture, string name)
    { if (capture != null) { IEnumerator routine = capture(name); if (routine != null) yield return routine; } }
    private static IEnumerator CaptureAndValidate(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check, string name)
    {
        yield return Capture(capture, name);
        Canvas.ForceUpdateCanvases();
        RectTransform safe = gm.gameplayPanel.transform.Find("SafeContent") as RectTransform;
        if (safe == null) throw new InvalidOperationException("Gameplay SafeContent is required for onboarding capture validation.");
        foreach (TMP_Text text in gm.gameplayPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!text.gameObject.activeInHierarchy || !IsActuallyVisible(text)) continue;
            text.ForceMeshUpdate();
            check(!text.isTextOverflowing, "Visible onboarding text fits in " + name + ": " + text.name);
            check(RectFits(text.rectTransform, safe), "Visible onboarding text stays within SafeContent in " + name + ": " + text.name);
        }
    }
    private static bool IsActuallyVisible(TMP_Text text)
    {
        if (!text.enabled || text.color.a <= .001f) return false;
        for (Transform cursor = text.transform; cursor != null; cursor = cursor.parent)
        {
            CanvasGroup group = cursor.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= .001f) return false;
        }
        return true;
    }
    private static bool RectFits(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++)
            if (!parent.rect.Contains(parent.InverseTransformPoint(corners[i]))) return false;
        return true;
    }
    private static void CheckRealRunResources(GameManager gm, Action<bool, string> check, string context)
    {
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        ActiveRunData run = data.activeRun;
        UpgradeSnapshotData expected = UpgradeCatalog.CaptureSnapshot(gm.gameConfig, data.profile);
        check(run != null && run.currentHealth == expected.maxHealth &&
              Mathf.Approximately(run.currentReserveSeconds, expected.startingReserveSeconds),
            context + " uses the profile's real starting health and reserve snapshot.");
    }
    private static IEnumerator WaitStep(GameManager gm, OnboardingStep expected, Action<bool, string> check)
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while ((gm.CurrentOnboardingStep != expected || !gm.IsOnboardingActive || !PresentationReady(gm, expected)) && Time.realtimeSinceStartup < deadline) yield return null;
        check(gm.CurrentOnboardingStep == expected && gm.IsOnboardingActive && PresentationReady(gm, expected), "Onboarding reaches stable " + expected + ".");
        if (gm.CurrentOnboardingStep != expected || !gm.IsOnboardingActive || !PresentationReady(gm, expected)) throw new InvalidOperationException("Onboarding timeout: " + expected + ".");
        yield return null;
    }
    private static IEnumerator WaitFlow(GameManager gm, string expected, Action<bool, string> check)
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while ((Flow(gm) != expected || !FlowPresentationReady(gm, expected)) && Time.realtimeSinceStartup < deadline) yield return null;
        check(Flow(gm) == expected && FlowPresentationReady(gm, expected), "Flow returns to " + expected + ".");
        if (Flow(gm) != expected || !FlowPresentationReady(gm, expected)) throw new InvalidOperationException("Flow timeout: " + expected + ".");
        yield return null;
    }
    private static string Flow(GameManager gm) => Field<object>(gm, "state").ToString();
    private static IEnumerator WaitForInteraction(GameManager gm, OnboardingStep step)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while ((gm.CurrentOnboardingStep != step || Flow(gm) != "Onboarding" || !PresentationReady(gm, step)) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (gm.CurrentOnboardingStep != step || Flow(gm) != "Onboarding" || !PresentationReady(gm, step))
            throw new InvalidOperationException("Onboarding input was never ready for " + step + ".");
    }
    private static bool PresentationReady(GameManager gm, OnboardingStep expected)
    {
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        bool inExpectedFlow = Flow(gm) == "Onboarding" || (expected == OnboardingStep.NextLevel && Flow(gm) == "LevelTransition");
        return inExpectedFlow && ui != null && ui.OnboardingPresentationReady;
    }
    private static bool FlowPresentationReady(GameManager gm, string expected)
    {
        if (expected != "Settings") return true;
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        NeonScreenMotion motion = gm.settingsPanel == null ? null : gm.settingsPanel.GetComponent<NeonScreenMotion>();
        return ui != null && ui.IsSettingsVisible && (motion == null || (motion.IsReady && motion.Opacity >= .999f));
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
}
#endif
