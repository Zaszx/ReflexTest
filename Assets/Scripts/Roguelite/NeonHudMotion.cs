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
    private TMP_Text floatingDamage;
    private TMP_Text floatingHealing;
    private RectTransform healthTextRect;
    private RectTransform healthIconRect;
    private RectTransform healthVisualRect;
    private RectTransform timerValueRect;
    private Vector3 healthIconOriginScale, healthIconScaleFrom;
    private Vector3 healthVisualOriginScale, healthVisualScaleFrom, timerValueOriginScale;
    private Quaternion healthVisualOriginRotation, healthVisualRotationFrom;
    private bool initialized, hasState, paused;
    private int lastHealth, lastMaximum, lastProgress, lastRequired;
    private bool lastReserve;
    private float healthElapsed, healingElapsed, reserveElapsed, reservePulseElapsed, progressElapsed, floatingDamageElapsed, damageSinceLast;
    private bool healthMoving, healingMoving, reserveMoving, reservePulseActive, progressMoving, floatingDamageMoving, terminalPresentation;
    private int floatingDamageAmount;
    private float ghostEnd, healthRatio;
    private Vector2 floatingDamageStart;
    private float floatingDamageStartAlpha;
    private float renderedProgress, progressFrom, progressTo;

    public void Initialize(Image healthFill, Image progressFill, TMP_Text healthText,
        TMP_Text objectiveText, TMP_Text timerLabel, TMP_Text reserveText, Image timerFill,
        RectTransform healthIcon = null, RectTransform healthVisual = null, RectTransform timerValue = null)
    {
        if (initialized) return;
        initialized = true;
        objectiveFill = progressFill;
        healthTextRect = healthText == null ? null : healthText.rectTransform;
        healthIconRect = healthIcon;
        healthVisualRect = healthVisual == null ? healthTextRect : healthVisual;
        timerValueRect = timerValue;
        if (healthIconRect != null) healthIconOriginScale = healthIconScaleFrom = healthIconRect.localScale;
        if (healthVisualRect != null)
        {
            healthVisualOriginScale = healthVisualScaleFrom = healthVisualRect.localScale;
            healthVisualOriginRotation = healthVisualRotationFrom = healthVisualRect.localRotation;
        }
        if (timerValueRect != null) timerValueOriginScale = timerValueRect.localScale;
        if (healthTextRect != null)
        {
            floatingDamage = NeonStyle.Text("FloatingDamage", healthTextRect.parent, "", Mathf.Min(22f, healthText.fontSize),
                NeonTheme.T.Danger, TextAlignmentOptions.Center);
            RectTransform labelRect = floatingDamage.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, .5f);
            labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.sizeDelta = new Vector2(60f, 32f);
            labelRect.anchoredPosition = new Vector2(92f, 24f);
            floatingDamage.gameObject.SetActive(false);
            floatingHealing = NeonStyle.Text("FloatingHealing", healthTextRect.parent, "", Mathf.Min(22f, healthText.fontSize),
                NeonTheme.T.Primary, TextAlignmentOptions.Center);
            RectTransform healingRect = floatingHealing.rectTransform;
            healingRect.anchorMin = healingRect.anchorMax = new Vector2(0f, .5f);
            healingRect.pivot = new Vector2(.5f, .5f);
            healingRect.sizeDelta = new Vector2(76f, 32f);
            healingRect.anchoredPosition = new Vector2(92f, 24f);
            floatingHealing.gameObject.SetActive(false);
        }
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
                // State is authoritative and immediate. The caller starts the
                // one cosmetic response after refreshing this latest snapshot.
                ghostEnd = healthMoving ? Mathf.Max(ghostEnd, healthRatio, nextHealth) : Mathf.Max(healthRatio, nextHealth);
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

        reservePulseActive = usingReserve && !terminalPresentation;
        if (!reservePulseActive) { reservePulseElapsed = 0f; SettleTimerValue(); }

        lastHealth = safeHealth;
        lastMaximum = safeMaximum;
        lastProgress = safeProgress;
        lastRequired = safeRequired;
        lastReserve = usingReserve;
        RenderMotion();
    }

    public void SetPaused(bool value) { paused = value; }

    /// <summary>
    /// Starts one bounded cosmetic response for already-committed health loss.
    /// Call after SetState so the displayed number is already authoritative.
    /// </summary>
    public void PlayHealthDamage(int amount = 1)
    {
        if (!initialized || !isActiveAndEnabled || amount <= 0) return;
        // Loss and recovery use separate, mutually exclusive visual tracks so a
        // delayed positive cue cannot overlap a red damage treatment.
        healingMoving = false;
        SettleHealthVisual();
        if (floatingHealing != null) floatingHealing.gameObject.SetActive(false);
        if (lostHealth != null)
        {
            Color accent = NeonMotion.T.damageAccent;
            accent.a = lostHealth.color.a;
            lostHealth.color = accent;
        }
        ghostEnd = Mathf.Max(ghostEnd, healthRatio);
        healthElapsed = 0f;
        healthMoving = NeonMotion.T.healthDuration > 0f;
        if (!healthMoving) ClearDamage();

        if (!floatingDamageMoving || damageSinceLast > NeonMotion.T.damageAggregationWindow)
            floatingDamageAmount = 0;
        floatingDamageAmount = Mathf.Clamp(floatingDamageAmount + amount, 1, 99);
        damageSinceLast = 0f;
        if (healthIconRect != null) healthIconScaleFrom = healthIconRect.localScale;
        if (healthVisualRect != null)
        {
            healthVisualScaleFrom = healthVisualRect.localScale;
            healthVisualRotationFrom = healthVisualRect.localRotation;
        }
        if (floatingDamage != null)
        {
            floatingDamageStart = floatingDamageMoving ? floatingDamage.rectTransform.anchoredPosition : new Vector2(92f, 24f);
            floatingDamageStartAlpha = floatingDamageMoving ? floatingDamage.color.a
                : NeonMotion.T.damageAccentOpacity * NeonMotionAccent.Strength;
        }
        floatingDamageElapsed = 0f;
        floatingDamageMoving = NeonMotion.T.floatingDamageDuration > 0f;
        if (floatingDamage != null)
        {
            floatingDamage.SetText("−{0}", floatingDamageAmount);
            floatingDamage.gameObject.SetActive(floatingDamageMoving);
        }
        RenderMotion();
    }

    /// <summary>A distinct, non-rotating confirmation for health restored after its value has been committed.</summary>
    public void PlayHealthHealing(int amount = 1)
    {
        if (!initialized || !isActiveAndEnabled || amount <= 0) return;
        ClearDamage();
        floatingDamageMoving = false;
        floatingDamageAmount = 0;
        if (floatingDamage != null) floatingDamage.gameObject.SetActive(false);
        healingElapsed = 0f;
        healingMoving = NeonMotion.T.healthDuration > 0f;
        if (healthVisualRect != null)
        {
            healthVisualScaleFrom = healthVisualRect.localScale;
            healthVisualRotationFrom = healthVisualRect.localRotation;
        }
        if (floatingHealing != null)
        {
            floatingHealing.SetText("+{0}", Mathf.Clamp(amount, 1, 99));
            floatingHealing.color = NeonTheme.T.Primary;
            floatingHealing.gameObject.SetActive(healingMoving);
        }
        if (!healingMoving) SettleHealthVisual();
        RenderMotion();
    }

    /// <summary>Reserve-only terminal cue; it never alters health feedback.</summary>
    public void PlayReserveExhausted()
    {
        if (!initialized || !isActiveAndEnabled) return;
        if (reserveAccent != null)
        {
            Color accent = NeonMotion.T.reserveFailureAccent;
            accent.a = 0f;
            reserveAccent.color = accent;
        }
        reserveElapsed = 0f;
        reserveMoving = NeonMotion.T.reserveExhaustedDuration > 0f;
        RenderMotion();
    }

    /// <summary>Stops obsolete HUD tracks while retaining an in-flight final health cue.</summary>
    public void BeginTerminalPresentation(bool reserveFailure)
    {
        terminalPresentation = true;
        progressMoving = false;
        reservePulseActive = false;
        SettleTimerValue();
        if (reserveFailure)
        {
            // A timeout must not inherit a nearby health-loss label or ghost.
            ClearDamage();
            floatingDamageMoving = false;
            floatingDamageAmount = 0;
            if (floatingDamage != null) floatingDamage.gameObject.SetActive(false);
            if (floatingHealing != null) floatingHealing.gameObject.SetActive(false);
            PlayReserveExhausted();
        }
        else reserveMoving = false;
    }

    public void EndTerminalPresentation()
    {
        terminalPresentation = false;
        NormalizeImmediate();
    }

    /// <summary>Settle the current view and make the next snapshot a fresh baseline.</summary>
    public void NormalizeImmediate()
    {
        hasState = false;
        ClearDamage();
        reserveMoving = progressMoving = false;
        floatingDamageMoving = healingMoving = false;
        reservePulseActive = false;
        healthElapsed = healingElapsed = reserveElapsed = reservePulseElapsed = progressElapsed = floatingDamageElapsed = damageSinceLast = 0f;
        renderedProgress = progressTo;
        SetProgress(renderedProgress);
        NeonMotionAccent.Alpha(reserveAccent, 0f);
        if (floatingDamage != null) floatingDamage.gameObject.SetActive(false);
        if (floatingHealing != null) floatingHealing.gameObject.SetActive(false);
        SettleTimerValue();
    }

    private void Update()
    {
        if (!initialized || !hasState || (!healthMoving && !healingMoving && !reserveMoving && !reservePulseActive && !progressMoving && !floatingDamageMoving)) return;
        float delta = NeonMotion.Delta(paused);
        if (delta <= 0f) return;
        if (healthMoving) healthElapsed += delta;
        if (healingMoving) healingElapsed += delta;
        if (reserveMoving) reserveElapsed += delta;
        if (reservePulseActive) reservePulseElapsed += delta;
        if (progressMoving) progressElapsed += delta;
        if (floatingDamageMoving) floatingDamageElapsed += delta;
        if (floatingDamageMoving) damageSinceLast += delta;
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
                NeonMotionAccent.Alpha(lostHealth, strength * NeonMotion.T.lostHealthGhostOpacity * NeonMotionAccent.Strength);
            }
            if (healthIconRect != null)
            {
                float pulse = Mathf.Sin(t * Mathf.PI);
                float compression = NeonTheme.ReducedEffects ? 1f : Mathf.Lerp(1f, NeonMotion.T.healthDamageCompression, pulse);
                Vector3 target = healthIconOriginScale * compression;
                healthIconRect.localScale = Vector3.Lerp(healthIconScaleFrom, target, NeonMotion.Ease(Mathf.Min(1f, t * 4f)));
            }
            RenderDamageVisual(t);
            if (t >= 1f) ClearDamage();
        }
        if (healingMoving)
        {
            float t = NeonMotionAccent.Fraction(healingElapsed, NeonMotion.T.healthDuration);
            if (healthVisualRect != null)
            {
                float peak = NeonTheme.ReducedEffects ? 1.035f : RecoverySettingsProvider.Current.healthHealingPeakScale;
                float scale = Mathf.Lerp(1f, peak, Mathf.Sin(t * Mathf.PI));
                healthVisualRect.localScale = Vector3.Lerp(healthVisualScaleFrom, healthVisualOriginScale * scale, NeonMotion.Ease(Mathf.Min(1f, t * 4f)));
                healthVisualRect.localRotation = Quaternion.Lerp(healthVisualRotationFrom, healthVisualOriginRotation, NeonMotion.Ease(Mathf.Min(1f, t * 4f)));
            }
            if (floatingHealing != null)
            {
                Color tint = NeonTheme.T.Primary;
                tint.a = (1f - NeonMotion.Ease(t)) * NeonMotionAccent.Strength;
                floatingHealing.color = tint;
            }
            if (t >= 1f)
            {
                healingMoving = false;
                SettleHealthVisual();
                if (floatingHealing != null) floatingHealing.gameObject.SetActive(false);
            }
        }
        if (reserveMoving)
        {
            float duration = terminalPresentation ? NeonMotion.T.reserveExhaustedDuration : NeonMotion.T.resourceDuration;
            float t = NeonMotionAccent.Fraction(reserveElapsed, duration);
            float eased = NeonMotion.Ease(t);
            if (reserveAccent != null)
            {
                reserveAccent.rectTransform.anchorMin = new Vector2(Mathf.Lerp(.72f, 0f, eased), 0f);
                float opacity = terminalPresentation ? NeonMotion.T.reserveFailureAccentOpacity : .9f;
                NeonMotionAccent.Alpha(reserveAccent, (1f - eased) * opacity * NeonMotionAccent.Strength);
            }
            if (t >= 1f) { reserveMoving = false; NeonMotionAccent.Alpha(reserveAccent, 0f); }
        }
        if (floatingDamageMoving)
        {
            float t = NeonMotionAccent.Fraction(floatingDamageElapsed, NeonMotion.T.floatingDamageDuration);
            if (floatingDamage != null)
            {
                RectTransform rect = floatingDamage.rectTransform;
                float rise = Mathf.Min(14f, NeonMotion.T.floatingDamageTravel);
                rect.anchoredPosition = Vector2.Lerp(floatingDamageStart, new Vector2(92f, 24f + rise), NeonMotion.Ease(t));
                Color tint = NeonMotion.T.damageAccent;
                tint.a = Mathf.Lerp(floatingDamageStartAlpha, 0f, NeonMotion.Ease(t));
                floatingDamage.color = tint;
            }
            if (t >= 1f)
            {
                floatingDamageMoving = false;
                if (floatingDamage != null) floatingDamage.gameObject.SetActive(false);
            }
        }
        if (reservePulseActive && timerValueRect != null)
        {
            if (NeonTheme.ReducedEffects) timerValueRect.localScale = timerValueOriginScale;
            else
            {
                float cycle = Mathf.Repeat(reservePulseElapsed / RecoverySettingsProvider.Current.reserveValuePulseCycleSeconds, 1f);
                float pulse = (1f - Mathf.Cos(cycle * Mathf.PI * 2f)) * .5f;
                timerValueRect.localScale = timerValueOriginScale * Mathf.Lerp(1f, RecoverySettingsProvider.Current.reserveValuePulseScale, pulse);
            }
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
        if (healthIconRect != null) healthIconRect.localScale = healthIconOriginScale;
        SettleHealthVisual();
    }

    private void RenderDamageVisual(float time)
    {
        if (healthVisualRect == null) return;
        float pulse = Mathf.Sin(time * Mathf.PI);
        float peak = NeonTheme.ReducedEffects ? 1.06f : RecoverySettingsProvider.Current.healthDamagePeakScale;
        Vector3 targetScale = healthVisualOriginScale * Mathf.Lerp(1f, peak, pulse);
        healthVisualRect.localScale = Vector3.Lerp(healthVisualScaleFrom, targetScale, NeonMotion.Ease(Mathf.Min(1f, time * 4f)));
        Quaternion targetRotation = NeonTheme.ReducedEffects ? healthVisualOriginRotation : healthVisualOriginRotation * Quaternion.Euler(0f, 0f, RecoverySettingsProvider.Current.healthDamageRotationDegrees * pulse);
        healthVisualRect.localRotation = Quaternion.Lerp(healthVisualRotationFrom, targetRotation, NeonMotion.Ease(Mathf.Min(1f, time * 4f)));
    }

    private void SettleHealthVisual()
    {
        if (healthVisualRect == null) return;
        healthVisualRect.localScale = healthVisualOriginScale;
        healthVisualRect.localRotation = healthVisualOriginRotation;
    }

    private void SettleTimerValue()
    {
        if (timerValueRect != null) timerValueRect.localScale = timerValueOriginScale;
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
