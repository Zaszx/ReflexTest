using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))]
public sealed class NeonEnemyView : MonoBehaviour, IPointerDownHandler
{
    private NeonEnemyController owner;
    private int enemyId;
    private Image image, coreImage;
    private RectTransform tapArea;

    public void SetTapTargetScale(float scale)
    {
        if (tapArea == null)
        {
            var go = new GameObject("TapArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tapArea = (RectTransform)go.transform;
            tapArea.SetParent(transform, false);
            tapArea.SetAsFirstSibling();
            tapArea.anchorMin = Vector2.zero; tapArea.anchorMax = Vector2.one;
            tapArea.offsetMin = tapArea.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.clear;
        }
        tapArea.localScale = Vector3.one * Mathf.Clamp(scale, 1f, 2.5f);
    }

    public bool ContainsPointer(PointerEventData data) => RectTransformUtility.RectangleContainsScreenPoint(
        tapArea != null ? tapArea : (RectTransform)transform, data.position, data.pressEventCamera);

    public void Bind(NeonEnemyController controller, int id)
    {
        owner = controller;
        enemyId = id;
        if (image == null) image = GetComponent<Image>();
        image.raycastTarget = true;
        EnsureTreatment();
    }

    public void SetColor(Color color)
    {
        if (image == null) image = GetComponent<Image>();
        EnsureTreatment();
        // The outer square is a restrained red halo; the inset core keeps the
        // threat filled and readable without resembling a target outline.
        image.color = new Color(1f, .02f, .10f, color.a * .42f);
        coreImage.color = color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Enemy input is first-class even in the approach corridor. Using the
        // event prevents this exact pointer-down from reaching a grid cell.
        if (owner != null && owner.TryConsumeViewPointer(enemyId)) eventData.Use();
    }

    private void EnsureTreatment()
    {
        if (coreImage != null) return;
        GameObject core = new GameObject("Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        core.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)core.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one * .62f;
        coreImage = core.GetComponent<Image>();
        coreImage.sprite = image.sprite;
        coreImage.type = Image.Type.Simple;
        coreImage.raycastTarget = false;
    }
}
