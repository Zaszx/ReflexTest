using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonRunReportGraphic : MaskableGraphic
{
    public enum Kind
    {
        ShatteredSquare,
        EmptyHourglass,
        Card,
        PrimaryButton,
        Trophy,
        CoinStack,
        Coin
    }

    public Kind kind;
    public Color fillTint = new Color(.018f, .035f, .075f, 1f);
    public bool diagonalSheen = true;
    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper m)
    {
        m.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0)
            return;
        switch (kind)
        {
            case Kind.Card:
                Card(m, r, false);
                break;
            case Kind.PrimaryButton:
                Card(m, r, true);
                break;
            case Kind.ShatteredSquare:
                Shattered(m, r);
                break;
            case Kind.EmptyHourglass:
                Hourglass(m, r);
                break;
            case Kind.Trophy:
                Trophy(m, r);
                break;
            case Kind.CoinStack:
                Coins(m, r);
                break;
            case Kind.Coin:
                Coin(m, r);
                break;
        }
    }

    private void Card(VertexHelper m, Rect r, bool primary)
    {
        float c = Mathf.Min(20, r.height * .16f);
        var p = Chamfer(new Rect(r.xMin + 3, r.yMin + 3, r.width - 6, r.height - 6), c);
        Color top = primary ? Color.Lerp(fillTint, Color.white, .15f) : Color.Lerp(fillTint, color, .025f);
        top.a = 1;
        Color bottom = primary ? fillTint * .78f : fillTint * .38f;
        bottom.a = 1;
        GradientFan(m, p, r.center, top, bottom);
        Color edge = color;
        edge.a *= primary ? 1f : .9f;
        Glow(m, p, edge, primary ? 4f : 2.8f);
        if (diagonalSheen)
        {
            Color sheen = Color.Lerp(color, Color.white, .5f);
            sheen.a *= primary ? .038f : .003f;
            var q = new[]
            {
                new Vector2(r.xMin + 8, r.yMin + 8),
                new Vector2(r.xMin + r.width * .38f, r.yMin + 8),
                new Vector2(r.xMax - 8, r.yMax - 8),
                new Vector2(r.xMax - r.width * .38f, r.yMax - 8),
                new Vector2(r.xMin + 8, r.yMin + 8)
            };
            Fan(m, q, r.center, sheen, sheen);
        }
    }

    private void Shattered(VertexHelper m, Rect r)
    {
        float s = Mathf.Min(r.width, r.height) * .43f;
        Vector2 c = r.center;
        Color bright = color;
        bright.a *= .95f;
        var a = PolyRot(c, s, new[] { -.86f, .82f, -.15f, .82f, -.01f, .46f, -.18f, .18f, .03f, -.12f, -.24f, -.82f, -.86f, -.82f });
        var b = PolyRot(c, s, new[] { .24f, .71f, .86f, .71f, .86f, -.82f, .18f, -.82f, .06f, -.57f, .29f, -.14f, .1f, .17f, .35f, .47f });
        Glow(m, a, bright, Mathf.Max(2, s * .035f));
        Glow(m, b, bright, Mathf.Max(2, s * .035f));
        for (int i = 0; i < 3; i++)
        {
            float x = c.x + s * (i == 0 ? -1.02f : i == 1 ? 1.03f : .72f), y = c.y + s * (i == 2 ? .98f : -.96f);
            var t = PolyRot(new Vector2(x, y), s * .09f, new[] { -.5f, -.5f, .5f, -.5f, 0, .5f }, i * 12f - 8f);
            Glow(m, t, bright, Mathf.Max(1, s * .018f));
        }
    }

    private void Hourglass(VertexHelper m, Rect r)
    {
        float s = Mathf.Min(r.width, r.height) * .42f;
        Vector2 c = r.center;
        Color a = color;
        a.a *= .95f;
        var glass = Poly(c, s, new[] { -.72f, .85f, .72f, .85f, .7f, .62f, .17f, .06f, .17f, -.06f, .7f, -.62f, .72f, -.85f, -.72f, -.85f, -.7f, -.62f, -.17f, -.06f, -.17f, .06f, -.7f, .62f });
        Glow(m, glass, a, Mathf.Max(2, s * .035f));
        Glow(m, new[] { new Vector2(c.x - s * .86f, c.y + s * .95f), new Vector2(c.x + s * .86f, c.y + s * .95f) }, a, 2);
        Glow(m, new[] { new Vector2(c.x - s * .86f, c.y - s * .95f), new Vector2(c.x + s * .86f, c.y - s * .95f) }, a, 2);
        var sand = Poly(c, s, new[] { -.5f, -.73f, .5f, -.73f, 0, -.46f });
        Fan(m, sand, c + Vector2.down * s * .64f, a, a);
        for (int i = 0; i < 3; i++)
        {
            var grain = Poly(c + new Vector2((i - 1) * s * .12f, -s * .28f), s * .035f, new[] { -.5f, -.5f, .5f, -.5f, 0, .5f });
            Glow(m, grain, a, 1);
        }
    }

    private void Trophy(VertexHelper m, Rect r)
    {
        float s = Mathf.Min(r.width, r.height) * .5f;
        Vector2 c = r.center;
        Color a = color;
        a.a *= .95f;
        var cup = new[]
        {
            new Vector2(c.x - s * .5f, c.y + s * .34f),
            new Vector2(c.x + s * .5f, c.y + s * .34f),
            new Vector2(c.x + s * .32f, c.y - s * .24f),
            new Vector2(c.x - s * .32f, c.y - s * .24f),
            new Vector2(c.x - s * .5f, c.y + s * .34f)
        };
        Glow(m, cup, a, Mathf.Max(2, s * .03f));
        Glow(m, new[] { new Vector2(c.x - s * .5f, c.y + s * .23f), new Vector2(c.x - s * .76f, c.y + s * .1f), new Vector2(c.x - s * .62f, c.y - s * .15f), new Vector2(c.x - s * .32f, c.y - s * .24f) }, a, 2);
        Glow(m, new[] { new Vector2(c.x + s * .5f, c.y + s * .23f), new Vector2(c.x + s * .76f, c.y + s * .1f), new Vector2(c.x + s * .62f, c.y - s * .15f), new Vector2(c.x + s * .32f, c.y - s * .24f) }, a, 2);
        Glow(m, new[] { new Vector2(c.x - s * .1f, c.y - s * .24f), new Vector2(c.x - s * .1f, c.y - s * .52f), new Vector2(c.x + s * .1f, c.y - s * .52f), new Vector2(c.x + s * .1f, c.y - s * .24f) }, a, 2);
        Glow(m, new[] { new Vector2(c.x - s * .42f, c.y - s * .62f), new Vector2(c.x + s * .42f, c.y - s * .62f) }, a, 2);
    }

    private void Coins(VertexHelper m, Rect r)
    {
        float s = Mathf.Min(r.width, r.height);
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = r.center + new Vector2(0, (i - 1) * s * .14f);
            var e = EllipsePath(c, s * .33f, s * .11f, 24);
            Glow(m, e, color, Mathf.Max(1.5f, s * .018f));
        }
    }

    private void Coin(VertexHelper m, Rect r)
    {
        float radius = Mathf.Min(r.width, r.height) * .40f;
        var outer = EllipsePath(r.center, radius, radius, 64);
        var gold = new Color(1, .55f, .015f, 1);
        Fan(m, outer, r.center, new Color(1, .73f, .05f, 1), new Color(.72f, .29f, .006f, 1));
        Glow(m, outer, gold, 5);
        var rim = EllipsePath(r.center, radius * .76f, radius * .76f, 64);
        Glow(m, rim, new Color(1, .89f, .35f, 1), 2.2f);
        var mark = Poly(r.center, radius, new[] { .23f, .33f, -.14f, .38f, -.28f, .21f,
            -.17f, .04f, .16f, -.04f, .27f, -.21f, .12f, -.38f, -.24f, -.33f });
        // The dollar is an open S; Poly's closing segment would cross it.
        System.Array.Resize(ref mark, mark.Length - 1);
        Glow(m, mark, new Color(1, .94f, .53f, 1), 5.5f);
        Glow(m, new[] { r.center + Vector2.up * radius * .51f,
            r.center + Vector2.down * radius * .51f }, new Color(1, .94f, .53f, 1), 3.5f);
    }

    private static Vector2[] Chamfer(Rect r, float c)
    {
        return new[]
        {
            new Vector2(r.xMin + c, r.yMin),
            new Vector2(r.xMax - c, r.yMin),
            new Vector2(r.xMax, r.yMin + c),
            new Vector2(r.xMax, r.yMax - c),
            new Vector2(r.xMax - c, r.yMax),
            new Vector2(r.xMin + c, r.yMax),
            new Vector2(r.xMin, r.yMax - c),
            new Vector2(r.xMin, r.yMin + c),
            new Vector2(r.xMin + c, r.yMin)
        };
    }

    private static Vector2[] Poly(Vector2 c, float s, float[] xy)
    {
        var p = new Vector2[xy.Length / 2 + 1];
        for (int i = 0; i < p.Length - 1; i++)
            p[i] = c + new Vector2(xy[i * 2], xy[i * 2 + 1]) * s;
        p[p.Length - 1] = p[0];
        return p;
    }

    private static Vector2[] PolyRot(Vector2 c, float s, float[] xy, float degrees = -15f)
    {
        var p = Poly(Vector2.zero, s, xy);
        float a = degrees * Mathf.Deg2Rad;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 q = p[i];
            p[i] = c + new Vector2(q.x * Mathf.Cos(a) - q.y * Mathf.Sin(a), q.x * Mathf.Sin(a) + q.y * Mathf.Cos(a));
        }

        return p;
    }

    private static Vector2[] EllipsePath(Vector2 c, float rx, float ry, int n)
    {
        var p = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2 / n;
            p[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }

        return p;
    }

    private static void Fan(VertexHelper m, Vector2[] p, Vector2 center, Color middle, Color edge)
    {
        int s = m.currentVertCount;
        m.AddVert(center, middle, Vector2.zero);
        for (int i = 0; i < p.Length; i++)
        {
            m.AddVert(p[i], edge, Vector2.zero);
            if (i > 0)
                m.AddTriangle(s, s + i, s + i + 1);
        }
    }

    private static void GradientFan(VertexHelper m, Vector2[] p, Vector2 center, Color top, Color bottom)
    {
        int s = m.currentVertCount;
        Color mid = Color.Lerp(bottom, top, .5f);
        m.AddVert(center, mid, Vector2.zero);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < p.Length; i++)
        {
            min = Mathf.Min(min, p[i].y);
            max = Mathf.Max(max, p[i].y);
        }

        for (int i = 0; i < p.Length; i++)
        {
            float t = Mathf.InverseLerp(min, max, p[i].y);
            m.AddVert(p[i], Color.Lerp(bottom, top, t), Vector2.zero);
            if (i > 0)
                m.AddTriangle(s, s + i, s + i + 1);
        }
    }

    private static void Glow(VertexHelper m, Vector2[] p, Color c, float width)
    {
        width = Mathf.Max(.5f, width);
        Color clear = c;
        clear.a = 0;
        Color far = c;
        far.a *= .0015f;
        Color near = c;
        near.a *= .025f;
        Color edge = c;
        edge.a *= .30f;
        Color core = Color.Lerp(c, Color.white, .24f);
        core.a = c.a;
        Band(m, p, width * 5f, width * 9f, far, clear);
        Band(m, p, width * 2.8f, width * 5f, near, far);
        Band(m, p, width * 1.4f, width * 2.8f, edge, near);
        Band(m, p, width * .65f, width * 1.4f, core, edge);
        Stroke(m, p, core, width * .65f);
    }

    private static void Band(VertexHelper m, Vector2[] p, float inner, float outer, Color inside, Color outside)
    {
        int s = m.currentVertCount;
        bool closed = (p[0] - p[p.Length - 1]).sqrMagnitude < .001f;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = i > 0 ? p[i] - p[i - 1] : closed ? p[0] - p[p.Length - 2] : p[1] - p[0], b = i < p.Length - 1 ? p[i + 1] - p[i] : closed ? p[1] - p[0] : a;
            a.Normalize();
            b.Normalize();
            Vector2 n = new Vector2(-a.y - b.y, a.x + b.x).normalized;
            n /= Mathf.Max(.4f, Vector2.Dot(n, new Vector2(-b.y, b.x))) * 2;
            m.AddVert(p[i] + n * outer, outside, Vector2.zero);
            m.AddVert(p[i] + n * inner, inside, Vector2.zero);
            m.AddVert(p[i] - n * inner, inside, Vector2.zero);
            m.AddVert(p[i] - n * outer, outside, Vector2.zero);
            if (i > 0)
            {
                int v = s + i * 4;
                m.AddTriangle(v - 4, v, v + 1);
                m.AddTriangle(v - 4, v + 1, v - 3);
                m.AddTriangle(v - 2, v + 2, v + 3);
                m.AddTriangle(v - 2, v + 3, v - 1);
            }
        }
    }

    private static void Stroke(VertexHelper m, Vector2[] p, Color c, float width)
    {
        int s = m.currentVertCount;
        bool closed = (p[0] - p[p.Length - 1]).sqrMagnitude < .001f;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = i > 0 ? p[i] - p[i - 1] : closed ? p[0] - p[p.Length - 2] : p[1] - p[0], b = i < p.Length - 1 ? p[i + 1] - p[i] : closed ? p[1] - p[0] : a;
            a.Normalize();
            b.Normalize();
            Vector2 n = new Vector2(-a.y - b.y, a.x + b.x).normalized;
            Vector2 d = n * (width * .5f / Mathf.Max(.4f, Vector2.Dot(n, new Vector2(-b.y, b.x))));
            m.AddVert(p[i] + d, c, Vector2.zero);
            m.AddVert(p[i] - d, c, Vector2.zero);
            if (i > 0)
            {
                int v = s + i * 2;
                m.AddTriangle(v - 2, v, v + 1);
                m.AddTriangle(v - 2, v + 1, v - 1);
            }
        }
    }
}
