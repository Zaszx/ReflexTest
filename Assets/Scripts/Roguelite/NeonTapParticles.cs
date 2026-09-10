using UnityEngine;
using UnityEngine.UI;

/// <summary>Fixed UI particle pool, one Canvas mesh, no objects allocated per tap/frame.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonTapParticles : MaskableGraphic
{
    private struct Glint
    {
        public Vector2 origin, direction;
        public float age, duration, travel, length, width;
        public Color tint;
    }
    private readonly Glint[] glints = new Glint[GameplayFeedbackController.TapParticleCapacity];
    private readonly Vector2[] corners = new Vector2[4];
    private uint decorationRandom = 0x93a421u;
    public int ActiveCount { get; private set; }

    // This component is attached to a plain RectTransform at runtime. Declare
    // the renderer explicitly (as the other procedural UI Graphics do) so its
    // generated mesh cannot be silently omitted from Canvas rendering.
    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        color = Color.white;
        SetVerticesDirty();
    }

    public void Emit(Vector3[] worldCorners)
    {
        for (int i = 0; i < 4; i++) corners[i] = rectTransform.InverseTransformPoint(worldCorners[i]);
        Vector2 center = (corners[0] + corners[2]) * .5f;
        float side = Vector2.Distance(corners[0], corners[1]);
        int count = Mathf.Clamp(NeonMotion.T.successParticleCount, 0, 8);
        for (int n = 0; n < count; n++)
        {
            int slot = -1;
            for (int i = 0; i < glints.Length; i++) if (glints[i].duration <= 0) { slot = i; break; }
            if (slot < 0) break;
            int edge = n % 4;
            Vector2 origin = Vector2.Lerp(corners[edge], corners[(edge + 1) % 4], .15f + NextDecoration() * .7f);
            glints[slot] = new Glint
            {
                origin = origin, direction = (origin - center).normalized,
                duration = Mathf.Max(.01f, NeonMotion.T.successParticleDuration),
                travel = Mathf.Min(side * .06f, rectTransform.rect.width * NeonMotion.T.successParticleTravel),
                length = n % 2 == 0 ? 3.5f : Mathf.Clamp(side * .065f, 5, 12),
                width = n % 2 == 0 ? 3.5f : 2.4f,
                tint = n % 3 == 0 ? new Color(.3f, 1, .92f, 1) : NeonMotion.T.successAccent
            };
            ActiveCount++;
        }
        SetVerticesDirty();
    }

    private float NextDecoration()
    {
        decorationRandom = unchecked(decorationRandom * 1664525u + 1013904223u);
        return (decorationRandom >> 8) * (1f / 16777216f);
    }

    public void Advance(float delta)
    {
        if (ActiveCount == 0 || delta <= 0) return;
        for (int i = 0; i < glints.Length; i++)
        {
            if (glints[i].duration <= 0) continue;
            glints[i].age += delta;
            if (glints[i].age >= glints[i].duration) { glints[i].duration = 0; ActiveCount--; }
        }
        SetVerticesDirty();
    }

    public void Clear()
    {
        if (ActiveCount == 0) return;
        for (int i = 0; i < glints.Length; i++) glints[i].duration = 0;
        ActiveCount = 0; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < glints.Length; i++)
        {
            Glint g = glints[i];
            if (g.duration <= 0) continue;
            float t = Mathf.Clamp01(g.age / g.duration);
            Vector2 point = g.origin + g.direction * g.travel * (1f - (1f - t) * (1f - t));
            Color core = g.tint; core.a *= (1f - t) * (1f - t);
            Color glow = core; glow.a *= .18f;
            Quad(mesh, point, g.direction, g.length + 4, g.width + 4, glow);
            Quad(mesh, point, g.direction, g.length, g.width, core);
        }
    }

    private static void Quad(VertexHelper mesh, Vector2 p, Vector2 direction, float length, float width, Color tint)
    {
        Vector2 along = direction * length * .5f, across = new Vector2(-direction.y, direction.x) * width * .5f;
        int first = mesh.currentVertCount;
        mesh.AddVert(p - along - across, tint, Vector2.zero); mesh.AddVert(p - along + across, tint, Vector2.zero);
        mesh.AddVert(p + along + across, tint, Vector2.zero); mesh.AddVert(p + along - across, tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }
}
