using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Explicit, repeatable editor-only runtime capture. No scene or campaign assets are saved.
/// Every run receives a new profile beneath .utmp before GameManager.Start executes.
/// </summary>
[InitializeOnLoad]
public static class NeonPresentationQA
{
    [Serializable] private sealed class Command
    {
        public string action = "capture";
        public string stage = "After";
        public int width = 1080;
        public int height = 1920;
        public bool full = true;
        public int safeTop;
        public int safeBottom;
        public string previewState = "menu";
        public string clips;
    }

    private const string PendingKey = "NeonReflex.PresentationQA.Pending";
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string CommandPath = Path.Combine(ProjectRoot, ".utmp", "neon-ui-command.json");
    private static readonly string StatusPath = Path.Combine(ProjectRoot, ".utmp", "neon-ui-status.json");
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double nextPoll;
    private static bool running;
    private static readonly List<string> assertions = new List<string>();

    static NeonPresentationQA()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    [MenuItem("Neon Reflex/Presentation QA/Capture Before (isolated)")]
    public static void BeginBeforeCapture() => Begin(new Command { stage = "Before", full = false });

    [MenuItem("Neon Reflex/Presentation QA/Capture After (isolated)")]
    public static void BeginAfterCapture() => Begin(new Command { stage = "After", full = true });

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (File.Exists(CommandPath))
        {
            try
            {
                Command command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath));
                File.Delete(CommandPath);
                if (command.action == "stop") { EditorApplication.isPlaying = false; return; }
                if (command.action == "tests") { RunEditModeTests(); return; }
                Begin(command);
            }
            catch (Exception ex) { Status("error", ex.ToString()); }
        }
        if (EditorApplication.isPlaying && !running && !string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")))
            StartRunner();
    }

    private static void Begin(Command command)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Status("error", "Stop current play mode before starting another capture session.");
            return;
        }
        string profileDirectory = Path.Combine(ProjectRoot, ".utmp", "neon-ui-profile-" + Guid.NewGuid().ToString("N"));
        var envelope = SaveEnvelopeData.CreateDefault();
        envelope.profile.legacyMigrationComplete = true;
        new NeonSaveService(profileDirectory).Save(envelope);
        SessionState.SetString(NeonPresentationQAProfile.SessionKey, profileDirectory);
        SessionState.SetString(PendingKey, JsonUtility.ToJson(command));
        SetResolution(command.width, command.height);
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        Status("starting", command.stage + " — isolated profile " + profileDirectory);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Neon Reflex/Presentation QA/Run regression tests (no player preferences)")]
    public static void RunEditModeTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit play mode before running edit-mode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new RegressionCallbacks());
        api.RetrieveTestList(TestMode.EditMode, root =>
        {
            var names = new List<string>();
            CollectTests(root, names);
            Status("testing", "Running " + names.Count + " isolated EditMode cases. Legacy PlayerPrefs migration test deliberately excluded.");
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
        });
    }

    private static void CollectTests(ITestAdaptor test, List<string> names)
    {
        if (test.HasChildren)
        {
            foreach (ITestAdaptor child in test.Children) CollectTests(child, names);
            return;
        }
        if (!test.FullName.Contains("LegacyProgressMigrationRunsOnceAndPreservesHaptics") &&
            (test.FullName.StartsWith("NeonReflexRulesEditModeTests.", StringComparison.Ordinal) || test.FullName.StartsWith("CampaignDefinitionEditModeTests.", StringComparison.Ordinal) || test.FullName.StartsWith("NeonMotion", StringComparison.Ordinal) || test.FullName.StartsWith("NeonHudMotion", StringComparison.Ordinal) || test.FullName.StartsWith("GameSquareMotionEditModeTests.", StringComparison.Ordinal)))
            names.Add(test.FullName);
    }

    private sealed class RegressionCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string directory = Path.Combine(ProjectRoot, "Artifacts", "UI");
            Directory.CreateDirectory(directory);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(directory, "editmode-results.xml"));
            Status("tests-complete", "Passed " + result.PassCount + "; failed " + result.FailCount + "; skipped " + result.SkipCount + ".");
        }
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) StartRunner();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            running = false;
            SessionState.EraseString(NeonPresentationQAProfile.SessionKey);
            SessionState.EraseString(PendingKey);
        }
    }

    private static void StartRunner()
    {
        if (running) return;
        string pending = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(pending)) return;
        Command command = JsonUtility.FromJson<Command>(pending);
        running = true;
        var host = new GameObject("Neon Presentation QA — isolated editor session");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<NeonPresentationQARunner>().StartCoroutine(Guard(CaptureSuite(command)));
    }

    private static IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object current;
            try
            {
                var active = stack.Peek();
                if (!active.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                current = active.Current;
            }
            catch (Exception ex)
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                Status("error", ex.ToString());
                Debug.LogException(ex);
                SessionState.EraseString(PendingKey);
                EditorApplication.isPlaying = false;
                yield break;
            }
            if (current is IEnumerator nested && !(current is CustomYieldInstruction)) stack.Push(nested);
            else yield return current;
        }
    }

    private static IEnumerator CaptureSuite(Command command)
    {
        NeonSafeArea.PreviewSafeArea = command.safeTop > 0 || command.safeBottom > 0
            ? (Rect?)new Rect(0, command.safeBottom, command.width, command.height - command.safeTop - command.safeBottom)
            : null;
        yield return null;
        yield return null;
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.enabled) throw new InvalidOperationException("GameManager did not initialize.");
        if (!NeonPresentationQAProfile.IsActive) throw new InvalidOperationException("Profile isolation is inactive; QA aborted.");
        NeonSaveService service = Get<NeonSaveService>(gm, "saveService");
        if (service.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("GameManager did not use the isolated QA profile; QA aborted.");
        // The automated editor can begin play while Codex holds OS focus.
        // Model a foreground session before recording any animated menu.
        Invoke(gm,"OnApplicationFocus",true);
        assertions.Clear();
        Check(true, "All QA profile writes use " + service.DirectoryPath);
        string directory = Path.Combine(ProjectRoot, "Artifacts", "UI", command.stage, command.width + "x" + command.height + (command.safeTop > 0 || command.safeBottom > 0 ? "-safe-insets" : ""));
        Directory.CreateDirectory(directory);
        if (command.action == "run-report-capture")
        {
            yield return NeonRunReportQA.Run(gm, name => Capture(directory, name, gm), Check);
            File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
            Status("report-complete", directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "profile-debug-tests")
        {
            yield return NeonProfileDebugQA.Run(gm, name => Capture(directory, name, gm), Check);
            File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
            Status("profile-debug-complete", directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "gameplay-capture")
        {
            yield return NeonGameplayQA.Run(gm, name => Capture(directory, name, gm), Check);
            File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
            Status("gameplay-complete", directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "menu-capture")
        {
            yield return NeonMenuQA.Run(gm, name => Capture(directory, name, gm), Check);
            File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
            Status("menu-complete", directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "shop-capture")
        {
            yield return CaptureShopStates(gm, directory);
            File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
            Status("shop-complete", directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "grid-resize-tests" || command.action == "grid-resize-capture")
        {
            yield return NeonGridResizeQA.Run(gm, command.stage, command.action == "grid-resize-capture");
            Status("grid-resize-complete", "Grid resize checks saved under Artifacts/LevelTransition/" + command.stage);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "level-transition-tests" || command.action == "level-transition-capture")
        {
            yield return NeonLevelTransitionQA.Run(gm, command.stage, command.action == "level-transition-capture");
            Status("level-transition-complete", "Transition checks saved under Artifacts/LevelTransition/" + command.stage);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "motion" || command.action == "motion-tests")
        {
            if(command.action == "motion-tests") yield return NeonMotionQA.RunChecks(gm,command.stage);
            else yield return NeonMotionQA.Run(gm, command.stage,command.full,command.clips);
            Status("motion-complete", "Motion frames and timestamps saved under Artifacts/Motion/" + command.stage);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "flicker")
        {
            yield return NeonFlickerQA.Run(gm, command.stage);
            Status("flicker-complete", "Flicker captures and subpixel probe saved under Artifacts/Flicker/.");
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "performance" || command.action == "motion-performance")
        {
            if(command.action == "motion-performance")
            {
                directory=Path.Combine(ProjectRoot,"Artifacts","Motion","Performance",command.width+"x"+command.height);
                Directory.CreateDirectory(directory);
            }
            yield return MeasureEditorFrames(gm, directory);
            SessionState.EraseString(PendingKey);
            EditorApplication.isPlaying = false;
            yield break;
        }
        if (command.action == "diagnose")
        {
            gm.StartNewRun();
            Invoke(gm, "OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(0.65f);
            gm.enabled = false;
            var diagnostics = new List<string>();
            gm.OpenSettings();
            DumpModal(gm.settingsPanel, diagnostics, "settings immediate");
            Canvas.ForceUpdateCanvases();
            yield return null;
            DumpModal(gm.settingsPanel, diagnostics, "settings after force + null");
            yield return new WaitForEndOfFrame();
            DumpModal(gm.settingsPanel, diagnostics, "settings after rendered frame");
            gm.CloseSettings(); gm.ReturnToMainMenu();
            var ui = gm.GetComponent<RogueliteUIController>(); ui.ShowAbandonConfirmation();
            var dialog = Get<GameObject>(ui, "abandonDialog");
            DumpModal(dialog, diagnostics, "abandon immediate");
            Canvas.ForceUpdateCanvases(); yield return null;
            DumpModal(dialog, diagnostics, "abandon after force + null");
            yield return new WaitForEndOfFrame();
            DumpModal(dialog, diagnostics, "abandon after rendered frame");
            File.WriteAllLines(Path.Combine(ProjectRoot, ".utmp", "modal-diagnostics.txt"), diagnostics);
            Status("diagnosed", "See .utmp/modal-diagnostics.txt");
            SessionState.EraseString(PendingKey); EditorApplication.isPlaying = false; yield break;
        }
        yield return Capture(directory, "01-fresh-menu", gm);
        if (command.action == "preview")
        {
            if (command.previewState == "gameplay") gm.StartNewRun();
            Status("preview-ready", "Live " + command.previewState + " with isolated QA profile; use action stop to exit.");
            SessionState.EraseString(PendingKey);
            yield break;
        }
        gm.OpenUpgradeShop();
        yield return Capture(directory, "02-shop-unaffordable", gm);
        var shopScroll = Get<GameObject>(gm.GetComponent<RogueliteUIController>(), "shopPanel").GetComponentInChildren<ScrollRect>();
        if (shopScroll != null && command.full)
        {
            shopScroll.verticalNormalizedPosition = 0;
            yield return Capture(directory, "29-shop-scrolled-to-last-upgrade", gm);
            shopScroll.verticalNormalizedPosition = 1;
        }
        gm.ReturnToMainMenu();
        gm.OpenSettings();
        yield return Capture(directory, "03-settings", gm);
        gm.CloseSettings();
        yield return WaitForFlow(gm, "MainMenu", "Settings dismissal returns to the menu before a new run starts.");
        gm.StartNewRun();
        Invoke(gm, "OnApplicationFocus", true);
        yield return new WaitForSecondsRealtime(0.65f);
        gm.enabled = false;
        yield return WaitForFlow(gm, "Playing", "Initial gameplay is ready before the normal-state capture.");
        yield return Capture(directory, "04-normal-gameplay", gm);
        ActiveRunData run = Get<ActiveRunData>(gm, "sessionRun");
        Check(run.currentLevelIndex == 0 && run.currentHealth == gm.gameConfig.baseHealth, "New run starts Level 1 with configured base health.");
        if (command.full)
        {
            yield return ExpandedSuite(gm, directory);
        }
        File.WriteAllLines(Path.Combine(directory, "runtime-checks.txt"), assertions);
        Status("complete", directory + "\n" + string.Join("\n", assertions));
        SessionState.EraseString(PendingKey);
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator CaptureShopStates(GameManager gm, string directory)
    {
        var data = Get<SaveEnvelopeData>(gm, "saveData");
        var ui = gm.GetComponent<RogueliteUIController>();
        data.profile.coins = 1250;
        data.profile.maximumHealthTier = 5; data.profile.startingReserveTier = 3;
        data.profile.gridStabilizerTier = 3; data.profile.reverseResistanceTier = 1;
        gm.OpenUpgradeShop();
        yield return Capture(directory, "01-shop-affordable", gm);
        var panel = Get<GameObject>(ui, "shopPanel");
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>())
            Check(!text.isTextOverflowing, "Shop label fits: " + text.transform.parent.name + "/" + text.name);
        Button purchase = null;
        foreach (var button in panel.GetComponentsInChildren<Button>())
            if (button.name == "Purchase") { purchase = button; break; }
        long before = data.profile.coins;
        long cost = UpgradeCatalog.NextTier(gm.gameConfig, data.profile, UpgradeId.MaximumHealth).cost;
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, purchase.transform.position) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        bool correctTarget = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == purchase;
        Check(correctTarget, "The visible purchase control receives the native UI raycast through its decorative frame.");
        if (!correctTarget) throw new InvalidOperationException("Purchase control was blocked by presentation graphics.");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Check(data.profile.maximumHealthTier == 6 && data.profile.coins == before - cost,
            "Reference-style purchase commits one tier and the actual configured cost.");
        yield return Capture(directory, "02-shop-purchased", gm);
        data.profile.coins = 0; ui.RefreshShop(gm.gameConfig, data.profile, false);
        Check(!purchase.interactable, "Insufficient funds disables the purchase button.");
        yield return Capture(directory, "03-shop-unaffordable", gm);
        data.profile.coins = 1250;
        gm.ReturnToMainMenu(); gm.StartNewRun();
        yield return WaitForFlow(gm, "Playing", "Shop lock fixture starts an isolated run.");
        gm.ReturnToMainMenu(); gm.OpenUpgradeShop();
        Check(!purchase.interactable, "An active run disables purchases with an affordable wallet.");
        var runBefore = JsonUtility.ToJson(data.activeRun); before = data.profile.coins;
        purchase.onClick.Invoke();
        Check(data.profile.coins == before && JsonUtility.ToJson(data.activeRun) == runBefore,
            "Programmatic locked-button invocation cannot spend coins or change the saved run.");
        yield return Capture(directory, "04-shop-active-run", gm);
        var graphicState = new List<string>();
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>())
            graphicState.Add(graphic.transform.parent.name + "/" + graphic.name + " | cull " + graphic.canvasRenderer.cull +
                " | alpha " + graphic.canvasRenderer.GetAlpha() + " inherited " + graphic.canvasRenderer.GetInheritedAlpha() +
                " | color " + graphic.color + " | material " + graphic.materialForRendering.name);
        File.WriteAllLines(Path.Combine(directory, "shop-reopen-graphics.txt"), graphicState);
        data.activeRun = null;
        foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            UpgradeCatalog.SetTier(data.profile, id, UpgradeCatalog.Get(gm.gameConfig, id).tiers.Count);
        ui.RefreshShop(gm.gameConfig, data.profile, false);
        Check(!purchase.interactable, "A maximum-tier card disables purchasing.");
        yield return Capture(directory, "05-shop-maxed", gm);
        var scroll = panel.GetComponentInChildren<ScrollRect>();
        scroll.verticalNormalizedPosition = 0;
        yield return Capture(directory, "06-shop-scroll-end", gm);
    }

    private static IEnumerator ExpandedSuite(GameManager gm, string directory)
    {
        var run = Get<ActiveRunData>(gm, "sessionRun");
        var squares = Get<List<GameSquare>>(gm, "instantiatedSquares");
        foreach (string transition in new[] { "LevelIntro", "ReverseEntrance", "ReverseExit" })
        {
            gm.OpenSettings();
            object expected = Enum.Parse(Get<object>(gm, "state").GetType(), transition);
            Set(gm, "stateBeforeSettings", expected);
            float boundaryTimer = run.levelState.normalTimeRemaining;
            Invoke(gm, "CompletePresentation", expected, Get<int>(gm, "presentationToken"));
            Check(Get<object>(gm, "state").ToString() == "Settings" && run.levelState.normalTimeRemaining == boundaryTimer,
                transition + " final-frame completion preserves Settings and frozen timer.");
            // The exit is now asynchronous. Exercise its actual window instead
            // of assuming CloseSettings changes the authoritative flow inline.
            yield return WaitForScreen(gm.settingsPanel, "Settings entrance reaches its visible state.");
            gm.CloseSettings();
            NeonScreenMotion settingsMotion = gm.settingsPanel.GetComponent<NeonScreenMotion>();
            Check(!settingsMotion.IsAnimating || (Get<object>(gm, "state").ToString() == "Settings" &&
                  run.levelState.normalTimeRemaining == boundaryTimer),
                transition + " Settings dismissal keeps gameplay paused while its modal is leaving.");
            yield return WaitForFlow(gm, "Playing", transition + " asynchronous Settings dismissal finishes.");
            Check(Get<object>(gm, "state").ToString() == "Playing", transition + " final-frame pause resumes to playable state.");
        }
        var outsideHits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width - 10, 10) }, outsideHits);
        Check(!outsideHits.Exists(hit => hit.gameObject.GetComponentInParent<GameSquare>() != null), "Outside-grid screen point has no square hit target.");
        int oldHealth = run.currentHealth;
        int oldObjective = run.levelState.objectiveProgress;
        int oldLarge = run.levelState.largeIndex;
        Check(Mathf.Approximately(squares[run.levelState.smallIndex].outlineImage.transform.localScale.x, gm.campaign.GetLevel(0).smallScale) &&
              Mathf.Approximately(squares[run.levelState.mediumIndex].outlineImage.transform.localScale.x, gm.campaign.GetLevel(0).mediumScale) &&
              Mathf.Approximately(squares[run.levelState.largeIndex].outlineImage.transform.localScale.x, gm.campaign.GetLevel(0).fullScale),
            "Small/Medium/Large render their exact configured discrete outline sizes.");
        Vector3 originalTargetScale = squares[oldLarge].outlineImage.transform.localScale;
        squares[oldLarge].PlayAttentionPulse();
        Check(squares[oldLarge].outlineImage.transform.localScale == originalTargetScale, "Attention feedback does not distort authoritative target size.");
        bool originalReducedEffects = NeonTheme.ReducedEffects;
        NeonTheme.ReducedEffects = true;
        yield return Capture(directory, "28-gameplay-reduced-effects", gm);
        Check(squares[oldLarge].outlineImage.transform.localScale == originalTargetScale && run.levelState.largeIndex == oldLarge,
            "Reduced effects leaves target sizes and authoritative target selection unchanged.");
        NeonTheme.ReducedEffects = originalReducedEffects;
        DispatchSquarePointer(squares[run.levelState.mediumIndex]);
        Check(run.currentHealth == oldHealth - 1 && run.levelState.objectiveProgress == oldObjective && run.levelState.largeIndex == oldLarge,
            "Wrong square removes exactly one health without advancing targets or objective.");
        DispatchSquarePointer(squares[run.levelState.largeIndex]);
        Check(run.levelState.objectiveProgress == oldObjective + 1, "Correct largest square advances objective once.");
        gm.OpenSettings();
        var modalHits = new List<RaycastResult>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, squares[oldLarge].transform.position) };
        EventSystem.current.RaycastAll(pointer, modalHits);
        Check(modalHits.Count == 0 || modalHits[0].gameObject.transform.IsChildOf(gm.settingsPanel.transform),
            "Settings immediately suppresses underlying raycasts before its first rendered frame.");
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        modalHits.Clear();
        EventSystem.current.RaycastAll(pointer, modalHits);
        assertions.Add("OBSERVED: Settings top raycast = " + (modalHits.Count > 0 ? modalHits[0].gameObject.name : "none"));
        Check(modalHits.Count > 0 && modalHits[0].gameObject.transform.IsChildOf(gm.settingsPanel.transform),
            "Settings modal raycast blocks the grid underneath.");
        int pausedHealth = run.currentHealth;
        float pausedTime = run.levelState.normalTimeRemaining;
        gm.enabled = true;
        Invoke(gm, "OnSquareClicked", squares[run.levelState.mediumIndex]);
        yield return new WaitForSecondsRealtime(0.2f);
        gm.enabled = false;
        Check(run.currentHealth == pausedHealth && run.levelState.normalTimeRemaining == pausedTime, "Settings pause rejects square input and freezes normal timer.");
        yield return Capture(directory, "05-settings-over-gameplay", gm);
        gm.CloseSettings();
        yield return WaitForFlow(gm, "Playing", "Damage-time Settings dismissal restores gameplay before further state checks.");
        run.currentHealth = 1;
        Invoke(gm, "UpdateGameplayUI");
        yield return Capture(directory, "06-low-health", gm);
        run.levelState.normalTimeRemaining = 0f;
        run.levelState.reserveActive = true;
        run.currentReserveSeconds = 7.4f;
        Invoke(gm, "UpdateGameplayUI");
        yield return Capture(directory, "07-reserve-active", gm);
        gm.ReturnToMainMenu();
        yield return Capture(directory, "08-active-run-menu", gm);
        gm.OpenUpgradeShop();
        Check(!(bool)Invoke(gm, "TryPurchaseUpgrade", UpgradeId.MaximumHealth), "Upgrade purchases are rejected while a real run is active.");
        yield return Capture(directory, "09-shop-locked", gm);
        gm.ReturnToMainMenu();
        var ui = gm.GetComponent<RogueliteUIController>();
        ui.ShowAbandonConfirmation();
        modalHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, modalHits);
        Check(modalHits.Count == 0 || modalHits[0].gameObject.transform.IsChildOf(Get<GameObject>(ui, "abandonDialog").transform),
            "Abandon confirmation immediately suppresses underlying raycasts before its first rendered frame.");
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        modalHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, modalHits);
        assertions.Add("OBSERVED: Abandon top raycast = " + (modalHits.Count > 0 ? modalHits[0].gameObject.name : "none"));
        Check(modalHits.Count > 0 && modalHits[0].gameObject.transform.IsChildOf(Get<GameObject>(ui, "abandonDialog").transform),
            "Abandon confirmation raycast blocks underlying controls.");
        yield return Capture(directory, "10-abandon-confirmation", gm);
        ui.HideAbandonConfirmation();
        gm.ContinueRun();
        run = Get<ActiveRunData>(gm, "sessionRun");
        Check(run.currentHealth == 1 && Math.Abs(run.currentReserveSeconds - 7.4f) < 0.01f && run.levelState.reserveActive, "Return home and Continue retain actual health and reserve state.");
        var restored = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig);
        Check(restored.activeRun != null && restored.activeRun.runId == run.runId && restored.activeRun.currentHealth == run.currentHealth &&
              restored.activeRun.levelState.largeIndex == run.levelState.largeIndex && restored.activeRun.levelState.rotationAngle == run.levelState.rotationAngle,
            "Isolated save reload preserves active run identity, resources, targets, and transforms.");
        yield return WaitForFlow(gm, "Playing", "Continue reaches visible gameplay before the application-pause check.");
        Invoke(gm, "OnApplicationPause", true);
        gm.enabled = true;
        float pausedReserve = run.currentReserveSeconds;
        yield return new WaitForSecondsRealtime(0.2f);
        gm.enabled = false;
        Check(run.currentReserveSeconds == pausedReserve, "Application pause freezes reserve timer.");
        Invoke(gm, "OnApplicationPause", false);
        Invoke(gm, "ConfirmAbandonRun");
        yield return Capture(directory, "11-abandoned-run", gm);
        gm.ReturnToMainMenu();
        var data = Get<SaveEnvelopeData>(gm, "saveData");
        data.profile.coins = 100000;
        gm.OpenUpgradeShop();
        yield return Capture(directory, "12-shop-affordable", gm);
        long beforePurchase = data.profile.coins;
        int beforeTier = data.profile.maximumHealthTier;
        long exactCost = UpgradeCatalog.NextTier(gm.gameConfig, data.profile, UpgradeId.MaximumHealth).cost;
        foreach (Button purchase in Get<GameObject>(gm.GetComponent<RogueliteUIController>(), "shopPanel").GetComponentsInChildren<Button>())
        {
            if (purchase.name != "Purchase") continue;
            purchase.onClick.Invoke();
            break;
        }
        Check(data.profile.maximumHealthTier == beforeTier + 1 && data.profile.coins == beforePurchase - exactCost,
            "One purchase-button click upgrades exactly one tier and debits the authoritative cost once.");
        yield return Capture(directory, "13-shop-purchased", gm);
        for (int i = 0; i < 30; i++)
            if (!(bool)Invoke(gm, "TryPurchaseUpgrade", UpgradeId.MaximumHealth)) break;
        yield return Capture(directory, "14-shop-maximum-tier", gm);
        gm.ReturnToMainMenu();
        gm.StartNewRun();
        yield return new WaitForSecondsRealtime(0.6f);
        yield return WaitForFlow(gm, "Playing", "Upgraded run entrance is complete before level-completion checks.");
        run = Get<ActiveRunData>(gm, "sessionRun");
        run.currentHealth = 17;
        run.currentReserveSeconds = 11.3f;
        Invoke(gm, "UpdateGameplayUI");
        yield return Capture(directory, "15-health-20-maximum", gm);
        run.levelState.objectiveProgress = gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks;
        Invoke(gm, "CompleteCurrentLevel");
        long earned = run.pendingCoins;
        Invoke(gm, "CompleteCurrentLevel");
        Check(run.pendingCoins == earned && run.currentHealth == 17 && Math.Abs(run.currentReserveSeconds - 11.3f) < 0.01f,
            "Level completion rewards once and carries health/reserve unchanged.");
        run = Get<ActiveRunData>(gm, "sessionRun");
        Check(Math.Abs(run.levelState.normalTimeRemaining - gm.campaign.GetLevel(run.currentLevelIndex).timeLimit) < 0.01f,
            "Next level receives fresh normal time; no reserve regeneration.");
        yield return Capture(directory, "16-level-transition", gm);
        yield return WaitForFlow(gm, "Playing", "The next level becomes playable automatically after its shared transition.");
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        yield return Capture(directory, "17-health-failure", gm);
        long banked = data.profile.coins;
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        Check(data.profile.coins == banked, "Repeated terminal invocation does not duplicate banking.");
        restored = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig);
        Check(restored.activeRun == null && restored.profile.coins == banked, "Banked wallet survives reload and completed run remains cleared.");
        gm.ReturnToMainMenu();
        gm.StartNewRun();
        yield return new WaitForSecondsRealtime(0.55f);
        yield return WaitForFlow(gm, "Playing", "Reserve-failure fixture enters gameplay before its terminal action.");
        Invoke(gm, "FailRun", "RESERVE DEPLETED");
        yield return Capture(directory, "18-reserve-failure", gm);
        gm.ReturnToMainMenu();
        gm.StartNewRun();
        yield return new WaitForSecondsRealtime(0.55f);
        yield return WaitForFlow(gm, "Playing", "Campaign-completion fixture starts from ready gameplay.");
        run = Get<ActiveRunData>(gm, "sessionRun");
        run.currentLevelIndex = gm.campaign.LevelCount - 1;
        run.currentLevelId = gm.campaign.GetLevel(run.currentLevelIndex).stableId;
        run.levelsCompleted = gm.campaign.LevelCount - 1;
        run.pendingCoins = 0;
        for (int i = 0; i < run.currentLevelIndex; i++) run.pendingCoins += gm.campaign.GetLevel(i).completionCoinReward;
        Invoke(gm, "InitializeCurrentLevelState");
        Invoke(gm, "EnterCurrentLevel", false, false);
        yield return WaitForFlow(gm, "Playing", "Campaign final level is visible and playable before completion.");
        run.levelState.objectiveProgress = gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks;
        Invoke(gm, "CompleteCurrentLevel");
        Check(data.activeRun == null, "Campaign completion clears isolated active run after banking.");
        yield return Capture(directory, "19-campaign-complete", gm);
        gm.ReturnToMainMenu();
        int denseIndex = 0;
        for (int i = 0; i < gm.campaign.LevelCount; i++)
        {
            LevelData level = gm.campaign.GetLevel(i);
            if (level.gridSize >= gm.campaign.GetLevel(denseIndex).gridSize && level.movementEnabled && level.scaleEnabled && level.rotateSpeed != 0) denseIndex = i;
        }
        string realData = JsonUtility.ToJson(data);
        gm.StartDebugLevel(denseIndex);
        yield return new WaitForSecondsRealtime(0.55f);
        yield return WaitForFlow(gm, "Playing", "Combined-modifier sandbox reaches ready gameplay.");
        run = Get<ActiveRunData>(gm, "sessionRun");
        run.levelState.rotationAngle = 43f;
        run.levelState.scalePhase = 0f;
        run.levelState.movementPosition = new Vector2(9999, 9999);
        Invoke(gm, "ApplyGridMotionState", false, 0f);
        Invoke(gm, "UpdateGameplayUI");
        yield return Capture(directory, "20-combined-modifiers-minimum-scale", gm);
        CheckBounds(gm);
        run.levelState.scalePhase = 1f;
        run.levelState.reverseActive = true;
        run.levelState.reverseCorrectTapsRemaining = 4;
        Invoke(gm, "ApplyGridMotionState", false, 0f);
        gm.GetComponent<GameplayFeedbackController>().RestoreReverseActive();
        Invoke(gm, "UpdateGameplayUI");
        yield return Capture(directory, "21-reverse-combined-maximum-scale", gm);
        CheckBounds(gm);
        int originalWidth = Screen.width, originalHeight = Screen.height;
        Rect? originalSafeArea = NeonSafeArea.PreviewSafeArea;
        Vector2 oldBoundsSize = Get<RectTransform>(gm, "boundsRoot").rect.size;
        int originalSquareId = Get<List<GameSquare>>(gm, "instantiatedSquares")[0].GetInstanceID();
        float originalAngle = run.levelState.rotationAngle, originalPhase = run.levelState.scalePhase;
        int originalSmall = run.levelState.smallIndex;
        gm.OpenSettings();
        gm.enabled = true;
        NeonSafeArea.PreviewSafeArea = null;
        SetResolution(originalWidth, originalHeight + 360);
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);
        gm.enabled = false;
        Check(Get<RectTransform>(gm, "boundsRoot").rect.size != oldBoundsSize &&
              Get<List<GameSquare>>(gm, "instantiatedSquares")[0].GetInstanceID() == originalSquareId &&
              run.levelState.rotationAngle == originalAngle && run.levelState.scalePhase == originalPhase && run.levelState.smallIndex == originalSmall,
            "Live viewport resize refits existing grid without rebuilding cells or resetting target/motion phases.");
        CheckBounds(gm);
        SetResolution(originalWidth, originalHeight);
        NeonSafeArea.PreviewSafeArea = originalSafeArea;
        gm.enabled = true;
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);
        gm.enabled = false;
        gm.CloseSettings();
        yield return WaitForFlow(gm, "Playing", "Viewport-resize Settings dismissal completes before Reverse begins.");
        run.levelState.reverseActive = false;
        run.levelState.reverseCooldownRemaining = 0;
        gm.GetComponent<GameplayFeedbackController>().ResetImmediate();
        uint triggerSeed = 1;
        for (; triggerSeed < 100000; triggerSeed++)
        {
            var candidate = new DeterministicRandom(triggerSeed);
            if (candidate.NextFloat01() <= gm.gameConfig.reverseTriggerChancePerCorrectClick) break;
        }
        run.randomState = NeonSaveService.EncodeRandomState(triggerSeed);
        Invoke(gm, "TryBeginReverse");
        Check(Get<object>(gm, "state").ToString() == "ReverseEntrance", "Actual Reverse trigger enters its authorized presentation window.");
        gm.OpenSettings();
        yield return new WaitForSecondsRealtime(0.2f);
        Check(Get<object>(gm, "state").ToString() == "Settings", "Settings preserves the paused Reverse transition.");
        gm.CloseSettings();
        yield return WaitForFlow(gm, "ReverseEntrance", "Settings dismissal restores the still-active Reverse entrance.");
        yield return Capture(directory, "24-reverse-entrance", gm);
        yield return new WaitForSecondsRealtime(0.85f);
        Check(Get<object>(gm, "state").ToString() == "Playing" && run.levelState.reverseActive,
            "Reverse entrance resumes gameplay after its original transition window.");
        run.levelState.reverseCorrectTapsRemaining = 1;
        squares = Get<List<GameSquare>>(gm, "instantiatedSquares");
        int reverseObjective = run.levelState.objectiveProgress;
        Invoke(gm, "OnSquareClicked", squares[run.levelState.smallIndex]);
        Check(run.levelState.objectiveProgress == reverseObjective + 1 && !run.levelState.reverseActive,
            "Reverse smallest target advances sequence and returns the normal rule after final required tap.");
        yield return Capture(directory, "25-reverse-exit", gm);
        yield return new WaitForSecondsRealtime(0.65f);
        Check(Get<object>(gm, "state").ToString() == "Playing", "Reverse exit returns gameplay after its original transition window.");
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        yield return Capture(directory, "27-debug-failure", gm);
        gm.ReturnToMainMenu();
        Check(JsonUtility.ToJson(data) == realData, "Debug sandbox session leaves real profile and active-run data unchanged.");
        data.profile.coins = long.MaxValue;
        gm.OpenUpgradeShop();
        yield return Capture(directory, "22-shop-long-wallet-value", gm);
        gm.ReturnToMainMenu();
        yield return Capture(directory, "23-menu-long-wallet-value", gm);
        foreach (Button button in gm.mainMenuPanel.GetComponentsInChildren<Button>(true))
        {
            if (button.name != "DebugLevelSelectButton") continue;
            button.onClick.Invoke();
            yield return Capture(directory, "26-practice-selector", gm);
            break;
        }
    }

    private static IEnumerator MeasureEditorFrames(GameManager gm, string directory)
    {
        int levelIndex = gm.campaign.LevelCount - 1;
        gm.StartDebugLevel(levelIndex);
        Invoke(gm, "OnApplicationFocus", true);
        gm.enabled = true;
        yield return new WaitForSecondsRealtime(2f);
        var run = Get<ActiveRunData>(gm, "sessionRun");
        if (Get<object>(gm, "state").ToString() != "Playing")
            throw new InvalidOperationException("Performance sample requires actively playing simulation.");
        float initialTime = run.levelState.normalTimeRemaining;
        float initialAngle = run.levelState.rotationAngle;
        var samples = new List<float>(8192);
        int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
        Status("profiling", "Sampling 10 seconds of active combined-modifier Level " + (levelIndex + 1) + " in Unity Editor.");
        double started = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - started < 10)
        {
            yield return null;
            samples.Add(Time.unscaledDeltaTime * 1000f);
        }
        double elapsed = Time.realtimeSinceStartupAsDouble - started;
        int gc0 = GC.CollectionCount(0) - gen0, gc1 = GC.CollectionCount(1) - gen1, gc2 = GC.CollectionCount(2) - gen2;
        float timerDelta = initialTime - run.levelState.normalTimeRemaining;
        float finalAngle = run.levelState.rotationAngle;
        string finalState = Get<object>(gm, "state").ToString();
        gm.enabled = false;
        var raw = new List<string>(samples.Count + 1) { "frame,unscaled_frame_ms" };
        double sum = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            sum += samples[i];
            raw.Add(i + "," + samples[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }
        samples.Sort();
        float median = samples[samples.Count / 2];
        float p95 = samples[Mathf.Clamp(Mathf.CeilToInt(samples.Count * 0.95f) - 1, 0, samples.Count - 1)];
        var report = new List<string>
        {
            "Unity Editor timing sample; not a device benchmark or before/after performance comparison.",
            "UTC: " + DateTime.UtcNow.ToString("O"),
            "Unity: " + Application.unityVersion + "; editor OS: " + SystemInfo.operatingSystem,
            "CPU: " + SystemInfo.processorType + "; GPU: " + SystemInfo.graphicsDeviceName,
            "GameView: " + Screen.width + "x" + Screen.height + "; targetFrameRate: " + Application.targetFrameRate + "; vSyncCount: " + QualitySettings.vSyncCount,
            "Isolated debug Level " + (levelIndex + 1) + "; rotation, scale and movement enabled; no pointer input during sample.",
            "Warmup: 2 seconds; requested sample: 10 seconds; elapsed: " + elapsed.ToString("0.000") + " seconds; frames: " + samples.Count,
            "Frame time milliseconds: median " + median.ToString("0.000") + "; p95 " + p95.ToString("0.000") + "; mean " + (sum / samples.Count).ToString("0.000") + "; max " + samples[samples.Count - 1].ToString("0.000"),
            "Observed frames / elapsed seconds: " + (samples.Count / elapsed).ToString("0.00"),
            "Activity verification: normal timer decreased " + timerDelta.ToString("0.000") + " seconds; rotation " + initialAngle.ToString("0.00") + " to " + finalAngle.ToString("0.00") + "; final flow state " + finalState,
            "Process-wide GC collection deltas (generation 0/1/2): " + gc0 + "/" + gc1 + "/" + gc2 + ". These are not UI allocation measurements.",
            "Time.unscaledDeltaTime includes the running Editor and harness; no screenshots or disk I/O occur inside the measured loop. No GPU timing, profiler allocation attribution, mobile hardware, or baseline was measured."
        };
        File.WriteAllLines(Path.Combine(directory, "editor-frame-times.csv"), raw);
        File.WriteAllLines(Path.Combine(directory, "editor-performance.txt"), report);
        Status("performance-complete", string.Join("\n", report));
    }

    private static IEnumerator WaitForFlow(GameManager gm, string expected, string description)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (Get<object>(gm, "state").ToString() != expected && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(Get<object>(gm, "state").ToString() == expected,
            description + " (Expected " + expected + "; observed " + Get<object>(gm, "state") + ".)");
    }

    private static IEnumerator WaitForScreen(GameObject panel, string description)
    {
        NeonScreenMotion motion = panel.GetComponent<NeonScreenMotion>();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (motion != null && !motion.IsReady && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(motion != null && motion.IsReady, description);
    }

    private static IEnumerator WaitForCapturePresentation(string name)
    {
        // Panel fades and decorative result rows have different durations.
        // Observe their owners, while leaving the intentionally transient
        // Reverse announcement and the target feedback on their own timelines.
        NeonScreenMotion[] panels = UnityEngine.Object.FindObjectsByType<NeonScreenMotion>(FindObjectsSortMode.None);
        NeonResultMotion[] results = UnityEngine.Object.FindObjectsByType<NeonResultMotion>(FindObjectsSortMode.None);
        float deadline = Time.realtimeSinceStartup + 3f;
        bool pending;
        do
        {
            pending = false;
            foreach (NeonScreenMotion panel in panels)
                pending |= panel != null && panel.gameObject.activeInHierarchy && panel.IsAnimating;
            foreach (NeonResultMotion result in results)
                pending |= result != null && result.gameObject.activeInHierarchy && Get<bool>(result, "playing");
            if (!pending) break;
            yield return null;
        } while (Time.realtimeSinceStartup < deadline);
        Check(!pending, name + " capture waits for screen and result presentation readiness.");
    }

    private static IEnumerator Capture(string directory, string name, GameManager gm)
    {
        Canvas.ForceUpdateCanvases();
        // Capture settled panel entrances; feedback has its own dedicated transient checks.
        yield return new WaitForSecondsRealtime(0.25f);
        yield return WaitForCapturePresentation(name);
        // Unity may display cyan placeholder glyph quads while a new masked TMP
        // shader variant compiles. Wait for the editor compiler before evidence.
        PropertyInfo compiling = typeof(ShaderUtil).GetProperty("anythingCompiling", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        float shaderDeadline = Time.realtimeSinceStartup + 15;
        while (compiling != null && (bool)compiling.GetValue(null) && Time.realtimeSinceStartup < shaderDeadline)
            yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), capture.EncodeToPNG());
        UnityEngine.Object.Destroy(capture);
        var lines = new List<string>();
        foreach (TMP_Text label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            if (label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                lines.Add(label.name + " | " + label.text.Replace("\n", " / ") + " | " + label.rectTransform.rect.size + " | font " + label.fontSize + " | overflow " + label.isTextOverflowing);
        foreach (Selectable control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (!control.gameObject.activeInHierarchy) continue;
            RectTransform rect = control.transform as RectTransform;
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            lines.Add("CONTROL " + control.name + " | " + RectTransformUtility.WorldToScreenPoint(null, corners[0]) + " to " + RectTransformUtility.WorldToScreenPoint(null, corners[2]) + " | interactable " + control.interactable);
        }
        if (gm.gameplayPanel.activeInHierarchy)
        {
            var gridSquares = Get<List<GameSquare>>(gm, "instantiatedSquares");
            if (gridSquares.Count > 0)
            {
                Vector3[] corners = new Vector3[4];
                ((RectTransform)gridSquares[0].transform).GetWorldCorners(corners);
                float sidePixels = Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, corners[0]), RectTransformUtility.WorldToScreenPoint(null, corners[1]));
                lines.Add("GRID " + gridSquares.Count + " cells | measured cell edge " + sidePixels.ToString("0.00") + " pixels | configured touch minimum " + gm.gameConfig.minimumTouchTargetPixels);
            }
        }
        File.WriteAllLines(Path.Combine(directory, name + ".txt"), lines);
        Status("capturing", Path.Combine(directory, name + ".png"));
    }

    private static void CheckBounds(GameManager gm)
    {
        RectTransform bounds = Get<RectTransform>(gm, "boundsRoot");
        RectTransform content = Get<RectTransform>(gm, "gridContentRoot");
        var corners = new Vector3[4];
        content.GetWorldCorners(corners);
        bool within = true;
        foreach (Vector3 corner in corners)
        {
            Vector3 p = bounds.InverseTransformPoint(corner);
            within &= p.x >= bounds.rect.xMin - 1 && p.x <= bounds.rect.xMax + 1 && p.y >= bounds.rect.yMin - 1 && p.y <= bounds.rect.yMax + 1;
        }
        Check(within, "Transformed combined-modifier grid corners remain inside laid-out gameplay bounds.");
    }

    private static void Check(bool condition, string description)
    {
        assertions.Add((condition ? "PASS: " : "FAIL: ") + description);
        if (!condition) Debug.LogError("Presentation QA: " + description);
    }

    private static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Hidden).GetValue(instance);
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Hidden).SetValue(instance, value);
    private static object Invoke(object instance, string method, params object[] arguments) => instance.GetType().GetMethod(method, Hidden).Invoke(instance, arguments);

    private static void DispatchSquarePointer(GameSquare square)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, square.transform.position), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<GameSquare>() == square,
            "Rendered square center raycast resolves to its authoritative GameSquare.");
        if (hits.Count > 0) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
    }

    private static void DumpModal(GameObject panel, List<string> lines, string phase)
    {
        Vector2 p = new Vector2(Screen.width * .5f, Screen.height * .5f);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = p }, hits);
        lines.Add("PHASE " + phase + " point " + p);
        foreach (RaycastResult hit in hits)
            lines.Add("HIT " + hit.gameObject.name + " depth " + hit.depth + " sorting " + hit.sortingOrder + " distance " + hit.distance);
        var graphic = panel.GetComponent<Graphic>();
        var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
        lines.Add("PANEL " + panel.name + " active " + panel.activeInHierarchy + " layer " + panel.layer + " scale " + panel.transform.lossyScale + " rotation " + panel.transform.rotation);
        lines.Add("GRAPHIC enabled " + graphic.enabled + " depth " + graphic.depth + " cull " + graphic.canvasRenderer.cull + " raycastTarget " + graphic.raycastTarget + " color " + graphic.color + " directRaycast " + graphic.Raycast(p, null));
        lines.Add("RECT " + graphic.rectTransform.rect + " corners " + string.Join(";", Array.ConvertAll(corners, c => c.ToString())) + " contains " + RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,p,null));
        lines.Add("CANVAS " + graphic.canvas.name + " render " + graphic.canvas.renderMode + " overrideSorting " + graphic.canvas.overrideSorting + " order " + graphic.canvas.sortingOrder);
        foreach (Transform ancestor in panel.GetComponentsInParent<Transform>())
            foreach (CanvasGroup group in ancestor.GetComponents<CanvasGroup>())
                lines.Add("GROUP " + ancestor.name + " alpha " + group.alpha + " blocks " + group.blocksRaycasts + " interactable " + group.interactable + " ignoreParents " + group.ignoreParentGroups);
    }

    private static void SetResolution(int width, int height)
    {
        Assembly assembly = typeof(Editor).Assembly;
        Type viewType = assembly.GetType("UnityEditor.GameView");
        EditorWindow window = EditorWindow.GetWindow(viewType);
        Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
        Type sizeEnum = assembly.GetType("UnityEditor.GameViewSizeType");
        object size = Activator.CreateInstance(sizeType, Enum.Parse(sizeEnum, "FixedResolution"), width, height, "Neon QA " + width + "x" + height);
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, index);
        window.Show();
        window.Focus();
    }

    private static void Status(string state, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatusPath));
        File.WriteAllText(StatusPath, JsonUtility.ToJson(new StatusData { state = state, message = message, utc = DateTime.UtcNow.ToString("O") }, true));
    }
    [Serializable] private sealed class StatusData { public string state; public string message; public string utc; }
}
