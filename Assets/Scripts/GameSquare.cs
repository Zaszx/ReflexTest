using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class GameSquare : MonoBehaviour, IPointerDownHandler
{
    [Header("UI Components")]
    public Image bgImage;
    public Image outlineImage;

    public enum LitSize { None, Small, Medium, Large }
    
    [Header("Current State")]
    public int gridX;
    public int gridY;
    public LitSize currentLitSize = LitSize.None;

    public System.Action<GameSquare> onClicked;

    private Coroutine scaleCoroutine;
    private Coroutine attentionCoroutine;
    private float smallScale = 0.4f;
    private float mediumScale = 0.7f;
    private float fullScale = 1.0f;

    public void Setup(int x, int y, Sprite solidSprite, Sprite outlineSprite, Color cellColor, Color outlineColor, float small, float medium, float full)
    {
        gridX = x;
        gridY = y;
        
        bgImage.sprite = solidSprite;
        bgImage.color = cellColor;
        
        outlineImage.sprite = outlineSprite;
        outlineImage.color = outlineColor;

        smallScale = small;
        mediumScale = medium;
        fullScale = full;

        // Start unlit
        currentLitSize = LitSize.None;
        outlineImage.gameObject.SetActive(false);
        outlineImage.transform.localScale = Vector3.zero;
    }

    public void SetLitSize(LitSize size, bool animate)
    {
        LitSize previousSize = currentLitSize;
        currentLitSize = size;

        float targetScaleValue = GetScaleValue(size);

        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        if (attentionCoroutine != null)
        {
            StopCoroutine(attentionCoroutine);
            attentionCoroutine = null;
        }

        if (animate && gameObject.activeInHierarchy)
        {
            scaleCoroutine = StartCoroutine(AnimateScale(previousSize, size, targetScaleValue));
        }
        else
        {
            if (size == LitSize.None)
            {
                outlineImage.gameObject.SetActive(false);
                outlineImage.transform.localScale = Vector3.zero;
            }
            else
            {
                outlineImage.gameObject.SetActive(true);
                outlineImage.transform.localScale = Vector3.one * targetScaleValue;
                // Adjust opacity slightly for smaller ones so they feel "dimmer" but still visible
                Color c = outlineImage.color;
                c.a = size == LitSize.Large ? 1.0f : (size == LitSize.Medium ? 0.8f : 0.6f);
                outlineImage.color = c;
            }
        }
    }

    private float GetScaleValue(LitSize size)
    {
        switch (size)
        {
            case LitSize.Small: return smallScale;
            case LitSize.Medium: return mediumScale;
            case LitSize.Large: return fullScale;
            default: return 0f;
        }
    }

    private IEnumerator AnimateScale(LitSize fromSize, LitSize toSize, float targetScale)
    {
        float duration = 0.15f; // Fast, snappy neon transition
        float elapsed = 0f;

        Vector3 startScale = outlineImage.transform.localScale;
        
        if (fromSize == LitSize.None && toSize != LitSize.None)
        {
            outlineImage.gameObject.SetActive(true);
            startScale = Vector3.zero;
        }

        Color c = outlineImage.color;
        float startAlpha = c.a;
        float targetAlpha = toSize == LitSize.Large ? 1.0f : (toSize == LitSize.Medium ? 0.8f : 0.6f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Use smooth ease out for growing effect
            float tSmooth = Mathf.Sin(t * Mathf.PI * 0.5f);

            outlineImage.transform.localScale = Vector3.Lerp(startScale, Vector3.one * targetScale, tSmooth);
            
            c.a = Mathf.Lerp(startAlpha, targetAlpha, tSmooth);
            outlineImage.color = c;

            yield return null;
        }

        outlineImage.transform.localScale = Vector3.one * targetScale;
        c.a = targetAlpha;
        outlineImage.color = c;

        if (toSize == LitSize.None)
        {
            outlineImage.gameObject.SetActive(false);
        }

        scaleCoroutine = null;
    }

    public void PlayAttentionPulse(float delay = 0f)
    {
        if (!gameObject.activeInHierarchy || currentLitSize == LitSize.None) return;

        if (attentionCoroutine != null) StopCoroutine(attentionCoroutine);
        attentionCoroutine = StartCoroutine(AnimateAttentionPulse(currentLitSize, delay));
    }

    private IEnumerator AnimateAttentionPulse(LitSize expectedSize, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (currentLitSize != expectedSize || currentLitSize == LitSize.None)
        {
            attentionCoroutine = null;
            yield break;
        }

        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        float baseScale = GetScaleValue(currentLitSize);
        float peakScale = baseScale * 1.18f;
        float elapsed = 0f;
        const float growDuration = 0.09f;
        const float settleDuration = 0.13f;

        outlineImage.gameObject.SetActive(true);
        while (elapsed < growDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            outlineImage.transform.localScale = Vector3.one * Mathf.Lerp(baseScale, peakScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            outlineImage.transform.localScale = Vector3.one * Mathf.Lerp(peakScale, baseScale, t * t);
            yield return null;
        }

        outlineImage.transform.localScale = Vector3.one * baseScale;
        attentionCoroutine = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onClicked?.Invoke(this);
    }
}
