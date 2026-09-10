using UnityEngine;
using UnityEngine.UI;

/// <summary>Edge-only mesh on the existing flash Image; the center stays fully transparent.</summary>
public sealed class NeonTapVignette : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        mesh.Clear();
        Rect r = graphic.rectTransform.rect;
        float depth = Mathf.Min(r.width, r.height) * NeonMotion.T.vignetteEdgeWidth;
        Color outer = graphic.color;
        Color inner = outer; inner.a = 0;
        // Each side is a trapezoid, sharing corners without overlapping bright wedges.
        Vector2 a = new Vector2(r.xMin, r.yMin), b = new Vector2(r.xMin, r.yMax);
        Vector2 c = new Vector2(r.xMax, r.yMax), d = new Vector2(r.xMax, r.yMin);
        Vector2 ai = a + new Vector2(depth, depth), bi = b + new Vector2(depth, -depth);
        Vector2 ci = c + new Vector2(-depth, -depth), di = d + new Vector2(-depth, depth);
        Side(mesh, a, b, bi, ai, outer, inner);
        Side(mesh, b, c, ci, bi, outer, inner);
        Side(mesh, c, d, di, ci, outer, inner);
        Side(mesh, d, a, ai, di, outer, inner);
    }
    private static void Side(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color outer, Color inner)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(a, outer, Vector2.zero); mesh.AddVert(b, outer, Vector2.zero);
        mesh.AddVert(c, inner, Vector2.zero); mesh.AddVert(d, inner, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }
}
