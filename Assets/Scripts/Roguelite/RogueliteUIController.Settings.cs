using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

public sealed partial class RogueliteUIController
{
    private RectTransform settingsCard;
    private Vector2 settingsAvailableSize;
    private TMP_Text settingsSubtitle;
    private Button onboardingSettingsButton;
    private NeonSettingsSwitch hapticsSwitch, reducedSwitch;
    private static readonly Color SettingsCyan = NeonTheme.Hex("78E7FA");
    private static readonly Color SettingsMuted = NeonTheme.Hex("64869E");

    private static NeonSettingsGraphic SettingsArt(string name, Transform parent, NeonSettingsGraphic.Kind kind, Color tint)
    {
        var art = Rect(name, parent).gameObject.AddComponent<NeonSettingsGraphic>();
        art.kind = kind; art.color = tint; art.raycastTarget = false;
        return art;
    }

    private void CreateSettings()
    {
        settingsCard = Rect("SettingsCard", settings);
        At(settingsCard, .5f, .5f, 0, 0, 1000, 1424);
        var frame = SettingsArt("NeonPanel", settingsCard, NeonSettingsGraphic.Kind.Panel, NeonTheme.Hex("65CCFF"));
        Fill(frame.rectTransform);
        var brand = Txt("Brand", settingsCard, "NEON REFLEX", 25, 96, -78, 740, 38, SettingsCyan);
        brand.characterSpacing = 35;
        var slashes = SettingsArt("Slashes", settingsCard, NeonSettingsGraphic.Kind.Slashes, SettingsMuted);
        At(slashes.rectTransform, 1, 1, -99, -96, 72, 32);
        settingsHeading = Txt("Title", settingsCard, "SETTINGS", 91, 96, -180, 838, 124, NeonTheme.Hex("E7F7FF"));
        settingsHeading.fontStyle = FontStyles.Bold;
        settingsHeading.characterSpacing = 2;
        settingsHeading.enableAutoSizing = true; settingsHeading.fontSizeMin = 76; settingsHeading.fontSizeMax = 91;
        AddHudGlow(settingsHeading, NeonTheme.Hex("79C7FF"), .42f);
        settingsSubtitle = Txt("Subtitle", settingsCard, "PREFERENCES", 28, 100, -294, 800, 44, SettingsMuted);
        settingsSubtitle.characterSpacing = 30;
        SettingsDivider(396);
        hapticsToggle = SettingsRow("HapticsToggle", "Haptic Feedback", "A tactile cue on supported devices.", 422,
            NeonSettingsGraphic.Kind.Haptic, SettingsCyan, out hapticsSwitch);
        hapticsToggle.onValueChanged.AddListener(v => hapticsChanged?.Invoke(v));
        gm.hapticToggle = hapticsToggle; gm.sfxVolumeSlider = null;
        SettingsDivider(610);
        reducedToggle = SettingsRow("ReducedEffectsToggle", "Reduced Effects", "Quieter transitions and feedback.", 642,
            NeonSettingsGraphic.Kind.Reduced, NeonTheme.Hex("FF4CA7"), out reducedSwitch);
        reducedToggle.onValueChanged.AddListener(v => NeonTheme.ReducedEffects = v);
        reducedToggle.SetIsOnWithoutNotify(NeonTheme.ReducedEffects); reducedSwitch.Sync();
        SettingsDivider(828);
        onboardingSettingsButton = SettingsButton("HowToPlay", "HOW TO PLAY", 870, 112, false);
        onboardingSettingsButton.onClick.AddListener(() =>
        {
            if (gm.IsOnboardingActive)
                gm.SkipOnboarding();
            else
                gm.BeginOnboardingReplay();
        });
        SettingsDivider(1000);
        gm.settingsCloseButton = SettingsButton("CloseSettings", "DONE", 1042, 134, true);
        var home = SettingsButton("SettingsHome", "RETURN HOME", 1216, 120, false);
        home.onClick.AddListener(gm.ReturnToMainMenu);
        FitSettingsCard();
    }

    private void SettingsDivider(float y)
    {
        var line = Rule("Divider", settingsCard, new Color(.16f, .31f, .39f, .45f));
        At(line.rectTransform, 0, 1, 500, -y, 808, 2);
    }

    private Toggle SettingsRow(string name, string title, string description, float y,
        NeonSettingsGraphic.Kind icon, Color tint, out NeonSettingsSwitch view)
    {
        // The whole row is tappable; the small switch artwork never narrows its hit area.
        var surface = Panel(name, settingsCard, Color.clear, true);
        At(surface.rectTransform, 0, 1, 500, -y - 88, 848, 176);
        var toggle = surface.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = surface; toggle.graphic = null;
        toggle.transition = Selectable.Transition.None; toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        var plate = SettingsArt("IconPlate", surface.transform, NeonSettingsGraphic.Kind.IconPlate, tint);
        At(plate.rectTransform, 0, .5f, 70, 0, 122, 122);
        var glyph = SettingsArt("Icon", plate.transform, icon, tint);
        At(glyph.rectTransform, .5f, .5f, 0, 0, 72, 72);
        var label = Txt("SettingTitle", surface.transform, title, 38, 190, -35, 490, 55, NeonTheme.Hex("DFECF6"));
        label.fontStyle = FontStyles.Normal;
        Txt("SettingDescription", surface.transform, description, 28, 190, -97, 505, 44, SettingsMuted);
        var pill = SettingsArt("Switch", surface.transform, NeonSettingsGraphic.Kind.Toggle, NeonTheme.Hex("DCFF64"));
        At(pill.rectTransform, 1, .5f, -71, 0, 136, 74);
        view = surface.gameObject.AddComponent<NeonSettingsSwitch>(); view.Initialize(toggle, pill);
        return toggle;
    }

    private Button SettingsButton(string name, string caption, float y, float height, bool primary)
    {
        var button = Button(name, settingsCard, caption);
        At((RectTransform)button.transform, 0, 1, 500, -y - height * .5f, 848, height);
        button.image.color = Color.clear;
        var frame = SettingsArt("Frame", button.transform, primary ? NeonSettingsGraphic.Kind.PrimaryButton : NeonSettingsGraphic.Kind.SecondaryButton,
            primary ? NeonTheme.Hex("DDFF65") : SettingsCyan);
        Fill(frame.rectTransform); frame.transform.SetAsFirstSibling();
        var label = button.GetComponentInChildren<TMP_Text>();
        Fill(label.rectTransform, 70, 0, 70, 0); label.alignment = TextAlignmentOptions.Center;
        label.color = primary ? NeonTheme.Hex("102411") : SettingsCyan;
        label.fontSize = primary ? 44 : 31; label.characterSpacing = primary ? 5 : 7;
        if (primary)
        {
            var arrow = Shape("Forward", button.transform, NeonShape.Kind.Arrow, label.color, 5);
            At(arrow.rectTransform, 1, .5f, -62, 0, 43, 43);
        }
        button.GetComponent<NeonPressFeedback>().CaptureRestPose();
        return button;
    }

    public void SetHaptics(bool enabled)
    {
        hapticsToggle.SetIsOnWithoutNotify(enabled); hapticsSwitch.Sync();
        reducedToggle.SetIsOnWithoutNotify(NeonTheme.ReducedEffects); reducedSwitch.Sync();
    }

    public void RefreshSettings(bool duringGameplay)
    {
        settingsHeading.text = duringGameplay ? "PAUSED" : "SETTINGS";
        settingsSubtitle.text = duringGameplay ? "SETTINGS" : "PREFERENCES";
        ButtonText(gm.settingsCloseButton, duringGameplay ? "RESUME" : "DONE");
        ButtonText(onboardingSettingsButton, gm.IsOnboardingActive ? "SKIP PRACTICE" : "HOW TO PLAY");
        FitSettingsCard(); RefreshInputLayers();
    }

    private void LateUpdate()
    {
        if (settingsCard != null && settingsCard.gameObject.activeInHierarchy) FitSettingsCard();
    }

    private void FitSettingsCard()
    {
        Vector2 available = settings.rect.size;
        if (available == settingsAvailableSize) return;
        settingsAvailableSize = available;
        float fit = Mathf.Min(1f, (available.x - 44) / 1000f, (available.y - 72) / 1424f);
        settingsCard.localScale = Vector3.one * Mathf.Max(.1f, fit);
    }
}

/// <summary>One bounded switch track; the Toggle's value and callbacks stay immediate.</summary>
public sealed class NeonSettingsSwitch : MonoBehaviour
{
    private Toggle owner;
    private NeonSettingsGraphic art;
    private float from, to, elapsed;
    private bool moving;
    public void Initialize(Toggle toggle, NeonSettingsGraphic graphic)
    { owner = toggle; art = graphic; owner.onValueChanged.AddListener(Changed); Sync(); }
    public void Sync()
    { if (art == null || owner == null) return; moving = false; art.value = owner.isOn ? 1 : 0; art.SetVerticesDirty(); }
    private void Changed(bool value)
    { from = art.value; to = value ? 1 : 0; elapsed = 0; moving = true; }
    private void Update()
    {
        if (!moving) return;
        elapsed += NeonMotion.Delta();
        float duration = NeonTheme.ReducedEffects ? 0 : Mathf.Max(0, NeonMotion.T.releaseDuration);
        float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
        art.value = Mathf.Lerp(from, to, NeonMotion.Ease(t)); art.SetVerticesDirty();
        if (t >= 1) moving = false;
    }
    private void OnDisable() => Sync();
    private void OnDestroy() { if (owner != null) owner.onValueChanged.RemoveListener(Changed); }
}
