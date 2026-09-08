using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cosmetic HUD motion. Numbers, health's primary fill, timer values and timer
/// fill remain authoritative. Only the objective fill eases toward its target.
/// </summary>
public sealed class NeonHudMotion : MonoBehaviour
{
    private Image objectiveFill;
    private Image lostHealth;
    private Image reserveAccent;
    private RectTransform healthRect;
    private Vector2 healthOrigin;
    private bool initialized, hasState, paused;
    private int lastHealth, lastMaximum, lastProgress, lastRequired;
    private bool lastReserve;
    private float healthElapsed, reserveElapsed, progressElapsed;
    private bool healthMoving, reserveMoving, progressMoving;
    private float ghostEnd, healthRatio;
    private float renderedProgress, progressFrom, progressTo;

    public void Initialize(Image healthFill, Image progressFill, TMP_Text healthText,
        TMP_Text objectiveText, TMP_Text timerLabel, TMP_Text reserveText, Image timerFill)
    {
        if (initialized) return;
        initialized = true;
        objectiveFill = progressFill;
        healthRect = healthText == null ? null : healthText.rectTransform;
        if (healthRect != null) healthOrigin = healthRect.anchoredPosition;
        if (healthFill != null)
        {
            lostHealth = NeonMotionAccent.Create("LostHealth", healthFill.transform.parent, NeonTheme.T.Danger);
            lostHealth.transform.SetAsLastSibling();
        }
        if (timerLabel != null)
            reserveAccent = NeonMotionAccent.Underline("ReserveActivation", timerLabel.rectTransform, NeonTheme.T.Reserve);
        NormalizeImmediate();
    }

    public void SetState(int healthAmount, int maximumHealth, int objectiveProgress, int required, bool usingReserve)
    {
        if (!initialized) return;
        int safeMaximum = Mathf.Max(1, maximumHealth);
        int safeHealth = Mathf.Clamp(healthAmount, 0, safeMaximum);
        int safeRequired = Mathf.Max(1, required);
        int safeProgress = Mathf.Clamp(objectiveProgress, 0, safeRequired);
        float nextHealth = safeHealth / (float)safeMaximum;
        float nextProgress = safeProgress / (float)safeRequired;

        if (!hasState || !isActiveAndEnabled)
        {
            // Hidden snapshot refreshes establish the next view; they must not
            // replay a resource loss or activation when the panel is shown.
            ClearDamage();
            reserveMoving = progressMoving = false;
            NeonMotionAccent.Alpha(reserveAccent, 0f);
            hasState = true;
            healthRatio = nextHealth;
            renderedProgress = progressFrom = progressTo = nextProgress;
            SetProgress(renderedProgress);
        }
        else
        {
            if (safeMaximum != lastMaximum || safeHealth > lastHealth)
                ClearDamage();
            else if (safeHealth < lastHealth)
            {
                // One reusable trailing segment merges rapid losses. It can
                // never cover the remaining, authoritative health segment.
                ghostEnd = healthMoving ? Mathf.Max(ghostEnd, healthRatio) : healthRatio;
                healthElapsed = 0f;
                healthMoving = true;
            }
            healthRatio = nextHealth;

            if (safeRequired != lastRequired || safeProgress < lastProgress)
            {
                // A fresh objective is a new level, not an animated rollback.
                renderedProgress = progressFrom = progressTo = nextProgress;
                progressMoving = false;
                SetProgress(renderedProgress);
            }
            else if (!Mathf.Approximately(nextProgress, progressTo))
            {
                progressFrom = renderedProgress;
                progressTo = nextProgress;
                progressElapsed = 0f;
                progressMoving = true;
            }

            if (usingReserve && !lastReserve)
            {
                reserveElapsed = 0f;
                reserveMoving = true;
            }
        }

        lastHealth = safeHealth;
        lastMaximum = safeMaximum;
        lastProgress = safeProgress;
        lastRequired = safeRequired;
        lastReserve = usingReserve;
        RenderMotion();
    }

    public void SetPaused(bool value) { paused = value; }

    /// <summary>Settle the current view and make the next snapshot a fresh baseline.</summary>
    public void NormalizeImmediate()
    {
        hasState = false;
        ClearDamage();
        reserveMoving = progressMoving = false;
        healthElapsed = reserveElapsed = progressElapsed = 0f;
        renderedProgress = progressTo;
        SetProgress(renderedProgress);
        NeonMotionAccent.Alpha(reserveAccent, 0f);
    }

    private void Update()
    {
        if (!initialized || !hasState || (!healthMoving && !reserveMoving && !progressMoving)) return;
        float delta = NeonMotion.Delta(paused);
        if (delta <= 0f) return;
        if (healthMoving) healthElapsed += delta;
        if (reserveMoving) reserveElapsed += delta;
        if (progressMoving) progressElapsed += delta;
        RenderMotion();
    }

    private void RenderMotion()
    {
        if (progressMoving)
        {
            float t = NeonMotionAccent.Fraction(progressElapsed, NeonMotion.T.progressDuration);
            renderedProgress = Mathf.Lerp(progressFrom, progressTo, NeonMotion.Ease(t));
            SetProgress(renderedProgress);
            if (t >= 1f) progressMoving = false;
        }
        if (healthMoving)
        {
            float t = NeonMotionAccent.Fraction(healthElapsed, NeonMotion.T.healthDuration);
            float strength = 1f - NeonMotion.Ease(t);
            if (lostHealth != null)
            {
                RectTransform rect = lostHealth.rectTransform;
                rect.anchorMin = new Vector2(healthRatio, 0f);
                rect.anchorMax = new Vector2(Mathf.Max(healthRatio, ghostEnd), 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                NeonMotionAccent.Alpha(lostHealth, strength * .78f * NeonMotionAccent.Strength);
            }
            if (healthRect != null)
            {
                float offset = NeonTheme.ReducedEffects ? 0f : Mathf.Sin(t * Mathf.PI) * strength * 4f;
                healthRect.anchoredPosition = healthOrigin + new Vector2(offset, 0f);
            }
            if (t >= 1f) ClearDamage();
        }
        if (reserveMoving)
        {
            float t = NeonMotionAccent.Fraction(reserveElapsed, NeonMotion.T.resourceDuration);
            float eased = NeonMotion.Ease(t);
            if (reserveAccent != null)
            {
                reserveAccent.rectTransform.anchorMin = new Vector2(Mathf.Lerp(.72f, 0f, eased), 0f);
                NeonMotionAccent.Alpha(reserveAccent, (1f - eased) * .9f * NeonMotionAccent.Strength);
            }
            if (t >= 1f) { reserveMoving = false; NeonMotionAccent.Alpha(reserveAccent, 0f); }
        }
    }

    private void SetProgress(float amount)
    {
        if (objectiveFill == null) return;
        RectTransform rect = objectiveFill.rectTransform;
        rect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void ClearDamage()
    {
        healthMoving = false;
        ghostEnd = healthRatio;
        NeonMotionAccent.Alpha(lostHealth, 0f);
        if (healthRect != null) healthRect.anchoredPosition = healthOrigin;
    }

    private void OnDisable() { NormalizeImmediate(); }
}

/// <summary>Feedback for an already-committed purchase. Never changes shop data or labels.</summary>
public sealed class NeonPurchaseMotion : MonoBehaviour
{
    private Image rowWash, tierAccent, benefitAccent, progressGlint;
    private NeonWalletMotion walletMotion;
    private Color accent;
    private float elapsed;
    private bool initialized, moving, paused;

    public void Initialize(RectTransform row, Color categoryAccent, TMP_Text tierText,
        TMP_Text benefitText, TMP_Text walletText, Image progressImage)
    {
        if (initialized || row == null) return;
        initialized = true;
        accent = categoryAccent;
        rowWash = NeonMotionAccent.Create("CommittedPurchase", row, accent);
        rowWash.transform.SetAsFirstSibling();
        if (tierText != null) tierAccent = NeonMotionAccent.Underline("TierAccepted", tierText.rectTransform, accent);
        if (benefitText != null) benefitAccent = NeonMotionAccent.Underline("BenefitAccepted", benefitText.rectTransform, accent);
        if (progressImage != null) progressGlint = NeonMotionAccent.Create("TierGlint", progressImage.transform, NeonTheme.T.Text);
        if (walletText != null)
        {
            // All four cards share one owner for the wallet accent.
            walletMotion = walletText.GetComponent<NeonWalletMotion>();
            if (walletMotion == null) walletMotion = walletText.gameObject.AddComponent<NeonWalletMotion>();
            walletMotion.Initialize(walletText.rectTransform);
        }
        NormalizeImmediate();
    }

    public void PlayCommitted()
    {
        if (!initialized || !isActiveAndEnabled) return;
        elapsed = 0f;
        moving = true;
        walletMotion?.PlayCommitted(accent);
        RenderMotion();
    }

    public void SetPaused(bool value) { paused = value; }

    public void NormalizeImmediate()
    {
        moving = false;
        elapsed = 0f;
        NeonMotionAccent.Alpha(rowWash, 0f);
        NeonMotionAccent.Alpha(tierAccent, 0f);
        NeonMotionAccent.Alpha(benefitAccent, 0f);
        NeonMotionAccent.Alpha(progressGlint, 0f);
    }

    private void Update()
    {
        if (!moving) return;
        float delta = NeonMotion.Delta(paused);
        if (delta <= 0f) return;
        elapsed += delta;
        RenderMotion();
    }

    private void RenderMotion()
    {
        float t = NeonMotionAccent.Fraction(elapsed, NeonMotion.T.purchaseDuration);
        float eased = NeonMotion.Ease(t);
        float strength = (1f - eased) * NeonMotionAccent.Strength;
        NeonMotionAccent.Alpha(rowWash, strength * .075f);
        NeonMotionAccent.Alpha(tierAccent, strength * .65f);
        NeonMotionAccent.Alpha(benefitAccent, strength * .85f);
        NeonMotionAccent.Alpha(progressGlint, strength * .85f);
        if (progressGlint != null)
        {
            RectTransform rect = progressGlint.rectTransform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(eased - .12f), 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(eased + .12f), 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (t >= 1f) NormalizeImmediate();
    }

    private void OnDisable() { NormalizeImmediate(); }
}

/// <summary>A single shared wallet accent prevents overlapping card writers.</summary>
public sealed class NeonWalletMotion : MonoBehaviour
{
    private Image accent;
    private float elapsed;
    private bool moving;

    public void Initialize(RectTransform wallet)
    {
        if (accent != null || wallet == null) return;
        accent = NeonMotionAccent.Underline("WalletAccepted", wallet, NeonTheme.T.Primary);
    }

    public void PlayCommitted(Color color)
    {
        if (accent == null || !isActiveAndEnabled) return;
        color.a = 0f;
        accent.color = color;
        elapsed = 0f;
        moving = true;
        RenderMotion();
    }

    private void Update()
    {
        if (!moving) return;
        float delta = NeonMotion.Delta();
        if (delta <= 0f) return;
        elapsed += delta;
        RenderMotion();
    }

    private void RenderMotion()
    {
        float t = NeonMotionAccent.Fraction(elapsed, NeonMotion.T.purchaseDuration);
        NeonMotionAccent.Alpha(accent, (1f - NeonMotion.Ease(t)) * .8f * NeonMotionAccent.Strength);
        if (t >= 1f) { moving = false; NeonMotionAccent.Alpha(accent, 0f); }
    }

    private void OnDisable() { moving = false; NeonMotionAccent.Alpha(accent, 0f); }
}

internal static class NeonMotionAccent
{
    public static float Strength => NeonTheme.ReducedEffects
        ? Mathf.Clamp01(NeonMotion.T.reducedEffectsStrength) : 1f;

    public static Image Create(string name, Transform parent, Color color)
    {
        color.a = 0f;
        Image image = NeonStyle.Panel(name, parent, color);
        NeonStyle.Fill(image.rectTransform);
        image.raycastTarget = false;
        return image;
    }

    public static Image Underline(string name, RectTransform parent, Color color)
    {
        Image image = Create(name, parent, color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, NeonTheme.T.ruleWidth);
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    public static float Fraction(float elapsed, float duration)
    {
        return duration <= 0f || float.IsNaN(duration) ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    public static void Alpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
