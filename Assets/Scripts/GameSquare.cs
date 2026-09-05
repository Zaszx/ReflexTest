using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presentation for one pointer-down target. Size remains an immediate, discrete
/// signal; feedback never changes the cell transform used by gameplay.
/// </summary>
public class GameSquare : MonoBehaviour, IPointerDownHandler
{
    [Header("UI Components")]
    public Image bgImage;
    public Image outlineImage;

    public enum LitSize { None, Small, Medium, Large }

    [Header("Current State")]
    public int gridX;
    public int gridY;
    public LitSize currentLitSize = LitSize.None;

    public System.Action<GameSquare> onClicked;

    private PrecisionCellFrame targetFrame;
    private PrecisionCellFrame cellFrame;
    private Coroutine tapCoroutine;
    private Color baseCellColor;
    private float smallScale = 0.4f;
    private float mediumScale = 0.7f;
    private float fullScale = 1f;
    private bool animationsPaused;

    public void SetAnimationsPaused(bool paused)
    {
        animationsPaused = paused;
    }

    public void Setup(int x, int y, Sprite solidSprite, Sprite outlineSprite,
        Color cellColor, Color outlineColor, float small, float medium, float full)
    {
        gridX = x;
        gridY = y;
        StopTapFeedback();

        // Keep a trace of authored level palettes inside the shared visual system.
        // The three target sizes deliberately share one color and one opacity.
        baseCellColor = Color.Lerp(NeonTheme.T.Surface, cellColor, 0.06f);
        baseCellColor.a = 1f;
        Color targetColor = NeonTheme.LevelTarget(outlineColor);
        targetColor.a = 1f;
        bgImage.sprite = null;
        bgImage.color = baseCellColor;
        bgImage.raycastTarget = true;

        // Retain the prefab's serialized reference and size container. Replace
        // only its textured rendering with crisp, four-quad native UI geometry.
        outlineImage.enabled = false;
        outlineImage.raycastTarget = false;
        outlineImage.color = targetColor;
        if (cellFrame == null)
        {
            cellFrame = CreateFrame("CellBoundary", bgImage.rectTransform);
            cellFrame.transform.SetAsFirstSibling();
            cellFrame.LineWidth = 1.2f;
        }
        Color boundary = NeonTheme.T.Muted;
        boundary.a = NeonTheme.T.cellBoundaryOpacity;
        cellFrame.color = boundary;
        if (targetFrame == null)
            targetFrame = CreateFrame("PrecisionOutline", outlineImage.rectTransform);
        targetFrame.color = targetColor;

        smallScale = small;
        mediumScale = medium;
        fullScale = full;
        SetLitSize(LitSize.None, false);
    }

    public void SetLitSize(LitSize size, bool animate)
    {
        currentLitSize = size;
        if (outlineImage == null) return;

        // `animate` is kept for existing callers. Interpolating outlines would
        // momentarily report the wrong size while input is already available.
        float scale = GetScaleValue(size);
        outlineImage.transform.localScale = Vector3.one * scale;
        outlineImage.gameObject.SetActive(size != LitSize.None);
        if (targetFrame != null && scale > 0f)
        {
            // Keep equal apparent line weights across Small, Medium, and Large.
            targetFrame.LineWidth = NeonTheme.T.targetStrokeWidth / scale;
            targetFrame.SetVerticesDirty();
        }
    }

    /// <summary>Called only after GameManager validates an actual gameplay tap.</summary>
    public void PlayTapFeedback(bool correct)
    {
        if (bgImage == null || !gameObject.activeInHierarchy) return;
        StopTapFeedback();
        Color accent = correct ? NeonTheme.T.Primary : NeonTheme.T.Danger;
        float intensity = NeonTheme.ReducedEffects ? 0.13f : (correct ? 0.25f : 0.34f);
        Color impact = Color.Lerp(baseCellColor, accent, intensity);
        bgImage.color = impact;
        tapCoroutine = StartCoroutine(TapFeedbackRoutine(impact, correct ? 0.16f : 0.24f));
    }

    // Retained for API compatibility. Target-directed pulses reveal the answer
    // and distort the size challenge, so announcements now explain only the rule.
    public void PlayAttentionPulse(float delay = 0f) { }

    private IEnumerator TapFeedbackRoutine(Color impact, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!animationsPaused) elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bgImage.color = Color.Lerp(impact, baseCellColor, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        bgImage.color = baseCellColor;
        tapCoroutine = null;
    }

    private void StopTapFeedback()
    {
        if (tapCoroutine != null) StopCoroutine(tapCoroutine);
        tapCoroutine = null;
        if (bgImage != null) bgImage.color = baseCellColor;
    }

    private void OnDisable()
    {
        StopTapFeedback();
    }

    private float GetScaleValue(LitSize size)
    {
        switch (size)
        {
            case LitSize.Small: return smallScale;
            case LitSize.Medium: return mediumScale;
            case LitSize.Large: return fullScale;
            default: return 0f;
        }
    }

    private static PrecisionCellFrame CreateFrame(string name, RectTransform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PrecisionCellFrame));
        child.transform.SetParent(parent, false);
        PrecisionCellFrame frame = child.GetComponent<PrecisionCellFrame>();
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        frame.raycastTarget = false;
        return frame;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onClicked?.Invoke(this);
    }
}

/// <summary>A single native UI mesh without texture padding, glow, or fill.</summary>
public sealed class PrecisionCellFrame : MaskableGraphic
{
    public float LineWidth { get; set; } = 4.5f;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float width = Mathf.Min(LineWidth, Mathf.Min(rect.width, rect.height) * 0.5f);
        if (width <= 0f) return;
        AddBar(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + width);
        AddBar(mesh, rect.xMin, rect.yMax - width, rect.xMax, rect.yMax);
        AddBar(mesh, rect.xMin, rect.yMin + width, rect.xMin + width, rect.yMax - width);
        AddBar(mesh, rect.xMax - width, rect.yMin + width, rect.xMax, rect.yMax - width);
    }

    private void AddBar(VertexHelper mesh, float left, float bottom, float right, float top)
    {
        int start = mesh.currentVertCount;
        Color32 tint = color;
        mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
        mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }
}
