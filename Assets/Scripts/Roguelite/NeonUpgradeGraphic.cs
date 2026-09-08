using UnityEngine;
using UnityEngine.UI;

/// <summary>Small shop-only vector ornament renderer. It owns no animation or layout.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonUpgradeGraphic : MaskableGraphic
{
    public enum Kind { Card, Socket, Health, Reserve, Stabilizer, Reverse, Coin }
    public Kind kind;
    [Range(0f, 2f)] public float intensity = 1f;
    [Range(0f, .5f)] public float surfaceTint = .07f;

    protected override void OnEnable()
    {
        base.OnEnable(); raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        Color accent = color; accent.a *= Mathf.Clamp01(intensity);
        switch (kind)
        {
            case Kind.Card: Card(mesh, r, accent, surfaceTint); break;
            case Kind.Socket: Socket(mesh, r, accent); break;
            case Kind.Health: Heart(mesh, r, accent); break;
            case Kind.Reserve: Hourglass(mesh, r, accent); break;
            case Kind.Stabilizer: Stabilizer(mesh, r, accent); break;
            case Kind.Reverse: Reverse(mesh, r, accent); break;
            case Kind.Coin: Coin(mesh, r, accent); break;
        }
    }

    private static Color Dark(Color a, float amount, float alpha = 1f)
    { Color c = Color.Lerp(new Color(.018f, .027f, .075f, 1f), a, amount); c.a *= alpha; return c; }

    private static void Card(VertexHelper m, Rect r, Color a, float tint)
    {
        r = Inset(r, 10f);
        float chamfer = Mathf.Min(24f, Mathf.Min(r.width, r.height) * .16f);
        Gradient(m, Chamfer(r, chamfer), r, a, tint);
        Glow(m, Chamfer(r, chamfer), a, 1.5f, .75f);
    }

    private static void Socket(VertexHelper m, Rect r, Color a)
    {
        r = Inset(r, 11f);
        float chamfer = Mathf.Min(26f, Mathf.Min(r.width, r.height) * .19f);
        Gradient(m, Chamfer(r, chamfer), r, a, .11f);
        Glow(m, Chamfer(r, chamfer), a, 2f, .95f);
    }

    private static Vector2[] Chamfer(Rect r, float c)
    {
        return new[] { new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMax,r.yMin+c),new Vector2(r.xMax,r.yMax-c),new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMin,r.yMax-c),new Vector2(r.xMin,r.yMin+c),new Vector2(r.xMin+c,r.yMin) };
    }

    private static void Heart(VertexHelper m, Rect r, Color a)
    {
        var points = new Vector2[65];
        Vector2[] anchors={new Vector2(0,-.35f),new Vector2(-.36f,.23f),new Vector2(0,.22f),new Vector2(.36f,.23f),new Vector2(0,-.35f)};
        Vector2[] controls={new Vector2(-.11f,-.28f),new Vector2(-.5f,.02f),new Vector2(-.3f,.45f),new Vector2(-.09f,.47f),new Vector2(.09f,.47f),new Vector2(.3f,.45f),new Vector2(.5f,.02f),new Vector2(.11f,-.28f)};
        for(int section=0;section<4;section++)
            for(int i=0;i<=16;i++)
            {
                float t=i/16f,u=1-t;
                Vector2 p=u*u*u*anchors[section]+3*u*u*t*controls[section*2]+3*u*t*t*controls[section*2+1]+t*t*t*anchors[section+1];
                points[section*16+i]=r.center+new Vector2(p.x*r.width,p.y*r.height);
            }
        Glow(m, points, a, 3.5f, 1f);
    }

    private static void Hourglass(VertexHelper m, Rect r, Color a)
    {
        float w=r.width*.34f,h=r.height*.38f; Vector2 c=r.center;
        Glow(m,new[]{c+new Vector2(-w,h),c+new Vector2(w,h)},a,4f,1f);
        Glow(m,new[]{c+new Vector2(-w,-h),c+new Vector2(w,-h)},a,4f,1f);
        var left = new Vector2[25]; var right = new Vector2[25];
        for(int i=0;i<25;i++)
        {
            float t=i/24f; float x=w*(.14f+.69f*Mathf.Pow(Mathf.Abs(2*t-1),.72f));
            left[i]=c+new Vector2(-x,Mathf.Lerp(h-4,-h+4,t));
            right[i]=c+new Vector2(x,Mathf.Lerp(h-4,-h+4,t));
        }
        Glow(m,left,a,3.5f,1f); Glow(m,right,a,3.5f,1f);
        Glow(m,new[]{c+new Vector2(-w*.45f,-h*.68f),c+new Vector2(0,-h*.42f),c+new Vector2(w*.45f,-h*.68f)},a,2.5f,.75f);
    }

    private static void Stabilizer(VertexHelper m, Rect r, Color a)
    {
        Vector2 c=r.center; float s=Mathf.Min(r.width,r.height)*.21f, reach=s*1.8f;
        Glow(m,new[]{c+new Vector2(-s,-s),c+new Vector2(s,-s),c+new Vector2(s,s),c+new Vector2(-s,s),c+new Vector2(-s,-s)},a,4f,1f);
        Glow(m,new[]{c+new Vector2(-s*.5f,reach),c+new Vector2(s*.5f,reach)},a,4f,1f);
        Glow(m,new[]{c+new Vector2(-s*.5f,-reach),c+new Vector2(s*.5f,-reach)},a,4f,1f);
        Glow(m,new[]{c+new Vector2(-reach,-s*.5f),c+new Vector2(-reach,s*.5f)},a,4f,1f);
        Glow(m,new[]{c+new Vector2(reach,-s*.5f),c+new Vector2(reach,s*.5f)},a,4f,1f);
    }

    private static void Reverse(VertexHelper m, Rect r, Color a)
    {
        float w=Mathf.Max(2f,Mathf.Min(r.width,r.height)*.04f), y=r.center.y, x=r.width*.28f, head=r.width*.1f;
        Glow(m,new[]{new Vector2(r.center.x-x,y+r.height*.16f),new Vector2(r.center.x+x,y+r.height*.16f)},a,w,1f);
        Glow(m,new[]{new Vector2(r.center.x-x,y-r.height*.16f),new Vector2(r.center.x+x,y-r.height*.16f)},a,w,1f);
        Glow(m,new[]{new Vector2(r.center.x-x+head,y+r.height*.16f+head),new Vector2(r.center.x-x,y+r.height*.16f),new Vector2(r.center.x-x+head,y+r.height*.16f-head)},a,w,1f);
        Glow(m,new[]{new Vector2(r.center.x+x-head,y-r.height*.16f+head),new Vector2(r.center.x+x,y-r.height*.16f),new Vector2(r.center.x+x-head,y-r.height*.16f-head)},a,w,1f);
    }

    private static void Coin(VertexHelper m, Rect r, Color a)
    {
        Vector2 c=r.center; float radius=Mathf.Min(r.width,r.height)*.38f;
        int start=m.currentVertCount; m.AddVert(c,Color.Lerp(a,Color.white,.5f),Vector2.zero);
        for(int i=0;i<=48;i++)
        {
            float angle=Mathf.PI*2*i/48; Vector2 p=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            m.AddVert(p,Color.Lerp(a,new Color(.84f,.41f,.04f),.45f),Vector2.zero);
            if(i>0)m.AddTriangle(start,start+i,start+i+1);
        }
        Circle(m,c,radius,a,48,1f); Circle(m,c,radius*.74f,Color.Lerp(a,Color.white,.7f),48,.9f);
        Color mark=new Color(.62f,.29f,.025f,1);
        Vector2[] s={new Vector2(.3f,.38f),new Vector2(-.16f,.4f),new Vector2(-.3f,.22f),new Vector2(-.17f,.04f),new Vector2(.18f,-.05f),new Vector2(.28f,-.23f),new Vector2(.13f,-.4f),new Vector2(-.3f,-.36f)};
        for(int i=0;i<s.Length;i++)s[i]=c+s[i]*radius;
        Stroke(m,s,mark,1.3f,1f); Stroke(m,new[]{c+Vector2.up*radius*.53f,c+Vector2.down*radius*.53f},mark,1f,.9f);
    }

    private static void Circle(VertexHelper m, Vector2 c, float radius, Color col, int n, float alpha)
    { var p=new Vector2[n+1]; for(int i=0;i<=n;i++){float t=Mathf.PI*2*i/n;p[i]=c+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*radius;} Color x=col;x.a*=alpha;Stroke(m,p,x,Mathf.Max(1f,radius*.08f),1f); }
    private static void Stroke(VertexHelper m, Vector2[] p, Color col, float width, float alpha)
    {
        Color c=col;c.a*=alpha; int start=m.currentVertCount;
        bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;
        for(int i=0;i<p.Length;i++)
        {
            Vector2 before=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0];
            Vector2 after=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:before;
            before.Normalize(); after.Normalize();
            Vector2 normal=new Vector2(-before.y-after.y,before.x+after.x).normalized;
            float denominator=Mathf.Max(.35f,Vector2.Dot(normal,new Vector2(-after.y,after.x)));
            Vector2 offset=normal*(width*.5f/denominator);
            m.AddVert(p[i]+offset,c,Vector2.zero); m.AddVert(p[i]-offset,c,Vector2.zero);
            if(i>0){int v=start+i*2;m.AddTriangle(v-2,v,v+1);m.AddTriangle(v-2,v+1,v-1);}
        }
    }
    private static Rect Inset(Rect r,float n) => new Rect(r.xMin+n,r.yMin+n,Mathf.Max(0,r.width-2*n),Mathf.Max(0,r.height-2*n));
    private static void Glow(VertexHelper m,Vector2[] p,Color a,float width,float strength)
    {
        Stroke(m,p,a,width+16,.025f*strength); Stroke(m,p,a,width+11,.04f*strength);
        Stroke(m,p,a,width+7,.07f*strength); Stroke(m,p,a,width+3,.16f*strength);
        Stroke(m,p,a,width,strength);
        Color core=Color.Lerp(a,Color.white,.45f);core.a=a.a;
        Stroke(m,p,core,width*.35f,strength*.8f);
    }
    private static void Gradient(VertexHelper m,Vector2[] p,Rect r,Color a,float tint)
    {
        int start=m.currentVertCount; m.AddVert(r.center,Dark(a,tint > .15f ? tint*.65f : .015f),Vector2.zero);
        for(int i=0;i<p.Length;i++)
        {
            float side=Mathf.Abs((p[i].x-r.center.x)/Mathf.Max(1,r.width*.5f));
            m.AddVert(p[i],Dark(a,Mathf.Lerp(.025f,tint,side)),Vector2.zero);
            if(i>0)m.AddTriangle(start,start+i,start+i+1);
        }
    }
}
