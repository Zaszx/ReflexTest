using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

public sealed partial class RogueliteUIController
{
    private static readonly Color ShopInk = NeonTheme.Hex("060D1B");
    private static readonly Color ShopText = NeonTheme.Hex("E9F3FF");
    private static readonly Color ShopMuted = NeonTheme.Hex("99ADC6");
    private static readonly Color ShopGold = NeonTheme.Hex("FFD166");

    private static Color UpgradeAccent(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth: return NeonTheme.Hex("39FFB6");
            case UpgradeId.StartingReserve: return ShopGold;
            case UpgradeId.GridStabilizer: return NeonTheme.Hex("25CFFF");
            case UpgradeId.ReverseResistance: return NeonTheme.Hex("CE70FF");
            case UpgradeId.Rebound: return NeonTheme.Hex("FF6B8A");
            default: return NeonTheme.Hex("69E8A5");
        }
    }

    private static NeonUpgradeGraphic UpgradeArt(string name, Transform parent, NeonUpgradeGraphic.Kind kind, Color accent)
    {
        var rect = Rect(name, parent);
        var graphic = rect.gameObject.AddComponent<NeonUpgradeGraphic>();
        graphic.kind = kind; graphic.color = accent; graphic.raycastTarget = false;
        return graphic;
    }

    private void CreateShop()
    {
        shopPanel = Panel("PermanentUpgrades", canvas, ShopInk, true).gameObject;
        Fill((RectTransform)shopPanel.transform);
        shop = Rect("SafeContent", shopPanel.transform); Fill(shop);
        shop.gameObject.AddComponent<NeonSafeArea>();
        RegisterScreen(shopPanel, shop, NeonScreenMotion.Kind.Page);

        var eyebrow = Txt("Eyebrow", shop, "NEON REFLEX  /  PERMANENT UPGRADES", 22, 54, -64, 950, 36, ShopMuted);
        eyebrow.characterSpacing = 2;
        var heading = Txt("ShopTitle", shop, "UPGRADES", 82, 50, -122, 675, 108, ShopText);
        heading.fontStyle = FontStyles.Bold; heading.characterSpacing = 1;

        var bank = UpgradeArt("WalletFrame", shop, NeonUpgradeGraphic.Kind.Card, ShopGold);
        At(bank.rectTransform, 1, 1, -193, -182, 278, 114);
        var coin = UpgradeArt("BankCoin", bank.transform, NeonUpgradeGraphic.Kind.Coin, ShopGold);
        At(coin.rectTransform, 0, .5f, 40, 3, 38, 38);
        wallet = Txt("BankedCoins", bank.transform, "", 34, 70, -18, 188, 43, ShopText);
        wallet.fontStyle = FontStyles.Bold; wallet.enableAutoSizing = true; wallet.fontSizeMin = 20; wallet.fontSizeMax = 34;
        Txt("WalletLabel", bank.transform, "BANKED COINS", 17, 72, -67, 185, 28, ShopMuted).characterSpacing = 2;
        shopNotice = Txt("ShopNotice", shop, "", 25, 54, -256, 965, 76, ShopMuted);
        shopNotice.textWrappingMode = TextWrappingModes.Normal;

        var viewport = Panel("UpgradeViewport", shop, Color.clear, true);
        Fill(viewport.rectTransform, 36, 242, 36, 356);
        viewport.gameObject.AddComponent<RectMask2D>();
        var list = Rect("UpgradeList", viewport.transform);
        list.anchorMin = new Vector2(0, 1); list.anchorMax = Vector2.one; list.pivot = new Vector2(.5f, 1);
        list.sizeDelta = new Vector2(0, 1788); list.anchoredPosition = Vector2.zero;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = list; scroll.viewport = viewport.rectTransform;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        for (int i = 0; i < 6; i++) cards[(UpgradeId)i] = CreateUpgrade((UpgradeId)i, list, i);

        shopFeedback = Txt("PurchaseFeedback", shop, "PERMANENT BENEFITS. APPLIED TO NEW RUNS.", 20, 54, 225, 970, 40, ShopMuted, 0, 0);
        shopFeedback.characterSpacing = 1;
        var back = WideButton("ShopBack", shop, "RETURN HOME", 64, false);
        back.image.color = NeonTheme.Hex("142339");
        back.GetComponentInChildren<TMP_Text>().color = ShopText;
        back.onClick.AddListener(() => HideShop(true));
    }

    private UpgradeView CreateUpgrade(UpgradeId id, Transform parent, int index)
    {
        var v = new UpgradeView { accent = UpgradeAccent(id) };
        var row = Rect(id.ToString(), parent);
        row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(.5f, 1);
        row.sizeDelta = new Vector2(-8, 278); row.anchoredPosition = new Vector2(0, -index * 298 - 4);
        var frame = UpgradeArt("CardFrame", row, NeonUpgradeGraphic.Kind.Card, v.accent); Fill(frame.rectTransform);
        var socket = UpgradeArt("IconSocket", row, NeonUpgradeGraphic.Kind.Socket, v.accent);
        At(socket.rectTransform, 0, 1, 112, -130, 176, 208);
        var icon = UpgradeArt("UpgradeGlyph", socket.transform, UpgradeIcon(id), v.accent);
        At(icon.rectTransform, .5f, .5f, 0, 0, 110, 120);

        v.title = Txt("Title", row, "", 28, 223, -30, 372, 43, ShopText);
        v.title.fontStyle = FontStyles.Bold; v.title.characterSpacing = 1;
        v.tier = Txt("Tier", row, "", 19, 598, -34, 150, 34, ShopMuted);
        v.tier.alignment = TextAlignmentOptions.MidlineRight; v.tier.characterSpacing = 1;
        v.description = Txt("Description", row, "", 23, 223, -80, 526, 69, ShopMuted);
        v.description.textWrappingMode = TextWrappingModes.Normal;
        v.benefit = Txt("Benefit", row, "", 29, 223, -151, 526, 43, v.accent);
        v.benefit.fontStyle = FontStyles.Bold;
        v.progress = Panel("TierProgress", row, Color.clear);
        At(v.progress.rectTransform, 0, 1, 486, -211, 526, 19);
        v.status = Txt("Status", row, "", 17, 223, -237, 747, 26, ShopMuted);
        v.status.characterSpacing = 1.2f;

        v.purchaseFrame = UpgradeArt("PriceFrame", row, NeonUpgradeGraphic.Kind.Card, v.accent);
        At(v.purchaseFrame.rectTransform, 1, 1, -127, -140, 218, 178);
        var coin = UpgradeArt("PriceCoin", v.purchaseFrame.transform, NeonUpgradeGraphic.Kind.Coin, ShopGold);
        At(coin.rectTransform, 0, 1, 51, -49, 35, 35);
        v.price = Txt("Price", v.purchaseFrame.transform, "", 34, 77, -24, 124, 51, ShopText);
        v.price.enableAutoSizing = true; v.price.fontSizeMin = 22; v.price.fontSizeMax = 34;
        v.buy = Button("Purchase", v.purchaseFrame.transform, "UPGRADE", false);
        At((RectTransform)v.buy.transform, .5f, 0, 0, 48, 192, 73);
        v.buy.image.color = Color.clear;
        v.buttonFrame = UpgradeArt("ButtonEdge", v.buy.transform, NeonUpgradeGraphic.Kind.Card, v.accent);
        Fill(v.buttonFrame.rectTransform, -2, -2, -2, -2); v.buttonFrame.transform.SetAsFirstSibling();
        var label = v.buy.GetComponentInChildren<TMP_Text>();
        label.fontSize = 24; label.characterSpacing = 2; label.alignment = TextAlignmentOptions.Center;
        Fill(label.rectTransform, 3, 0, 3, 0);
        v.motion = row.gameObject.AddComponent<NeonPurchaseMotion>();
        v.motion.Initialize(row, v.accent, v.tier, v.benefit, wallet, v.progress);
        v.buy.onClick.AddListener(() =>
        {
            if (purchaseRequested == null || !purchaseRequested(id)) return;
            RefreshShop(); v.status.text = "INSTALLED / NEXT RUN UPDATED"; v.status.color = v.accent;
            shopFeedback.text = v.title.text + " INSTALLED"; shopFeedback.color = v.accent;
            feedbackUntil = 1.8f; v.motion.PlayCommitted();
        });
        return v;
    }

    private static NeonUpgradeGraphic.Kind UpgradeIcon(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth: return NeonUpgradeGraphic.Kind.Health;
            case UpgradeId.StartingReserve: return NeonUpgradeGraphic.Kind.Reserve;
            case UpgradeId.GridStabilizer: return NeonUpgradeGraphic.Kind.Stabilizer;
            case UpgradeId.ReverseResistance: return NeonUpgradeGraphic.Kind.Reverse;
            case UpgradeId.Rebound: return NeonUpgradeGraphic.Kind.Rebound;
            default: return NeonUpgradeGraphic.Kind.Healing;
        }
    }

    public void ShowShop(GameConfig gameConfig, PlayerProfileData player, bool activeRun)
    {
        config = gameConfig; profile = player; hasActiveRun = activeRun; RefreshShop();
        SetBasePanels(null); SetVisible(shopPanel, true); shopPanel.transform.SetAsLastSibling(); RefreshInputLayers();
    }

    public void RefreshShop(GameConfig gameConfig, PlayerProfileData player, bool activeRun)
    { config = gameConfig; profile = player; hasActiveRun = activeRun; RefreshShop(); }

    private void RefreshShop()
    {
        long coins = Math.Max(0, profile?.coins ?? 0); wallet.text = $"{coins:N0}";
        shopNotice.text = hasActiveRun
            ? "RUN IN PROGRESS  /  Purchases unlock when this run ends."
            : "Build a stronger start. Every upgrade stays with you.";
        shopNotice.color = hasActiveRun ? ShopGold : ShopMuted;
        foreach (var pair in cards)
        {
            UpgradeId id = pair.Key; var v = pair.Value;
            var definition = UpgradeCatalog.Get(config, id);
            var current = UpgradeCatalog.CurrentTier(config, profile, id);
            var next = UpgradeCatalog.NextTier(config, profile, id);
            int tier = UpgradeCatalog.ClampTier(config, id, UpgradeCatalog.GetTier(profile, id));
            int max = definition?.tiers?.Count ?? 0;
            v.title.text = (definition?.displayName ?? id.ToString()).ToUpperInvariant();
            v.tier.text = id == UpgradeId.Rebound ? (tier > 0 ? "OWNED" : "LOCKED") : $"TIER {tier} / {max:00}";
            v.description.text = definition?.description ?? "";
            float value = current?.effectValue ?? (id == UpgradeId.MaximumHealth ? config.baseHealth :
                id == UpgradeId.StartingReserve ? config.baseStartingReserveSeconds : id == UpgradeId.GridStabilizer ? 1 : 0);
            v.benefit.text = next == null ? Effect(id, value) : $"<color=#D3DFEF>{Effect(id, value)}</color>   →   {Effect(id, next.effectValue)}";
            RefreshTierSegments(v, tier, max);
            bool allowed = string.IsNullOrEmpty(UpgradeCatalog.DisabledReason(config, profile, id, hasActiveRun));
            v.buy.interactable = allowed;
            v.buy.image.color = Color.clear;
            v.buy.GetComponentInChildren<TMP_Text>().color = allowed ? ShopText : ShopMuted;
            v.price.text = next == null ? "MAX" : $"{next.cost:N0}";
            v.price.color = next == null ? v.accent : ShopText;
            ButtonText(v.buy, next == null ? "MAXED" : hasActiveRun ? "RUN ACTIVE" : "UPGRADE");
            v.status.text = next == null ? "MAXIMUM BENEFIT REACHED" : hasActiveRun ? "AVAILABLE AFTER THIS RUN" :
                coins < next.cost ? $"{next.cost - coins:N0} MORE COINS NEEDED" : "READY TO UPGRADE";
            v.status.color = allowed ? v.accent : ShopMuted;
            v.purchaseFrame.intensity = allowed ? 1f : .55f; v.purchaseFrame.SetVerticesDirty();
            v.buttonFrame.intensity = allowed ? 1f : .35f; v.buttonFrame.SetVerticesDirty();
            v.buttonFrame.surfaceTint = allowed ? .4f : .13f;
        }
    }

    private static void RefreshTierSegments(UpgradeView view, int tier, int count)
    {
        if (view.segments.Count != count)
        {
            foreach (var old in view.segments) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            view.segments.Clear();
            for (int i = 0; i < count; i++)
            {
                var segment = Panel("Tier" + (i + 1), view.progress.transform, Color.white);
                float width = Mathf.Min(38f, (526f - 4f * (count - 1)) / Mathf.Max(1, count));
                At(segment.rectTransform, 0, .5f, i * (width + 4) + width * .5f, 0, width, 19);
                var fill = Panel("Fill", segment.transform, Color.white); Fill(fill.rectTransform, 1, 1, 1, 1);
                view.segments.Add(segment);
            }
        }
        for (int i = 0; i < view.segments.Count; i++)
        {
            bool installed = i < tier;
            view.segments[i].color = installed ? Color.Lerp(view.accent, Color.white, .32f) : NeonTheme.Hex("2E425F");
            view.segments[i].transform.GetChild(0).GetComponent<Image>().color = installed ? view.accent : NeonTheme.Hex("182941");
        }
    }
}
