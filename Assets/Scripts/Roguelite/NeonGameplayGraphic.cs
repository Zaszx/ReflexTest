using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedural, non-interactive HUD decoration. Values are supplied by the UI owner.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonGameplayGraphic : MaskableGraphic
{
    public enum Kind { Panel, Board, Heart, Grid, Target, Home, Pause, Clock, Floor, Segments }
    public Kind kind;
    public Color fillTint = new Color(.018f, .035f, .075f, 1f);
    public bool glowPerimeter;
    [Range(0, 1)] public float value = 1f;
    [Min(1)] public int segments = 13;

    protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
    protected override void OnPopulateMesh(VertexHelper m)
    {
        m.Clear(); Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float side = Mathf.Min(r.width, r.height);
        switch (kind)
        {
            case Kind.Panel:
                var panel = Chamfer(new Rect(r.xMin + 4, r.yMin + 4, r.width - 8, r.height - 8), Mathf.Min(18, side * .12f));
                PanelFill(m, panel, r);
                Color quiet = color; quiet.a *= .22f; Stroke(m, panel, quiet, Mathf.Max(1f, side * .006f));
                if (glowPerimeter) { Color glow = color; glow.a *= .6f; Glow(m, panel, glow, 2f); }
                Glow(m, new[] { panel[6] + Vector2.down * Mathf.Min(62, side * .55f), panel[6], panel[5] }, color, 2.6f); break;
            case Kind.Board:
                var board = Chamfer(new Rect(r.xMin + 7, r.yMin + 7, r.width - 14, r.height - 14), Mathf.Min(24, side * .12f));
                Stroke(m, board, Subdued(color), 1.2f);
                float len = Mathf.Min(55f, side * .16f), inset = Mathf.Min(18f, side * .06f), bw = 2.2f;
                Corner(m, new Vector2(r.xMin + inset, r.yMax - inset), new Vector2(1, -1), len, color, bw);
                Corner(m, new Vector2(r.xMax - inset, r.yMax - inset), new Vector2(-1, -1), len, color, bw);
                Corner(m, new Vector2(r.xMin + inset, r.yMin + inset), new Vector2(1, 1), len, color, bw);
                Corner(m, new Vector2(r.xMax - inset, r.yMin + inset), new Vector2(-1, 1), len, color, bw); break;
            case Kind.Heart: Heart(m, r, color); break;
            case Kind.Grid: Grid(m, r, color); break;
            case Kind.Target: Target(m, r, color); break;
            case Kind.Home: Home(m, r, color); break;
            case Kind.Pause: Pause(m, r, color); break;
            case Kind.Clock: Clock(m, r, color); break;
            case Kind.Floor: Floor(m, r, color); break;
            case Kind.Segments: Segments(m, r, color); break;
        }
    }

    private Color Dark(float amount, float alpha)
    { Color c = Color.Lerp(fillTint, color, amount); c.a *= alpha; return c; }
    private void PanelFill(VertexHelper m, Vector2[] points, Rect r)
    {
        int start = m.currentVertCount; m.AddVert(r.center, Dark(.018f, .98f), Vector2.zero);
        for (int i = 0; i < points.Length; i++)
        {
            float t = Mathf.InverseLerp(r.yMin, r.yMax, points[i].y);
            Color tint = Color.Lerp(fillTint * .38f, Dark(.01f, 1), t); tint.a = .98f;
            m.AddVert(points[i], tint, Vector2.zero);
            if (i > 0) m.AddTriangle(start, start + i, start + i + 1);
        }
    }
    private Color Subdued(Color c) { c.a *= .28f; return c; }
    private static Vector2[] Chamfer(Rect r, float c)
    { return new[] { new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMax,r.yMin+c),new Vector2(r.xMax,r.yMax-c),new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMin,r.yMax-c),new Vector2(r.xMin,r.yMin+c),new Vector2(r.xMin+c,r.yMin) }; }
    private static Vector2[] Box(Rect r) { return new[] { new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin) }; }
    private static Vector2[] Map(Rect r, Vector2[] p, float scale = 1f)
    { float s=Mathf.Min(r.width,r.height)*scale; for(int i=0;i<p.Length;i++) p[i]=r.center+p[i]*s; return p; }
    private static void Heart(VertexHelper m, Rect r, Color c)
    { var p=Map(r,new[]{new Vector2(-.0f,-.47f),new Vector2(-.42f,.03f),new Vector2(-.37f,.34f),new Vector2(-.15f,.47f),new Vector2(0,.31f),new Vector2(.15f,.47f),new Vector2(.37f,.34f),new Vector2(.42f,.03f),new Vector2(0,-.47f)}); Fan(m,p,r.center,c,c); Glow(m,p,c,Mathf.Max(2,r.height*.035f)); }
    private static void Grid(VertexHelper m, Rect r, Color c)
    { float s=Mathf.Min(r.width,r.height)*.19f, gap=s*.32f; for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++){Rect b=new Rect(r.center.x+x*(s+gap)-s*.5f,r.center.y+y*(s+gap)-s*.5f,s,s);Fan(m,Box(b),b.center,c,c);Glow(m,Box(b),c,Mathf.Max(1,s*.08f));} }
    private static void Target(VertexHelper m, Rect r, Color c)
    { for(int i=0;i<3;i++){float s=.72f-i*.19f; Glow(m,Map(r,new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f),new Vector2(-.5f,-.5f)},s),c,Mathf.Max(1.5f,r.height*.025f));} }
    private static void Home(VertexHelper m, Rect r, Color c)
    {
        float s = Mathf.Min(r.width, r.height), y = r.center.y + s * .02f;
        float left = r.center.x - s * .36f, right = r.center.x + s * .36f, bottom = r.center.y - s * .36f, door = s * .20f;
        var roof = new[] { new Vector2(r.center.x - s * .48f, y), new Vector2(r.center.x, r.center.y + s * .43f), new Vector2(r.center.x + s * .48f, y), new Vector2(r.center.x - s * .48f, y) };
        Fan(m, roof, new Vector2(r.center.x, y + s * .1f), c, c);
        Quad(m, new Rect(left, bottom, (right-left-door)*.5f, y-bottom), c);
        Quad(m, new Rect(r.center.x+door*.5f, bottom, (right-left-door)*.5f, y-bottom), c);
        Quad(m, new Rect(r.center.x-door*.5f, y-s*.11f, door, s*.11f), c);
        Glow(m, roof, c, 1.3f);
    }
    private static void Pause(VertexHelper m, Rect r, Color c)
    { float w=Mathf.Min(r.width,r.height)*.18f; for(int i=-1;i<=1;i+=2){Rect b=new Rect(r.center.x+i*w*.9f-w*.5f,r.center.y-r.height*.32f,w,r.height*.64f);var p=Chamfer(b,w*.18f);Fan(m,p,b.center,c,c);Glow(m,p,c,Mathf.Max(1.5f,w*.08f));} }
    private void Clock(VertexHelper m, Rect r, Color c)
    { int n=48; var track=Circle(r.center,Mathf.Min(r.width,r.height)*.36f,n); Color muted=c; muted.a*=.22f; Stroke(m,track,muted,Mathf.Max(2,r.height*.035f)); int used=Mathf.RoundToInt(Mathf.Clamp01(value)*n); if(used>0){var arc=Arc(r.center,Mathf.Min(r.width,r.height)*.36f,used,-Mathf.PI*.5f);Glow(m,arc,c,Mathf.Max(2,r.height*.045f));} }
    private static void Floor(VertexHelper m, Rect r, Color c)
    {
        Color horizon = c, foreground = c; horizon.a *= .0008f; foreground.a *= .008f;
        for (int i = 1; i <= 6; i++)
        {
            float t = i / 6f; float y = Mathf.Lerp(r.yMax, r.yMin, t * t);
            Color tint = Color.Lerp(horizon, foreground, t);
            Stroke(m, new[] { new Vector2(r.xMin, y), new Vector2(r.xMax, y) }, tint, 2);
        }
        for (int i = -6; i <= 6; i++)
        {
            float x = r.center.x + i * r.width / 6f;
            Vector2 top = new Vector2(r.center.x + (x-r.center.x)*.12f, r.yMax), bottom = new Vector2(x, r.yMin);
            Vector2 normal = new Vector2(-(bottom-top).y, (bottom-top).x).normalized;
            int s = m.currentVertCount;
            m.AddVert(top-normal, horizon, Vector2.zero); m.AddVert(top+normal, horizon, Vector2.zero);
            m.AddVert(bottom+normal, foreground, Vector2.zero); m.AddVert(bottom-normal, foreground, Vector2.zero);
            m.AddTriangle(s,s+1,s+2); m.AddTriangle(s,s+2,s+3);
        }
    }
    private void Segments(VertexHelper m, Rect r, Color c)
    { int count=Mathf.Max(1,segments); float gap=Mathf.Max(1,r.width*.012f), w=(r.width-gap*(count-1))/count; int lit=Mathf.RoundToInt(Mathf.Clamp01(value)*count); for(int i=0;i<count;i++){Rect b=new Rect(r.xMin+i*(w+gap),r.yMin,w,r.height);Color fill=i<lit?c:fillTint;fill.a*=i<lit?1f:.7f;Fan(m,Box(b),b.center,fill,fill);if(i<lit)Glow(m,Box(b),c,Mathf.Max(1,r.height*.05f));} }
    private static Vector2[] Circle(Vector2 center,float radius,int count,float start=0)
    { var p=new Vector2[Mathf.Max(2,count)+1]; for(int i=0;i<p.Length;i++){float a=start+Mathf.PI*2*i/count;p[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;} return p; }
    private static Vector2[] Arc(Vector2 center,float radius,int count,float start)
    { var p=new Vector2[Mathf.Max(1,count)+1]; float sweep=Mathf.PI*2*Mathf.Clamp01(count/48f); for(int i=0;i<p.Length;i++){float a=start+sweep*i/(p.Length-1);p[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;} return p; }
    private static void Corner(VertexHelper m,Vector2 origin,Vector2 direction,float length,Color c,float width)
    { Glow(m,new[]{origin,origin+new Vector2(direction.x*length,0)},c,width); Glow(m,new[]{origin,origin+new Vector2(0,direction.y*length)},c,width); }
    private static void Quad(VertexHelper m,Rect r,Color c)
    { m.AddVert(new Vector2(r.xMin,r.yMin),c,Vector2.zero);m.AddVert(new Vector2(r.xMax,r.yMin),c,Vector2.zero);m.AddVert(new Vector2(r.xMax,r.yMax),c,Vector2.zero);m.AddVert(new Vector2(r.xMin,r.yMax),c,Vector2.zero);int s=m.currentVertCount-4;m.AddTriangle(s,s+1,s+2);m.AddTriangle(s,s+2,s+3); }
    private static void QuadGradient(VertexHelper m,Vector2[] p,Color low,Color high)
    { int s=m.currentVertCount;m.AddVert(p[0],low,Vector2.zero);m.AddVert(p[1],low,Vector2.zero);m.AddVert(p[2],high,Vector2.zero);m.AddVert(p[3],high,Vector2.zero);m.AddTriangle(s,s+1,s+2);m.AddTriangle(s,s+2,s+3); }
    private static void Fan(VertexHelper m,Vector2[] p,Vector2 center,Color middle,Color edge)
    { int s=m.currentVertCount;m.AddVert(center,middle,Vector2.zero);for(int i=0;i<p.Length;i++){m.AddVert(p[i],edge,Vector2.zero);if(i>0)m.AddTriangle(s,s+i,s+i+1);} }
    private static void Glow(VertexHelper m,Vector2[] p,Color c,float width)
    { Color clear=c;clear.a=0;Color far=c;far.a*=.025f;Color near=c;near.a*=.14f;Color edge=c;edge.a*=.55f;Color core=Color.Lerp(c,Color.white,.45f);core.a=c.a;Band(m,p,width*5,width*9,far,clear);Band(m,p,width*2.8f,width*5,near,far);Band(m,p,width*1.4f,width*2.8f,edge,near);Band(m,p,width*.65f,width*1.4f,core,edge);Stroke(m,p,core,width*.65f); }
    private static void Band(VertexHelper m,Vector2[] p,float inner,float outer,Color inside,Color outside)
    { int s=m.currentVertCount;bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;for(int i=0;i<p.Length;i++){Vector2 a=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0],b=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:a;a.Normalize();b.Normalize();Vector2 n=new Vector2(-a.y-b.y,a.x+b.x).normalized;n/=Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-b.y,b.x)))*2;m.AddVert(p[i]+n*outer,outside,Vector2.zero);m.AddVert(p[i]+n*inner,inside,Vector2.zero);m.AddVert(p[i]-n*inner,inside,Vector2.zero);m.AddVert(p[i]-n*outer,outside,Vector2.zero);if(i>0){int v=s+i*4;m.AddTriangle(v-4,v,v+1);m.AddTriangle(v-4,v+1,v-3);m.AddTriangle(v-2,v+2,v+3);m.AddTriangle(v-2,v+3,v-1);}} }
    private static void Stroke(VertexHelper m,Vector2[] p,Color c,float width)
    { int s=m.currentVertCount;bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;for(int i=0;i<p.Length;i++){Vector2 a=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0],b=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:a;a.Normalize();b.Normalize();Vector2 n=new Vector2(-a.y-b.y,a.x+b.x).normalized;float d=Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-b.y,b.x)));Vector2 off=n*(width*.5f/d);m.AddVert(p[i]+off,c,Vector2.zero);m.AddVert(p[i]-off,c,Vector2.zero);if(i>0){int v=s+i*2;m.AddTriangle(v-2,v,v+1);m.AddTriangle(v-2,v+1,v-1);}} }
}
