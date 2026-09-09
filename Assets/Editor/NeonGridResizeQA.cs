using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Deterministic QA for retained-cell grid resizing transitions.</summary>
public static class NeonGridResizeQA
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string checkPath;
    private static int passed;

    public static IEnumerator Run(GameManager gm, string stage, bool capture)
    {
        NeonSaveService saves = Get<NeonSaveService>(gm, "saveService");
        if (!NeonPresentationQAProfile.IsActive || saves.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Grid resize QA requires isolated storage.");

        string root = Path.Combine(Application.dataPath, "..", "Artifacts", "LevelTransition", stage,
            Screen.width + "x" + Screen.height, "GridResize");
        Directory.CreateDirectory(root);
        checkPath = Path.Combine(root, "checks.txt");
        File.WriteAllText(checkPath, string.Empty);
        passed = 0;

        CampaignDefinition original = gm.campaign;
        CampaignDefinition fixture = UnityEngine.Object.Instantiate(original);
        fixture.levels = new List<LevelData>
        {
            UnityEngine.Object.Instantiate(original.GetLevel(0)),
            UnityEngine.Object.Instantiate(original.GetLevel(1))
        };
        float oldDuration = NeonMotion.T.levelTransitionDuration;
        bool oldReduced = NeonTheme.ReducedEffects;
        gm.campaign = fixture;
        try
        {
            NeonMotion.T.levelTransitionDuration = 1f;
            NeonTheme.ReducedEffects = false;
            foreach (int oldSize in new[] { 2, 3, 4, 5 })
            {
                foreach (int newSize in new[] { 2, 3, 4, 5 })
                {
                    if (oldSize == newSize)
                        continue;
                    bool pairCapture = capture && ((oldSize == 3 && newSize == 4) ||
                        (oldSize == 4 && newSize == 3) || (oldSize == 2 && newSize == 5) ||
                        (oldSize == 5 && newSize == 2));
                    yield return RunResize(gm, fixture, oldSize, newSize, root, pairCapture, false);
                }
            }

            NeonTheme.ReducedEffects = true;
            yield return RunResize(gm, fixture, 3, 4, root, capture, true);
            NeonTheme.ReducedEffects = false;
            NeonMotion.T.levelTransitionDuration = 0f;
            yield return RunResize(gm, fixture, 4, 3, root, capture, false);
            NeonMotion.T.levelTransitionDuration = 1f;
            yield return CheckShrinkLifecycle(gm, fixture);
            File.AppendAllText(checkPath, "COMPLETE: " + passed + " passed; 0 failed.\n");
        }
        finally
        {
            NeonTheme.ReducedEffects = oldReduced;
            NeonMotion.T.levelTransitionDuration = oldDuration;
            gm.enabled = true;
            gm.ReturnToMainMenu();
            gm.campaign = original;
            foreach (LevelData level in fixture.levels)
                UnityEngine.Object.Destroy(level);
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static IEnumerator CheckShrinkLifecycle(GameManager gm, CampaignDefinition fixture)
    {
        Configure(fixture.GetLevel(0), 5, 37, true, true);
        Configure(fixture.GetLevel(1), 2, -65, true, true);
        yield return Start(gm);
        var run = Get<ActiveRunData>(gm, "sessionRun");
        run.levelState.objectiveProgress = fixture.GetLevel(0).requiredCorrectClicks - 1;
        Tap(gm, true);
        gm.enabled = false;
        Invoke(gm, "AdvanceLevelTransition", .2f);
        string prepared = JsonUtility.ToJson(run);
        var cells = Get<List<GameSquare>>(gm, "instantiatedSquares");
        var frozen = SharedCorners(cells, 2, 2);
        gm.OpenSettings(); gm.enabled = true;
        yield return new WaitForSecondsRealtime(.2f);
        Check(Mathf.Approximately(Get<float>(gm, "levelTransitionElapsed"), .2f) && JsonUtility.ToJson(run) == prepared,
            "Settings freezes a shrinking transition and its prepared checkpoint.");
        CheckCorners(frozen, SharedCorners(cells, 2, 2), .1f, "Settings freezes surviving cell geometry");
        gm.CloseSettings();
        float deadline = Time.realtimeSinceStartup + 4;
        while (Flow(gm) == "Settings" && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Flow(gm) == "LevelTransition", "Settings returns to shrinking transition.");
        Invoke(gm, "OnApplicationPause", true);
        float pausedTime = Get<float>(gm, "levelTransitionElapsed");
        yield return new WaitForSecondsRealtime(.2f);
        Check(Get<float>(gm, "levelTransitionElapsed") == pausedTime && JsonUtility.ToJson(run) == prepared,
            "Backgrounding freezes removal and preserves the checkpoint.");
        Invoke(gm, "OnApplicationPause", false);
        gm.ReturnToMainMenu();
        Check(RetiredCount(gm) == 0 && Get<object>(gm, "transitionLayout") == null &&
            Get<RectTransform>(gm, "gridContentRoot").GetComponent<UnityEngine.UI.GridLayoutGroup>().enabled,
            "Home during shrink retires outside cells and restores the canonical layout.");
        var disk = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig).activeRun;
        Check(JsonUtility.ToJson(disk) == prepared, "Home during shrink persists the exact prepared level.");
        gm.ContinueRun();
        Check(JsonUtility.ToJson(Get<ActiveRunData>(gm, "sessionRun")) == prepared,
            "Continue after interrupted shrink does not replay rewards or initialize a new sequence.");

        yield return Start(gm);
        run = Get<ActiveRunData>(gm, "sessionRun");
        run.levelState.objectiveProgress = fixture.GetLevel(0).requiredCorrectClicks - 1;
        Tap(gm, true); gm.enabled = false;
        Invoke(gm, "AdvanceLevelTransition", .5f);
        prepared = JsonUtility.ToJson(run);
        var bounds = Get<RectTransform>(gm, "boundsRoot");
        Vector2 originalSize = bounds.sizeDelta;
        try
        {
            bounds.sizeDelta = originalSize - new Vector2(80, 160);
            Canvas.ForceUpdateCanvases(); Invoke(gm, "LateUpdate");
            Check(Mathf.Approximately(Get<float>(gm, "levelTransitionElapsed"), .5f) && JsonUtility.ToJson(run) == prepared,
                "A mid-resize viewport refit preserves timeline, targets, modifiers and resources.");
            cells = Get<List<GameSquare>>(gm, "instantiatedSquares");
            foreach (var cell in cells)
            {
                var corners = new Vector3[4]; ((RectTransform)cell.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                    Check(bounds.rect.Contains(bounds.InverseTransformPoint(corner)), "Refitted surviving cell stays inside gameplay bounds.");
            }
            Invoke(gm, "AdvanceLevelTransition", .5f);
            Check(Flow(gm) == "Playing" && Get<object>(gm, "transitionLayout") == null,
                "Resized viewport reaches a clean playable endpoint.");
        }
        finally
        {
            bounds.sizeDelta = originalSize;
            Canvas.ForceUpdateCanvases(); Invoke(gm, "LateUpdate");
            gm.enabled = true;
        }
    }

    private static IEnumerator RunResize(GameManager gm, CampaignDefinition fixture, int oldSize,
        int newSize, string root, bool capture, bool reduced)
    {
        LevelData oldLevel = fixture.GetLevel(0);
        LevelData newLevel = fixture.GetLevel(1);
        Configure(oldLevel, oldSize, 37f, true, true);
        Configure(newLevel, newSize, -65f, true, true);
        yield return Start(gm);
        string pairName = oldSize + "-to-" + newSize +
            (reduced ? "-reduced" : NeonMotion.T.levelTransitionDuration <= 0f ? "-zero" : string.Empty);

        ActiveRunData run = Get<ActiveRunData>(gm, "sessionRun");
        run.levelState.objectiveProgress = oldLevel.requiredCorrectClicks - 1;
        run.currentHealth = 2;
        run.currentReserveSeconds = 7.25f;
        run.pendingCoins = 7;
        run.levelState.rotationAngle = 37f;
        run.levelState.scalePhase = .65f;
        run.levelState.movementPosition = new Vector2(9f, -7f);
        run.levelState.movementDirection = new Vector2(.8f, -.6f).normalized;
        Invoke(gm, "ApplyGridMotionState", false, 0f);
        Invoke(gm, "UpdateGameplayUI");

        List<GameSquare> oldCells = new List<GameSquare>(Get<List<GameSquare>>(gm, "instantiatedSquares"));
        int shared = Mathf.Min(oldSize, newSize);
        Vector3[][] oldCorners = SharedCorners(oldCells, shared, oldSize);
        Tap(gm, true);
        Check(Flow(gm) == "LevelTransition", oldSize + " to " + newSize + " enters transition.");
        string checkpointBefore = JsonUtility.ToJson(run);
        Check(run.currentLevelIndex == 1 && run.levelState.objectiveProgress == 0 &&
            run.currentHealth == 2 && run.currentReserveSeconds == 7.25f && run.levelState.normalTimeRemaining == 20 &&
            run.pendingCoins == 7 + oldLevel.completionCoinReward,
            oldSize + " to " + newSize + " prepares exact next-level resources and one reward.");
        ActiveRunData diskRun = Get<NeonSaveService>(gm, "saveService").Load(gm.gameConfig).activeRun;
        Check(diskRun != null && JsonUtility.ToJson(diskRun) == checkpointBefore,
            oldSize + " to " + newSize + " disk checkpoint matches prepared state.");

        List<GameSquare> incoming = Get<List<GameSquare>>(gm, "instantiatedSquares");
        Check(incoming.Count == newSize * newSize, newSize + "x" + newSize + " incoming count.");
        HashSet<GameSquare> unique = new HashSet<GameSquare>();
        for (int i = 0; i < incoming.Count; i++)
        {
            Check(unique.Add(incoming[i]), "No duplicate incoming cell " + i + ".");
            int x = i % newSize;
            int y = i / newSize;
            Check(incoming[i].gridX == x && incoming[i].gridY == y, "Incoming coordinate " + x + "," + y + ".");
        }
        for (int y = 0; y < shared; y++)
        {
            for (int x = 0; x < shared; x++)
            {
                GameSquare expected = oldCells[y * oldSize + x];
                Check(incoming[y * newSize + x] == expected,
                    "Retains shared cell " + x + "," + y + " for " + oldSize + " to " + newSize + ".");
            }
        }

        CheckTransitionCollections(gm, oldSize, newSize, oldCells.Count - shared * shared);
        gm.enabled = false;
        CheckCorners(oldCorners, SharedCorners(incoming, shared, newSize), .1f, "t0 shared world corner continuity");
        CheckCellPhases(gm, 0, oldSize, newSize);
        string transitionStart = Presentation(gm);
        yield return CaptureFrame(root, pairName, "t0", capture);
        Vector3[][] preFinishCorners = null;
        int steps = NeonMotion.T.levelTransitionDuration <= 0 ? 1 : 4;
        for (int step = 1; step <= steps; step++)
        {
            gm.enabled = false;
            if (step == steps)
            {
                // Compare the mathematical endpoint against Unity's re-enabled
                // GridLayoutGroup, rather than comparing two animation times.
                Invoke(gm, "RenderLevelTransition", 1f);
                Canvas.ForceUpdateCanvases();
                preFinishCorners = SharedCorners(incoming, shared, newSize);
            }
            Invoke(gm, "AdvanceLevelTransition", NeonMotion.T.levelTransitionDuration <= 0f ? 0f : .25f);
            Canvas.ForceUpdateCanvases();
            if (step == steps)
            {
                Tap(gm, true);
                Check(run.levelState.objectiveProgress == 0, "Endpoint frame rejects input.");
            }
            else
            {
                CheckCellPhases(gm, step * .25f, oldSize, newSize);
                if (oldSize > newSize && step == 1)
                    CheckCorners(oldCorners, SharedCorners(incoming, shared, newSize), .1f,
                        "Survivors hold their pose until outside rows disappear.");
            }
            Check(JsonUtility.ToJson(run) == checkpointBefore,
                oldSize + " to " + newSize + " checkpoint stable at t" + (step * .25f).ToString("F2", CultureInfo.InvariantCulture) + ".");
            CanvasGroup incomingGroup = Get<CanvasGroup>(gm, "incomingGridGroup");
            Check(incomingGroup == null || Mathf.Approximately(incomingGroup.alpha, 1f),
                "Surviving grid remains opaque at t" + (step * .25f).ToString("F2", CultureInfo.InvariantCulture) + ".");
            yield return CaptureFrame(root, pairName,
                "t" + (step * .25f).ToString("F2", CultureInfo.InvariantCulture), capture);
        }
        Check(Flow(gm) == "Playing", oldSize + " to " + newSize + " reaches Playing.");
        Check(Get<List<GameSquare>>(gm, "instantiatedSquares").Count == newSize * newSize,
            oldSize + " to " + newSize + " leaves only incoming cells.");
        Check(Get<object>(gm, "retiredGridCells") == null || RetiredCount(gm) == 0,
            oldSize + " to " + newSize + " retires removed cells.");
        CheckSharedGeometry(incoming, shared, newSize);
        UnityEngine.UI.GridLayoutGroup transitionLayout = Get<UnityEngine.UI.GridLayoutGroup>(gm, "transitionLayout");
        UnityEngine.UI.GridLayoutGroup finalLayout = Get<RectTransform>(gm, "gridContentRoot") == null ? null :
            Get<RectTransform>(gm, "gridContentRoot").GetComponent<UnityEngine.UI.GridLayoutGroup>();
        Check(transitionLayout == null && finalLayout != null && finalLayout.enabled,
            oldSize + " to " + newSize + " transition layout active at endpoint.");
        Check(transitionStart != Presentation(gm) || NeonMotion.T.levelTransitionDuration == 0f,
            oldSize + " to " + newSize + " transition presentation advances.");

        CheckCorners(preFinishCorners, SharedCorners(incoming, shared, newSize), .1f, "t1 geometry remains stable after finish");
        gm.enabled = false;
        int before = run.levelState.objectiveProgress;
        yield return null;
        gm.enabled = true;
        Tap(gm, true);
        Check(run.levelState.objectiveProgress == before + 1,
            oldSize + " to " + newSize + " accepts first next-frame input.");
        yield return null;
        Tap(gm, true);
        Check(run.levelState.objectiveProgress == before + 2,
            oldSize + " to " + newSize + " accepts second next-frame input.");
    }

    private static void CheckCellPhases(GameManager gm, float progress, int oldSize, int newSize)
    {
        foreach (object item in (IEnumerable)Get<object>(gm, "transitionCellLayouts"))
        {
            Type type = item.GetType();
            bool added = (bool)type.GetField("added").GetValue(item);
            bool removed = (bool)type.GetField("removed").GetValue(item);
            var group = (CanvasGroup)type.GetField("group").GetValue(item);
            if (!added && !removed)
                Check(Mathf.Approximately(group.alpha, 1), "Shared cells stay fully opaque throughout resize.");
            if (added && progress <= .6f)
                Check(Mathf.Approximately(group.alpha, 0), "New cells wait until the shared grid fits its corner.");
            if (removed && progress >= .4f)
                Check(Mathf.Approximately(group.alpha, 0), "Outside cells disappear before survivors expand.");
            Check(!group.blocksRaycasts && !group.interactable, "Transition cell cannot receive gameplay input.");
        }
    }

    private static void Configure(LevelData level, int size, float rotate, bool moving, bool scaling)
    {
        level.gridSize = size;
        level.requiredCorrectClicks = 3;
        level.timeLimit = 20f;
        level.reverseEnabled = false;
        level.rotateSpeed = rotate;
        level.movementEnabled = moving;
        level.movementSpeedNormalized = .16f;
        level.scaleEnabled = scaling;
        level.minimumGridScale = .75f;
        level.maximumGridScale = 1f;
        level.scaleCycleDuration = 2f;
        level.movementTravelPaddingNormalized = .02f;
    }

    private static IEnumerator Start(GameManager gm)
    {
        gm.enabled = true;
        gm.ReturnToMainMenu();
        Get<SaveEnvelopeData>(gm, "saveData").activeRun = null;
        Invoke(gm, "OnApplicationPause", false);
        Invoke(gm, "OnApplicationFocus", true);
        gm.StartNewRun();
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Flow(gm) != "Playing" && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Flow(gm) != "Playing")
            throw new InvalidOperationException("Timed out starting grid resize fixture.");
    }

    private static void Tap(GameManager gm, bool correct)
    {
        ActiveRunData run = Get<ActiveRunData>(gm, "sessionRun");
        List<GameSquare> cells = Get<List<GameSquare>>(gm, "instantiatedSquares");
        int index = correct ? run.levelState.largeIndex : run.levelState.mediumIndex;
        index = Mathf.Clamp(index, 0, cells.Count - 1);
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, cells[index].transform.position)
        };
        ExecuteEvents.Execute(cells[index].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Canvas.ForceUpdateCanvases();
    }

    private static void CheckTransitionCollections(GameManager gm, int oldSize, int newSize, int expectedRetired)
    {
        Check(gm.GetType().GetField("transitionCellLayouts", Hidden) != null,
            "Transition cell layout diagnostics are exposed.");
        Check(gm.GetType().GetField("retiredGridCells", Hidden) != null,
            "Retired cell diagnostics are exposed.");
        object layouts = Get<object>(gm, "transitionCellLayouts");
        if (layouts is IEnumerable enumerable)
        {
            int count = 0;
            foreach (object entry in enumerable)
            {
                CheckFiniteMember(entry, "sourceCenter");
                CheckFiniteMember(entry, "destinationCenter");
                CheckFiniteMember(entry, "sourceSize");
                CheckFiniteMember(entry, "destinationSize");
                count++;
            }
            Check(count == Mathf.Max(oldSize, newSize) * Mathf.Max(oldSize, newSize),
                "Transition layout count covers the retained board footprint.");
        }
        int retired = RetiredCount(gm);
        Check(retired == expectedRetired, "Retired cell count is " + expectedRetired + ".");
    }

    private static int RetiredCount(GameManager gm)
    {
        object value = Get<object>(gm, "retiredGridCells");
        if (!(value is ICollection collection))
            return 0;
        return collection.Count;
    }

    private static void CheckFiniteMember(object entry, string name)
    {
        if (entry == null)
            return;
        Type type = entry.GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object value = field != null ? field.GetValue(entry) : property != null ? property.GetValue(entry, null) : null;
        if (value is Vector2 vector2)
            Check(!float.IsNaN(vector2.x) && !float.IsNaN(vector2.y), "Finite transition " + name + ".");
        else if (value is Vector3 vector3)
            Check(!float.IsNaN(vector3.x) && !float.IsNaN(vector3.y) && !float.IsNaN(vector3.z), "Finite transition " + name + ".");
    }

    private static Vector3[] SharedPositions(List<GameSquare> cells, int shared, int size)
    {
        Vector3[] result = new Vector3[shared * shared];
        for (int y = 0; y < shared; y++)
            for (int x = 0; x < shared; x++)
                result[y * shared + x] = cells[y * size + x].transform.position;
        return result;
    }

    private static Vector3[][] SharedCorners(List<GameSquare> cells, int shared, int size)
    {
        Vector3[][] result = new Vector3[shared * shared][];
        for (int y = 0; y < shared; y++)
        {
            for (int x = 0; x < shared; x++)
            {
                Vector3[] corners = new Vector3[4];
                RectTransform rect = cells[y * size + x].transform as RectTransform;
                rect.GetWorldCorners(corners);
                result[y * shared + x] = corners;
            }
        }
        return result;
    }

    private static void CheckCorners(Vector3[][] before, Vector3[][] after, float tolerance, string message)
    {
        Check(before.Length == after.Length, message + " count.");
        for (int i = 0; i < before.Length; i++)
            for (int corner = 0; corner < 4; corner++)
                Check((before[i][corner] - after[i][corner]).magnitude <= tolerance,
                    message + " cell " + i + " corner " + corner + ".");
    }

    private static void CheckSharedGeometry(List<GameSquare> cells, int shared, int size)
    {
        for (int y = 0; y < shared; y++)
        {
            for (int x = 0; x < shared; x++)
            {
                GameSquare cell = cells[y * size + x];
                RectTransform rect = cell.transform as RectTransform;
                Check(rect != null && rect.rect.width > 0f && rect.rect.height > 0f,
                    "Shared cell has finite endpoint geometry " + x + "," + y + ".");
                Check(!float.IsNaN(cell.transform.position.x) && !float.IsNaN(cell.transform.position.y),
                    "Shared cell world center is finite " + x + "," + y + ".");
            }
        }
    }

    private static IEnumerator CaptureFrame(string root, string pair, string name, bool capture)
    {
        if (!capture)
            yield break;
        yield return new WaitForEndOfFrame();
        string directory = Path.Combine(root, pair);
        Directory.CreateDirectory(directory);
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
    }

    private static void Check(bool condition, string message)
    {
        File.AppendAllText(checkPath, (condition ? "PASS: " : "FAIL: ") + message + "\n");
        if (!condition)
            throw new InvalidOperationException(message);
        passed++;
    }

    private static string Presentation(GameManager gm)
    {
        return Get<float>(gm, "levelTransitionElapsed") + ":" + Flow(gm);
    }

    private static string Flow(GameManager gm)
    {
        return Get<object>(gm, "state").ToString();
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    }

    private static T Get<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Hidden);
        return field == null ? default(T) : (T)field.GetValue(target);
    }
}
