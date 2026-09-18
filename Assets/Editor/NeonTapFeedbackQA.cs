#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Focused real-runtime coverage for the cosmetic tap-feedback owner.  This is
/// deliberately separate from the broader gameplay suite: the fixture only
/// touches the editor session profile created by NeonPresentationQA.
/// </summary>
public static class NeonTapFeedbackQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null || check == null)
            throw new ArgumentNullException(gm == null ? nameof(gm) : nameof(check));
        NeonSaveService saves = Field<NeonSaveService>(gm, "saveService");
        if (!NeonPresentationQAProfile.IsActive || saves == null || saves.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Tap-feedback QA requires NeonPresentationQAProfile isolated storage.");

        bool reducedBefore = NeonTheme.ReducedEffects;
        bool particlesBefore = NeonMotion.T.successParticlesEnabled;
        bool controllerEnabled = true;
        CampaignDefinition authoredCampaign = gm.campaign;
        CampaignDefinition fixture = null;
        try
        {
            fixture = CreateFixture(authoredCampaign);
            gm.campaign = fixture;
            NeonTheme.ReducedEffects = false;
            // Keep regression coverage for the optional, currently disabled burst.
            NeonMotion.T.successParticlesEnabled = true;
            yield return StartFresh(gm, check);
            GameplayFeedbackController feedback = gm.GetComponent<GameplayFeedbackController>();
            if (feedback == null)
                throw new InvalidOperationException("GameplayFeedbackController was not configured for gameplay.");
            controllerEnabled = feedback.enabled;

            yield return VerifyAcceptedInput(gm, feedback, check);
            yield return VerifyVisualOwnership(gm, feedback, capture, check);
            yield return VerifyPriorityAndReducedEffects(gm, feedback, check);
            yield return VerifyLevelTransition(gm, capture, check);
            yield return VerifyTerminalAndLifecycle(gm, feedback, check);
            yield return VerifyOnboardingIgnoredTap(gm, feedback, check);
        }
        finally
        {
            NeonTheme.ReducedEffects = reducedBefore;
            NeonMotion.T.successParticlesEnabled = particlesBefore;
            if (gm != null)
            {
                GameplayFeedbackController feedback = gm.GetComponent<GameplayFeedbackController>();
                if (feedback != null)
                {
                    feedback.ResetTapFeedback();
                    feedback.SetPresentationPaused(false);
                    feedback.enabled = controllerEnabled;
                }
                gm.enabled = true;
                gm.ReturnToMainMenu();
                gm.campaign = authoredCampaign;
            }
            DestroyFixture(fixture);
        }
    }

    private static IEnumerator VerifyAcceptedInput(GameManager gm, GameplayFeedbackController feedback, Action<bool, string> check)
    {
        yield return StartFresh(gm, check);
        ActiveRunData run = Run(gm);
        run.upgrades.maxHealth = Mathf.Max(run.upgrades.maxHealth, 10);
        run.currentHealth = 10;
        run.levelState.normalTimeRemaining = 30f;
        Invoke(gm, "UpdateGameplayUI");

        string sequence = Sequence(run);
        string random = run.randomState;
        int objective = run.levelState.objectiveProgress;
        Tap(gm, false);
        yield return null;
        check(run.currentHealth == 9 && Sequence(run) == sequence && run.levelState.objectiveProgress == objective && run.randomState == random,
            "Wrong normal tap removes exactly one health without advancing sequence, objective, or target RNG.");
        check(Priority(feedback) == "Damage", "A valid mistake immediately owns the shared feedback as Damage.");

        Tap(gm, true);
        yield return null;
        check(run.levelState.objectiveProgress == objective + 1 && run.randomState != random,
            "Correct normal tap advances immediately and consumes the gameplay RNG only once.");
        check(Priority(feedback) == "Damage", "A correct tap immediately after damage keeps the red shared-feedback priority.");

        run.levelState.reverseActive = true;
        run.levelState.reverseCooldownRemaining = 999f;
        run.levelState.reverseCorrectTapsRemaining = 3;
        sequence = Sequence(run);
        random = run.randomState;
        objective = run.levelState.objectiveProgress;
        Tap(gm, false);
        yield return null;
        check(run.currentHealth == 8 && Sequence(run) == sequence && run.levelState.objectiveProgress == objective && run.randomState == random,
            "Wrong Reverse tap is one damage event and leaves the reversed sequence and RNG intact.");
        Tap(gm, true);
        yield return null;
        check(run.levelState.objectiveProgress == objective + 1 && run.randomState != random,
            "Correct Reverse tap accepts the visible Small target immediately and advances deterministic RNG.");

        int outsideHealth = run.currentHealth;
        string outsideSequence = Sequence(run);
        string outsideRandom = run.randomState;
        int outsideObjective = run.levelState.objectiveProgress;
        feedback.ResetTapFeedback();
        var outside = new PointerEventData(EventSystem.current) { position = new Vector2(-10000f, -10000f) };
        ExecuteEvents.Execute(gm.gameplayPanel, outside, ExecuteEvents.pointerDownHandler);
        yield return null;
        check(run.currentHealth == outsideHealth && Sequence(run) == outsideSequence && run.randomState == outsideRandom &&
              run.levelState.objectiveProgress == outsideObjective && Priority(feedback) == "None",
            "Outside-grid input is ignored with no game mutation or tap-feedback ownership.");

        gm.OpenSettings();
        yield return null;
        int lockedHealth = run.currentHealth;
        string lockedSequence = Sequence(run);
        string lockedRandom = run.randomState;
        int lockedObjective = run.levelState.objectiveProgress;
        Tap(gm, false);
        yield return null;
        check(run.currentHealth == lockedHealth && Sequence(run) == lockedSequence && run.randomState == lockedRandom &&
              run.levelState.objectiveProgress == lockedObjective && Priority(feedback) == "None",
            "Input while Settings locks gameplay is ignored without stale feedback.");
        gm.CloseSettings();
        yield return WaitForFlow(gm, "Playing");
    }

    private static IEnumerator VerifyVisualOwnership(GameManager gm, GameplayFeedbackController feedback, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        yield return StartFresh(gm, check);
        feedback.ResetTapFeedback();
        bool wasEnabled = feedback.enabled;
        bool gameWasEnabled = gm.enabled;
        gm.enabled = false;
        feedback.enabled = false; // deterministic explicit stepping; Update cannot add a second delta.
        try
        {
            RectTransform wrapper = feedback.TapFeedbackRoot;
            check(wrapper != null, "Tap feedback owns a presentation wrapper above gameplay content.");
            Vector2 basePosition = wrapper.anchoredPosition;
            Quaternion baseRotation = wrapper.localRotation;
            Vector3 baseScale = wrapper.localScale;
            GameSquare consumed = Target(gm, true);
            RectTransform rotationRoot = Field<RectTransform>(gm, "rotationScaleRoot");
            RectTransform grid = Field<RectTransform>(gm, "gridContainer");
            rotationRoot.localRotation = Quaternion.Euler(0f, 0f, 27f);
            rotationRoot.localScale = Vector3.one * .8f;
            grid.anchoredPosition = new Vector2(40f, 20f);
            Canvas.ForceUpdateCanvases();
            Vector3 renderedOrigin = consumed.outlineImage.transform.position;

            feedback.CaptureSuccessPose(consumed);
            feedback.PlayAcceptedSuccess();
            feedback.AdvanceTapFeedback(.04f);
            check(feedback.ActiveTapParticleCount > 0 && feedback.ActiveTapParticleCount <= GameplayFeedbackController.TapParticleCapacity &&
                  GameplayFeedbackController.TapParticleCapacity == 48,
                "Success decoration is pooled and remains within the configured 48-particle cap.");
            check(Vector3.Distance(feedback.LastBurstCenterWorld, renderedOrigin) < .01f,
                "Success particles use the consumed outline's rendered world pose before reassignment.");

            Canvas.ForceUpdateCanvases();
            NeonTapParticles particles = gm.gameplayPanel.GetComponentInChildren<NeonTapParticles>(true);
            CanvasRenderer particleRenderer = particles != null ? particles.canvasRenderer : null;
            Mesh particleMesh = particleRenderer != null ? particleRenderer.GetMesh() : null;
            float brightest = 0f, highestAlpha = 0f;
            foreach (Color32 vertexColor in particleMesh != null ? particleMesh.colors32 : Array.Empty<Color32>())
            {
                brightest = Mathf.Max(brightest, Mathf.Max(vertexColor.r, Mathf.Max(vertexColor.g, vertexColor.b)) / 255f);
                highestAlpha = Mathf.Max(highestAlpha, vertexColor.a / 255f);
            }
            Rect particleRect = particles != null ? particles.rectTransform.rect : default;
            Color graphicColor = particles != null ? particles.color : Color.clear;
            Color rendererColor = particleRenderer != null ? particleRenderer.GetColor() : Color.clear;
            int particleDepth = particles != null ? particles.depth : -1;
            bool visibleMesh = particleRenderer != null && !particleRenderer.cull && particleMesh != null && particleMesh.vertexCount > 0 &&
                highestAlpha > .05f && brightest > .2f;
            check(visibleMesh,
                $"Success glint mesh: vertices={(particleMesh != null ? particleMesh.vertexCount : 0)}, cull={(particleRenderer != null && particleRenderer.cull)}, " +
                $"rect={particleRect}, depth={particleDepth}, graphic={graphicColor}, renderer={rendererColor}, " +
                $"maxAlpha={highestAlpha:F3}, maxBrightness={brightest:F3}.");
            if (capture != null) yield return capture("tap-feedback-rendered-particles");
            Invoke(gm, "ApplyGridMotionState", false, 0f);
            Canvas.ForceUpdateCanvases();

            feedback.PlayAcceptedDamage();
            feedback.AdvanceTapFeedback(.04f);
            float offset = feedback.ShakeOffsetNormalized;
            check(Mathf.Abs(offset) <= .0041f,
                "Damage shake stays bounded to 0.4% horizontally with no vertical displacement.");
            check(wrapper.localRotation == baseRotation && wrapper.localScale == baseScale && Mathf.Approximately(wrapper.anchoredPosition.y, basePosition.y),
                "Shake changes neither presentation Y, rotation, nor scale.");

            Canvas.ForceUpdateCanvases();
            GameSquare visible = Target(gm, true);
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, visible.transform.position)
            };
            EventSystem.current.RaycastAll(pointer, hits);
            check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<GameSquare>() == visible,
                "During screen shake, the translated visible target center still resolves to its actual UI cell raycast target.");

            for (int i = 0; i < 8; i++)
            {
                feedback.PlayAcceptedDamage();
                feedback.AdvanceTapFeedback(.012f);
            }
            offset = feedback.ShakeOffsetNormalized;
            check(Mathf.Abs(offset) <= .0041f,
                "Rapid mistakes retarget the current shake without accumulating amplitude.");

            feedback.ResetTapFeedback();
            check(Mathf.Approximately(feedback.ShakeOffsetNormalized, 0f) && wrapper.anchoredPosition == basePosition &&
                  feedback.ActiveTapParticleCount == 0 && Priority(feedback) == "None",
                "Explicit feedback reset returns offset, particles, and shared ownership exactly to rest.");
        }
        finally
        {
            gm.enabled = gameWasEnabled;
            feedback.enabled = wasEnabled;
            feedback.ResetTapFeedback();
        }
        yield return null;
    }

    private static IEnumerator VerifyPriorityAndReducedEffects(GameManager gm, GameplayFeedbackController feedback, Action<bool, string> check)
    {
        yield return StartFresh(gm, check);
        bool wasEnabled = feedback.enabled;
        feedback.enabled = false;
        bool reducedBefore = NeonTheme.ReducedEffects;
        try
        {
            feedback.ResetTapFeedback();
            feedback.PlayAcceptedSuccess();
            check(Priority(feedback) == "Success", "Success owns the shared presentation when no stronger feedback is active.");
            feedback.PlayAcceptedDamage();
            check(Priority(feedback) == "Damage", "Damage preempts a current success vignette.");
            feedback.BeginTerminalFeedback(false);
            check(Priority(feedback) == "Terminal", "Terminal presentation preempts ordinary damage feedback.");
            feedback.PlayAcceptedSuccess();
            feedback.PlayAcceptedDamage();
            check(Priority(feedback) == "Terminal", "Accepted input effects cannot recolor shared presentation after terminal ownership starts.");

            feedback.ResetTapFeedback();
            NeonTheme.ReducedEffects = true;
            feedback.PlayAcceptedDamage();
            feedback.AdvanceTapFeedback(.08f);
            check(Mathf.Approximately(feedback.ShakeOffsetNormalized, 0f),
                "Reduced Effects disables the optional horizontal shake without changing feedback ownership.");
            GameSquare target = Target(gm, true);
            feedback.CaptureSuccessPose(target);
            feedback.PlayAcceptedSuccess();
            feedback.AdvanceTapFeedback(.04f);
            check(feedback.ActiveTapParticleCount <= 4,
                "Reduced Effects substantially reduces optional success decoration.");
        }
        finally
        {
            NeonTheme.ReducedEffects = reducedBefore;
            feedback.enabled = wasEnabled;
            feedback.ResetTapFeedback();
        }
        yield return null;
    }

    private static IEnumerator VerifyTerminalAndLifecycle(GameManager gm, GameplayFeedbackController feedback, Action<bool, string> check)
    {
        yield return StartFresh(gm, check);
        ActiveRunData run = Run(gm);
        run.currentHealth = 1;
        run.levelState.normalTimeRemaining = 20f;
        feedback.ResetTapFeedback();
        Tap(gm, false);
        yield return null;
        check(Flow(gm) == "RunEnding" || Flow(gm) == "RunSummary",
            "Fatal wrong input leaves playable flow immediately while terminal presentation takes ownership.");
        check(Priority(feedback) == "Terminal" || Priority(feedback) == "Damage",
            "The final damage cue remains present until terminal feedback hand-off is established.");

        gm.ReturnToMainMenu();
        yield return StartFresh(gm, check);
        run = Run(gm);
        feedback.ResetTapFeedback();
        run.levelState.normalTimeRemaining = 0f;
        run.levelState.reserveActive = true;
        run.currentReserveSeconds = 0f;
        Invoke(gm, "Update");
        yield return null;
        check(Mathf.Approximately(feedback.ShakeOffsetNormalized, 0f) && Priority(feedback) != "Damage",
            "Reserve exhaustion keeps its timer terminal treatment and never impersonates a damage shake.");

        yield return StartFresh(gm, check);
        feedback.ResetTapFeedback();
        bool wasEnabled = feedback.enabled;
        feedback.enabled = false;
        try
        {
            feedback.PlayAcceptedDamage();
            feedback.AdvanceTapFeedback(.025f);
            float frozen = feedback.ShakeOffsetNormalized;
            feedback.SetPresentationPaused(true);
            feedback.AdvanceTapFeedback(.12f);
            check(feedback.ShakeOffsetNormalized == frozen,
                "Presentation pause freezes feedback at its current frame without time catch-up.");
            feedback.SetPresentationPaused(false);
        }
        finally { feedback.enabled = wasEnabled; }
        gm.ReturnToMainMenu();
        yield return null;
        check(Mathf.Approximately(feedback.ShakeOffsetNormalized, 0f) && feedback.ActiveTapParticleCount == 0 && Priority(feedback) == "None",
            "Home navigation clears shake, pooled particles, and old feedback ownership before another run.");
    }

    private static IEnumerator VerifyLevelTransition(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm.campaign == null || gm.campaign.LevelCount < 3)
            yield break;

        LevelTransitionAnnouncementSettings settings = Resources.Load<LevelTransitionAnnouncementSettings>("LevelTransitionAnnouncementSettings");
        if (settings == null) settings = ScriptableObject.CreateInstance<LevelTransitionAnnouncementSettings>();
        var selector = new LevelTransitionAnnouncementSelector(settings, 741);
        MotionChallengeBoundary boundary = selector.FindBoundary(gm.campaign);
        check(boundary.Exists && boundary.completedLevelNumber == 2 && boundary.enteringLevelNumber == 3,
            "Authored campaign selects exactly the L2-to-L3 boundary for its first motion challenge.");
        LevelTransitionAnnouncement selected = selector.Select(gm.campaign, boundary.completedLevelNumber, true);
        LevelTransitionAnnouncement later = selector.Select(gm.campaign, boundary.completedLevelNumber + 1, true);
        LevelTransitionAnnouncement repeated = new LevelTransitionAnnouncementSelector(settings, 741)
            .Select(gm.campaign, boundary.completedLevelNumber, true);
        check(selected.IsMotionChallenge && selected.Subline == ChallengeLine(settings, boundary.kind) &&
              !later.IsMotionChallenge && repeated.IsMotionChallenge && repeated.Subline == selected.Subline,
            "The same fresh-run boundary repeats its one challenge, while later transitions use ordinary motivation.");
        check(Mathf.Abs(NeonMotion.T.levelTransitionDuration - 1.8f) < .01f,
            "The configured ordinary level-transition window is 1.8 seconds.");

        yield return StartFresh(gm, check);
        for (int i = 0; i < 3; i++)
            gm.campaign.GetLevel(i).requiredCorrectClicks = 1;
        // Advance the first ordinary transition deterministically to reach the authored L2 -> L3 boundary.
        gm.enabled = false;
        try
        {
            Run(gm).levelState.objectiveProgress = gm.campaign.GetLevel(0).requiredCorrectClicks - 1;
            Tap(gm, true);
            check(Flow(gm) == "LevelTransition", "Completing L1 enters the shared in-place transition immediately.");
            Invoke(gm, "AdvanceLevelTransition", 2f);
            yield return null;
            check(Flow(gm) == "Playing", "The ordinary transition returns directly to playable flow with no second intro.");
            yield return null; // avoid the one-frame transition input gate.

            ActiveRunData run = Run(gm);
            int health = run.currentHealth;
            float reserve = run.currentReserveSeconds;
            run.levelState.objectiveProgress = gm.campaign.GetLevel(1).requiredCorrectClicks - 1;
            Tap(gm, true);
            check(Flow(gm) == "LevelTransition" && run.currentLevelIndex == 2,
                "Completing L2 prepares Level 3 during the same transition window before input can resume.");
            float normal = run.levelState.normalTimeRemaining;
            string random = run.randomState;
            float angle = run.levelState.rotationAngle;
            float phase = run.levelState.scalePhase;
            Vector2 movement = run.levelState.movementPosition;
            float duration = Field<float>(gm, "levelTransitionDuration");
            var ui = gm.GetComponent<RogueliteUIController>();
            TMP_Text title = Field<TMP_Text>(ui, "transitionTitle");
            TMP_Text subtitle = Field<TMP_Text>(ui, "transitionSubtitle");
            CanvasGroup group = Field<CanvasGroup>(ui, "transitionGroup");
            check(Mathf.Abs(duration - 1.8f) < .01f && title.text == "LEVEL 2 COMPLETE" &&
                  subtitle.gameObject.activeSelf && subtitle.text == ChallengeLine(settings, boundary.kind),
                "L2-to-L3 uses a large completed-level heading and its one selected motion challenge subline.");

            Invoke(gm, "AdvanceLevelTransition", .24f);
            Canvas.ForceUpdateCanvases();
            check(group.alpha > .95f && !title.isTextOverflowing && !subtitle.isTextOverflowing && title.fontSize > subtitle.fontSize,
                "Transition heading and subline have a readable visible hold with bounded TMP layout.");
            Invoke(gm, "AdvanceLevelTransition", 1.31f);
            check(Mathf.Abs(Field<float>(gm, "levelTransitionElapsed") - 1.55f) < .02f && group.alpha > .95f &&
                  run.currentHealth == health && Mathf.Approximately(run.currentReserveSeconds, reserve) &&
                  Mathf.Approximately(run.levelState.normalTimeRemaining, normal) && run.randomState == random &&
                  Mathf.Approximately(run.levelState.rotationAngle, angle) && Mathf.Approximately(run.levelState.scalePhase, phase) &&
                  run.levelState.movementPosition == movement,
                "The 1.4-second reading hold leaves normal time, reserve, health, RNG, and motion simulation paused.");
            if (capture != null)
                yield return capture("tap-feedback-level-complete-hold");

            gm.OpenSettings();
            yield return null;
            float frozen = Field<float>(gm, "levelTransitionElapsed");
            Invoke(gm, "AdvanceLevelTransition", .25f);
            check(Flow(gm) == "Settings" && Mathf.Approximately(Field<float>(gm, "levelTransitionElapsed"), frozen),
                "External Settings pause prevents the level-complete transition from finishing behind its modal.");
            gm.CloseSettings();
            gm.enabled = true;
            yield return WaitForFlow(gm, "Playing");
        }
        finally
        {
            gm.enabled = true;
            for (int i = 0; gm.campaign != null && i < gm.campaign.LevelCount; i++)
                gm.campaign.GetLevel(i).requiredCorrectClicks = Mathf.Max(40, gm.campaign.GetLevel(i).requiredCorrectClicks);
        }
    }

    private static IEnumerator VerifyOnboardingIgnoredTap(GameManager gm, GameplayFeedbackController feedback, Action<bool, string> check)
    {
        yield return StartFresh(gm, check);
        gm.OpenSettings();
        yield return WaitForFlow(gm, "Settings");
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        string envelope = JsonUtility.ToJson(data);
        try
        {
            gm.BeginOnboardingReplay();
            yield return WaitForOnboardingFirstTap(gm);
            ActiveRunData practice = Run(gm);
            int health = practice.currentHealth;
            int objective = practice.levelState.objectiveProgress;
            string sequence = Sequence(practice);
            string random = practice.randomState;
            feedback.ResetTapFeedback();
            Tap(gm, false);
            yield return null;
            check(practice.currentHealth == health && practice.levelState.objectiveProgress == objective &&
                  Sequence(practice) == sequence && practice.randomState == random && Priority(feedback) == "None",
                "Ignored onboarding wrong input creates neither practice damage nor unrelated tap feedback.");
            Tap(gm, true);
            yield return null;
            check(gm.CurrentOnboardingStep == OnboardingStep.FollowSequence && JsonUtility.ToJson(data) == envelope,
                "Accepted isolated practice feedback advances only the practice sequence and never mutates the saved run envelope.");
            gm.SkipOnboarding();
            yield return WaitForFlow(gm, "Settings");
            check(JsonUtility.ToJson(data) == envelope,
                "Leaving replay practice restores the complete isolated run envelope exactly.");
        }
        finally
        {
            gm.ReturnToMainMenu();
        }
    }

    private static CampaignDefinition CreateFixture(CampaignDefinition source)
    {
        CampaignDefinition fixture = UnityEngine.Object.Instantiate(source);
        fixture.hideFlags = HideFlags.HideAndDontSave;
        fixture.levels = new List<LevelData>();
        for (int i = 0; i < source.LevelCount; i++)
        {
            LevelData level = UnityEngine.Object.Instantiate(source.GetLevel(i));
            level.hideFlags = HideFlags.HideAndDontSave;
            level.requiredCorrectClicks = Mathf.Max(40, level.requiredCorrectClicks);
            level.timeLimit = Mathf.Max(20f, level.timeLimit);
            fixture.levels.Add(level);
        }
        return fixture;
    }

    private static void DestroyFixture(CampaignDefinition fixture)
    {
        if (fixture == null) return;
        if (fixture.levels != null)
            foreach (LevelData level in fixture.levels)
                if (level != null) UnityEngine.Object.Destroy(level);
        UnityEngine.Object.Destroy(fixture);
    }

    private static IEnumerator StartFresh(GameManager gm, Action<bool, string> check)
    {
        gm.enabled = true;
        gm.ReturnToMainMenu();
        yield return null;
        SaveEnvelopeData data = Field<SaveEnvelopeData>(gm, "saveData");
        data.activeRun = null;
        data.lastRunResult = null;
        data.profile.hasStartedRealRun = true;
        data.profile.onboardingStatus = OnboardingStatus.Completed;
        gm.StartNewRun();
        yield return WaitForFlow(gm, "Playing");
        check(!gm.IsOnboardingActive, "Tap-feedback fixture starts a normal isolated gameplay run.");
    }

    private static IEnumerator WaitForFlow(GameManager gm, string expected)
    {
        float until = Time.realtimeSinceStartup + 4f;
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        while ((Flow(gm) != expected || (expected == "Playing" && (ui == null || !ui.IsGameplayReady))) &&
               Time.realtimeSinceStartup < until)
            yield return null;
        if (Flow(gm) != expected || (expected == "Playing" && (ui == null || !ui.IsGameplayReady)))
            throw new InvalidOperationException("Timed out waiting for " + expected + "; got " + Flow(gm));
        // The first frame the screen reports ready is still deliberately input-gated.
        yield return null;
    }

    private static IEnumerator WaitForOnboardingFirstTap(GameManager gm)
    {
        float until = Time.realtimeSinceStartup + 4f;
        RogueliteUIController ui = gm.GetComponent<RogueliteUIController>();
        while ((Flow(gm) != "Onboarding" || Field<bool>(gm, "levelTransitionActive") ||
                gm.CurrentOnboardingStep != OnboardingStep.FirstTap || ui == null || !ui.OnboardingPresentationReady) &&
               Time.realtimeSinceStartup < until)
            yield return null;
        if (Flow(gm) != "Onboarding" || Field<bool>(gm, "levelTransitionActive") ||
            gm.CurrentOnboardingStep != OnboardingStep.FirstTap || ui == null || !ui.OnboardingPresentationReady)
            throw new InvalidOperationException("Timed out waiting for replay onboarding FirstTap presentation.");
        yield return null;
    }

    private static string ChallengeLine(LevelTransitionAnnouncementSettings settings, MotionChallengeKind kind)
    {
        switch (kind)
        {
            case MotionChallengeKind.Rotation: return settings.rotationChallenge;
            case MotionChallengeKind.Move: return settings.moveChallenge;
            case MotionChallengeKind.Scale: return settings.scaleChallenge;
            case MotionChallengeKind.Combined: return settings.combinedMotionChallenge;
            default: return string.Empty;
        }
    }

    private static void Tap(GameManager gm, bool correct)
    {
        ActiveRunData run = Run(gm);
        int index = correct ? (run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex) : run.levelState.mediumIndex;
        Squares(gm)[index].OnPointerDown(new PointerEventData(EventSystem.current));
        Canvas.ForceUpdateCanvases();
    }

    private static GameSquare Target(GameManager gm, bool correct)
    {
        ActiveRunData run = Run(gm);
        int index = correct ? (run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex) : run.levelState.mediumIndex;
        return Squares(gm)[index];
    }

    private static string Priority(GameplayFeedbackController feedback)
    {
        object value = feedback.GetType().GetProperty("TapFeedbackPriority")?.GetValue(feedback, null);
        return value == null ? "None" : value.ToString();
    }

    private static ActiveRunData Run(GameManager gm) => Field<ActiveRunData>(gm, "sessionRun");
    private static List<GameSquare> Squares(GameManager gm) => Field<List<GameSquare>>(gm, "instantiatedSquares");
    private static string Sequence(ActiveRunData run) => run.levelState.smallIndex + ":" + run.levelState.mediumIndex + ":" + run.levelState.largeIndex;
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
