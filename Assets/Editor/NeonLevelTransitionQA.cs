using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Isolated real-flow fixtures and native rendered transition sequences.</summary>
public static class NeonLevelTransitionQA
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string root, checkPath;
    private static int passed;
    private sealed class Frame { public Texture2D image; public string row; }

    public static IEnumerator Run(GameManager gm, string stage, bool capture)
    {
        if (!NeonPresentationQAProfile.IsActive || Get<NeonSaveService>(gm, "saveService").DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Transition QA requires isolated storage.");
        root = Path.Combine(Application.dataPath, "..", "Artifacts", "LevelTransition", stage, Screen.width + "x" + Screen.height);
        Directory.CreateDirectory(root); checkPath = Path.Combine(root, "checks.txt"); File.WriteAllText(checkPath, ""); passed = 0;
        CampaignDefinition original = gm.campaign;
        var fixture = UnityEngine.Object.Instantiate(original);
        fixture.levels = new List<LevelData> { UnityEngine.Object.Instantiate(original.GetLevel(0)), UnityEngine.Object.Instantiate(original.GetLevel(1)) };
        fixture.completionBonusCoins = 73;
        float originalDuration = NeonMotion.T.levelTransitionDuration;
        gm.campaign = fixture;
        try
        {
            NeonMotion.T.levelTransitionDuration = 1f;
            Configure(fixture, 20, false); yield return Start(gm);
            ActiveRunData run = Prime(gm, 40f, false);
            var oldCell = Get<List<GameSquare>>(gm, "instantiatedSquares")[0];
            long expectedReward = run.pendingCoins + fixture.GetLevel(0).completionCoinReward;
            Tap(gm, true);
            Check(Flow(gm) == "LevelTransition" && !gm.successPanel.activeInHierarchy, "Intermediate completion stays in gameplay.");
            Check(run.currentLevelIndex == 1 && run.currentLevelId == fixture.GetLevel(1).stableId && run.levelState.objectiveProgress == 0 && !run.betweenLevels, "Next canonical level is prepared with zero earned progress.");
            Check(run.pendingCoins == expectedReward && run.levelsCompleted == 1 && !run.levelState.rewardGranted, "Exactly one completed-level reward is committed.");
            Check(run.currentHealth == 2 && run.currentReserveSeconds == 7.25f && run.levelState.normalTimeRemaining == 20f, "Health/reserve carry forward; new normal time is full.");
            Check(Number(gm.remainingText.GetParsedText()) == 0 && Number(gm.timerText.GetParsedText()) == 40f, "Displays begin at zero remaining and the prior NORMAL timer.");
            Check(Get<List<GameSquare>>(gm, "instantiatedSquares")[0] == oldCell, "Matching dimensions reuse cells.");
            string prepared = JsonUtility.ToJson(run);
            for (int i = 0; i < 12; i++) { Tap(gm, i % 2 == 0); Invoke(gm, "CompleteCurrentLevel"); }
            Check(JsonUtility.ToJson(run) == prepared, "Extra taps and repeated completion cannot mutate prepared state.");
            var disk = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig).activeRun;
            Check(JsonUtility.ToJson(disk) == prepared, "Mid-transition disk checkpoint retains exact timers, targets, PRNG and rewards.");
            gm.enabled = false;
            for (int step = 1; step <= 20; step++)
            {
                Invoke(gm, "AdvanceLevelTransition", .05f);
                Canvas.ForceUpdateCanvases();
                Check(Mathf.RoundToInt(Number(gm.remainingText.GetParsedText())) == step, "Count-up visits " + step + " without earning progress.");
                Check(JsonUtility.ToJson(run) == prepared, "Presentation step " + step + " leaves authoritative state unchanged.");
            }
            Check(Flow(gm) == "Playing" && Number(gm.timerText.GetParsedText()) == 20f && Number(gm.remainingText.GetParsedText()) == 20f, "One-second timeline ends at exact counters and automatically enables play.");
            Invoke(gm, "CompleteCurrentLevel");
            Check(run.currentLevelIndex == 1 && run.pendingCoins == expectedReward, "Late duplicate completion cannot skip the unplayed level.");
            gm.enabled = true;

            Configure(fixture, 160, true); yield return Start(gm);
            run = Prime(gm, 0f, true); Tap(gm, true); prepared = JsonUtility.ToJson(run);
            Check(Number(gm.timerText.GetParsedText()) == 0 && !run.levelState.reserveActive && run.currentReserveSeconds == 7.25f, "Reserve completion refills normal display from zero, preserving reserve exactly.");
            Check(Get<List<GameSquare>>(gm, "instantiatedSquares").Count == 16 && Get<UnityEngine.UI.GridLayoutGroup>(gm, "transitionLayout") != null, "Different dimensions resize retained cells while preparing added rows and columns.");
            gm.OpenSettings(); string frozen = Presentation(gm);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Presentation(gm) == frozen && JsonUtility.ToJson(run) == prepared, "Settings freezes counters, transition clock, modifiers and resources.");
            gm.CloseSettings(); yield return WaitFor(gm, "LevelTransition");
            Invoke(gm, "OnApplicationPause", true); frozen = Presentation(gm);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Presentation(gm) == frozen && JsonUtility.ToJson(run) == prepared, "Backgrounding freezes the transition without catch-up.");
            Invoke(gm, "OnApplicationPause", false); gm.ReturnToMainMenu();
            yield return new WaitForSecondsRealtime(1.1f);
            Check(Flow(gm) == "MainMenu" && Get<UnityEngine.UI.GridLayoutGroup>(gm, "transitionLayout") == null && Get<List<GameSquare>>(gm, "retiredGridCells").Count == 0, "Home cancels presentation; no stale callback reopens gameplay.");
            disk = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig).activeRun;
            Check(JsonUtility.ToJson(disk) == prepared, "Home saves the same prepared checkpoint.");
            Get<SaveEnvelopeData>(gm, "saveData").activeRun = disk; gm.ContinueRun();
            Check(JsonUtility.ToJson(RunData(gm)) == prepared, "Continue from disk restores exact state without reinitialization or reward replay.");
            yield return WaitFor(gm, "Playing"); run = RunData(gm);
            Check(run.currentLevelIndex == 1 && run.levelState.objectiveProgress == 0 && run.currentHealth == 2 && run.currentReserveSeconds == 7.25f, "Continue reaches the prepared level with carried resources.");
            long banked = Get<SaveEnvelopeData>(gm, "saveData").profile.coins;
            long finalExpected = run.pendingCoins + fixture.GetLevel(1).completionCoinReward + fixture.completionBonusCoins;
            run.levelState.objectiveProgress = fixture.GetLevel(1).requiredCorrectClicks - 1; run.levelState.reverseActive = false; Tap(gm, true);
            Check(Flow(gm) == "RunSummary" && Get<SaveEnvelopeData>(gm, "saveData").activeRun == null && Get<SaveEnvelopeData>(gm, "saveData").profile.coins == banked + finalExpected, "Final configured level preserves victory and banks the final reward and bonus once.");
            gm.ReturnToMainMenu(); string real = JsonUtility.ToJson(Get<SaveEnvelopeData>(gm, "saveData"));
            gm.StartDebugLevel(0); yield return WaitFor(gm, "Playing"); run = Prime(gm, 10f, false); Tap(gm, true);
            Check(Flow(gm) == "LevelTransition" && run.currentLevelIndex == 1 && JsonUtility.ToJson(Get<SaveEnvelopeData>(gm, "saveData")) == real, "Debug advances locally without changing profile or saved run.");
            gm.ReturnToMainMenu();
            if (capture)
            {
                Configure(fixture, 20, false); yield return Start(gm); Prime(gm, 40f, false); yield return Capture(gm, "01-matching-grid-20-downward");
                Configure(fixture, 160, true); yield return Start(gm); Prime(gm, 0f, true); yield return Capture(gm, "02-grid-and-modifiers-160-reserve");
            }
            File.AppendAllText(checkPath, "COMPLETE: " + passed + " passed; 0 failed.\n");
        }
        finally
        {
            gm.enabled = true; gm.ReturnToMainMenu(); gm.campaign = original; NeonMotion.T.levelTransitionDuration = originalDuration;
            foreach (LevelData level in fixture.levels) UnityEngine.Object.Destroy(level); UnityEngine.Object.Destroy(fixture);
        }
    }

    private static void Configure(CampaignDefinition fixture, int required, bool different)
    {
        var a = fixture.GetLevel(0); var b = fixture.GetLevel(1);
        a.requiredCorrectClicks = 3; a.timeLimit = 50; a.gridSize = 3; a.completionCoinReward = 11;
        a.reverseEnabled = false; a.rotateSpeed = 30; a.scaleEnabled = true; a.minimumGridScale = .85f; a.maximumGridScale = 1f; a.scaleCycleDuration = 2f;
        b.requiredCorrectClicks = required; b.timeLimit = 20; b.gridSize = different ? 4 : 3; b.completionCoinReward = 17;
        b.reverseEnabled = true; b.rotateSpeed = different ? -65 : 0; b.scaleEnabled = different; b.movementEnabled = different;
        b.minimumGridScale = .75f; b.maximumGridScale = 1f; b.movementSpeedNormalized = .16f;
        a.cellColor = new Color(.05f, .5f, .7f); a.outlineColor = Color.cyan;
        b.cellColor = different ? new Color(.7f, .08f, .3f) : a.cellColor; b.outlineColor = different ? Color.magenta : a.outlineColor;
    }
    private static IEnumerator Start(GameManager gm)
    {
        gm.enabled = true; gm.ReturnToMainMenu(); Get<SaveEnvelopeData>(gm, "saveData").activeRun = null;
        Invoke(gm, "OnApplicationPause", false); Invoke(gm, "OnApplicationFocus", true); gm.StartNewRun(); yield return WaitFor(gm, "Playing");
    }
    private static ActiveRunData Prime(GameManager gm, float normalTime, bool reserve)
    {
        var run = RunData(gm); run.currentHealth = 2; run.currentReserveSeconds = 7.25f; run.pendingCoins = 7;
        run.levelState.normalTimeRemaining = normalTime; run.levelState.reserveActive = reserve;
        run.levelState.reverseActive = reserve; run.levelState.reverseCorrectTapsRemaining = reserve ? 1 : 0;
        run.levelState.reverseCooldownRemaining = 999; run.levelState.objectiveProgress = gm.campaign.GetLevel(0).requiredCorrectClicks - 1;
        run.levelState.rotationAngle = 37f; run.levelState.scalePhase = .65f;
        Invoke(gm, "ApplyGridMotionState", false, 0f);
        Invoke(gm, "UpdateGameplayUI"); return run;
    }
    private static IEnumerator Capture(GameManager gm, string name)
    {
        string dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
        var frames = new List<Frame>(); var rows = new List<string> { "frame,elapsed,flow,transitionElapsed,shownRemaining,shownNormal,actualProgress,actualNormal,reserve,health" };
        Tap(gm, true); double start = Time.realtimeSinceStartupAsDouble, next = 0, readyAt = -1;
        string checkpoint = JsonUtility.ToJson(RunData(gm));
        while (Time.realtimeSinceStartupAsDouble - start < 1.25)
        {
            if (Time.realtimeSinceStartupAsDouble - start < next) { yield return null; continue; }
            yield return new WaitForEndOfFrame(); double elapsed = Time.realtimeSinceStartupAsDouble - start; var run = RunData(gm);
            if (Flow(gm) == "LevelTransition") { if (JsonUtility.ToJson(run) != checkpoint) throw new InvalidOperationException("Gameplay changed during recorded transition."); }
            else if (readyAt < 0) readyAt = elapsed;
            string row = string.Join(",", frames.Count, elapsed.ToString("F6", CultureInfo.InvariantCulture), Flow(gm), Get<float>(gm, "levelTransitionElapsed").ToString("F6", CultureInfo.InvariantCulture), Number(gm.remainingText.GetParsedText()), Number(gm.timerText.GetParsedText()).ToString("F3", CultureInfo.InvariantCulture), run.levelState.objectiveProgress, run.levelState.normalTimeRemaining.ToString("F6", CultureInfo.InvariantCulture), run.currentReserveSeconds.ToString("F6", CultureInfo.InvariantCulture), run.currentHealth);
            frames.Add(new Frame { image = ScreenCapture.CaptureScreenshotAsTexture(), row = row }); next += 1d / 30d;
        }
        gm.enabled = false;
        for (int i = 0; i < frames.Count; i++) { rows.Add(frames[i].row); File.WriteAllBytes(Path.Combine(dir, "frame-" + i.ToString("D4") + ".png"), frames[i].image.EncodeToPNG()); UnityEngine.Object.Destroy(frames[i].image); yield return null; }
        gm.enabled = true; File.WriteAllLines(Path.Combine(dir, "timestamps.csv"), rows);
        Check(readyAt >= .90 && readyAt < 1.25, name + " ready in approximately one second: " + readyAt.ToString("F3") + "s.");
        Check(Get<UnityEngine.UI.GridLayoutGroup>(gm, "transitionLayout") == null && Get<List<GameSquare>>(gm, "retiredGridCells").Count == 0 && !Get<bool>(gm, "levelTransitionActive"), name + " removes obsolete visuals and overrides.");
    }
    private static void Check(bool condition, string message)
    {
        File.AppendAllText(checkPath, (condition ? "PASS: " : "FAIL: ") + message + "\n"); if (!condition) throw new InvalidOperationException(message); passed++;
    }
    private static IEnumerator WaitFor(GameManager gm, string state)
    {
        float deadline = Time.realtimeSinceStartup + 4f; while (Flow(gm) != state && Time.realtimeSinceStartup < deadline) yield return null;
        if (Flow(gm) != state) throw new InvalidOperationException("Timed out waiting for " + state + "; got " + Flow(gm));
    }
    private static void Tap(GameManager gm, bool correct)
    {
        var run = RunData(gm); var cells = Get<List<GameSquare>>(gm, "instantiatedSquares");
        int index = correct ? (run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex) : run.levelState.mediumIndex;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(null, cells[index].transform.position) };
        ExecuteEvents.Execute(cells[index].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Canvas.ForceUpdateCanvases();
    }
    private static float Number(string value) => float.Parse(Regex.Match(value, @"\d+(?:\.\d+)?").Value, CultureInfo.InvariantCulture);
    private static string Presentation(GameManager gm) => Get<float>(gm, "levelTransitionElapsed") + ":" + gm.timerText.text + ":" + gm.remainingText.text;
    private static string Flow(GameManager gm) => Get<object>(gm, "state").ToString();
    private static ActiveRunData RunData(GameManager gm) => Get<ActiveRunData>(gm, "sessionRun");
    private static object Invoke(object o, string n, params object[] a) => o.GetType().GetMethod(n, Hidden).Invoke(o, a);
    private static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Hidden).GetValue(o);
}
