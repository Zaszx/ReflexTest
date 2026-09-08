using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared analytic UI rectangle rendering; never changes layout or hit geometry.</summary>
public static class NeonGridRendering
{
    private static Material shared;
    public static Material Material
    {
        get
        {
            if (shared == null) shared = Resources.Load<Material>("NeonGridAntialias");
            return shared != null && shared.shader != null && shared.shader.isSupported ? shared : null;
        }
    }

    public static void EnsureChannels(Canvas canvas)
    {
        if (canvas != null && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public static void Populate(VertexHelper mesh, Rect rect, Color color, float lineWidth, float cornerFraction)
    {
        mesh.Clear();
        if (rect.width <= 0 || rect.height <= 0 || lineWidth == 0) return;
        Vector2 half = rect.size * .5f;
        float width = lineWidth < 0 ? -1 : Mathf.Min(lineWidth, Mathf.Min(half.x, half.y));
        // Transparent raster padding lets the shader cover both sides of an
        // edge. This is mesh-only padding, not a RectTransform/hitbox change.
        // It covers minified phone/editor views and the smallest target role.
        float padding = Mathf.Max(16f, Mathf.Max(0, width));
        Vector2 extent = half + Vector2.one * padding;
        Vector4 shape = new Vector4(half.x, half.y, width, Mathf.Clamp01(cornerFraction));
        Add(mesh, rect.center, new Vector2(-extent.x, -extent.y), color, shape);
        Add(mesh, rect.center, new Vector2(-extent.x, extent.y), color, shape);
        Add(mesh, rect.center, new Vector2(extent.x, extent.y), color, shape);
        Add(mesh, rect.center, new Vector2(extent.x, -extent.y), color, shape);
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }

    private static void Add(VertexHelper mesh, Vector2 center, Vector2 point, Color32 color, Vector4 shape)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = center + point;
        vertex.color = color;
        vertex.uv0 = new Vector4(point.x, point.y, 0, 0);
        vertex.uv1 = shape;
        mesh.AddVert(vertex);
    }
}

