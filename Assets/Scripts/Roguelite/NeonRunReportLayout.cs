using UnityEngine;
using UnityEngine.UI;

/// <summary>Fits the report inside any portrait safe area and crops its full-bleed corridor.</summary>
public sealed class NeonRunReportLayout : MonoBehaviour
{
    private RectTransform artboard;
    private RawImage backdrop;
    private Vector2 previousSize, previousBackdropSize;

    public void Initialize(RawImage image)
    {
        artboard = (RectTransform)transform;
        backdrop = image;
        Fit();
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (artboard == null || !(artboard.parent is RectTransform parent)) return;
        var size = parent.rect.size;
        if (size != previousSize && size.x > 0 && size.y > 0)
        {
            artboard.localScale = Vector3.one * Mathf.Min(size.x / 1080f, size.y / 1800f);
            previousSize = size;
        }
        if (backdrop == null || backdrop.texture == null) return;
        var viewport = backdrop.rectTransform.rect.size;
        if (viewport == previousBackdropSize || viewport.x <= 0 || viewport.y <= 0) return;
        // The floor stays visible while the horizon meets the banked-earnings card.
        float height = .72f;
        float width = viewport.x / viewport.y * backdrop.texture.height * height / backdrop.texture.width;
        if (width > 1) { height /= width; width = 1; }
        backdrop.uvRect = new Rect((1 - width) * .5f, 0, width, height);
        previousBackdropSize = viewport;
    }
}
