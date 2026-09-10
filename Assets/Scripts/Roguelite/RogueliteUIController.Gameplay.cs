using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

public sealed partial class RogueliteUIController
{
    private static readonly Color PlayBackground = NeonTheme.Hex("030E17");
    private static readonly Color PlaySurface = NeonTheme.Hex("091B27");
    private static readonly Color PlayCyan = NeonTheme.Hex("59EFFF");
    private static readonly Color PlayWhite = NeonTheme.Hex("E2F7FF");
    private static readonly Color PlayMuted = NeonTheme.Hex("7393A7");
    private static readonly Color PlayLime = NeonTheme.Hex("ACF35F");
    private static readonly Color PlayGold = NeonTheme.Hex("FFD05B");
    private static readonly Color PlayPink = NeonTheme.Hex("F461D3");
    private NeonGameplayGraphic healthSegments, healthSymbol, timeDial, ruleSymbol, ruleFrame;
    private Material timerGlow;
    private Color terminalTimerTint, terminalDialTint;

    private static NeonGameplayGraphic GameplayArt(string name, Transform parent, NeonGameplayGraphic.Kind kind, Color tint)
    {
        var rect = Rect(name, parent);
        var graphic = rect.gameObject.AddComponent<NeonGameplayGraphic>();
        graphic.kind = kind; graphic.color = tint; graphic.fillTint = PlaySurface;
        graphic.raycastTarget = false;
        return graphic;
    }

    private NeonGameplayGraphic HudPanel(string name, float x, float y, float width, float height, Color tint)
    {
        var graphic = GameplayArt(name, play, NeonGameplayGraphic.Kind.Panel, tint);
        At(graphic.rectTransform, 0, 1, x + width * .5f, -y - height * .5f, width, height);
        return graphic;
    }

    private void CreateHud()
    {
        gm.gameplayPanel.GetComponent<Image>().color = PlayBackground;
        var floor = GameplayArt("PerspectiveFloor", gm.gameplayPanel.transform, NeonGameplayGraphic.Kind.Floor, PlayCyan);
        floor.transform.SetAsFirstSibling();
        floor.rectTransform.anchorMin = Vector2.zero; floor.rectTransform.anchorMax = new Vector2(1, .18f);
        floor.rectTransform.offsetMin = floor.rectTransform.offsetMax = Vector2.zero;

        var motto = Txt("GameplayMotto", play, "SPEED   RECOGNIZE   TAP", 17, 34, -3, 1012, 33, NeonTheme.Hex("325367"));
        motto.alignment = TextAlignmentOptions.Center; motto.characterSpacing = 6;

        var levelPanel = HudPanel("LevelPanel", 34, 56, 500, 176, PlayCyan);
        var levelLabel = Txt("LevelLabel", levelPanel.transform, "LEVEL", 23, 45, -20, 375, 33, PlayMuted);
        levelLabel.characterSpacing = 5;
        gm.levelText = Txt("Level", levelPanel.transform, "01", 94, 43, -52, 413, 111, PlayWhite);
        gm.levelText.fontStyle = FontStyles.Bold;
        AddHudGlow(gm.levelText, PlayWhite, .22f);

        var objectivePanel = HudPanel("ObjectivePanel", 34, 252, 508, 89, NeonTheme.Hex("479FC6"));
        var gridIcon = GameplayArt("ObjectiveGlyph", objectivePanel.transform, NeonGameplayGraphic.Kind.Grid, PlayMuted);
        At(gridIcon.rectTransform, 0, .5f, 52, 0, 49, 49);
        objective = Txt("Objective", objectivePanel.transform, "", 32, 108, -20, 367, 48, PlayWhite);
        objective.fontStyle = FontStyles.Bold; gm.remainingText = objective;
        progressFill = Track("ObjectiveProgress", objectivePanel.transform, new Vector2(108, -77), 364, 3);
        progressFill.transform.parent.GetComponent<Image>().color = Color.clear;

        var hpPanel = HudPanel("HealthPanel", 34, 361, 508, 85, PlayLime);
        healthSymbol = GameplayArt("HealthGlyph", hpPanel.transform, NeonGameplayGraphic.Kind.Heart, PlayLime);
        At(healthSymbol.rectTransform, 0, .5f, 52, 0, 34, 34);
        health = Txt("Health", hpPanel.transform, "", 23, 108, -22, 221, 40, PlayWhite);
        healthFill = Track("HealthProgress", hpPanel.transform, new Vector2(337, -43), 145, 17);
        healthFill.enabled = false; healthFill.transform.parent.GetComponent<Image>().color = Color.clear;
        healthSegments = GameplayArt("HealthSegments", healthFill.transform.parent, NeonGameplayGraphic.Kind.Segments, PlayLime);
        Fill(healthSegments.rectTransform); healthSegments.fillTint = NeonTheme.Hex("1E342C");

        var timePanel = HudPanel("TimePanel", 620, 56, 426, 267, PlayCyan);
        timerLabel = Txt("TimerLabel", timePanel.transform, "TIME", 23, 47, -20, 280, 33, PlayMuted);
        timerLabel.characterSpacing = 3;
        timeDial = GameplayArt("TimeDial", timePanel.transform, NeonGameplayGraphic.Kind.Clock, PlayCyan);
        At(timeDial.rectTransform, 1, 1, -48, -46, 61, 61);
        gm.timerText = Txt("Timer", timePanel.transform, "", 111, 38, -54, 359, 134, PlayCyan);
        gm.timerText.fontStyle = FontStyles.Bold;
        gm.timerText.enableAutoSizing = true; gm.timerText.fontSizeMin = 68; gm.timerText.fontSizeMax = 111;
        timerGlow = AddHudGlow(gm.timerText, PlayCyan, .48f);
        timerFill = Track("TimeProgress", timePanel.transform, new Vector2(35, -190), 356, 3);
        timerFill.transform.parent.GetComponent<Image>().color = NeonTheme.Hex("14333E");
        var reserveBand = GameplayArt("ReserveBand", timePanel.transform, NeonGameplayGraphic.Kind.Panel, PlayGold);
        At(reserveBand.rectTransform, .5f, 0, 0, 38, 426, 77);
        reserveBand.fillTint = NeonTheme.Hex("211E10");
        reserveBand.glowPerimeter = true;
        reserve = Txt("Reserve", reserveBand.transform, "", 29, 34, -18, 359, 45, PlayGold);
        reserve.enableAutoSizing = true; reserve.fontSizeMin = 21; reserve.fontSizeMax = 29;

        ruleFrame = HudPanel("RulePanel", 620, 349, 426, 107, PlayCyan);
        ruleSymbol = GameplayArt("RuleGlyph", ruleFrame.transform, NeonGameplayGraphic.Kind.Target, PlayCyan);
        At(ruleSymbol.rectTransform, 0, .5f, 62, 0, 47, 47);
        rule = Txt("TargetRule", ruleFrame.transform, "LARGEST OUTLINE", 27, 120, -25, 278, 56, PlayCyan);
        rule.fontStyle = FontStyles.Bold; rule.enableAutoSizing = true; rule.fontSizeMin = 22; rule.fontSizeMax = 27;
        rule.textWrappingMode = TextWrappingModes.Normal;
        // Retain the existing feedback owner, with its accent tucked into the panel's rail.
        ruleRail = Rule("RuleRail", ruleFrame.transform, PlayCyan); At(ruleRail.rectTransform, 0, .5f, 5, 0, 3, 53);
        modifiers = Txt("Modifiers", play, "", 19, 34, -467, 1012, 33, PlayMuted);
        modifiers.alignment = TextAlignmentOptions.Center; modifiers.characterSpacing = 3;

        var coin = UpgradeArt("PendingCoin", play, NeonUpgradeGraphic.Kind.Coin, PlayGold);
        At(coin.rectTransform, 0, 0, 74, 207, 31, 31);
        pending = Txt("PendingEarnings", play, "", 23, 114, 229, 890, 44, PlayMuted, 0, 0);
        debugBadge = Txt("Sandbox", play, "PRACTICE / NO PROGRESSION", 20, 58, 270, 950, 35, PlayGold, 0, 0);
        gm.homeButton = GameplayButton("Home", "HOME", NeonGameplayGraphic.Kind.Home, PlayCyan, false);
        gm.settingsButton = GameplayButton("Pause", "PAUSE", NeonGameplayGraphic.Kind.Pause, PlayPink, true);
        var footer = Txt("GameplayFooter", play, "STAY SHARP", 15, 34, 30, 1012, 24, NeonTheme.Hex("264252"), 0, 0);
        footer.characterSpacing = 7; footer.alignment = TextAlignmentOptions.Center;

        hudMotion = play.gameObject.AddComponent<NeonHudMotion>();
        hudMotion.Initialize(healthFill, progressFill, health, objective, timerLabel, reserve, timerFill, healthSymbol.rectTransform);
        var transitionPanel = GameplayArt("LevelTransition", play, NeonGameplayGraphic.Kind.Panel, PlayCyan);
        transitionRect = transitionPanel.rectTransform; At(transitionRect, .5f, .5f, 0, -70, 620, 112);
        transitionGroup = transitionPanel.gameObject.AddComponent<CanvasGroup>(); transitionGroup.alpha = 0;
        transitionGroup.blocksRaycasts = false; transitionGroup.interactable = false; transitionPanel.gameObject.SetActive(false);
        transitionTitle = Text("TransitionTitle", transitionPanel.transform, "LEVEL COMPLETE", 34, PlayCyan, TextAlignmentOptions.Center);
        Fill(transitionTitle.rectTransform, 18, 8, 18, 8); transitionTitle.fontStyle = FontStyles.Bold;
    }

    private Button GameplayButton(string name, string label, NeonGameplayGraphic.Kind kind, Color tint, bool right)
    {
        var button = Button(name, play, label); button.image.color = Color.clear;
        At((RectTransform)button.transform, right ? 1 : 0, 0, right ? -207 : 207, 108, 346, 115);
        var frame = GameplayArt("ControlFrame", button.transform, NeonGameplayGraphic.Kind.Panel, tint);
        Fill(frame.rectTransform); frame.transform.SetAsFirstSibling();
        frame.glowPerimeter = true;
        var glyph = GameplayArt("ControlGlyph", button.transform, kind, tint);
        At(glyph.rectTransform, 0, .5f, 72, 0, 49, 49);
        var labelText = button.GetComponentInChildren<TMP_Text>();
        labelText.fontSize = 28; labelText.characterSpacing = 6; labelText.color = PlayWhite;
        labelText.alignment = TextAlignmentOptions.Center; Fill(labelText.rectTransform, 122, 0, 26, 0);
        button.GetComponent<NeonPressFeedback>().CaptureRestPose();
        return button;
    }

    private static Material AddHudGlow(TMP_Text label, Color tint, float opacity)
    {
        // TMP owns this material instance and disposes it with the label.
        var material = label.fontMaterial;
        material.EnableKeyword("UNDERLAY_ON");
        tint.a = opacity; material.SetColor("_UnderlayColor", tint);
        material.SetFloat("_UnderlayOffsetX", 0); material.SetFloat("_UnderlayOffsetY", 0);
        // Keep the feather inside this font atlas's signed-distance range.
        // Excess softness gives small rich-text glyphs a visible quad backdrop.
        material.SetFloat("_UnderlayDilate", 0); material.SetFloat("_UnderlaySoftness", .12f);
        label.UpdateMeshPadding(); return material;
    }

    private void SetTimerGlow(Color tint)
    {
        if (timerGlow == null) return;
        tint.a = .48f; timerGlow.SetColor("_UnderlayColor", tint);
    }

    private void RefreshHudGraphics(int currentHealth, int maximumHealth, float timeRatio, bool reserveActive, bool reverseActive)
    {
        var hpColor = currentHealth <= 1 ? T.Danger : PlayLime;
        healthSymbol.color = hpColor;
        float hpRatio = Mathf.Clamp01(currentHealth / (float)Mathf.Max(1, maximumHealth));
        if (healthSegments.segments != maximumHealth || !Mathf.Approximately(healthSegments.value, hpRatio))
        { healthSegments.segments = Mathf.Clamp(maximumHealth, 1, 20); healthSegments.value = hpRatio; healthSegments.SetVerticesDirty(); }
        healthSegments.color = hpColor;
        float dialValue = Mathf.Round(Mathf.Clamp01(timeRatio) * 48) / 48;
        if (!Mathf.Approximately(timeDial.value, dialValue)) { timeDial.value = dialValue; timeDial.SetVerticesDirty(); }
        timeDial.color = reserveActive ? PlayGold : PlayCyan;
        ruleFrame.color = ruleSymbol.color = reverseActive ? PlayPink : PlayCyan;
    }
}
