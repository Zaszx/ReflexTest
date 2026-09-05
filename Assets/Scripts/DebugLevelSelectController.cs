#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugLevelSelectController : MonoBehaviour
{
    private static readonly Color DebugOrange = new Color(1f, 0.52f, 0.08f, 1f);
    private GameObject overlay;
    private Action<int> onLevelSelected;

    public void Initialize(RectTransform mainMenuRoot, IReadOnlyList<LevelData> levels, Action<int> levelSelected)
    {
        if (overlay != null || mainMenuRoot == null) return;

        onLevelSelected = levelSelected;
        CreateOpenButton(mainMenuRoot);
        CreateLevelOverlay(mainMenuRoot, levels);
        overlay.SetActive(false);
    }

    public void Close()
    {
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
        Close();
        onLevelSelected?.Invoke(levelIndex);
    }

    private void CreateOpenButton(RectTransform parent)
    {
        Button button = CreateButton("DebugLevelSelectButton", parent, "DEBUG: LEVEL SELECT", DebugOrange);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.08f);
        rect.anchorMax = new Vector2(0.5f, 0.08f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(560f, 78f);
        rect.anchoredPosition = Vector2.zero;
        button.onClick.AddListener(Open);
    }

    private void CreateLevelOverlay(RectTransform parent, IReadOnlyList<LevelData> levels)
    {
        overlay = new GameObject("DebugLevelSelectOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        StretchToParent(overlayRect);
        Image backdrop = overlay.GetComponent<Image>();
        backdrop.color = new Color(0.015f, 0.012f, 0.025f, 0.97f);
        backdrop.raycastTarget = true;

        TMP_Text title = CreateText("Title", overlayRect, "LEVEL SELECT", 76f, Color.white);
        SetRect(title.rectTransform, new Vector2(850f, 100f), new Vector2(0f, 650f));
        title.fontStyle = FontStyles.Bold;

        TMP_Text subtitle = CreateText("Subtitle", overlayRect, "EDITOR / DEVELOPMENT BUILD", 28f, DebugOrange);
        SetRect(subtitle.rectTransform, new Vector2(800f, 55f), new Vector2(0f, 575f));
        subtitle.characterSpacing = 3f;

        Button closeButton = CreateButton("CloseButton", overlayRect, "CLOSE", new Color(0.18f, 0.16f, 0.24f, 1f));
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0.5f);
        closeRect.anchorMax = new Vector2(0.5f, 0.5f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.sizeDelta = new Vector2(420f, 90f);
        closeRect.anchoredPosition = new Vector2(0f, -700f);
        closeButton.onClick.AddListener(Close);

        GameObject viewportObject = new GameObject("LevelViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(overlayRect, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetRect(viewportRect, new Vector2(820f, 1050f), new Vector2(0f, -20f));
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;

        RectTransform gridRect = CreateRect("LevelGrid", viewportRect);
        int levelCount = levels?.Count ?? 0;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(levelCount / 2f));
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.sizeDelta = new Vector2(800f, rowCount * 148f);
        gridRect.anchoredPosition = Vector2.zero;
        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(375f, 120f);
        grid.spacing = new Vector2(28f, 28f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = gridRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        if (levels == null || levels.Count == 0)
        {
            TMP_Text empty = CreateText("NoLevels", gridRect, "NO LEVELS CONFIGURED", 34f, Color.gray);
            empty.rectTransform.sizeDelta = new Vector2(760f, 100f);
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            int capturedIndex = i;
            LevelData level = levels[i];
            string modifiers = level == null ? string.Empty : GetModifierLabel(level);
            string label = level == null
                ? $"MISSING LEVEL\nINDEX {i}"
                : $"LEVEL {level.levelNumber}\n<size=22>{level.gridSize}x{level.gridSize} • {level.timeLimit:0}s • {modifiers}</size>";

            Button levelButton = CreateButton($"Level_{i + 1}", gridRect, label, new Color(0.07f, 0.075f, 0.13f, 1f));
            levelButton.interactable = level != null;
            levelButton.onClick.AddListener(() => SelectLevel(capturedIndex));

            Outline outline = levelButton.gameObject.AddComponent<Outline>();
            outline.effectColor = level != null ? level.outlineColor : Color.gray;
            outline.effectDistance = new Vector2(3f, -3f);
        }
    }

    private static string GetModifierLabel(LevelData level)
    {
        List<string> labels = new List<string>();
        if (level.reverseEnabled) labels.Add("REV");
        if (!Mathf.Approximately(level.rotateSpeed, 0f)) labels.Add("ROT");
        if (level.scaleEnabled) labels.Add("SCALE");
        if (level.movementEnabled) labels.Add("MOVE");
        return labels.Count == 0 ? "BASE" : string.Join("+", labels);
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text text = CreateText("Text", buttonObject.transform, label, 36f, Color.white);
        StretchToParent(text.rectTransform);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        return button;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, string value, float fontSize, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
#endif
