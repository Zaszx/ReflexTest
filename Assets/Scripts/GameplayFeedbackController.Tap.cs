using UnityEngine;
using UnityEngine.UI;

public enum TapFeedbackPriority { None, Success, Damage, Terminal }

/// <summary>One bounded cosmetic clock for accepted taps; never participates in simulation or saves.</summary>
public partial class GameplayFeedbackController
{
    public const int TapParticleCapacity = 48;
    private RectTransform tapFeedbackRoot;
    private GameObject tapGameplayPanel;
    private Image tapEdges;
    private NeonTapParticles tapParticles;
    private readonly Vector3[] outgoingCorners = new Vector3[4];
    private bool outgoingPoseValid;
    private TapFeedbackPriority tapPriority;
    private float edgeElapsed, edgeDuration, edgePeak, shakeElapsed, shakeStart;
    private Color edgeAccent;
    private bool shakeActive;

    public TapFeedbackPriority TapFeedbackPriority => tapPriority;
    public float ShakeOffsetNormalized { get; private set; }
    public int ActiveTapParticleCount => tapParticles != null ? tapParticles.ActiveCount : 0;
    public RectTransform TapFeedbackRoot => tapFeedbackRoot;
    public Vector3 LastBurstCenterWorld { get; private set; }

    public void InitializeTapFeedback(GameObject gameplayPanel, Image existingFlash)
    {
        if (tapFeedbackRoot != null || gameplayPanel == null) return;
        tapGameplayPanel = gameplayPanel;
        RectTransform panel = (RectTransform)gameplayPanel.transform;
        Transform parent = panel.parent;
        int sibling = panel.GetSiblingIndex();
        // The overscanned backdrop fills exposed pixels while the full panel and
        // its UI raycast geometry recoil together. SafeContent paths remain intact.
        tapFeedbackRoot = CreateRect("GameplayTapOffset", parent);
        StretchToParent(tapFeedbackRoot);
        tapFeedbackRoot.SetSiblingIndex(sibling);
        panel.SetParent(tapFeedbackRoot, false);
        StretchToParent(panel);
        var backdrop = CreateImage("RecoilBackdrop", tapFeedbackRoot, new Color(.012f, .055f, .09f, 1));
        StretchToParent(backdrop.rectTransform);
        backdrop.rectTransform.offsetMin = new Vector2(-16, 0);
        backdrop.rectTransform.offsetMax = new Vector2(16, 0);
        backdrop.transform.SetAsFirstSibling();
        // The backdrop is shown only with this screen, including its normal fade.
        backdrop.gameObject.SetActive(false);
        recoilBackdrop = backdrop;

        tapEdges = existingFlash;
        if (tapEdges != null)
        {
            tapEdges.rectTransform.SetParent(panel, false);
            StretchToParent(tapEdges.rectTransform);
            tapEdges.sprite = null;
            tapEdges.raycastTarget = false;
            tapEdges.color = Color.clear;
            if (tapEdges.GetComponent<NeonTapVignette>() == null) tapEdges.gameObject.AddComponent<NeonTapVignette>();
            tapEdges.transform.SetAsLastSibling();
        }
        RectTransform particles = CreateRect("AcceptedTapGlints", panel);
        StretchToParent(particles);
        tapParticles = particles.gameObject.AddComponent<NeonTapParticles>();
        tapParticles.raycastTarget = false;
    }

    private Image recoilBackdrop;

    public void CaptureSuccessPose(GameSquare consumed)
    {
        outgoingPoseValid = consumed != null && consumed.TryGetRenderedOutlineCorners(outgoingCorners);
    }

    public void PlayAcceptedSuccess()
    {
        if (tapPriority == TapFeedbackPriority.Terminal) { outgoingPoseValid = false; return; }
        if (outgoingPoseValid && tapParticles != null && NeonMotion.T.successParticlesEnabled && !NeonTheme.ReducedEffects)
        {
            LastBurstCenterWorld = (outgoingCorners[0] + outgoingCorners[2]) * .5f;
            tapParticles.Emit(outgoingCorners);
        }
        outgoingPoseValid = false;
        if (!CanReplaceTapEdge(tapPriority, TapFeedbackPriority.Success)) return;
        SetTapEdge(TapFeedbackPriority.Success, NeonMotion.T.successAccent,
            NeonMotion.T.successVignetteDuration, NeonMotion.T.successVignetteIntensity);
    }

    public void PlayAcceptedDamage()
    {
        if (!CanReplaceTapEdge(tapPriority, TapFeedbackPriority.Damage)) return;
        SetTapEdge(TapFeedbackPriority.Damage, NeonMotion.T.damageAccent,
            NeonMotion.T.damageVignetteDuration, NeonMotion.T.damageVignetteIntensity);
        if (!NeonTheme.ReducedEffects)
        {
            shakeStart = ShakeOffsetNormalized;
            shakeElapsed = 0;
            shakeActive = true;
        }
    }

    public static bool CanReplaceTapEdge(TapFeedbackPriority current, TapFeedbackPriority requested) =>
        current != TapFeedbackPriority.Terminal && requested >= current;

    private void SetTapEdge(TapFeedbackPriority priority, Color accent, float duration, float peak)
    {
        tapPriority = priority;
        edgeAccent = accent;
        edgeElapsed = 0;
        edgeDuration = Mathf.Max(.001f, duration);
        edgePeak = Mathf.Clamp01(peak) * (NeonTheme.ReducedEffects ? NeonMotion.T.reducedEffectsStrength : 1f);
        // Immediate bounded attack. Refreshing never adds to the existing opacity.
        RenderTapEdge();
    }

    public void BeginTerminalFeedback(bool reserveFailure)
    {
        StopPresentation();
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        if (tapParticles != null) tapParticles.Clear();
        outgoingPoseValid = false;
        if (reserveFailure) ResetTapFeedback();
        // Keep the fatal hit's already-started recoil and red cue exactly once.
        tapPriority = TapFeedbackPriority.Terminal;
    }

    private void Update() => AdvanceTapFeedback(NeonMotion.Delta(presentationPaused));

    public void AdvanceTapFeedback(float delta)
    {
        if (tapFeedbackRoot == null) return;
        if (tapGameplayPanel == null || !tapGameplayPanel.activeInHierarchy)
        {
            ResetTapFeedback();
            return;
        }
        if (presentationPaused || NeonMotion.ApplicationSuspended) return;
        delta = Mathf.Max(0f, delta);
        if (tapParticles != null)
        {
            if (!NeonMotion.T.successParticlesEnabled || NeonTheme.ReducedEffects) tapParticles.Clear();
            else tapParticles.Advance(delta);
        }
        if (edgeDuration > 0)
        {
            edgeElapsed = Mathf.Min(edgeDuration, edgeElapsed + delta);
            RenderTapEdge();
            if (edgeElapsed >= edgeDuration)
            {
                edgeDuration = 0;
                if (tapPriority != TapFeedbackPriority.Terminal) tapPriority = TapFeedbackPriority.None;
            }
        }
        if (shakeActive)
        {
            shakeElapsed += delta;
            float t = Mathf.Clamp01(shakeElapsed / Mathf.Max(.001f, NeonMotion.T.damageShakeDuration));
            ShakeOffsetNormalized = NeonTheme.ReducedEffects ? 0 : EvaluateDamageRecoil(t, shakeStart, NeonMotion.T.damageShakeViewportFraction);
            if (t >= 1f || NeonTheme.ReducedEffects) shakeActive = false;
        }
        tapFeedbackRoot.anchoredPosition = new Vector2(ShakeOffsetNormalized * tapFeedbackRoot.rect.width, 0);
        recoilBackdrop.gameObject.SetActive(ShakeOffsetNormalized != 0);
    }

    public static float EvaluateDamageRecoil(float progress, float start, float amplitude)
    {
        float t = Mathf.Clamp01(progress), limit = Mathf.Max(0, amplitude);
        if (t >= 1f) return 0;
        float remaining = 1f - t;
        return Mathf.Clamp(start * remaining * remaining + Mathf.Sin(t * Mathf.PI * 6f) * limit * remaining * remaining, -limit, limit);
    }

    private void RenderTapEdge()
    {
        if (tapEdges == null) return;
        float t = edgeDuration <= 0 ? 1 : Mathf.Clamp01(edgeElapsed / edgeDuration);
        Color tint = edgeAccent;
        tint.a = edgePeak * (1f - t) * (1f - t);
        tapEdges.color = tint;
    }

    public void ResetTapFeedback()
    {
        tapPriority = TapFeedbackPriority.None;
        edgeElapsed = edgeDuration = edgePeak = 0;
        shakeActive = outgoingPoseValid = false;
        ShakeOffsetNormalized = shakeStart = shakeElapsed = 0;
        if (tapFeedbackRoot != null) tapFeedbackRoot.anchoredPosition = Vector2.zero;
        if (tapEdges != null) tapEdges.color = Color.clear;
        if (tapParticles != null) tapParticles.Clear();
        if (recoilBackdrop != null) recoilBackdrop.gameObject.SetActive(false);
    }
}
