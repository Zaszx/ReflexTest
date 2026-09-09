using UnityEngine;
using UnityEngine.UI;

/// <summary>Static vector ornaments for the pause and settings screen.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonSettingsGraphic : MaskableGraphic
{
    public enum Kind { Panel, IconPlate, Haptic, Reduced, Toggle, PrimaryButton, SecondaryButton, Slashes }
    public Kind kind;
    [Range(0, 1)] public float value;

    protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }

    protected override void OnPopulateMesh(VertexHelper m)
    {
        m.Clear(); Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        Color accent = color; Color navy = new Color(.012f, .031f, .046f, 1f);
        switch (kind)
        {
            case Kind.Panel: Panel(m, r, navy, accent); break;
            case Kind.IconPlate: Plate(m, r, navy, accent); break;
            case Kind.Haptic: Haptic(m, r, accent); break;
            case Kind.Reduced: Reduced(m, r, new Color(1f, .22f, .65f, accent.a)); break;
            case Kind.Toggle: Toggle(m, r); break;
            case Kind.PrimaryButton: Button(m, r, true, accent); break;
            case Kind.SecondaryButton: Button(m, r, false, accent); break;
            case Kind.Slashes: Slashes(m, r, accent); break;
        }
    }

    private static void Panel(VertexHelper m, Rect r, Color navy, Color accent)
    {
        var p = Chamfer(Inset(r, 8), Mathf.Min(34, r.width * .035f));
        Fan(m, p, r.center, new Color(.018f, .041f, .056f, 1f), navy);
        Glow(m, p, accent, 2.2f);
    }

    private static void Plate(VertexHelper m, Rect r, Color navy, Color accent)
    {
        var p = Chamfer(Inset(r, 4), Mathf.Min(15, r.width * .14f));
        Fan(m, p, r.center, new Color(navy.r, navy.g, navy.b, .96f), navy);
        Glow(m, p, accent, 1.35f);
    }

    private static void Haptic(VertexHelper m, Rect r, Color accent)
    {
        float s = Mathf.Min(r.width, r.height), x = r.center.x, y = r.center.y;
        Vector2[] p = { new Vector2(x-s*.43f,y), new Vector2(x-s*.24f,y), new Vector2(x-s*.13f,y+s*.24f), new Vector2(x+s*.01f,y-s*.28f), new Vector2(x+s*.14f,y+s*.12f), new Vector2(x+s*.24f,y), new Vector2(x+s*.43f,y) };
        Glow(m, p, accent, Mathf.Max(1.5f, s * .035f));
    }

    private static void Reduced(VertexHelper m, Rect r, Color pink)
    {
        float s = Mathf.Min(r.width, r.height) * .36f;
        for (int i = 0; i < 11; i++)
        {
            float a = Mathf.Lerp(-75, 250, i / 10f) * Mathf.Deg2Rad;
            Dot(m, r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, Mathf.Lerp(2f, 4.2f, i / 10f), pink);
        }
    }

    private void Toggle(VertexHelper m, Rect r)
    {
        float t = Mathf.Clamp01(value); Color dark = new Color(.032f, .073f, .112f, color.a); Color lime = color;
        Color fill = Color.Lerp(dark, lime, t);
        Rect inset = Inset(r, 3);
        var p = Rounded(inset, Mathf.Min(inset.height, inset.width) * .5f);
        Fan(m, p, r.center, fill, fill);
        Glow(m, p, Color.Lerp(new Color(.12f,.23f,.31f,color.a), lime, t), Mathf.Lerp(1.2f, 2.6f, t), .15f * t);
        float knob = r.height * .35f, x = Mathf.Lerp(r.xMin + r.height * .5f, r.xMax - r.height * .5f, t);
        Color white = Color.white; white.a = color.a;
        Dot(m, new Vector2(x, r.center.y), knob, white);
    }

    private static void Button(VertexHelper m, Rect r, bool primary, Color accent)
    {
        var p = Chamfer(Inset(r, 4), Mathf.Min(25, r.height * .19f));
        if (primary)
        {
            Color lime = accent;
            Fan(m, p, r.center, Color.Lerp(lime, Color.white, .12f), lime);
            Glow(m, p, lime, 3.3f);
            Color facet = Color.white; facet.a = lime.a * .07f;
            Quad(m, new Vector2(r.xMin + r.width*.55f,r.yMin+5), new Vector2(r.xMax-5,r.yMin+5), new Vector2(r.xMax-5,r.yMax-5), new Vector2(r.xMin+r.width*.73f,r.yMax-5), facet);
        }
        else
        {
            Color fill = new Color(.008f, .027f, .07f, .76f);
            Fan(m, p, r.center, fill, fill); Glow(m, p, accent, 1.8f);
        }
    }

    private static void Slashes(VertexHelper m, Rect r, Color accent)
    {
        float s = Mathf.Min(r.width, r.height);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 c = r.center + Vector2.right * i * s * .28f;
            Vector2[] p = { c + new Vector2(-s*.10f,-s*.27f), c + new Vector2(s*.02f,-s*.27f), c + new Vector2(s*.10f,s*.27f), c + new Vector2(-s*.02f,s*.27f), c + new Vector2(-s*.10f,-s*.27f) };
            Fan(m, p, c, accent, accent); Glow(m, p, accent, 1.1f);
        }
    }

    private static Rect Inset(Rect r, float d) { return new Rect(r.xMin + d, r.yMin + d, r.width - d * 2, r.height - d * 2); }
    private static Vector2[] Chamfer(Rect r, float c) { return new[] { new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMax,r.yMin+c),new Vector2(r.xMax,r.yMax-c),new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMin,r.yMax-c),new Vector2(r.xMin,r.yMin+c),new Vector2(r.xMin+c,r.yMin) }; }
    private static Vector2[] Rounded(Rect r, float rad)
    {
        var p = new Vector2[49];
        for (int corner = 0; corner < 4; corner++) for (int step = 0; step < 12; step++)
        {
            Vector2 c = corner == 0 ? new Vector2(r.xMax-rad,r.yMax-rad) : corner == 1 ? new Vector2(r.xMin+rad,r.yMax-rad) : corner == 2 ? new Vector2(r.xMin+rad,r.yMin+rad) : new Vector2(r.xMax-rad,r.yMin+rad);
            float a = (corner * 90 + step * 90f / 12) * Mathf.Deg2Rad; p[corner*12+step] = c + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad;
        }
        p[48] = p[0]; return p;
    }
    private static void Dot(VertexHelper m, Vector2 c, float radius, Color tint)
    {
        const int count = 40; int start = m.currentVertCount;
        Color clear = tint; clear.a = 0;
        m.AddVert(c, tint, Vector2.zero);
        for (int i = 0; i <= count; i++)
        {
            float a = i * Mathf.PI * 2 / count; var unit = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            m.AddVert(c + unit * Mathf.Max(0, radius - .5f), tint, Vector2.zero);
            m.AddVert(c + unit * (radius + 1.1f), clear, Vector2.zero);
            if (i == 0) continue;
            int v = start + 1 + i * 2;
            m.AddTriangle(start, v - 2, v); m.AddTriangle(v - 2, v - 1, v + 1); m.AddTriangle(v - 2, v + 1, v);
        }
    }
    private static void Quad(VertexHelper m, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint) { int s=m.currentVertCount; m.AddVert(a,tint,Vector2.zero);m.AddVert(b,tint,Vector2.zero);m.AddVert(c,tint,Vector2.zero);m.AddVert(d,tint,Vector2.zero);m.AddTriangle(s,s+1,s+2);m.AddTriangle(s,s+2,s+3); }
    private static void Fan(VertexHelper m, Vector2[] p, Vector2 c, Color center, Color edge) { int s=m.currentVertCount; m.AddVert(c,center,Vector2.zero); for(int i=0;i<p.Length;i++){m.AddVert(p[i],edge,Vector2.zero);if(i>0)m.AddTriangle(s,s+i,s+i+1);} }
    private static void Glow(VertexHelper m, Vector2[] p, Color c, float width, float whiteCore = .35f)
    {
        c.a *= .92f; Color faint=c; faint.a*=.08f; Color clear=c; clear.a=0;
        Color core = Color.Lerp(c, Color.white, whiteCore); core.a = c.a;
        Band(m,p,width*2,width*6,faint,clear); Band(m,p,width,width*2,c,faint); Stroke(m,p,core,width);
    }
    private static void Band(VertexHelper m, Vector2[] p, float inner, float outer, Color inside, Color outside)
    {
        int s=m.currentVertCount; bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;
        for(int i=0;i<p.Length;i++) { Vector2 a=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0], b=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:a; a.Normalize();b.Normalize();Vector2 n=new Vector2(-a.y-b.y,a.x+b.x).normalized; n/=Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-b.y,b.x)))*2; m.AddVert(p[i]+n*outer,outside,Vector2.zero);m.AddVert(p[i]+n*inner,inside,Vector2.zero);m.AddVert(p[i]-n*inner,inside,Vector2.zero);m.AddVert(p[i]-n*outer,outside,Vector2.zero);if(i>0){int v=s+i*4;m.AddTriangle(v-4,v,v+1);m.AddTriangle(v-4,v+1,v-3);m.AddTriangle(v-2,v+2,v+3);m.AddTriangle(v-2,v+3,v-1);} }
    }
    private static void Stroke(VertexHelper m, Vector2[] p, Color c, float width)
    {
        int s=m.currentVertCount; bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;
        for(int i=0;i<p.Length;i++) { Vector2 a=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0], b=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:a; a.Normalize();b.Normalize();Vector2 n=new Vector2(-a.y-b.y,a.x+b.x).normalized; Vector2 d=n*(width*.5f/Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-b.y,b.x))));m.AddVert(p[i]+d,c,Vector2.zero);m.AddVert(p[i]-d,c,Vector2.zero);if(i>0){int v=s+i*2;m.AddTriangle(v-2,v,v+1);m.AddTriangle(v-2,v+1,v-1);} }
    }
}
