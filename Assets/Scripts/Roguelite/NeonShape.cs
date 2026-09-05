using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector family. No textures, runtime network or raycast surfaces.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonShape : MaskableGraphic
{
    public enum Kind { Frame, Reticle, Arrow, Health, Reserve, Stabilizer, Reverse, Check, Pause, Home }
    public Kind kind;
    public float thickness = 3;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect r=rectTransform.rect; float side=Mathf.Min(r.width,r.height);
        Vector2 c=r.center;
        System.Action<Vector2,Vector2> line=(a,b)=>Line(vh,c+a*side,c+b*side,thickness);
        switch(kind)
        {
            case Kind.Frame: Frame(vh,r,thickness); break;
            case Kind.Reticle:
                Frame(vh,new Rect(c-Vector2.one*side*.38f,Vector2.one*side*.76f),thickness);
                Frame(vh,new Rect(c+new Vector2(-.16f,-.24f)*side,Vector2.one*side*.40f),thickness);
                Frame(vh,new Rect(c+new Vector2(.02f,-.1f)*side,Vector2.one*side*.15f),thickness);
                line(new Vector2(-.5f,0),new Vector2(-.44f,0)); line(new Vector2(.44f,0),new Vector2(.5f,0));
                line(new Vector2(0,-.5f),new Vector2(0,-.44f)); line(new Vector2(0,.44f),new Vector2(0,.5f)); break;
            case Kind.Arrow: line(new Vector2(-.35f,0),new Vector2(.35f,0)); line(new Vector2(.07f,.28f),new Vector2(.35f,0)); line(new Vector2(.07f,-.28f),new Vector2(.35f,0)); break;
            case Kind.Health: line(new Vector2(-.34f,0),new Vector2(.34f,0)); line(new Vector2(0,-.34f),new Vector2(0,.34f)); Frame(vh,new Rect(c-Vector2.one*side*.46f,Vector2.one*side*.92f),thickness*.5f); break;
            case Kind.Reserve: line(new Vector2(-.32f,.38f),new Vector2(.32f,.38f)); line(new Vector2(-.32f,-.38f),new Vector2(.32f,-.38f)); line(new Vector2(-.26f,.3f),new Vector2(.26f,-.3f)); line(new Vector2(.26f,.3f),new Vector2(-.26f,-.3f)); break;
            case Kind.Stabilizer: Frame(vh,new Rect(c-Vector2.one*side*.23f,Vector2.one*side*.46f),thickness); line(new Vector2(-.46f,.15f),new Vector2(-.46f,-.15f)); line(new Vector2(.46f,.15f),new Vector2(.46f,-.15f)); line(new Vector2(-.15f,.46f),new Vector2(.15f,.46f)); line(new Vector2(-.15f,-.46f),new Vector2(.15f,-.46f)); break;
            case Kind.Reverse: line(new Vector2(-.36f,.16f),new Vector2(.36f,.16f)); line(new Vector2(-.36f,.16f),new Vector2(-.14f,.38f)); line(new Vector2(-.36f,-.16f),new Vector2(.36f,-.16f)); line(new Vector2(.36f,-.16f),new Vector2(.14f,-.38f)); break;
            case Kind.Check: line(new Vector2(-.34f,0),new Vector2(-.1f,-.25f)); line(new Vector2(-.1f,-.25f),new Vector2(.38f,.3f)); break;
            case Kind.Pause: line(new Vector2(-.17f,.3f),new Vector2(-.17f,-.3f)); line(new Vector2(.17f,.3f),new Vector2(.17f,-.3f)); break;
            case Kind.Home: line(new Vector2(-.38f,.02f),new Vector2(0,.36f)); line(new Vector2(0,.36f),new Vector2(.38f,.02f)); line(new Vector2(-.28f,.02f),new Vector2(-.28f,-.32f)); line(new Vector2(-.28f,-.32f),new Vector2(.28f,-.32f)); line(new Vector2(.28f,-.32f),new Vector2(.28f,.02f)); break;
        }
    }
    private void Frame(VertexHelper vh,Rect r,float w)
    { Line(vh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),w); Line(vh,new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),w); Line(vh,new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax),w); Line(vh,new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin),w); }
    private void Line(VertexHelper vh,Vector2 a,Vector2 b,float w)
    {
        Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*w*.5f; int i=vh.currentVertCount;
        vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero); vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
    }
}
