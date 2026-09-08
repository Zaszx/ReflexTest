using TMPro;
using UnityEngine;

/// <summary>Presentation tokens only. Never used by run, timer or hit-test rules.</summary>
[CreateAssetMenu(menuName = "NeonReflex/Presentation Theme")]
public sealed class NeonTheme : ScriptableObject
{
    public Color Background = Hex("101A1B");
    public Color Surface = Hex("192628");
    public Color Raised = Hex("253537");
    public Color Text = Hex("F0F2E8");
    public Color Muted = Hex("9EAFAD");
    public Color Primary = Hex("D8F85C");
    public Color Target = Hex("9AD9DD");
    public Color Reverse = Hex("B7A2FF");
    public Color Reserve = Hex("FFCB75");
    public Color Danger = Hex("FF806D");
    [Header("Typography / 1080 reference width")]
    public TMP_FontAsset font;
    public float displaySize = 144;
    public float headingSize = 84;
    public float bodySize = 32;
    public float labelSize = 24;
    [Header("Geometry and motion")]
    public float pageMargin = 64;
    public float ruleWidth = 2;
    public float targetStrokeWidth = 6;
    [Range(0,1)] public float cellBoundaryOpacity = .18f;
    public float controlHeight = 112;
    private static NeonTheme instance;
    public static NeonTheme T
    {
        get
        {
            if (instance == null) instance = Resources.Load<NeonTheme>("NeonPresentationTheme");
            if (instance == null) { instance = CreateInstance<NeonTheme>(); instance.hideFlags = HideFlags.DontSave; }
            return instance;
        }
    }
    private static bool? reduced;
    public static bool ReducedEffects
    {
        get { if (!reduced.HasValue) reduced = PlayerPrefs.GetInt("NeonReducedEffects", 0) == 1; return reduced.Value; }
        set
        {
            reduced = value;
#if UNITY_EDITOR
            if (NeonPresentationQAProfile.IsActive) return;
#endif
            PlayerPrefs.SetInt("NeonReducedEffects", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    // Keep level variety on targets, while the HUD retains fixed semantic colors.
    public static Color LevelTarget(Color levelColor) => Color.Lerp(T.Target, levelColor, .22f);
}
