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
        c.pressedColor = new Color(.73f,.78f,.69f); c.disabledColor = new Color(.52f,.58f,.56f); c.fadeDuration = NeonTheme.T.pressDuration; b.colors = c;
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
    private RectTransform label; private Vector2 origin; private bool pressed;
    void Awake() { label = GetComponentInChildren<TMP_Text>()?.rectTransform; if(label!=null) origin = label.anchoredPosition; }
    public void OnPointerDown(PointerEventData e) { if(GetComponent<Button>().interactable) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnPointerExit(PointerEventData e) { pressed = false; }
    void Update()
    {
        if(label==null)return;
        Vector2 target=origin+(pressed&&!NeonTheme.ReducedEffects?new Vector2(4,-2):Vector2.zero);
        if((label.anchoredPosition-target).sqrMagnitude<.0001f)return;
        label.anchoredPosition=Vector2.Lerp(label.anchoredPosition,target,1-Mathf.Exp(-35*Time.unscaledDeltaTime));
    }
    void OnDisable() { pressed=false; if(label!=null) label.anchoredPosition=origin; }
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

/// <summary>A short, nonblocking panel fade. Never owns input locks or simulation time.</summary>
public sealed class NeonPanelEntrance : MonoBehaviour
{
    private CanvasGroup group; private float elapsed;
    void Awake() { group = gameObject.AddComponent<CanvasGroup>(); }
    void OnEnable() { elapsed = 0; if(group!=null) group.alpha = NeonTheme.ReducedEffects ? 1 : .35f; }
    void Update() { if(group==null || group.alpha>=1) return; elapsed+=Time.unscaledDeltaTime; group.alpha=Mathf.Lerp(.35f,1,Mathf.Clamp01(elapsed/NeonTheme.T.enterDuration)); }
    void OnDisable() { if(group!=null) group.alpha=1; }
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
        elapsed+=Time.unscaledDeltaTime;
        transform.localRotation=basis*Quaternion.Euler(0,0,Mathf.Sin(elapsed*.32f)*4);
    }
}
