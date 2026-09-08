using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One visibility/input owner per panel. Gameplay geometry is never translated.</summary>
public sealed class NeonScreenMotion : MonoBehaviour
{
    public enum Kind { Page, Gameplay, Modal, Result }
    private RectTransform visual;
    private CanvasGroup group;
    private CanvasGroup modalContent;
    private Image modalBackdrop;
    private float backdropAlpha;
    private Kind kind;
    private Vector2 origin;
    private Vector2 fromOffset;
    private Vector2 toOffset;
    private float fromAlpha, elapsed, duration;
    private bool visible, animating, inputAllowed, initialized;
    private Action hidden;
    public event Action Settled;

    public bool WantsVisible => visible;
    public bool IsAnimating => animating;
    public bool CoversInput => visible || (kind == Kind.Modal && animating);
    public bool IsReady => visible && !animating && gameObject.activeInHierarchy;
    public float Opacity => modalContent != null ? modalContent.alpha : group == null ? 0 : group.alpha;

    public void Initialize(RectTransform visualContent, Kind panelKind)
    {
        visual = visualContent;
        origin = visual.anchoredPosition;
        kind = panelKind;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (kind == Kind.Modal)
        {
            modalContent = visual.GetComponent<CanvasGroup>();
            if (modalContent == null) modalContent = visual.gameObject.AddComponent<CanvasGroup>();
            modalBackdrop = GetComponent<Image>();
            backdropAlpha = modalBackdrop != null ? modalBackdrop.color.a : 1;
        }
        initialized = true;
        SetImmediate(gameObject.activeSelf);
    }

    public void SetInputAllowed(bool allowed)
    {
        inputAllowed = allowed;
        ApplyInput();
    }

    public void SetVisible(bool show, Action onHidden = null)
    {
        if (!initialized) return;
        // An external disable can interrupt this owner. Reopening must still
        // reactivate the object, even when its previous desired state was true.
        if (show && visible && !gameObject.activeSelf) visible = false;
        if (visible == show)
        {
            if (!show && onHidden != null)
            {
                if (animating) hidden = onHidden;
                else onHidden();
            }
            return;
        }
        visible = show;
        hidden = show ? null : onHidden;
        elapsed = 0;
        float travel = kind == Kind.Gameplay || NeonTheme.ReducedEffects ? 0 :
            kind == Kind.Modal ? NeonMotion.T.modalTravel : NeonMotion.T.screenTravel;
        if (show && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            SetOpacity(0);
            visual.anchoredPosition = origin + new Vector2(0, -travel);
        }
        fromAlpha = Opacity;
        fromOffset = visual.anchoredPosition - origin;
        toOffset = show ? Vector2.zero : new Vector2(0, travel * .35f);
        duration = kind == Kind.Modal
            ? (show ? NeonMotion.T.modalDuration : NeonMotion.T.modalExitDuration)
            : (show ? NeonMotion.T.screenDuration : NeonMotion.T.screenExitDuration);
        duration = Mathf.Max(0, duration);
        if (!enabled || (!show && !gameObject.activeInHierarchy)) duration = 0;
        // Reversal starts at the actual current opacity/position, without a queue.
        duration *= Mathf.Abs((show ? 1 : 0) - fromAlpha);
        animating = true;
        ApplyInput();
        if (duration <= 0) Finish();
    }

    public void SetImmediate(bool show)
    {
        visible = show;
        hidden = null;
        animating = false;
        SetOpacity(show ? 1 : 0);
        if (visual != null) visual.anchoredPosition = origin;
        gameObject.SetActive(show);
        ApplyInput();
    }

    public void SettleVisible()
    {
        if (visible && animating) Finish();
    }

    private void Update()
    {
        if (!animating) return;
        elapsed += NeonMotion.Delta();
        float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
        float eased = NeonMotion.Ease(t);
        SetOpacity(Mathf.Lerp(fromAlpha, visible ? 1 : 0, eased));
        visual.anchoredPosition = origin + Vector2.Lerp(fromOffset, toOffset, eased);
        ApplyInput();
        if (t >= 1) Finish();
    }

    private void Finish()
    {
        animating = false;
        SetOpacity(visible ? 1 : 0);
        visual.anchoredPosition = origin;
        Action completion = hidden;
        hidden = null;
        if (!visible) gameObject.SetActive(false);
        ApplyInput();
        Settled?.Invoke();
        completion?.Invoke();
    }

    private void ApplyInput()
    {
        if (group == null) return;
        group.blocksRaycasts = inputAllowed && CoversInput;
        group.interactable = inputAllowed && visible && Opacity >= .5f;
    }

    private void SetOpacity(float opacity)
    {
        if (group == null) return;
        if (modalContent == null)
        {
            group.alpha = opacity;
            return;
        }
        // Cover the underlying headings before modal text becomes prominent,
        // then retain that cover until the departing text is almost gone.
        // The root owns input; this child owns content opacity independently.
        group.alpha = opacity > 0 ? 1 : 0;
        modalContent.alpha = opacity;
        if (modalBackdrop != null)
        {
            Color color = modalBackdrop.color;
            color.a = backdropAlpha * Mathf.Clamp01(opacity * Mathf.Max(1, NeonMotion.T.modalBackdropLead));
            modalBackdrop.color = color;
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        bool finishingHidden = !visible && animating;
        Action completion = finishingHidden ? hidden : null;
        animating = false;
        hidden = null;
        if (visual != null) visual.anchoredPosition = origin;
        SetOpacity(visible ? 1 : 0);
        ApplyInput();
        // A disappearing modal is already visually gone if another owner
        // disables it. Report readiness once; GameManager validates the request.
        if (finishingHidden)
        {
            Settled?.Invoke();
            completion?.Invoke();
        }
    }
}

/// <summary>Small result choreography. Owns only decorative text/mark children, never values.</summary>
public sealed class NeonResultMotion : MonoBehaviour
{
    private sealed class Item
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 origin;
        public int order;
    }
    private readonly List<Item> items = new List<Item>();
    private float elapsed;
    private bool playing;

    public void Add(RectTransform rect, int order)
    {
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        items.Add(new Item { rect = rect, group = group, origin = rect.anchoredPosition, order = order });
    }

    public void Play()
    {
        if (playing) NormalizeImmediate();
        elapsed = 0;
        playing = true;
        foreach (var item in items) item.origin = item.rect.anchoredPosition;
        if (NeonMotion.T.resultDuration <= 0) NormalizeImmediate();
        else Render();
    }

    public void NormalizeImmediate()
    {
        playing = false;
        foreach (var item in items)
        {
            item.rect.anchoredPosition = item.origin;
            item.group.alpha = 1;
        }
    }

    private void Update()
    {
        if (!playing) return;
        elapsed += NeonMotion.Delta();
        Render();
    }

    private void Render()
    {
        bool complete = true;
        foreach (var item in items)
        {
            float delay = Mathf.Max(0, NeonMotion.T.resultStagger) * item.order;
            float duration = Mathf.Max(0, NeonMotion.T.resultDuration);
            float t = duration <= 0 ? 1 : Mathf.Clamp01((elapsed - delay) / duration);
            float amount = NeonMotion.Ease(t);
            item.group.alpha = Mathf.Lerp(.35f, 1, amount);
            item.rect.anchoredPosition = item.origin + new Vector2(0,
                NeonTheme.ReducedEffects ? 0 : -(1 - amount) * NeonMotion.T.resultTravel);
            complete &= t >= 1;
        }
        if (complete) NormalizeImmediate();
    }

    private void OnDisable() => NormalizeImmediate();
}
