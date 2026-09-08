using UnityEngine;
using UnityEngine.UI;

/// <summary>Menu-only vector lettering, controls and luminous square frames.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonMenuGraphic : MaskableGraphic
{
    public enum Kind { Button, Frame, Play, Bars, Gear, Plus, NeonWord, ReflexWord }
    public Kind kind;
    [Range(0, 1)] public float fillTint = .08f;
    public float strokeWidth = 3f;
    protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float side = Mathf.Min(r.width, r.height);
        switch (kind)
        {
            case Kind.Button:
                Rect inner = new Rect(r.xMin + 13, r.yMin + 13, r.width - 26, r.height - 26);
                var edge = Rounded(inner, Mathf.Min(20, inner.height * .16f));
                Fan(mesh, edge, inner.center, Tint(fillTint * .45f), Tint(fillTint));
                Glow(mesh, edge, color, 2.6f);
                break;
            case Kind.Frame:
                Glow(mesh, Rounded(new Rect(r.xMin+15,r.yMin+15,r.width-30,r.height-30), 1), color, strokeWidth);
                break;
            case Kind.NeonWord: Word(mesh, r, "NEON"); break;
            case Kind.ReflexWord: Word(mesh, r, "REFLEX"); break;
            case Kind.Play:
                var tri = Map(r, new[] {new Vector2(-.28f,-.4f),new Vector2(-.28f,.4f),new Vector2(.4f,0),new Vector2(-.28f,-.4f)});
                Fan(mesh, tri, r.center, color, color); Glow(mesh, tri, color, 1.7f); break;
            case Kind.Bars:
                for (int i=0;i<3;i++)
                {
                    Rect bar = new Rect(r.center.x+side*(-.39f+i*.29f),r.center.y-side*.37f,side*.19f,side*(.32f+i*.22f));
                    var p=Rounded(bar,1); Fan(mesh,p,bar.center,color,color); Glow(mesh,p,color,1.3f);
                }
                break;
            case Kind.Plus:
                Glow(mesh,Map(r,new[]{new Vector2(-.33f,0),new Vector2(.33f,0)}),color,3.5f);
                Glow(mesh,Map(r,new[]{new Vector2(0,-.33f),new Vector2(0,.33f)}),color,3.5f); break;
            case Kind.Gear:
                var teeth=new Vector2[65]; var hole=new Vector2[65];
                for(int i=0;i<=64;i++)
                {
                    float a=i*Mathf.PI*2/64;
                    float radius=(i%8>=2&&i%8<=5)?.44f:.34f;
                    teeth[i]=r.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*side*radius;
                    hole[i]=r.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*side*.17f;
                }
                Ring(mesh,teeth,hole,color); Glow(mesh,teeth,color,1); break;
        }
    }

    private Color Tint(float amount)
    { Color result=Color.Lerp(new Color(.008f,.024f,.04f,1),color,amount);result.a=.94f;return result; }

    private void Word(VertexHelper m, Rect r, string word)
    {
        float gap=r.width*.035f, width=(r.width-gap*(word.Length-1))/word.Length;
        float height=r.height*.70f, baseline=r.center.y-height*.5f;
        for(int i=0;i<word.Length;i++)
        {
            Rect cell=new Rect(r.xMin+i*(width+gap)+7,baseline,width-14,height);
            float line=r.height*.105f;
            System.Action<Vector2[]> path = p =>
            {
                var points=new Vector2[p.Length];
                for(int j=0;j<p.Length;j++)points[j]=new Vector2(cell.xMin+p[j].x*cell.width,cell.yMin+p[j].y*cell.height);
                Glow(m,points,color,line);
            };
            switch(word[i])
            {
                case 'N': path(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,0),new Vector2(1,1)});break;
                case 'E':
                    path(new[]{new Vector2(1,1),new Vector2(0,1),new Vector2(0,0),new Vector2(1,0)});
                    path(new[]{new Vector2(0,.5f),new Vector2(.9f,.5f)});break;
                case 'O': Glow(m,Rounded(cell,cell.height*.25f),color,line);break;
                case 'R':
                    path(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(.83f,1),new Vector2(1,.85f),new Vector2(1,.65f),new Vector2(.83f,.5f),new Vector2(0,.5f)});
                    path(new[]{new Vector2(.45f,.5f),new Vector2(1,0)});break;
                case 'F': path(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1)});path(new[]{new Vector2(0,.5f),new Vector2(.86f,.5f)});break;
                case 'L':path(new[]{new Vector2(0,1),new Vector2(0,0),new Vector2(1,0)});break;
                case 'X':path(new[]{new Vector2(0,0),new Vector2(1,1)});path(new[]{new Vector2(0,1),new Vector2(1,0)});break;
            }
        }
    }

    private static Vector2[] Map(Rect r, Vector2[] p)
    { float side=Mathf.Min(r.width,r.height);for(int i=0;i<p.Length;i++)p[i]=r.center+p[i]*side;return p; }
    private static Vector2[] Rounded(Rect r,float radius)
    {
        var p=new Vector2[33];
        for(int corner=0;corner<4;corner++)
        {
            Vector2 c=corner==0?new Vector2(r.xMax-radius,r.yMax-radius):corner==1?new Vector2(r.xMin+radius,r.yMax-radius):corner==2?new Vector2(r.xMin+radius,r.yMin+radius):new Vector2(r.xMax-radius,r.yMin+radius);
            for(int step=0;step<8;step++)
            {
                float angle=(corner*90+step*90f/7)*Mathf.Deg2Rad;
                p[corner*8+step]=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            }
        }
        p[32]=p[0];return p;
    }
    private static void Fan(VertexHelper m,Vector2[] p,Vector2 center,Color middle,Color edge)
    {
        int start=m.currentVertCount; m.AddVert(center,middle,Vector2.zero);
        for(int i=0;i<p.Length;i++){m.AddVert(p[i],edge,Vector2.zero);if(i>0)m.AddTriangle(start,start+i,start+i+1);}
    }
    private static void Ring(VertexHelper m,Vector2[] outside,Vector2[] inside,Color tint)
    {
        int start=m.currentVertCount;
        for(int i=0;i<outside.Length;i++)
        {
            m.AddVert(outside[i],tint,Vector2.zero);m.AddVert(inside[i],tint,Vector2.zero);
            if(i>0){int v=start+i*2;m.AddTriangle(v-2,v,v+1);m.AddTriangle(v-2,v+1,v-1);}
        }
    }
    private static void Glow(VertexHelper m,Vector2[] p,Color color,float width)
    {
        Color clear=color;clear.a=0;
        Color far=color;far.a*=.035f;
        Color near=color;near.a*=.16f;
        Color edge=color;edge.a*=.6f;
        Color core=Color.Lerp(color,Color.white,.55f);core.a=color.a;
        Band(m,p,width+18,width+36,far,clear);
        Band(m,p,width+7,width+18,near,far);
        Band(m,p,width+1,width+7,edge,near);
        Band(m,p,width*.65f,width+1,core,edge);
        Stroke(m,p,core,width*.65f,1);
    }
    // Continuous vertex-alpha falloff avoids visible steps in wide glow strokes.
    private static void Band(VertexHelper m,Vector2[] p,float inner,float outer,Color inside,Color outside)
    {
        int start=m.currentVertCount;bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;
        for(int i=0;i<p.Length;i++)
        {
            Vector2 before=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0];
            Vector2 after=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:before;
            before.Normalize();after.Normalize();Vector2 n=new Vector2(-before.y-after.y,before.x+after.x).normalized;
            n/=Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-after.y,after.x)))*2;
            m.AddVert(p[i]+n*outer,outside,Vector2.zero);m.AddVert(p[i]+n*inner,inside,Vector2.zero);
            m.AddVert(p[i]-n*inner,inside,Vector2.zero);m.AddVert(p[i]-n*outer,outside,Vector2.zero);
            if(i>0){int v=start+i*4;m.AddTriangle(v-4,v,v+1);m.AddTriangle(v-4,v+1,v-3);m.AddTriangle(v-2,v+2,v+3);m.AddTriangle(v-2,v+3,v-1);}
        }
    }
    private static void Stroke(VertexHelper m,Vector2[] p,Color color,float width,float alpha)
    {
        color.a*=alpha; int start=m.currentVertCount; bool closed=(p[0]-p[p.Length-1]).sqrMagnitude<.001f;
        for(int i=0;i<p.Length;i++)
        {
            Vector2 before=i>0?p[i]-p[i-1]:closed?p[0]-p[p.Length-2]:p[1]-p[0];
            Vector2 after=i<p.Length-1?p[i+1]-p[i]:closed?p[1]-p[0]:before;
            before.Normalize();after.Normalize();
            Vector2 n=new Vector2(-before.y-after.y,before.x+after.x).normalized;
            float denom=Mathf.Max(.4f,Vector2.Dot(n,new Vector2(-after.y,after.x)));
            Vector2 d=n*(width*.5f/denom);
            m.AddVert(p[i]+d,color,Vector2.zero);m.AddVert(p[i]-d,color,Vector2.zero);
            if(i>0){int v=start+i*2;m.AddTriangle(v-2,v,v+1);m.AddTriangle(v-2,v+1,v-1);}
        }
    }
}
