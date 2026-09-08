using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class NeonStyle
{
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    public static void Fill(RectTransform r, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f,.5f);
        r.offsetMin = new Vector2(left,bottom); r.offsetMax = new Vector2(-right,-top);
    }
    public static void At(RectTransform r, float ax, float ay, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(ax,ay); r.pivot = new Vector2(.5f,.5f);
        r.sizeDelta = new Vector2(w,h); r.anchoredPosition = new Vector2(x,y);
    }
    public static void Box(RectTransform r, float x0, float y0, float x1, float y1, float pad = 0)
    {
        r.anchorMin = new Vector2(x0,y0); r.anchorMax = new Vector2(x1,y1);
        r.offsetMin = new Vector2(pad,pad); r.offsetMax = new Vector2(-pad,-pad);
    }
    public static Image Panel(string name, Transform parent, Color color, bool blocks = false)
    {
        var rect = Rect(name,parent); var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = blocks; return image;
    }
    public static TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var rect = Rect(name,parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (NeonTheme.T.font != null) text.font = NeonTheme.T.font;
        text.text = value; text.fontSize = size; text.color = color; text.alignment = align;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis; text.extraPadding = true;
        return text;
    }
    public static TMP_Text Label(string name, Transform parent, string value)
    {
        var t = Text(name,parent,value,NeonTheme.T.labelSize,NeonTheme.T.Muted);
        t.characterSpacing = 2; return t;
    }
    public static Button Button(string name, Transform parent, string label, bool primary = false)
    {
        var bg = Panel(name,parent,primary ? NeonTheme.T.Primary : NeonTheme.T.Raised,true);
        var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
        var c = b.colors; c.normalColor = c.highlightedColor = c.selectedColor = Color.white;
        c.pressedColor = new Color(.73f,.78f,.69f); c.disabledColor = new Color(.52f,.58f,.56f); c.fadeDuration = NeonMotion.T.pressDuration; b.colors = c;
        b.transition = Selectable.Transition.None; // NeonPressFeedback owns the tint clock.
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var t = Text("Label",b.transform,label,NeonTheme.T.bodySize,primary ? NeonTheme.T.Background : NeonTheme.T.Text);
        t.fontStyle = FontStyles.Bold; Fill(t.rectTransform,32,0,32,0);
        b.gameObject.AddComponent<NeonPressFeedback>(); return b;
    }
    public static void ButtonText(Button button,string value)
    { if (button != null) button.GetComponentInChildren<TMP_Text>(true).text = value; }
    public static NeonShape Shape(string name, Transform parent, NeonShape.Kind kind, Color color, float thickness = 3)
    {
        var r = Rect(name,parent); r.gameObject.AddComponent<CanvasRenderer>();
        var s = r.gameObject.AddComponent<NeonShape>(); s.kind = kind; s.color = color; s.thickness = thickness; s.raycastTarget = false; return s;
    }
    public static Image Rule(string name, Transform parent, Color? color = null)
    { return Panel(name,parent,color ?? NeonTheme.T.Raised); }
}

/// <summary>Moves only the button's label; target rectangles stay fixed for fast taps.</summary>
public sealed class NeonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private RectTransform label;
    private Button button;
    private Vector2 origin, from, target;
    private float elapsed, duration;
    private bool moving;
    private bool pressed;
    private CanvasRenderer tintRenderer;
    private Color tint = Color.white, tintFrom = Color.white, tintTo = Color.white;
    private float tintElapsed, tintDuration;
    void Awake()
    {
        label = GetComponentInChildren<TMP_Text>()?.rectTransform;
        button = GetComponent<Button>();
        if (button != null && button.targetGraphic != null) tintRenderer = button.targetGraphic.canvasRenderer;
        if (label != null) origin = label.anchoredPosition;
    }
    public void OnPointerDown(PointerEventData e) { if(button != null && button.IsInteractable()) Retarget(true); }
    // Builders can finish their icon/text layout after this component's Awake.
    public void CaptureRestPose()
    {
        if (label == null) return;
        origin = from = target = label.anchoredPosition;
        moving = pressed = false;
    }
    public void OnPointerUp(PointerEventData e) => Retarget(false);
    public void OnPointerExit(PointerEventData e) => Retarget(false);
    private void Retarget(bool pressed)
    {
        if (label == null) return;
        this.pressed = pressed;
        from = label.anchoredPosition;
        target = origin + (pressed && !NeonTheme.ReducedEffects ? NeonMotion.T.pressOffset : Vector2.zero);
        duration = Mathf.Max(0, pressed ? NeonMotion.T.pressDuration : NeonMotion.T.releaseDuration);
        elapsed = 0;
        moving = true;
        if (duration <= 0) { label.anchoredPosition = target; moving = false; }
    }
    void Update()
    {
        float delta = NeonMotion.Delta();
        if (button != null && tintRenderer != null)
        {
            Color desired = !button.IsInteractable() ? button.colors.disabledColor :
                pressed ? button.colors.pressedColor : button.colors.normalColor;
            if (desired != tintTo)
            {
                tintFrom = tint;
                tintTo = desired;
                tintElapsed = 0;
                tintDuration = Mathf.Max(0, pressed ? NeonMotion.T.pressDuration : NeonMotion.T.releaseDuration);
            }
            if (tint != tintTo)
            {
                tintElapsed += delta;
                float colorT = tintDuration <= 0 ? 1 : Mathf.Clamp01(tintElapsed / tintDuration);
                tint = Color.Lerp(tintFrom, tintTo, NeonMotion.Ease(colorT));
                tintRenderer.SetColor(tint);
            }
        }
        if (moving && label != null)
        {
            elapsed += delta;
            float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
            label.anchoredPosition = Vector2.Lerp(from, target, NeonMotion.Ease(t));
            if (t >= 1) { label.anchoredPosition = target; moving = false; }
        }
    }
    void OnDisable()
    {
        moving = pressed = false;
        if(label!=null) label.anchoredPosition=origin;
        tint = tintFrom = tintTo = Color.white;
        if(tintRenderer!=null) tintRenderer.SetColor(Color.white);
    }
}

/// <summary>The ON/OFF value changes immediately; a separate accent acknowledges the toggle.</summary>
public sealed class NeonToggleFeedback : MonoBehaviour
{
    private Toggle toggle;
    private Image accent;
    private float elapsed;
    private bool moving;
    public void Initialize(Toggle owner)
    {
        toggle = owner;
        accent = NeonMotionAccent.Underline("ToggleAccepted", (RectTransform)transform, NeonTheme.T.Primary);
        toggle.onValueChanged.AddListener(OnChanged);
    }
    private void OnChanged(bool value)
    {
        elapsed = 0;
        moving = true;
        Render();
    }
    private void Update()
    {
        if (!moving) return;
        elapsed += NeonMotion.Delta();
        Render();
    }
    private void Render()
    {
        float duration = Mathf.Max(0, NeonMotion.T.releaseDuration);
        float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
        NeonMotionAccent.Alpha(accent, (1 - NeonMotion.Ease(t)) * NeonMotionAccent.Strength);
        if (t >= 1) moving = false;
    }
    private void OnDisable() { moving = false; NeonMotionAccent.Alpha(accent, 0); }
    private void OnDestroy() { if(toggle!=null) toggle.onValueChanged.RemoveListener(OnChanged); }
}

/// <summary>Recomputes only on physical viewport/inset changes. QA overrides are editor-only.</summary>
public sealed class NeonSafeArea : MonoBehaviour
{
    private Rect previous; private Vector2 size;
#if UNITY_EDITOR
    public static Rect? PreviewSafeArea;
#endif
    void OnEnable() { Apply(); }
    void Update() { Apply(); }
    private void Apply()
    {
        Rect area = Screen.safeArea;
#if UNITY_EDITOR
        if(PreviewSafeArea.HasValue) area = PreviewSafeArea.Value;
#endif
        Vector2 dimensions = new Vector2(Screen.width,Screen.height);
        if (dimensions.x < 1 || dimensions.y < 1 || (area == previous && dimensions == size)) return;
        previous = area; size = dimensions;
        var r = (RectTransform)transform; r.anchorMin = area.min / dimensions; r.anchorMax = area.max / dimensions;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}

/// <summary>Quiet menu-only optical motion; it is never attached to the gameplay grid.</summary>
public sealed class NeonOpticalMotion : MonoBehaviour
{
    private Quaternion basis;
    private float elapsed;
    void Awake() { basis=transform.localRotation; }
    void Update()
    {
        if(NeonTheme.ReducedEffects) { transform.localRotation=basis; return; }
        elapsed+=NeonMotion.Delta();
        transform.localRotation=basis*Quaternion.Euler(0,0,Mathf.Sin(elapsed*.32f)*4);
    }
}
