using UnityEngine;
using UnityEngine.UI;

/// <summary>Small procedural surfaces for onboarding. They are decorative and never receive input.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonOnboardingGraphic : MaskableGraphic
{
    public enum Kind { ChamferPanel, ChamferFrame, LimeButton, Brackets, Circle }
    public Kind kind;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;
        switch (kind)
        {
            case Kind.ChamferPanel: DrawPanel(mesh, rect, color); break;
            case Kind.ChamferFrame: DrawFrame(mesh, rect, color); break;
            case Kind.LimeButton: DrawButton(mesh, rect, color); break;
            case Kind.Brackets: DrawBrackets(mesh, rect, color); break;
            case Kind.Circle: DrawCircle(mesh, rect, color); break;
        }
    }

    private static void DrawPanel(VertexHelper mesh, Rect rect, Color accent)
    {
        Vector2[] points = Chamfer(Inset(rect, 7f), Mathf.Min(28f, rect.height * .2f));
        Color inside = new Color(.015f, .055f, .08f, 1f);
        Fan(mesh, points, rect.center, inside);
        Color glow = accent; glow.a *= .14f;
        Stroke(mesh, points, glow, 11f);
        Stroke(mesh, points, accent, 3f);
        Stroke(mesh, InsetPoints(points, rect.center, 11f), new Color(accent.r, accent.g, accent.b, accent.a * .18f), 1f);
    }

    private static void DrawFrame(VertexHelper mesh, Rect rect, Color accent)
    {
        Vector2[] points = Chamfer(Inset(rect, 6f), Mathf.Min(22f, rect.height * .25f));
        Stroke(mesh, points, accent, 2.5f);
    }

    private static void DrawButton(VertexHelper mesh, Rect rect, Color lime)
    {
        Vector2[] points = Chamfer(Inset(rect, 5f), Mathf.Min(22f, rect.height * .2f));
        Fan(mesh, points, rect.center, Color.Lerp(lime, Color.white, .12f));
        Stroke(mesh, points, lime, 3f);
    }

    private static void DrawBrackets(VertexHelper mesh, Rect rect, Color accent)
    {
        float corner = Mathf.Min(34f, Mathf.Min(rect.width, rect.height) * .28f);
        float inset = 2f;
        Vector2 bl = new Vector2(rect.xMin + inset, rect.yMin + inset);
        Vector2 br = new Vector2(rect.xMax - inset, rect.yMin + inset);
        Vector2 tr = new Vector2(rect.xMax - inset, rect.yMax - inset);
        Vector2 tl = new Vector2(rect.xMin + inset, rect.yMax - inset);
        Line(mesh, bl, bl + Vector2.up * corner, accent, 4f); Line(mesh, bl, bl + Vector2.right * corner, accent, 4f);
        Line(mesh, br, br + Vector2.up * corner, accent, 4f); Line(mesh, br, br + Vector2.left * corner, accent, 4f);
        Line(mesh, tr, tr + Vector2.down * corner, accent, 4f); Line(mesh, tr, tr + Vector2.left * corner, accent, 4f);
        Line(mesh, tl, tl + Vector2.down * corner, accent, 4f); Line(mesh, tl, tl + Vector2.right * corner, accent, 4f);
    }

    private static void DrawCircle(VertexHelper mesh, Rect rect, Color accent)
    {
        const int segments = 40;
        float radius = Mathf.Min(rect.width, rect.height) * .5f - 3f;
        Vector2 center = rect.center;
        Vector2 previous = center + Vector2.right * radius;
        Color glow = accent; glow.a *= .18f;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Line(mesh, previous, point, glow, 9f);
            Line(mesh, previous, point, accent, 3f);
            previous = point;
        }
    }

    private static Rect Inset(Rect rect, float amount) => new Rect(rect.xMin + amount, rect.yMin + amount, rect.width - amount * 2f, rect.height - amount * 2f);
    private static Vector2[] Chamfer(Rect rect, float cut) => new[]
    {
        new Vector2(rect.xMin + cut, rect.yMin), new Vector2(rect.xMax - cut, rect.yMin),
        new Vector2(rect.xMax, rect.yMin + cut), new Vector2(rect.xMax, rect.yMax - cut),
        new Vector2(rect.xMax - cut, rect.yMax), new Vector2(rect.xMin + cut, rect.yMax),
        new Vector2(rect.xMin, rect.yMax - cut), new Vector2(rect.xMin, rect.yMin + cut),
        new Vector2(rect.xMin + cut, rect.yMin)
    };
    private static Vector2[] InsetPoints(Vector2[] points, Vector2 center, float amount)
    {
        var inset = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
            inset[i] = Vector2.MoveTowards(points[i], center, amount);
        return inset;
    }
    private static void Fan(VertexHelper mesh, Vector2[] points, Vector2 center, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i < points.Length; i++)
        {
            mesh.AddVert(points[i], tint, Vector2.zero);
            if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
    private static void Stroke(VertexHelper mesh, Vector2[] points, Color tint, float width)
    {
        for (int i = 1; i < points.Length; i++)
            Line(mesh, points[i - 1], points[i], tint, width);
    }
    private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color tint, float width)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * (width * .5f);
        int start = mesh.currentVertCount;
        mesh.AddVert(a - normal, tint, Vector2.zero);
        mesh.AddVert(a + normal, tint, Vector2.zero);
        mesh.AddVert(b + normal, tint, Vector2.zero);
        mesh.AddVert(b - normal, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
