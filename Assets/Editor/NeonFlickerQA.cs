using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Small actual-render probe for transformed grid edge shimmer.</summary>
public static class NeonFlickerQA
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class Sample { public Texture2D image; public double elapsed; public bool keep; }

    public static IEnumerator Run(GameManager gm, string stage)
    {
        if (!NeonPresentationQAProfile.IsActive)
            throw new InvalidOperationException("Flicker QA requires isolated profile storage.");
        string root = Path.Combine(Application.dataPath, "..", "Artifacts", "Flicker", stage,
            Screen.width + "x" + Screen.height);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "README.txt"),
            "Actual Unity screen captures at 1/30 second sampling. Edge metrics are luminance means and frame-to-frame absolute variation in a 3px perimeter around the transformed grid bounds.\n");
        gm.ReturnToMainMenu();
        Invoke(gm, "OnApplicationFocus", true);
        yield return new WaitForSecondsRealtime(.25f);
        gm.enabled = false;
        yield return SubpixelProbe(root);
        gm.enabled = true;

        // Campaign indices are zero based: L19 scale, L3 rotation, L5 movement, L100 combined.
        yield return Case(gm, root, "01-L19-scale", 18, 1019, (run, level) =>
        {
            run.levelState.scalePhase = .17f; run.levelState.scaleDirection = 1;
            run.levelState.rotationAngle = 0f; run.levelState.movementPosition = Vector2.zero;
        });
        yield return Case(gm, root, "02-L3-rotation", 2, 1003, (run, level) =>
        {
            run.levelState.rotationAngle = -31f; run.levelState.scalePhase = 0f;
            run.levelState.movementPosition = Vector2.zero;
        });
        yield return Case(gm, root, "03-L5-movement", 4, 1005, (run, level) =>
        {
            run.levelState.movementPosition = new Vector2(-18f, 11f);
            run.levelState.movementDirection = new Vector2(.83f, .56f).normalized;
            run.levelState.rotationAngle = 0f; run.levelState.scalePhase = 0f;
        });
        yield return Case(gm, root, "04-L100-combined", gm.campaign.LevelCount - 1, 1100, (run, level) =>
        {
            run.levelState.rotationAngle = -43f; run.levelState.scalePhase = .63f;
            run.levelState.scaleDirection = -1; run.levelState.movementPosition = new Vector2(12f, -9f);
            run.levelState.movementDirection = new Vector2(-.71f, .7f).normalized;
        });
        File.WriteAllText(Path.Combine(root, "complete.txt"), DateTime.UtcNow.ToString("O"));
    }

    private static IEnumerator Case(GameManager gm, string root, string name, int index, int seed, Action<ActiveRunData, LevelData> fixture)
    {
        // The previous case leaves the manager paused while its textures are encoded.
        // Re-enable before starting the next debug level so intro presentation can advance.
        gm.enabled = true;
        gm.ReturnToMainMenu();
        gm.StartDebugLevel(Mathf.Clamp(index, 0, gm.campaign.LevelCount - 1));
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Flow(gm) != "Playing" && Time.realtimeSinceStartup < deadline) yield return null;
        if (Flow(gm) != "Playing") throw new InvalidOperationException(name + " did not reach Playing.");
        var run = RunData(gm); var level = gm.campaign.GetLevel(run.currentLevelIndex);
        fixture(run, level);
        run.randomState = NeonSaveService.EncodeRandomState((uint)seed);
        Invoke(gm, "ApplyGridMotionState", false, 0f);
        Invoke(gm, "UpdateGameplayUI");
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        string dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "frame-*.png")) File.Delete(old);
        var samples = new List<Sample>();
        var rows = new List<string> { "frame,elapsed,rotation,scale,movementX,movementY,borderMean,borderDelta,gridX,gridY,gridW,gridH,raycastOk" };
        bool roleChanged = false;
        int raycastFailures = 0;
        Vector2 initialMovement = run.levelState.movementPosition;
        float initialRotation = run.levelState.rotationAngle, initialScale = GridScale(gm);
        bool motionChanged = false;
        double start = Time.realtimeSinceStartupAsDouble, next = 0;
        bool previousEnabled = gm.enabled;
        try
        {
            gm.enabled = true;
            while (Time.realtimeSinceStartupAsDouble - start < 2.0)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - start;
                if (name.Contains("combined") && !roleChanged && elapsed >= .65)
                { TapCorrect(gm); roleChanged = true; }
                if (elapsed >= next)
                {
                    yield return new WaitForEndOfFrame();
                    Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
                    Rect bounds = GridBounds(gm); float mean = BorderMean(image, bounds);
                    float delta = samples.Count == 0 ? 0f : Mathf.Abs(mean - BorderMean(samples[samples.Count - 1].image, bounds));
                    bool ray = RaycastCenter(gm); if (!ray) raycastFailures++;
                    var state = run.levelState;
                    motionChanged |= !Mathf.Approximately(initialRotation, state.rotationAngle) || !Mathf.Approximately(initialScale, GridScale(gm)) || Vector2.Distance(initialMovement, state.movementPosition) > .01f;
                    rows.Add(samples.Count.ToString(CultureInfo.InvariantCulture) + "," + elapsed.ToString("F6", CultureInfo.InvariantCulture) + "," +
                        state.rotationAngle.ToString("F4", CultureInfo.InvariantCulture) + "," + GridScale(gm).ToString("F4", CultureInfo.InvariantCulture) + "," +
                        state.movementPosition.x.ToString("F3", CultureInfo.InvariantCulture) + "," + state.movementPosition.y.ToString("F3", CultureInfo.InvariantCulture) + "," +
                        mean.ToString("F4", CultureInfo.InvariantCulture) + "," + delta.ToString("F4", CultureInfo.InvariantCulture) + "," +
                        bounds.x.ToString("F2", CultureInfo.InvariantCulture) + "," + bounds.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                        bounds.width.ToString("F2", CultureInfo.InvariantCulture) + "," + bounds.height.ToString("F2", CultureInfo.InvariantCulture) + "," + (ray ? "1" : "0"));
                    samples.Add(new Sample { image = image, elapsed = elapsed, keep = samples.Count % 3 == 0 });
                    next += 1d / 30d;
                }
                else yield return null;
            }
        }
        finally { gm.enabled = previousEnabled; }
        bool expectedMotion = name.Contains("scale") ? !Mathf.Approximately(initialScale, GridScale(gm)) : name.Contains("rotation") ? !Mathf.Approximately(initialRotation, run.levelState.rotationAngle) : name.Contains("movement") ? Vector2.Distance(initialMovement, run.levelState.movementPosition) > .01f : motionChanged;
        File.WriteAllText(Path.Combine(dir, "checks.txt"), "profileIsolated=" + NeonPresentationQAProfile.IsActive + "\nframes=" + samples.Count + "\nraycastFailures=" + raycastFailures + "\nmotionChanged=" + motionChanged + "\nexpectedMotionChanged=" + expectedMotion + "\n");
        if (!NeonPresentationQAProfile.IsActive || samples.Count <= 1 || raycastFailures != 0 || !expectedMotion)
            Debug.LogError("Flicker QA assertion failed for " + name);
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].keep)
                File.WriteAllBytes(Path.Combine(dir, "frame-" + i.ToString("D4") + ".png"), samples[i].image.EncodeToPNG());
            UnityEngine.Object.Destroy(samples[i].image); yield return null;
        }
        File.WriteAllLines(Path.Combine(dir, "metrics.csv"), rows);
    }

    private static Rect GridBounds(GameManager gm)
    {
        var squares = Get<List<GameSquare>>(gm, "instantiatedSquares");
        if (squares == null || squares.Count == 0) return new Rect(0, 0, Screen.width, Screen.height);
        Vector3[] corners = new Vector3[4]; var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var square in squares) { var rect = square.transform as RectTransform; rect.GetWorldCorners(corners); foreach (var c in corners) { var p = RectTransformUtility.WorldToScreenPoint(null, c); min = Vector2.Min(min, p); max = Vector2.Max(max, p); } }
        return Rect.MinMaxRect(Mathf.Clamp(min.x, 0, Screen.width), Mathf.Clamp(min.y, 0, Screen.height), Mathf.Clamp(max.x, 0, Screen.width), Mathf.Clamp(max.y, 0, Screen.height));
    }

    private static float BorderMean(Texture2D image, Rect bounds)
    {
        int x0 = Mathf.Clamp(Mathf.RoundToInt(bounds.xMin), 0, image.width - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(bounds.xMax), 0, image.width - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(bounds.yMin), 0, image.height - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(bounds.yMax), 0, image.height - 1);
        long sum = 0; int count = 0; for (int y = y0; y <= Mathf.Min(y0 + 2, y1); y++) for (int x = x0; x <= x1; x++) { sum += Luma(image.GetPixel(x, y)); count++; }
        for (int y = Mathf.Max(y1 - 2, y0); y <= y1; y++) for (int x = x0; x <= x1; x++) { sum += Luma(image.GetPixel(x, y)); count++; }
        return count == 0 ? 0 : (float)sum / count / 255f;
    }
    private static int Luma(Color c) => Mathf.RoundToInt((.2126f * c.r + .7152f * c.g + .0722f * c.b) * 255f);
    private static bool RaycastCenter(GameManager gm)
    {
        var run = RunData(gm); var squares = Get<List<GameSquare>>(gm, "instantiatedSquares");
        if (run == null || squares == null || squares.Count == 0) return false;
        int index = run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex;
        if (index < 0 || index >= squares.Count) return false;
        GameSquare expected = squares[index];
        var p = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, expected.transform.position) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        return hits.Exists(h => h.gameObject.GetComponentInParent<GameSquare>() == expected);
    }
    private static float GridScale(GameManager gm) { var root = Get<RectTransform>(gm, "rotationScaleRoot"); return root == null ? 1f : root.localScale.x; }
    private static void TapCorrect(GameManager gm)
    {
        var run = RunData(gm); var squares = Get<List<GameSquare>>(gm, "instantiatedSquares");
        int index = run.levelState.reverseActive ? run.levelState.smallIndex : run.levelState.largeIndex;
        var square = squares[index]; var pointer = new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(null, square.transform.position) };
        ExecuteEvents.Execute(square.gameObject, pointer, ExecuteEvents.pointerDownHandler);
    }
    private static string Flow(GameManager gm) => Get<object>(gm, "state").ToString();
    private static ActiveRunData RunData(GameManager gm) => Get<ActiveRunData>(gm, "sessionRun");
    private static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Hidden).GetValue(instance);
    private static object Invoke(object instance, string name, params object[] values) => instance.GetType().GetMethod(name, Hidden).Invoke(instance, values);

    private static IEnumerator SubpixelProbe(string root)
    {
        string dir = Path.Combine(root, "00-subpixel-probe"); Directory.CreateDirectory(dir);
        var canvasObject = new GameObject("Flicker probe canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var background = new GameObject("Probe background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        background.transform.SetParent(canvasObject.transform, false); var bg = background.GetComponent<UnityEngine.UI.Image>(); bg.color = Color.black;
        var bgRect = background.GetComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var frameObject = new GameObject("Probe border", typeof(RectTransform), typeof(CanvasRenderer), typeof(PrecisionCellFrame));
        frameObject.transform.SetParent(canvasObject.transform, false); var frame = frameObject.GetComponent<PrecisionCellFrame>();
        var rect = frameObject.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(64f, 64f); frame.color = Color.white; frame.LineWidth = .45f;
        var rows = new List<string> { "phase,rotation,roiMean,roiMin,roiMax,roiCV" };
        try
        {
            yield return null; yield return new WaitForEndOfFrame();
            var probeMesh = frame.canvasRenderer.GetMesh();
            var uv0 = new List<Vector4>(); var uv1 = new List<Vector4>();
            probeMesh.GetUVs(0, uv0); probeMesh.GetUVs(1, uv1);
            File.WriteAllText(Path.Combine(dir, "mesh.txt"), "shader=" + frame.materialForRendering.shader.name + " channels=" + canvas.additionalShaderChannels + " line=" + frame.LineWidth + " corners=" + frame.CornerFraction + "\nvertices=" + string.Join(";", probeMesh.vertices) + "\nuv0=" + string.Join(";", uv0) + "\nuv1=" + string.Join(";", uv1));
            for (int rotationIndex = 0; rotationIndex < 2; rotationIndex++)
            {
                float rotation = rotationIndex == 0 ? 0f : 27f; rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                for (int i = 0; i < 24; i++)
                {
                    float phase = i / 24f; rect.anchoredPosition = new Vector2(phase, phase);
                    yield return new WaitForEndOfFrame();
                    Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
                    ProbeStats stats = IntegrateProbe(image);
                    rows.Add(phase.ToString("F5", CultureInfo.InvariantCulture) + "," + rotation.ToString("F1", CultureInfo.InvariantCulture) + "," + stats.mean.ToString("F6", CultureInfo.InvariantCulture) + "," + stats.min.ToString("F6", CultureInfo.InvariantCulture) + "," + stats.max.ToString("F6", CultureInfo.InvariantCulture) + "," + stats.cv.ToString("F6", CultureInfo.InvariantCulture));
                    if (i % 3 == 0) File.WriteAllBytes(Path.Combine(dir, "frame-r" + rotationIndex + "-" + i.ToString("D2") + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image); yield return null;
                }
            }
        }
        finally { UnityEngine.Object.Destroy(canvasObject); }
        File.WriteAllLines(Path.Combine(dir, "probe.csv"), rows);
    }

    private struct ProbeStats { public float mean, min, max, cv; }
    private static ProbeStats IntegrateProbe(Texture2D image)
    {
        int cx = image.width / 2, cy = image.height / 2, radius = 55, count = 0; double sum = 0, sum2 = 0; float min = 1f, max = 0f;
        for (int y = Mathf.Max(0, cy - radius); y < Mathf.Min(image.height, cy + radius); y++) for (int x = Mathf.Max(0, cx - radius); x < Mathf.Min(image.width, cx + radius); x++)
        { Color c = image.GetPixel(x, y).linear; float value = .2126f * c.r + .7152f * c.g + .0722f * c.b; sum += value; sum2 += value * value; min = Mathf.Min(min, value); max = Mathf.Max(max, value); count++; }
        float mean = count == 0 ? 0f : (float)(sum / count); float variance = count == 0 ? 0f : Mathf.Max(0f, (float)(sum2 / count) - mean * mean);
        return new ProbeStats { mean = mean, min = min, max = max, cv = mean <= .00001f ? 0f : Mathf.Sqrt(variance) / mean };
    }
}
