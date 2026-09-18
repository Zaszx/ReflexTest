using UnityEngine;
using UnityEngine.UI;

public partial class GameSquare
{
    private Image smileyImage;
    private bool smileyActive;
    private float smileyElapsed, smileyStartAlpha;

    private void InitializeSmiley()
    {
        if (smileyImage != null) return;
        var child = new GameObject("TapSmiley", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(bgImage.transform, false);
        // The face stays inside the cell and behind the role-defining outlines.
        child.transform.SetAsFirstSibling();
        smileyImage = child.GetComponent<Image>();
        smileyImage.raycastTarget = false;
        smileyImage.preserveAspect = true;
        smileyImage.material = NeonSmileyArt.Material;
        ClearSmiley();
    }

    private void PlaySmiley(bool correct)
    {
        if (smileyImage == null) return;
        Sprite sprite = NeonSmileyArt.Select(correct);
        if (sprite == null) return;
        smileyStartAlpha = smileyActive ? smileyImage.color.a : 0f;
        smileyElapsed = 0f;
        smileyActive = true;
        smileyImage.sprite = sprite;
        float inset = (1f - Mathf.Clamp01(NeonMotion.T.smileyCellFraction)) * .5f;
        RectTransform rect = smileyImage.rectTransform;
        rect.anchorMin = Vector2.one * inset;
        rect.anchorMax = Vector2.one * (1f - inset);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        smileyImage.color = new Color(1, 1, 1, smileyStartAlpha);
        smileyImage.gameObject.SetActive(true);
    }

    private void AdvanceSmiley(float delta)
    {
        if (!smileyActive) return;
        smileyElapsed += delta;
        float fadeIn = Mathf.Max(.01f, NeonMotion.T.smileyFadeInDuration);
        float fadeOut = Mathf.Max(.01f, NeonMotion.T.smileyFadeOutDuration);
        float holdEnd = fadeIn + Mathf.Max(0f, NeonMotion.T.smileyHoldDuration);
        float alpha = smileyElapsed < fadeIn
            ? Mathf.Lerp(smileyStartAlpha, 1f, Mathf.SmoothStep(0f, 1f, smileyElapsed / fadeIn))
            : 1f - Mathf.SmoothStep(0f, 1f, (smileyElapsed - holdEnd) / fadeOut);
        if (terminalPresentation) alpha *= 1f - terminalShutdown;
        smileyImage.color = new Color(1, 1, 1, alpha);
        if (smileyElapsed >= holdEnd + fadeOut) ClearSmiley();
    }

    private void ClearSmiley()
    {
        smileyActive = false;
        if (smileyImage == null) return;
        smileyImage.color = Color.clear;
        smileyImage.gameObject.SetActive(false);
    }
}

/// <summary>Shared artwork and decorative-only selection. Never touches gameplay RNG.</summary>
internal static class NeonSmileyArt
{
    private static readonly System.Random random = new System.Random();
    private static Sprite[] right;
    private static Sprite wrong;
    private static Material material;
    private static int lastRight = -1;

    public static Material Material
    {
        get
        {
            if (material == null)
                material = new Material(Resources.Load<Shader>("NeonSmileyWhiteKey"))
                    { name = "Smiley white-background removal", hideFlags = HideFlags.HideAndDontSave };
            return material;
        }
    }

    public static Sprite Select(bool correct)
    {
        if (right == null)
        {
            right = new Sprite[4];
            // Authored canvases have different padding. Crop the cached sprite
            // geometry (including glow margin), leaving source pixels intact.
            right[0] = Load("right1", new Rect(106, 153, 316, 205));
            right[1] = Load("right2", new Rect(90, 186, 332, 156));
            right[2] = Load("right3", new Rect(106, 58, 296, 198));
            right[3] = Load("right4", new Rect(106, 297, 300, 142));
            wrong = Load("wrong", new Rect(38, 58, 432, 379));
        }
        if (!correct) return wrong;
        int next = lastRight < 0 ? random.Next(right.Length) : (lastRight + 1 + random.Next(right.Length - 1)) % right.Length;
        lastRight = next;
        return right[next];
    }

    private static Sprite Load(string name, Rect rect)
    {
        Sprite source = Resources.Load<Sprite>("FeedbackSmileys/" + name);
        if (source == null) return null;
        Sprite cropped = Sprite.Create(source.texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        cropped.name = name;
        cropped.hideFlags = HideFlags.HideAndDontSave;
        return cropped;
    }
}
