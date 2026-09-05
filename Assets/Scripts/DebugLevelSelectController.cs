#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

/// <summary>Development-only practice navigation. GameManager owns sandbox isolation.</summary>
public class DebugLevelSelectController : MonoBehaviour
{
    private GameObject overlay;
    private Action<int> onLevelSelected;
    private ScrollRect levelScroll;
    private NeonTheme T => NeonTheme.T;

    public void Initialize(RectTransform mainMenuRoot, IReadOnlyList<LevelData> levels, Action<int> levelSelected)
    {
        if (overlay != null || mainMenuRoot == null) return;
        onLevelSelected = levelSelected;
        // Accept either the authored panel or its responsive content root.
        RectTransform safe = mainMenuRoot.Find("SafeContent") as RectTransform;
        if (safe == null) safe = mainMenuRoot;
        CreateOpenButton(safe);
        CreateLevelOverlay(safe, levels);
        overlay.SetActive(false);
    }

    public void Close()
    {
        if (levelScroll != null) levelScroll.StopMovement();
        if (overlay != null) overlay.SetActive(false);
    }

    private void Open()
    {
        if (overlay == null) return;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    private void SelectLevel(int levelIndex)
    {
        if (overlay == null || !overlay.activeInHierarchy) return;
        Close();
        onLevelSelected?.Invoke(levelIndex);
    }

    private void CreateOpenButton(RectTransform parent)
    {
        Button button = Button("DebugLevelSelectButton", parent, "PRACTICE");
        At((RectTransform)button.transform, 1f, 0f, -170f, 48f, 216f, 72f);
        button.image.color = T.Background;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        label.fontSize = 20f;
        label.color = T.Muted;
        label.alignment = TextAlignmentOptions.MidlineRight;
        Fill(label.rectTransform, 16f, 0f, 12f, 0f);
        button.onClick.AddListener(Open);
    }

    private void CreateLevelOverlay(RectTransform parent, IReadOnlyList<LevelData> levels)
    {
        Image backdrop = Panel("DebugLevelSelectOverlay", parent, T.Background, true);
        overlay = backdrop.gameObject;
        RectTransform root = backdrop.rectTransform;
        Fill(root);
        int count = levels?.Count ?? 0;

        TMP_Text eyebrow = Text("Eyebrow", root, "DEBUG SANDBOX", T.labelSize, T.Muted);
        Place(eyebrow.rectTransform, T.pageMargin, -52f, 600f, 48f);
        eyebrow.characterSpacing = 3f;
        TMP_Text total = Text("LevelCount", root, count + " LEVELS", 24f, T.Muted, TextAlignmentOptions.MidlineRight);
        At(total.rectTransform, 1f, 1f, -T.pageMargin - 140f, -76f, 280f, 48f);

        TMP_Text title = Text("Title", root, "PRACTICE", T.headingSize, T.Text);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, T.pageMargin, -124f, 952f, 138f);
        TMP_Text description = Text("Subtitle", root,
            "Choose a campaign level.\nPractice earns no coins and saves no progression.", 28f, T.Muted);
        description.textWrappingMode = TextWrappingModes.Normal;
        Place(description.rectTransform, T.pageMargin, -258f, 952f, 96f);

        Image line = Rule("Divider", root);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(1f, 1f);
        line.rectTransform.offsetMin = new Vector2(T.pageMargin, -374f);
        line.rectTransform.offsetMax = new Vector2(-T.pageMargin, -372f);

        Image viewportImage = Panel("LevelViewport", root, Color.clear, true);
        RectTransform viewport = viewportImage.rectTransform;
        Fill(viewport, T.pageMargin, 216f, T.pageMargin, 402f);
        viewport.gameObject.AddComponent<RectMask2D>();
        levelScroll = viewport.gameObject.AddComponent<ScrollRect>();
        RectTransform content = Rect("LevelGrid", viewport);
        int rows = Mathf.CeilToInt(count / 2f);
        const float rowPitch = 184f;
        const float rowGap = 20f;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, Mathf.Max(172f, rows * rowPitch - rowGap));
        content.anchoredPosition = Vector2.zero;
        levelScroll.viewport = viewport;
        levelScroll.content = content;
        levelScroll.horizontal = false;
        levelScroll.vertical = true;
        levelScroll.movementType = ScrollRect.MovementType.Clamped;
        levelScroll.scrollSensitivity = 52f;
        levelScroll.decelerationRate = 0.08f;

        if (count == 0)
        {
            TMP_Text empty = Text("NoLevels", content, "No campaign levels configured.", 32f, T.Muted);
            Fill(empty.rectTransform, 24f, 0f, 24f, 0f);
        }
        else
        {
            for (int i = 0; i < count; i++) CreateLevelButton(content, levels[i], i, rowPitch, rowGap);
        }

        Button close = Button("CloseButton", root, "BACK TO MENU");
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = new Vector2(0f, 0f);
        closeRect.anchorMax = new Vector2(1f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.offsetMin = new Vector2(T.pageMargin, 64f);
        closeRect.offsetMax = new Vector2(-T.pageMargin, 176f);
        close.onClick.AddListener(Close);
        TMP_Text buildLabel = Text("BuildLabel", root, "EDITOR / DEVELOPMENT BUILD", 20f, T.Muted);
        Place(buildLabel.rectTransform, T.pageMargin, 34f, 952f, 32f, 0f);
    }

    private void CreateLevelButton(RectTransform parent, LevelData level, int index, float rowPitch, float rowGap)
    {
        bool valid = level != null;
        Button button = Button("Level_" + (index + 1), parent, valid ? $"LEVEL {level.levelNumber:00}" : "MISSING LEVEL");
        button.image.color = T.Surface;
        button.interactable = valid;
        RectTransform rect = (RectTransform)button.transform;
        int column = index % 2;
        int row = index / 2;
        // Anchored columns remain responsive without rebuilding a layout every frame.
        rect.anchorMin = new Vector2(column * 0.5f, 1f);
        rect.anchorMax = new Vector2((column + 1) * 0.5f, 1f);
        rect.offsetMin = new Vector2(column == 0 ? 0f : 10f, -(row + 1) * rowPitch + rowGap);
        rect.offsetMax = new Vector2(column == 0 ? -10f : 0f, -row * rowPitch);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        label.fontSize = 36f;
        label.color = valid ? T.Text : T.Muted;
        // Keep the label's centered rect used by shared press feedback, and
        // position its content with text margins rather than a new transform.
        Fill(label.rectTransform, 24f, 0f, 24f, 0f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.margin = new Vector4(0f, 14f, 0f, 86f);

        string details = valid ? $"{level.gridSize} × {level.gridSize}   ·   {level.timeLimit:0.#}s   ·   {level.requiredCorrectClicks} targets" : "Campaign entry " + (index + 1);
        TMP_Text metadata = Text("Details", rect, details, 22f, T.Muted);
        metadata.rectTransform.anchorMin = new Vector2(0f, 1f);
        metadata.rectTransform.anchorMax = new Vector2(1f, 1f);
        metadata.rectTransform.offsetMin = new Vector2(24f, -114f);
        metadata.rectTransform.offsetMax = new Vector2(-24f, -76f);
        TMP_Text modifiers = Text("Modifiers", rect, valid ? GetModifierLabel(level) : "UNAVAILABLE", 20f, valid ? T.Target : T.Muted);
        modifiers.characterSpacing = 1f;
        modifiers.rectTransform.anchorMin = new Vector2(0f, 1f);
        modifiers.rectTransform.anchorMax = new Vector2(1f, 1f);
        modifiers.rectTransform.offsetMin = new Vector2(24f, -150f);
        modifiers.rectTransform.offsetMax = new Vector2(-24f, -116f);
        button.onClick.AddListener(() => SelectLevel(index));
    }

    private static string GetModifierLabel(LevelData level)
    {
        List<string> labels = new List<string>();
        if (level.reverseEnabled) labels.Add("REV");
        if (!Mathf.Approximately(level.rotateSpeed, 0f)) labels.Add("ROT");
        if (level.scaleEnabled) labels.Add("SCALE");
        if (level.movementEnabled) labels.Add("MOVE");
        return labels.Count == 0 ? "STANDARD" : string.Join(" · ", labels);
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height, float anchorY = 1f)
    {
        At(rect, 0f, anchorY, left + width * 0.5f, top - height * 0.5f, width, height);
    }
}
#endif
