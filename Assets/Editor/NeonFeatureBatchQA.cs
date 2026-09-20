#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

public static class NeonFeatureBatchQA
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        var authored = gm.campaign;
        var fixture = ScriptableObject.CreateInstance<CampaignDefinition>();
        var sizes = new[] { 3, 4, 5, 2 };
        var counts = new[] { 2, 4, 5, 3 };
        for (int i = 0; i < sizes.Length; i++)
        {
            var level = UnityEngine.Object.Instantiate(authored.GetLevel(i));
            level.gridSize = sizes[i]; level.outlineCount = counts[i];
            level.enemies.enabled = false; level.reverseEnabled = false;
            level.rotateSpeed = i == 0 ? 0f : 70f;
            level.scaleEnabled = level.movementEnabled = i > 0;
            level.timeLimit = 120f; level.requiredCorrectClicks = 100;
            fixture.levels.Add(level);
        }
        try
        {
            gm.ReturnToMainMenu(); gm.campaign = fixture;
            var data = Field<SaveEnvelopeData>(gm, "saveData");
            data.activeRun = null; data.lastRunResult = null;
            data.profile.hasStartedRealRun = true; data.profile.onboardingStatus = OnboardingStatus.Completed;
            gm.StartNewRun();
            yield return Ready(gm);
            gm.enabled = false;
            for (int level = 0; level < sizes.Length; level++)
            {
                var run = Field<ActiveRunData>(gm, "sessionRun");
                var cells = Field<List<GameSquare>>(gm, "instantiatedSquares");
                check(run.levelState.targetIndices.Length == counts[level] && cells.Count == sizes[level] * sizes[level],
                    $"K={counts[level]} is authoritative on the {sizes[level]}x{sizes[level]} grid.");
                Invoke(gm, "NormalizeTargetPresentation");
                float previousScale = 0f;
                foreach (int index in run.levelState.targetIndices)
                {
                    float scale = cells[index].outlineImage.rectTransform.localScale.x;
                    check(scale > previousScale, $"Level {level + 1} outline ranks remain strictly ordered.");
                    previousScale = scale;
                }
                yield return capture("targets-" + counts[level]);
                int consumed = run.levelState.largeIndex;
                int progress = run.levelState.objectiveProgress;
                Tap(cells[consumed], level * 10 + 1);
                check(run.levelState.objectiveProgress == progress + 1 && run.levelState.smallIndex != consumed,
                    "Normal accepted tap advances once and prefers a different spawn cell.");
                if (level == 0) yield return ExitFrames(cells[consumed], "normal", capture);
                run.levelState.reverseActive = true; run.levelState.reverseCorrectTapsRemaining = 10;
                Invoke(gm, "ApplySequenceVisuals", false);
                GameSquare reversed = cells[run.levelState.smallIndex];
                Tap(reversed, level * 10 + 2);
                check(run.levelState.objectiveProgress == progress + 2, "Reverse accepts the smallest of every configured K.");
                if (level == 0) yield return ExitFrames(reversed, "reverse", capture);
                run.levelState.reverseActive = false;
                if (level == sizes.Length - 1) break;
                run.levelState.objectiveProgress = fixture.GetLevel(level).requiredCorrectClicks - 1;
                Tap(cells[run.levelState.largeIndex], level * 10 + 3);
                check(Field<bool>(gm, "levelTransitionActive"), "Completing a level uses the existing in-place transition.");
                run = Field<ActiveRunData>(gm, "sessionRun");
                cells = Field<List<GameSquare>>(gm, "instantiatedSquares");
                int before = run.levelState.objectiveProgress;
                Tap(cells[run.levelState.largeIndex], level * 10 + 4);
                check(run.levelState.objectiveProgress == before, "Pointer-down during popup is ignored without queueing.");
                if (level == 0)
                {
                    float elapsed = Field<float>(gm, "levelTransitionElapsed");
                    float time = run.levelState.normalTimeRemaining;
                    Invoke(gm, "OnApplicationPause", true);
                    Invoke(gm, "AdvanceLevelTransition", 5f);
                    check(Field<float>(gm, "levelTransitionElapsed") == elapsed, "Backgrounding freezes transition readiness.");
                    Invoke(gm, "OnApplicationPause", false);
                    gm.OpenSettings();
                    Invoke(gm, "AdvanceLevelTransition", 5f);
                    Invoke(gm, "TickActiveGameplay", .5f);
                    check(Field<bool>(gm, "levelTransitionActive") && run.levelState.normalTimeRemaining == time,
                        "Settings preserves the popup lock and resource clock.");
                    gm.CloseSettings();
                    float deadline = Time.realtimeSinceStartup + 3f;
                    while (Field<object>(gm, "state").ToString() == "Settings" && Time.realtimeSinceStartup < deadline) yield return null;
                    yield return null;
                }
                Invoke(gm, "AdvanceLevelTransition", .10f);
                if (level == 0) yield return capture("transition-outgoing-shrink");
                Invoke(gm, "AdvanceLevelTransition", .48f);
                if (level == 0) yield return capture("transition-grid-morph");
                Invoke(gm, "AdvanceLevelTransition", 5f);
                var ui = gm.GetComponent<RogueliteUIController>();
                float renderDeadline = Time.realtimeSinceStartup + 2f;
                while (Field<bool>(gm, "levelTransitionActive") && Time.realtimeSinceStartup < renderDeadline)
                {
                    check(Field<CanvasGroup>(ui, "transitionGroup").alpha > .1f,
                        "An unregistered new UI cell keeps the popup visibly present until it is ready.");
                    yield return null;
                    Invoke(gm, "AdvanceLevelTransition", 0f);
                }
                check(!Field<bool>(gm, "levelTransitionActive") && ui.IsGameplayReady &&
                    !Field<CanvasGroup>(ui, "transitionGroup").gameObject.activeSelf,
                    "Board and UI settle before the popup disappears.");
                Canvas.ForceUpdateCanvases();
                var hits = new List<RaycastResult>();
                bool everyCellReady = true; string failure = "";
                foreach (GameSquare cell in cells)
                {
                    hits.Clear();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, cell.transform.position) }, hits);
                    if (hits.Count > 0 && hits[0].gameObject.GetComponentInParent<GameSquare>() == cell) continue;
                    everyCellReady = false;
                    failure = $" {cell.name}: top={(hits.Count > 0 ? hits[0].gameObject.name : "none")}, depth={cell.bgImage.depth}, culled={cell.bgImage.canvasRenderer.cull}";
                    break;
                }
                check(everyCellReady, $"Every cell in level {level + 2}, including new rows, receives its real UI raycast immediately after handoff.{failure}");
                // Deliberately dispatch in the exact same frame as the handoff.
                Tap(cells[run.levelState.largeIndex], level * 10 + 5);
                check(run.levelState.objectiveProgress == before + 1, "Fresh pointer-down is accepted on the exact transition completion frame.");
                float clock = run.levelState.normalTimeRemaining;
                Invoke(gm, "TickActiveGameplay", .1f);
                check(Mathf.Abs(run.levelState.normalTimeRemaining - (clock - .1f)) < .001f, "Resource time resumes immediately with gameplay.");
            }
            Invoke(gm, "SaveRealRunCritical");
            var saved = Field<NeonSaveService>(gm, "saveService").Load(gm.gameConfig);
            check(saved.activeRun != null && saved.activeRun.levelState.targetIndices.Length == 3,
                "Ordered targets persist through the existing atomic save service.");
        }
        finally
        {
            gm.ReturnToMainMenu(); gm.campaign = authored; gm.enabled = true;
            foreach (var level in fixture.levels) UnityEngine.Object.Destroy(level);
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static IEnumerator ExitFrames(GameSquare cell, string label, Func<string, IEnumerator> capture)
    {
        for (int frame = 1; frame <= 3; frame++)
        {
            cell.SetAnimationsPaused(false);
            cell.AdvancePresentation(.04f);
            cell.SetAnimationsPaused(true);
            yield return capture(label + "-exit-" + (frame * 40));
        }
        cell.SetAnimationsPaused(false);
    }

    public static IEnumerator Ready(GameManager gm)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while ((Field<object>(gm, "state").ToString() != "Playing" || !gm.GetComponent<RogueliteUIController>().IsGameplayReady) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Field<object>(gm, "state").ToString() != "Playing") throw new InvalidOperationException("Gameplay did not become ready.");
    }
    private static void Tap(GameSquare cell, int pointer) => cell.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = pointer,
        position = RectTransformUtility.WorldToScreenPoint(null, cell.transform.position) });
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
    private static object Invoke(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
}
#endif
