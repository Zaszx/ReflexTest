using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the existing Image as the cell's input and feedback owner.</summary>
public sealed class NeonGridFill : BaseMeshEffect
{
    public static void Apply(Image image)
    {
        Material material = NeonGridRendering.Material;
        if (image == null || material == null) return;
        NeonGridRendering.EnsureChannels(image.canvas);
        image.material = material;
        if (image.GetComponent<NeonGridFill>() == null) image.gameObject.AddComponent<NeonGridFill>();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || graphic == null) return;
        NeonGridRendering.Populate(mesh, graphic.rectTransform.rect, graphic.color, -1, 1);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (graphic != null) NeonGridRendering.EnsureChannels(graphic.canvas);
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        if (graphic != null) NeonGridRendering.EnsureChannels(graphic.GetComponentInParent<Canvas>());
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        if (graphic != null) NeonGridRendering.EnsureChannels(graphic.canvas);
    }
}
