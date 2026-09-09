using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

public sealed partial class RogueliteUIController
{
    private static readonly Color ReportRed = NeonTheme.Hex("FF7981");
    private static readonly Color ReportAmber = NeonTheme.Hex("FFD065");
    private static readonly Color ReportLime = NeonTheme.Hex("D1FF39");
    private static readonly Color ReportIce = NeonTheme.Hex("B5DFFF");
    private static readonly Color ReportMuted = NeonTheme.Hex("89ADC6");
    private static readonly Color ReportNavy = NeonTheme.Hex("071924");

    private static Material AddReportGlow(TMP_Text text, Color tint, float opacity)
    {
        // A referenced material keeps the TMP glow shader variant in player builds.
        // TMP retains ownership of the per-label material and its font atlas.
        var preset = Resources.Load<Material>("NeonReportTextGlow");
        var material = text.fontMaterial;
        if (preset == null) return material;
        material.shader = preset.shader;
        material.DisableKeyword("UNDERLAY_ON");
        material.EnableKeyword("GLOW_ON");
        tint.a = opacity; material.SetColor("_GlowColor", tint);
        material.SetFloat("_GlowOffset", 0);
        material.SetFloat("_GlowInner", .03f);
        material.SetFloat("_GlowOuter", .22f);
        material.SetFloat("_GlowPower", .65f);
        text.UpdateMeshPadding();
        return material;
    }

    private static NeonRunReportGraphic ReportArt(string name, Transform parent, NeonRunReportGraphic.Kind kind, Color color)
    {
        var art = Rect(name, parent).gameObject.AddComponent<NeonRunReportGraphic>();
        art.kind = kind; art.color = color; art.raycastTarget = false;
        return art;
    }

    // All positions use a single portrait artboard, fitted uniformly inside the safe area.
    private static void ReportPlace(RectTransform rect, float x, float y, float width, float height)
        => At(rect, 0, 1, x + width * .5f, -y - height * .5f, width, height);

    private TMP_Text ReportText(string name, Transform parent, string value, float size,
        float x, float y, float width, float height, Color tint, bool centered = false)
    {
        var text = Text(name, parent, value, size, tint, centered ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
        ReportPlace(text.rectTransform, x, y, width, height);
        text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = Mathf.Min(22, size);
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private ResultView CreateRunReport(RectTransform safe)
    {
        var background = Rect("ReportCorridor", gm.failPanel.transform);
        Fill(background); background.SetAsFirstSibling();
        var image = background.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("MenuArt/neon-corridor"); image.raycastTarget = false;

        var root = Rect("RunReport", safe); At(root, .5f, .5f, 0, 0, 1080, 1800);
        root.gameObject.AddComponent<NeonRunReportLayout>().Initialize(image);
        var v = new ResultView();
        var headerLine = Rule("HeaderRule", root, new Color(.23f, .65f, .85f, .6f));
        ReportPlace(headerLine.rectTransform, 70, 75, 68, 2);
        v.eyebrow = ReportText("ReportEyebrow", root, "CAMPAIGN / RUN REPORT", 20, 167, 56, 580, 40, ReportIce);
        v.eyebrow.characterSpacing = 4;
        var logo = Rect("ReportWordmark", root); ReportPlace(logo, 792, 35, 220, 100);
        var neon = ReportText("NeonWord", logo, "NEON", 48, 0, 0, 220, 48, MenuIce, true);
        neon.characterSpacing = 7; AddReportGlow(neon, MenuBlue, .6f);
        var reflex = ReportText("ReflexWord", logo, "REFLEX", 38, 0, 46, 220, 43, MenuPink, true);
        reflex.characterSpacing = 3; AddReportGlow(reflex, MenuPink, .6f);

        var hero = Rect("ReportHero", root); ReportPlace(hero, 0, 140, 1080, 555);
        v.reportEmblem = ReportArt("RunOverEmblem", hero, NeonRunReportGraphic.Kind.ShatteredSquare, ReportRed);
        ReportPlace(v.reportEmblem.rectTransform, 375, 0, 330, 305);
        v.title = ReportText("ResultTitle", hero, "RUN OVER", 116, 85, 300, 910, 133, ReportRed, true);
        v.title.fontStyle = FontStyles.Bold; v.title.characterSpacing = 3;
        v.headlineGlow = AddReportGlow(v.title, ReportRed, .65f);
        v.reason = ReportText("FailureReason", hero, "HEALTH DEPLETED", 30, 110, 430, 860, 50, ReportIce, true);
        v.reason.characterSpacing = 7;
        var underline = Rule("ReasonUnderline", hero, new Color(.5f, .65f, .75f, .38f));
        ReportPlace(underline.rectTransform, 466, 493, 148, 2);
        v.description = ReportText("ResultDescription", hero, "Earnings banked. Upgrade. Go again.", 30, 75, 510, 930, 55, ReportMuted, true);

        var earnings = ReportArt("BankedEarningsCard", root, NeonRunReportGraphic.Kind.Card, ReportLime);
        ReportPlace(earnings.rectTransform, 98, 742, 884, 270);
        earnings.fillTint = NeonTheme.Hex("122B10");
        var coin = ReportArt("EarnedCoin", earnings.transform, NeonRunReportGraphic.Kind.Coin, ShopGold);
        ReportPlace(coin.rectTransform, 53, 49, 190, 190);
        var divider = Rule("CoinDivider", earnings.transform, new Color(.67f, .91f, .13f, .6f));
        ReportPlace(divider.rectTransform, 278, 52, 2, 166);
        v.amountLabel = ReportText("AmountLabel", earnings.transform, "COINS EARNED / BANKED", 23, 335, 37, 510, 46, ReportIce);
        v.amountLabel.characterSpacing = 4;
        v.amount = ReportText("Amount", earnings.transform, "+0", 150, 330, 86, 514, 155, ReportLime);
        v.amount.fontStyle = FontStyles.Bold;
        AddReportGlow(v.amount, ReportLime, .48f);

        var left = ReportStat(root, "LevelReachedCard", 98, false, out v.leftLabel, out v.leftValue);
        var right = ReportStat(root, "LevelsCompletedCard", 554, true, out v.rightLabel, out v.rightValue);
        var balance = ReportArt("PermanentBalanceCard", root, NeonRunReportGraphic.Kind.Card, ReportIce);
        ReportPlace(balance.rectTransform, 98, 1270, 884, 92); balance.diagonalSheen = false;
        var stack = ReportArt("BankedCoinStack", balance.transform, NeonRunReportGraphic.Kind.CoinStack, ShopGold);
        ReportPlace(stack.rectTransform, 44, 19, 53, 53);
        var balanceRule = Rule("BalanceDivider", balance.transform, new Color(.3f, .62f, .78f, .4f));
        ReportPlace(balanceRule.rectTransform, 127, 21, 2, 50);
        v.footer = ReportText("ResultFooter", balance.transform, "", 29, 166, 13, 683, 65, ReportMuted);
        v.footer.fontSizeMin = 19;

        gm.failPrimaryButton = ReportButton(root, "ResultPrimary", "UPGRADES", 1418, 162, true);
        gm.failMenuButton = ReportButton(root, "ResultHome", "RETURN HOME", 1608, 134, false);
        gm.failLevelText = v.title; gm.failReasonText = v.reason;
        v.motion = root.gameObject.AddComponent<NeonResultMotion>();
        v.motion.Add(hero, 0);
        v.motion.Add(earnings.rectTransform, 1); v.motion.Add(left, 1);
        v.motion.Add(right, 1); v.motion.Add(balance.rectTransform, 1);
        v.motion.Add((RectTransform)gm.failPrimaryButton.transform, 2, true, true);
        v.motion.Add((RectTransform)gm.failMenuButton.transform, 2, true, true);
        return v;
    }

    private RectTransform ReportStat(Transform parent, string name, float x, bool trophy, out TMP_Text label, out TMP_Text value)
    {
        var card = ReportArt(name, parent, NeonRunReportGraphic.Kind.Card, MenuBlue);
        ReportPlace(card.rectTransform, x, 1040, 428, 202);
        label = ReportText(trophy ? "RightLabel" : "LeftLabel", card.transform,
            trophy ? "LEVELS COMPLETED" : "LEVEL REACHED", 21, 42, 24, 360, 48, ReportMuted);
        label.characterSpacing = 9;
        Graphic icon = trophy
            ? (Graphic)ReportArt("Trophy", card.transform, NeonRunReportGraphic.Kind.Trophy, MenuBlue)
            : MenuArt("LevelBars", card.transform, NeonMenuGraphic.Kind.Bars, MenuBlue);
        ReportPlace(icon.rectTransform, 42, 95, 76, 80);
        var divider = Rule("StatDivider", card.transform, new Color(.3f, .66f, .8f, .5f));
        ReportPlace(divider.rectTransform, 149, 93, 2, 72);
        value = ReportText(trophy ? "RightValue" : "LeftValue", card.transform, "00", 92, 185, 77, 209, 105, ReportIce);
        value.fontStyle = FontStyles.Bold; value.characterSpacing = 6;
        AddReportGlow(value, MenuBlue, .28f);
        return card.rectTransform;
    }

    private Button ReportButton(Transform parent, string name, string caption, float y, float height, bool primary)
    {
        var button = Button(name, parent, caption);
        ReportPlace((RectTransform)button.transform, 98, y, 884, height);
        button.image.color = Color.clear;
        var frame = ReportArt("ReportButtonFrame", button.transform,
            primary ? NeonRunReportGraphic.Kind.PrimaryButton : NeonRunReportGraphic.Kind.Card,
            primary ? ReportLime : ReportIce);
        Fill(frame.rectTransform); frame.transform.SetAsFirstSibling();
        frame.fillTint = primary ? ReportLime : ReportNavy;
        frame.diagonalSheen = primary;
        var ink = primary ? NeonTheme.Hex("152609") : ReportIce;
        Graphic icon = primary
            ? (Graphic)MenuArt("UpgradeBars", button.transform, NeonMenuGraphic.Kind.Bars, ink)
            : GameplayArt("Home", button.transform, NeonGameplayGraphic.Kind.Home, ink);
        At(icon.rectTransform, 0, .5f, 98, 0, primary ? 84 : 70, primary ? 84 : 70);
        var label = button.GetComponentInChildren<TMP_Text>();
        Fill(label.rectTransform, 207, 0, primary ? 142 : 42, 0);
        label.color = ink; label.fontSize = primary ? 52 : 38; label.characterSpacing = 4;
        label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = 26;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        if (primary)
        {
            var arrow = Shape("ForwardArrow", button.transform, NeonShape.Kind.Arrow, ink, 5);
            At(arrow.rectTransform, 1, .5f, -80, 0, 48, 48);
        }
        else
        {
            var rule = Rule("HomeDivider", button.transform, new Color(.3f, .65f, .8f, .4f));
            At(rule.rectTransform, 0, .5f, 170, 0, 2, 60);
        }
        button.GetComponent<NeonPressFeedback>().CaptureRestPose();
        return button;
    }

    private void SetReportCause(ResultView v, string cause, bool practice)
    {
        bool time = cause == "RESERVE DEPLETED";
        bool abandoned = cause == "RUN ABANDONED";
        var accent = abandoned ? ReportIce : time ? ReportAmber : ReportRed;
        v.title.text = practice ? "PRACTICE OVER" : abandoned ? "RUN CLOSED" : "RUN OVER";
        v.title.color = accent;
        v.reason.text = abandoned ? "RUN ABANDONED" : time ? "OUT OF TIME" : "HEALTH DEPLETED";
        v.reportEmblem.kind = time ? NeonRunReportGraphic.Kind.EmptyHourglass : NeonRunReportGraphic.Kind.ShatteredSquare;
        v.reportEmblem.color = accent; v.reportEmblem.SetVerticesDirty();
        accent.a = .65f; v.headlineGlow.SetColor("_GlowColor", accent);
        v.description.text = practice ? "Practice ended. Your real run is untouched."
            : abandoned ? "Run closed. Your earnings are banked."
            : time ? "Time and reserve exhausted. Go again."
            : "Earnings banked. Upgrade. Go again.";
    }

    private void ShowFailureReport(RunSummaryData summary)
    {
        var v = failView; v.motion.NormalizeImmediate();
        SetReportCause(v, summary.reason, false);
        v.eyebrow.text = "CAMPAIGN / RUN REPORT";
        v.amountLabel.text = "COINS EARNED / BANKED";
        v.amount.text = $"+{summary.totalEarned:N0}";
        v.amount.fontSizeMax = 150; v.amount.color = ReportLime;
        v.leftLabel.text = "LEVEL REACHED"; v.leftValue.text = summary.highestLevelEntered.ToString("00");
        v.rightLabel.text = "LEVELS COMPLETED"; v.rightValue.text = summary.levelsCompleted.ToString("00");
        v.footer.text = $"Permanent balance: <color=#D7EBF9><b>{summary.newWalletBalance:N0}</b></color> coins";
        ShowBaseScreen(gm.failPanel); v.motion.Play();
    }

    private void ShowPracticeReport(string reason, ActiveRunData run, int level)
    {
        var v = failView; v.motion.NormalizeImmediate();
        SetReportCause(v, reason, true);
        v.eyebrow.text = "SANDBOX / PRACTICE REPORT";
        v.amountLabel.text = "PRACTICE / NO REWARDS";
        v.amount.text = "NO COINS BANKED"; v.amount.fontSizeMax = 48; v.amount.color = ReportIce;
        v.leftLabel.text = "LEVEL PRACTICED"; v.leftValue.text = level.ToString("00");
        v.rightLabel.text = "CORRECT TARGETS"; v.rightValue.text = run.levelState.objectiveProgress.ToString("00");
        v.footer.text = "No coins, upgrades or saved run state changed.";
        ShowBaseScreen(gm.failPanel); v.motion.Play();
    }
}
