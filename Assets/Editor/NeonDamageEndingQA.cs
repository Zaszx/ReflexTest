using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Isolated, real-input QA for damage feedback and the committed run-ending flow.</summary>
public static class NeonDamageEndingQA
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string root, checkPath;
    private static int passed;
    private sealed class Frame { public Texture2D image; public string row; }

    public static IEnumerator Run(GameManager gm, string stage, bool capture)
    {
        NeonSaveService saves = Get<NeonSaveService>(gm, "saveService");
        if (!NeonPresentationQAProfile.IsActive || saves == null || saves.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Damage-ending QA requires NeonPresentationQAProfile isolated storage.");
        root = Path.Combine(Application.dataPath, "..", "Artifacts", "DamageEnding", stage, Screen.width + "x" + Screen.height);
        Directory.CreateDirectory(root); checkPath = Path.Combine(root, "checks.txt"); File.WriteAllText(checkPath, ""); passed = 0;

        CampaignDefinition original = gm.campaign;
        CampaignDefinition fixture = UnityEngine.Object.Instantiate(original);
        fixture.levels = new List<LevelData> { UnityEngine.Object.Instantiate(original.GetLevel(0)) };
        Configure(fixture.GetLevel(0)); gm.campaign = fixture;
        float oldDuration = NeonMotion.T.terminalTransitionDuration;
        bool oldReduced = NeonTheme.ReducedEffects;
        try
        {
            NeonMotion.T.terminalTransitionDuration = .9f; NeonTheme.ReducedEffects = false;
            yield return Start(gm);
            yield return DamageInputChecks(gm, fixture);
            yield return FatalAndPersistenceChecks(gm, saves);
            yield return ReserveAndLifecycleChecks(gm, saves);
            yield return CancellationAndModeChecks(gm);
            if (capture) yield return CaptureSequences(gm, fixture);
            NeonTheme.ReducedEffects = true; NeonMotion.T.terminalTransitionDuration = 0f;
            yield return Start(gm); Prime(gm, 1, 4f, false);
            Tap(gm, false); yield return null; yield return null; yield return null;
            Check(Flow(gm) == "RunSummary" && Property<bool>(Get<object>(gm, "rogueliteUI"), "IsRunReportInputReady"), "Reduced effects and zero terminal duration settle to an input-ready report after a fresh frame.");
            yield return NeonTerminalInputQA.Run(gm, Check);
            File.AppendAllText(checkPath, "COMPLETE: " + passed + " passed; 0 failed.\n");
        }
        finally
        {
            NeonMotion.T.terminalTransitionDuration = oldDuration; NeonTheme.ReducedEffects = oldReduced;
            gm.enabled = true; gm.ReturnToMainMenu(); gm.campaign = original;
            foreach (LevelData level in fixture.levels) UnityEngine.Object.Destroy(level);
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static IEnumerator DamageInputChecks(GameManager gm, CampaignDefinition fixture)
    {
        ActiveRunData run = Prime(gm, 5, 9f, true); gm.enabled = false;
        string sequence = Sequence(run); int objective = run.levelState.objectiveProgress, reverse = run.levelState.reverseCorrectTapsRemaining;
        int cells = Squares(gm).Count; GameSquare wrong = Squares(gm)[run.levelState.mediumIndex]; Tap(gm, false); yield return null;
        Check(run.currentHealth == 4 && Sequence(run) == sequence && run.levelState.objectiveProgress == objective && run.levelState.reverseCorrectTapsRemaining == reverse,
            "Actual nonfatal pointer-down removes one health while sequence, objective and Reverse count remain unchanged.");
        Check(Squares(gm).Count == cells && HudHealth(gm).StartsWith("4") && Get<object>(wrong, "errorFrame") != null, "Latest health HUD, local wrong-cell error layer, and hierarchy update without allocation churn.");
        Tap(gm, true); yield return null;
        Check(run.levelState.objectiveProgress == objective + 1, "A correct pointer-down remains accepted during damage feedback.");
        yield return new WaitForSecondsRealtime(.35f);
        int before = run.currentHealth; for (int i = 0; i < 3; i++) Tap(gm, false); yield return null;
        object hudMotion = Get<object>(Get<object>(gm, "rogueliteUI"), "hudMotion");
        Check(run.currentHealth == before - 3 && Squares(gm).Count == cells && Get<TMPro.TMP_Text>(hudMotion, "floatingDamage").text == "−3", "Rapid damage aggregates into one bounded floating −3 while each valid hit remains authoritative.");
        string unchanged = JsonUtility.ToJson(run); gm.OpenSettings(); Tap(gm, false); yield return null;
        Check(JsonUtility.ToJson(run) == unchanged, "Settings ignores grid input and produces no damage mutation.");
        gm.CloseSettings(); yield return WaitFor(gm, "Playing");
        Invoke(gm, "OnApplicationPause", true); Tap(gm, false); yield return null;
        Check(JsonUtility.ToJson(run) == unchanged, "Suspended gameplay ignores grid input.");
        Invoke(gm, "OnApplicationPause", false); gm.enabled = false;
        var outside = new PointerEventData(EventSystem.current) { position = new Vector2(-10000, -10000) };
        ExecuteEvents.Execute(gm.gameplayPanel, outside, ExecuteEvents.pointerDownHandler); yield return null;
        Check(JsonUtility.ToJson(run) == unchanged, "Out-of-range pointer event produces no damage or feedback state.");
        gm.enabled = true;
        float normalBefore = run.levelState.normalTimeRemaining;
        yield return new WaitForSecondsRealtime(.12f);
        Check(run.levelState.normalTimeRemaining < normalBefore && Flow(gm) == "Playing", "Ordinary damage never pauses the gameplay timer or changes playable flow.");
    }

    private static IEnumerator FatalAndPersistenceChecks(GameManager gm, NeonSaveService saves)
    {
        gm.ReturnToMainMenu(); yield return Start(gm); ActiveRunData run = Prime(gm, 1, 8f, true);
        run.pendingCoins = 17; run.levelState.rotationAngle = 31; run.levelState.scalePhase = .73f; run.levelState.movementPosition = new Vector2(.18f, -.21f);
        Invoke(gm, "ApplyGridMotionState", false, 0f); long wallet = Get<SaveEnvelopeData>(gm, "saveData").profile.coins;
        string pose = GridPose(gm);
        gm.enabled = false; Tap(gm, false); string frozen = JsonUtility.ToJson(run); yield return null;
        SaveEnvelopeData data = Get<SaveEnvelopeData>(gm, "saveData");
        Check(Flow(gm) == "RunEnding" || Flow(gm) == "RunSummary", "Fatal pointer-down immediately leaves playable flow.");
        Check(data.activeRun == null && data.lastRunResult != null && data.profile.coins == wallet + 17, "Fatal terminal transaction clears active run, banks once, and writes a result snapshot.");
        Check(HudHealth(gm).StartsWith("0"), "Fatal health number displays zero before report presentation.");
        Check(JsonUtility.ToJson(run) == frozen && GridPose(gm) == pose,
            "Terminal frames retain frozen authoritative timers, RNG/motion state and grid pose.");
        string committed = JsonUtility.ToJson(data.lastRunResult); Invoke(gm, "FailRun", "RESERVE DEPLETED"); yield return null;
        Check(JsonUtility.ToJson(data.lastRunResult) == committed && data.profile.coins == wallet + 17, "A second terminal request cannot replace cause or bank twice.");
        for (int i = 0; i < 8; i++)
        {
            Invoke(gm, "AdvanceRunEndingPresentation", .1f);
            Check(GridPose(gm) == pose && JsonUtility.ToJson(run) == frozen, "Frozen grid and run state remain unchanged at terminal step " + i + ".");
        }
        for (int i = 0; i < 4; i++) Invoke(gm, "AdvanceRunEndingPresentation", .1f); gm.enabled = true;
        Check(JsonUtility.ToJson(data.lastRunResult) == committed, "Report animation callbacks do not mutate the committed save snapshot.");
        SaveEnvelopeData disk = saves.Load(gm.gameConfig);
        Check(disk.activeRun == null && JsonUtility.ToJson(disk.lastRunResult) == committed, "Reload data contains a stable report and no failed active run.");
        Set(gm, "saveData", disk); Invoke(gm, "RestoreCommittedRunReport");
        Check(Flow(gm) == "RunSummary" && Get<SaveEnvelopeData>(gm, "saveData").profile.coins == wallet + 17, "Committed report restores without replaying banking.");
    }

    private static IEnumerator ReserveAndLifecycleChecks(GameManager gm, NeonSaveService saves)
    {
        gm.ReturnToMainMenu(); yield return Start(gm); ActiveRunData run = Prime(gm, 5, 8f, false); int health = run.currentHealth;
        run.levelState.normalTimeRemaining = .001f; yield return null; gm.enabled = false;
        Check(run.levelState.reserveActive && run.currentHealth == health, "Normal expiry enters reserve instead of terminal failure.");
        run.levelState.reserveActive = true; run.currentReserveSeconds = 0f; Invoke(gm, "Update"); yield return null; gm.enabled = true;
        Check(run.currentReserveSeconds == 0f && Get<SaveEnvelopeData>(gm, "saveData").lastRunResult.reason == "RESERVE DEPLETED" && run.currentHealth == health,
            "Reserve exhaustion clamps timer to zero, preserves health, and records the reserve cause.");
        object hud = Get<object>(Get<object>(gm, "rogueliteUI"), "hudMotion");
        Check(!Get<TMPro.TMP_Text>(hud, "floatingDamage").gameObject.activeSelf && gm.timerText.text.Contains("0.0"), "Reserve failure displays zero with no health-loss label.");
        gm.enabled = false;
        float elapsed = Get<float>(gm, "runEndingElapsed"); gm.OpenSettings(); yield return new WaitForSecondsRealtime(.12f);
        Invoke(gm, "AdvanceRunEndingPresentation", .1f);
        Check(Mathf.Approximately(Get<float>(gm, "runEndingElapsed"), elapsed), "Settings freezes terminal presentation without catch-up.");
        gm.CloseSettings(); yield return WaitFor(gm, "RunEnding");
        foreach (float phase in new[] { .12f, .40f, .72f })
        {
            Invoke(gm, "AdvanceRunEndingPresentation", Mathf.Max(0f, phase - Get<float>(gm, "runEndingElapsed")));
            Invoke(gm, "OnApplicationPause", true); elapsed = Get<float>(gm, "runEndingElapsed"); yield return new WaitForSecondsRealtime(.12f);
            Invoke(gm, "AdvanceRunEndingPresentation", .1f);
            Check(Mathf.Approximately(Get<float>(gm, "runEndingElapsed"), elapsed), "Backgrounding freezes terminal phase " + phase + ".");
            Invoke(gm, "OnApplicationPause", false); Invoke(gm, "AdvanceRunEndingPresentation", .01f);
            Check(Mathf.Abs(Get<float>(gm, "runEndingElapsed") - elapsed - .01f) < .0001f, "Resume advances only the supplied frame delta at phase " + phase + ".");
        }
        gm.enabled = true;
        yield return null;
    }

    private static IEnumerator CancellationAndModeChecks(GameManager gm)
    {
        gm.ReturnToMainMenu(); yield return Start(gm); ActiveRunData run = Prime(gm, 1, 5f, false); Tap(gm, false); yield return null;
        gm.ReturnToMainMenu(); gm.StartNewRun(); yield return WaitFor(gm, "Playing"); yield return new WaitForSecondsRealtime(1.05f);
        Check(Flow(gm) == "Playing" && Get<SaveEnvelopeData>(gm, "saveData").activeRun != null && Get<SaveEnvelopeData>(gm, "saveData").lastRunResult == null,
            "Home then a new run cancels obsolete terminal callbacks and clears the old report.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gm.ReturnToMainMenu(); string real = JsonUtility.ToJson(Get<SaveEnvelopeData>(gm, "saveData")); gm.StartDebugLevel(0); yield return WaitFor(gm, "Playing"); Prime(gm, 1, 4f, false); Tap(gm, false); yield return null;
        Check(JsonUtility.ToJson(Get<SaveEnvelopeData>(gm, "saveData")) == real, "Debug terminal sandbox leaves the isolated real envelope unchanged.");
#endif
    }

    private static IEnumerator CaptureSequences(GameManager gm, CampaignDefinition fixture)
    {
        yield return Capture(gm, "01-single-hit", false, false);
        yield return Capture(gm, "02-rapid-damage", true, false);
        yield return Capture(gm, "03-fatal-after-recent-hit", false, false);
        yield return Capture(gm, "04-reserve-exhaustion", false, true);
    }

    private static IEnumerator Capture(GameManager gm, string name, bool rapid, bool reserve)
    {
        bool fatal = name.Contains("fatal");
        gm.ReturnToMainMenu(); yield return Start(gm); ActiveRunData run = Prime(gm, fatal ? 2 : 5, 8f, rapid || fatal);
        run.levelState.rotationAngle = 42f; run.levelState.scalePhase = .6f; run.levelState.movementPosition = new Vector2(.2f, -.15f); Invoke(gm, "ApplyGridMotionState", false, 0f);
        string dir = Path.Combine(root, name); Directory.CreateDirectory(dir); var frames = new List<Frame>(); var rows = new List<string> { "frame,elapsed,flow,endingElapsed,health,reserve" };
        double start = Time.realtimeSinceStartupAsDouble, next = 0;
        bool cueA = false, cueB = false, cueC = false;
        var events = new List<string> { "elapsed,event" };
        while (Time.realtimeSinceStartupAsDouble - start < (fatal || reserve ? 2.2 : .8))
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            // Record a baseline first, then make each named scenario's cue explicit.
            if (!cueA && elapsed >= .20) { cueA = true; if (reserve) { run.levelState.normalTimeRemaining = 0f; run.levelState.reserveActive = true; } else Tap(gm, false); events.Add(elapsed.ToString("F6", CultureInfo.InvariantCulture) + (reserve ? ",enter-reserve" : ",wrong-tap")); }
            if (!cueB && elapsed >= (reserve ? .50 : .28)) { cueB = true; if (reserve) run.currentReserveSeconds = 0f; else if (rapid || fatal) Tap(gm, false); if (reserve || rapid || fatal) events.Add(elapsed.ToString("F6", CultureInfo.InvariantCulture) + (reserve ? ",exhaust-reserve" : ",wrong-tap")); }
            if (rapid && cueB && !cueC && elapsed >= .34 && run.currentHealth > 1) { Tap(gm, false); cueC = true; events.Add(elapsed.ToString("F6", CultureInfo.InvariantCulture) + ",wrong-tap"); }
            if (elapsed < next) { yield return null; continue; }
            yield return new WaitForEndOfFrame(); elapsed = Time.realtimeSinceStartupAsDouble - start;
            frames.Add(new Frame { image = ScreenCapture.CaptureScreenshotAsTexture(), row = string.Join(",", frames.Count, elapsed.ToString("F6", CultureInfo.InvariantCulture), Flow(gm), Get<float>(gm, "runEndingElapsed").ToString("F6", CultureInfo.InvariantCulture), run.currentHealth, run.currentReserveSeconds.ToString("F6", CultureInfo.InvariantCulture)) }); next += 1d / 30d;
        }
        gm.enabled = false;
        for (int i = 0; i < frames.Count; i++) { rows.Add(frames[i].row); File.WriteAllBytes(Path.Combine(dir, "frame-" + i.ToString("D4") + ".png"), frames[i].image.EncodeToPNG()); UnityEngine.Object.Destroy(frames[i].image); yield return null; }
        gm.enabled = true; File.WriteAllLines(Path.Combine(dir, "events.csv"), events);
        File.WriteAllLines(Path.Combine(dir, "timestamps.csv"), rows);
    }

    private static void Configure(LevelData level) { level.gridSize = 3; level.requiredCorrectClicks = 20; level.timeLimit = 30; level.reverseEnabled = true; level.rotateSpeed = 48f; level.scaleEnabled = true; level.minimumGridScale = .78f; level.maximumGridScale = 1f; level.movementEnabled = true; level.movementSpeedNormalized = .14f; }
    private static IEnumerator Start(GameManager gm) { gm.enabled = true; gm.ReturnToMainMenu(); Get<SaveEnvelopeData>(gm, "saveData").activeRun = null; gm.StartNewRun(); yield return WaitFor(gm, "Playing"); }
    private static ActiveRunData Prime(GameManager gm, int health, float reserve, bool reverse) { ActiveRunData run = Get<ActiveRunData>(gm, "sessionRun"); run.upgrades.maxHealth = Mathf.Max(run.upgrades.maxHealth, health); run.currentHealth = health; run.currentReserveSeconds = reserve; run.levelState.normalTimeRemaining = 30f; run.levelState.reserveActive = false; run.levelState.reverseActive = reverse; run.levelState.reverseCooldownRemaining = 999f; run.levelState.reverseCorrectTapsRemaining = reverse ? 3 : 0; Invoke(gm, "UpdateGameplayUI"); return run; }
    private static string GridPose(GameManager gm)
    {
        var transforms = new List<Transform> { gm.gridContainer, Get<RectTransform>(gm, "rotationScaleRoot"), Get<RectTransform>(gm, "gridContentRoot") };
        foreach (GameSquare square in Squares(gm)) transforms.Add(square.transform);
        var pose = new System.Text.StringBuilder();
        foreach (Transform t in transforms) pose.Append(t.localPosition.ToString("F6")).Append(t.localRotation.ToString("F6")).Append(t.localScale.ToString("F6"));
        return pose.ToString();
    }
    private static void Tap(GameManager gm, bool correct) { ActiveRunData run = Get<ActiveRunData>(gm, "sessionRun"); int index = correct ? (run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex) : run.levelState.mediumIndex; Squares(gm)[index].OnPointerDown(new PointerEventData(EventSystem.current)); Canvas.ForceUpdateCanvases(); }
    private static List<GameSquare> Squares(GameManager gm) => Get<List<GameSquare>>(gm, "instantiatedSquares");
    private static string HudHealth(GameManager gm) { object ui = Get<object>(gm, "rogueliteUI"); return Get<TMPro.TMP_Text>(ui, "health").text; }
    private static string Sequence(ActiveRunData run) => run.levelState.smallIndex + ":" + run.levelState.mediumIndex + ":" + run.levelState.largeIndex;
    private static string Flow(GameManager gm) => Get<object>(gm, "state").ToString();
    private static void Check(bool condition, string message) { File.AppendAllText(checkPath, (condition ? "PASS: " : "FAIL: ") + message + "\n"); if (!condition) throw new InvalidOperationException(message); passed++; }
    private static IEnumerator WaitFor(GameManager gm, string state) { float until = Time.realtimeSinceStartup + 4f; while (Flow(gm) != state && Time.realtimeSinceStartup < until) yield return null; if (Flow(gm) != state) throw new InvalidOperationException("Timed out waiting for " + state + "; got " + Flow(gm)); if (state == "Playing") yield return null; }
    private static object Invoke(object target, string name, params object[] args) { MethodInfo method = target.GetType().GetMethod(name, Hidden); if (method == null) throw new MissingMethodException(target.GetType().Name, name); return method.Invoke(target, args); }
    private static T Get<T>(object target, string name) { FieldInfo field = target.GetType().GetField(name, Hidden); if (field == null) throw new MissingFieldException(target.GetType().Name, name); return (T)field.GetValue(target); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static T Property<T>(object target, string name) { PropertyInfo property = target.GetType().GetProperty(name); if (property == null) throw new MissingMemberException(target.GetType().Name, name); return (T)property.GetValue(target, null); }
}
