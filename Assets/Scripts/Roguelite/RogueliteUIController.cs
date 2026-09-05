using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the extra roguelite presentation at runtime. Core run and economy
/// state stay owned by GameManager and UpgradeCatalog.
/// </summary>
public sealed class RogueliteUIController : MonoBehaviour
{
    private static readonly Color Cyan = new Color(0f, 0.96f, 1f, 1f);
    private static readonly Color Pink = new Color(1f, 0.08f, 0.48f, 1f);
    private static readonly Color Lime = new Color(0.55f, 1f, 0.38f, 1f);
    private static readonly Color Gold = new Color(1f, 0.76f, 0.22f, 1f);
    private static readonly Color Danger = new Color(1f, 0.16f, 0.25f, 1f);
    private static readonly Color Dark = new Color(0.01f, 0.015f, 0.045f, 0.985f);
    private static readonly Color Surface = new Color(0.025f, 0.035f, 0.09f, 0.98f);
    private static readonly Color RaisedSurface = new Color(0.055f, 0.07f, 0.16f, 1f);
    private static readonly Color Muted = new Color(0.56f, 0.62f, 0.76f, 1f);

    private sealed class UpgradeCardView
    {
        public RectTransform root;
        public Image background;
        public Outline outline;
        public Image icon;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text effect;
        public TMP_Text tier;
        public TMP_Text status;
        public Image progressFill;
        public Button purchaseButton;
        public Color accent;
    }

    private RectTransform canvasRoot;
    private RectTransform mainMenuRoot;
    private RectTransform gameplayRoot;
    private RectTransform settingsRoot;
    private Button startContinueButton;
    private Button upgradesButton;
    private Button settingsButton;
    private Toggle hapticsToggle;
    private Sprite buttonSprite;
    private TMP_Text gameplayLevelText;
    private TMP_Text gameplayTimerText;
    private TMP_Text gameplayObjectiveText;
    private Button gameplayHomeButton;
    private Button gameplaySettingsButton;
    private RectTransform gameplayGridContainer;

    private Button abandonButton;
    private TMP_Text menuStatus;
    private TMP_Text healthValueText;
    private TMP_Text reserveValueText;
    private TMP_Text pendingValueText;
    private TMP_Text debugBadgeText;
    private Image healthFill;
    private Image reserveFill;
    private Image timerFill;
    private Image objectiveFill;
    private Outline gameplayTopOutline;
    private Outline gameplayVitalsOutline;
    private GameObject shopPanel;
    private TMP_Text walletText;
    private TMP_Text shopNoticeText;
    private TMP_Text shopFeedbackText;
    private GameObject abandonDialog;
    private TMP_Text abandonMessage;
    private readonly Dictionary<UpgradeId, UpgradeCardView> upgradeCards = new Dictionary<UpgradeId, UpgradeCardView>();
    private Coroutine purchaseFeedbackCoroutine;

    private Action startOrContinueRequested;
    private Action abandonConfirmed;
    private Action upgradesRequested;
    private Action settingsRequested;
    private Action shopClosed;
    private Action<bool> hapticsChanged;
    private Func<UpgradeId, bool> purchaseRequested;
    private GameConfig config;
    private PlayerProfileData profile;
    private bool hasActiveRun;
    private long pendingCoins;

    public bool IsShopVisible => shopPanel != null && shopPanel.activeSelf;
    public bool IsAbandonConfirmationVisible => abandonDialog != null && abandonDialog.activeSelf;

    public void Initialize(
        RectTransform canvas,
        RectTransform mainMenu,
        RectTransform gameplay,
        RectTransform settings,
        Button startContinue,
        Button upgrades,
        Button menuSettings,
        Toggle existingHaptics,
        TMP_Text existingLevelText,
        TMP_Text existingTimerText,
        TMP_Text existingObjectiveText,
        Button existingHomeButton,
        Button existingGameplaySettingsButton,
        RectTransform existingGridContainer,
        Sprite sprite)
    {
        canvasRoot = canvas;
        mainMenuRoot = mainMenu;
        gameplayRoot = gameplay;
        settingsRoot = settings;
        startContinueButton = startContinue;
        upgradesButton = upgrades;
        settingsButton = menuSettings;
        hapticsToggle = existingHaptics;
        gameplayLevelText = existingLevelText;
        gameplayTimerText = existingTimerText;
        gameplayObjectiveText = existingObjectiveText;
        gameplayHomeButton = existingHomeButton;
        gameplaySettingsButton = existingGameplaySettingsButton;
        gameplayGridContainer = existingGridContainer;
        buttonSprite = sprite;

        if (canvasRoot == null)
            return;

        CreateMenuExtras();
        CreateHud();
        CreateShop();
        CreateAbandonDialog();
        EnsureHapticsToggle();
        BindButtons();
    }

    public void BindCallbacks(
        Action startOrContinue,
        Action abandon,
        Action openUpgrades,
        Action openSettings,
        Action closeShop,
        Action<bool> changeHaptics,
        Func<UpgradeId, bool> purchaseUpgrade)
    {
        startOrContinueRequested = startOrContinue;
        abandonConfirmed = abandon;
        upgradesRequested = openUpgrades;
        settingsRequested = openSettings;
        shopClosed = closeShop;
        hapticsChanged = changeHaptics;
        purchaseRequested = purchaseUpgrade;
        BindButtons();
    }

    public void RefreshMenu(bool activeRun, int currentLevelNumber, long wallet, long pending)
    {
        hasActiveRun = activeRun;
        pendingCoins = Math.Max(0L, pending);

        SetButtonLabel(startContinueButton, activeRun ? "CONTINUE RUN" : "START RUN");
        SetButtonLabel(upgradesButton, "UPGRADES");
        SetButtonLabel(settingsButton, "SETTINGS");
        if (abandonButton != null)
            abandonButton.gameObject.SetActive(activeRun);

        LayoutMenuButtons(activeRun);
        if (menuStatus != null)
        {
            menuStatus.text = activeRun
                ? $"WALLET {Math.Max(0L, wallet):N0}  •  RUN LEVEL {Mathf.Max(1, currentLevelNumber)}  •  PENDING {pendingCoins:N0}"
                : $"WALLET {Math.Max(0L, wallet):N0}  •  NEXT RUN STARTS AT LEVEL 1";
        }
    }

    public void RefreshHud(
        int health,
        int maximumHealth,
        float reserveSeconds,
        float maximumReserveSeconds,
        long pending,
        int objectiveProgress,
        int objectiveRequired,
        float timeRemaining,
        float timeLimit,
        bool reserveActive,
        bool reverseActive,
        bool debug)
    {
        int safeHealth = Mathf.Max(0, health);
        int safeMaximumHealth = Mathf.Max(1, maximumHealth);
        float safeReserve = Mathf.Max(0f, reserveSeconds);
        float safeMaximumReserve = Mathf.Max(0.01f, maximumReserveSeconds);
        int safeRequired = Mathf.Max(1, objectiveRequired);

        if (healthValueText != null)
            healthValueText.text = $"<b>{safeHealth}</b><size=22><color=#7D89AA> / {safeMaximumHealth}</color></size>";
        if (reserveValueText != null)
            reserveValueText.text = $"<b>{safeReserve:0.0}</b><size=20><color=#7D89AA>s</color></size>";
        if (pendingValueText != null)
            pendingValueText.text = $"<color=#FFD66B>+</color><b>{Math.Max(0L, pending):N0}</b>";

        SetFill(healthFill, safeHealth / (float)safeMaximumHealth, safeHealth <= 1 ? Danger : Lime);
        SetFill(reserveFill, safeReserve / safeMaximumReserve, reserveActive ? Pink : Cyan);
        SetFill(objectiveFill, Mathf.Clamp(objectiveProgress, 0, safeRequired) / (float)safeRequired, reverseActive ? Pink : Cyan);

        float timerMaximum = reserveActive ? safeMaximumReserve : Mathf.Max(0.01f, timeLimit);
        float timerCurrent = reserveActive ? safeReserve : Mathf.Max(0f, timeRemaining);
        Color timerColor = reserveActive ? Pink : timeRemaining <= 5f ? Danger : Cyan;
        SetFill(timerFill, timerCurrent / timerMaximum, timerColor);

        if (debugBadgeText != null)
        {
            debugBadgeText.gameObject.SetActive(debug);
            debugBadgeText.text = "DEBUG SANDBOX • PROGRESS IS NOT SAVED";
        }
    }

    public void ApplyGameplayTheme(Color primary, Color accent)
    {
        if (gameplayLevelText != null)
            gameplayLevelText.color = primary;
        if (gameplayTopOutline != null)
            gameplayTopOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.7f);
        if (gameplayVitalsOutline != null)
            gameplayVitalsOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.42f);
    }

    public void ShowShop(GameConfig gameConfig, PlayerProfileData player, bool activeRun)
    {
        config = gameConfig;
        profile = player;
        hasActiveRun = activeRun;
        RefreshShop();
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            shopPanel.transform.SetAsLastSibling();
        }
    }

    public void RefreshShop(GameConfig gameConfig, PlayerProfileData player, bool activeRun)
    {
        config = gameConfig;
        profile = player;
        hasActiveRun = activeRun;
        RefreshShop();
    }

    public void HideShop(bool notify = false)
    {
        if (purchaseFeedbackCoroutine != null)
        {
            StopCoroutine(purchaseFeedbackCoroutine);
            purchaseFeedbackCoroutine = null;
        }
        foreach (UpgradeCardView card in upgradeCards.Values)
            card.root.localScale = Vector3.one;
        if (shopFeedbackText != null)
        {
            shopFeedbackText.text = "UPGRADES ARE PERMANENT • EFFECTS SNAPSHOT WHEN A NEW RUN STARTS";
            shopFeedbackText.color = Muted;
        }
        if (shopPanel != null)
            shopPanel.SetActive(false);
        if (notify)
            shopClosed?.Invoke();
    }

    public void ShowAbandonConfirmation()
    {
        if (abandonDialog == null)
            return;

        if (abandonMessage != null)
        {
            abandonMessage.text =
                "ABANDON ACTIVE RUN?\n\n" +
                $"{pendingCoins:N0} pending coins from completed levels will be banked. " +
                "The current level earns nothing and the run will end.";
        }
        abandonDialog.SetActive(true);
        abandonDialog.transform.SetAsLastSibling();
    }

    public void HideAbandonConfirmation()
    {
        if (abandonDialog != null)
            abandonDialog.SetActive(false);
    }

    public void SetHaptics(bool enabled)
    {
        hapticsToggle?.SetIsOnWithoutNotify(enabled);
    }

    private void CreateMenuExtras()
    {
        if (mainMenuRoot == null)
            return;

        abandonButton = CreateButton("AbandonRunButton", mainMenuRoot, "ABANDON RUN", new Color(0.55f, 0.08f, 0.2f, 1f));
        SetRect(abandonButton.GetComponent<RectTransform>(), new Vector2(600f, 120f), new Vector2(0f, -220f));
        abandonButton.gameObject.SetActive(false);

        menuStatus = CreateText("RogueliteStatus", mainMenuRoot, 27f, Cyan);
        menuStatus.textWrappingMode = TextWrappingModes.Normal;
        SetRect(menuStatus.rectTransform, new Vector2(940f, 90f), new Vector2(0f, -670f));
    }

    private void LayoutMenuButtons(bool activeRun)
    {
        SetMenuButtonPosition(startContinueButton, activeRun ? -60f : -100f);
        if (activeRun)
        {
            SetMenuButtonPosition(abandonButton, -220f);
            SetMenuButtonPosition(upgradesButton, -380f);
            SetMenuButtonPosition(settingsButton, -540f);
        }
        else
        {
            SetMenuButtonPosition(upgradesButton, -260f);
            SetMenuButtonPosition(settingsButton, -420f);
        }
    }

    private static void SetMenuButtonPosition(Button button, float y)
    {
        if (button == null)
            return;
        button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
    }

    private void CreateHud()
    {
        if (gameplayRoot == null)
            return;

        ConfigureGameplayTopBar();

        GameObject vitals = CreatePanel("RunVitals", gameplayRoot, Surface);
        RectTransform vitalsRect = vitals.GetComponent<RectTransform>();
        SetAnchoredRect(vitalsRect, new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.832f), Vector2.zero, Vector2.zero);
        Image vitalsImage = vitals.GetComponent<Image>();
        vitalsImage.raycastTarget = false;
        ApplyRoundedSprite(vitalsImage);
        gameplayVitalsOutline = vitals.AddComponent<Outline>();
        gameplayVitalsOutline.effectColor = new Color(0f, 0.96f, 1f, 0.42f);
        gameplayVitalsOutline.effectDistance = new Vector2(2f, -2f);

        CreateHudMetric(
            "Integrity",
            vitals.transform,
            new Vector2(0f, 0f),
            new Vector2(0.333f, 1f),
            "INTEGRITY",
            Lime,
            out healthValueText,
            out healthFill);
        CreateHudMetric(
            "Reserve",
            vitals.transform,
            new Vector2(0.333f, 0f),
            new Vector2(0.667f, 1f),
            "RESERVE",
            Cyan,
            out reserveValueText,
            out reserveFill);
        CreateHudMetric(
            "RunBank",
            vitals.transform,
            new Vector2(0.667f, 0f),
            new Vector2(1f, 1f),
            "RUN BANK",
            Gold,
            out pendingValueText,
            out _);

        debugBadgeText = CreateText("DebugBadge", gameplayRoot, 17f, new Color(1f, 0.55f, 0.18f, 1f));
        debugBadgeText.fontStyle = FontStyles.Bold;
        debugBadgeText.characterSpacing = 2f;
        SetPointRect(debugBadgeText.rectTransform, new Vector2(0.5f, 0.752f), new Vector2(560f, 38f), Vector2.zero);
        debugBadgeText.gameObject.SetActive(false);

        RectTransform gridBounds = gameplayGridContainer == null ? null : gameplayGridContainer.parent as RectTransform;
        if (gridBounds != null)
        {
            gridBounds.anchorMin = new Vector2(0.05f, 0.055f);
            gridBounds.anchorMax = new Vector2(0.95f, 0.745f);
            gridBounds.offsetMin = Vector2.zero;
            gridBounds.offsetMax = Vector2.zero;
        }
    }

    private void ConfigureGameplayTopBar()
    {
        RectTransform topBar = gameplayLevelText == null ? null : gameplayLevelText.transform.parent as RectTransform;
        if (topBar == null)
            return;

        SetAnchoredRect(topBar, new Vector2(0.035f, 0.845f), new Vector2(0.965f, 0.955f), Vector2.zero, Vector2.zero);
        Image background = topBar.GetComponent<Image>();
        if (background == null)
            background = topBar.gameObject.AddComponent<Image>();
        background.color = Surface;
        background.raycastTarget = false;
        ApplyRoundedSprite(background);

        gameplayTopOutline = topBar.GetComponent<Outline>();
        if (gameplayTopOutline == null)
            gameplayTopOutline = topBar.gameObject.AddComponent<Outline>();
        gameplayTopOutline.effectColor = new Color(0f, 0.96f, 1f, 0.7f);
        gameplayTopOutline.effectDistance = new Vector2(2f, -2f);

        StyleGameplayLabel(gameplayLevelText, new Vector2(0.25f, 0.56f), new Vector2(260f, 118f), 32f);
        StyleGameplayLabel(gameplayTimerText, new Vector2(0.5f, 0.56f), new Vector2(250f, 124f), 40f);
        StyleGameplayLabel(gameplayObjectiveText, new Vector2(0.75f, 0.56f), new Vector2(260f, 118f), 32f);

        StyleGameplayButton(gameplayHomeButton, "MENU", new Vector2(0.065f, 0.55f), Cyan);
        StyleGameplayButton(gameplaySettingsButton, "PAUSE", new Vector2(0.935f, 0.55f), Pink);

        CreateProgressTrack(
            "TimerProgress",
            topBar,
            new Vector2(0.38f, 0.105f),
            new Vector2(0.62f, 0.145f),
            Cyan,
            out timerFill);
        CreateProgressTrack(
            "ObjectiveProgress",
            topBar,
            new Vector2(0.65f, 0.105f),
            new Vector2(0.85f, 0.145f),
            Cyan,
            out objectiveFill);
    }

    private static void StyleGameplayLabel(TMP_Text text, Vector2 anchor, Vector2 size, float fontSize)
    {
        if (text == null)
            return;
        SetPointRect(text.rectTransform, anchor, size, Vector2.zero);
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private void StyleGameplayButton(Button button, string label, Vector2 anchor, Color accent)
    {
        if (button == null)
            return;

        SetPointRect(button.GetComponent<RectTransform>(), anchor, new Vector2(108f, 76f), Vector2.zero);
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 1f);
            ApplyRoundedSprite(image);
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);

        SetButtonLabel(button, label);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.fontSize = 19f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1.5f;
            text.color = Color.white;
        }
    }

    private void CreateHudMetric(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label,
        Color accent,
        out TMP_Text valueText,
        out Image fill)
    {
        GameObject metric = CreatePanel(objectName, parent, RaisedSurface);
        SetAnchoredRect(
            metric.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            new Vector2(7f, 8f),
            new Vector2(-7f, -8f));
        Image metricImage = metric.GetComponent<Image>();
        metricImage.raycastTarget = false;
        ApplyRoundedSprite(metricImage);

        TMP_Text labelText = CreateText("Label", metric.transform, 16f, Muted);
        labelText.text = label;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 2.2f;
        SetPointRect(labelText.rectTransform, new Vector2(0.5f, 0.73f), new Vector2(260f, 25f), Vector2.zero);

        valueText = CreateText("Value", metric.transform, 31f, Color.white);
        valueText.fontStyle = FontStyles.Bold;
        SetPointRect(valueText.rectTransform, new Vector2(0.5f, 0.43f), new Vector2(260f, 48f), Vector2.zero);

        CreateProgressTrack(
            "Progress",
            metric.transform as RectTransform,
            new Vector2(0.09f, 0.11f),
            new Vector2(0.91f, 0.18f),
            accent,
            out fill);
    }

    private void CreateShop()
    {
        shopPanel = CreatePanel("RogueliteShop", canvasRoot, Dark);
        Stretch(shopPanel.GetComponent<RectTransform>());

        GameObject header = CreatePanel("Header", shopPanel.transform, Surface);
        SetAnchoredRect(
            header.GetComponent<RectTransform>(),
            new Vector2(0f, 0.82f),
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        header.GetComponent<Image>().raycastTarget = false;

        GameObject topAccent = CreatePanel("TopAccent", header.transform, Cyan);
        SetAnchoredRect(
            topAccent.GetComponent<RectTransform>(),
            new Vector2(0f, 0.985f),
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        topAccent.GetComponent<Image>().raycastTarget = false;

        TMP_Text eyebrow = CreateText("Eyebrow", header.transform, 17f, Muted);
        eyebrow.text = "NEON REFLEX // PERMANENT SYSTEMS";
        eyebrow.alignment = TextAlignmentOptions.MidlineLeft;
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.characterSpacing = 2.5f;
        SetPointRect(eyebrow.rectTransform, new Vector2(0.43f, 0.78f), new Vector2(520f, 34f), Vector2.zero);

        TMP_Text title = CreateText("Title", header.transform, 54f, Color.white);
        title.text = "UPGRADE <color=#00F5FF>LAB</color>";
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 0.6f;
        SetPointRect(title.rectTransform, new Vector2(0.43f, 0.55f), new Vector2(520f, 78f), Vector2.zero);

        TMP_Text subtitle = CreateText("Subtitle", header.transform, 21f, Muted);
        subtitle.text = "Build the next run before it begins.";
        subtitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetPointRect(subtitle.rectTransform, new Vector2(0.43f, 0.32f), new Vector2(520f, 38f), Vector2.zero);

        Button back = CreateButton("Back", header.transform, "BACK", new Color(0.07f, 0.09f, 0.19f, 1f));
        SetPointRect(back.GetComponent<RectTransform>(), new Vector2(0.09f, 0.55f), new Vector2(150f, 78f), Vector2.zero);
        StyleOutlinedButton(back, Cyan);
        back.onClick.AddListener(() => HideShop(true));

        GameObject walletPanel = CreatePanel("Wallet", header.transform, new Color(0.09f, 0.075f, 0.025f, 1f));
        SetPointRect(walletPanel.GetComponent<RectTransform>(), new Vector2(0.84f, 0.55f), new Vector2(245f, 112f), Vector2.zero);
        Image walletBackground = walletPanel.GetComponent<Image>();
        walletBackground.raycastTarget = false;
        ApplyRoundedSprite(walletBackground);
        Outline walletOutline = walletPanel.AddComponent<Outline>();
        walletOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.75f);
        walletOutline.effectDistance = new Vector2(2f, -2f);

        walletText = CreateText("Value", walletPanel.transform, 34f, Color.white);
        walletText.fontStyle = FontStyles.Bold;
        Stretch(walletText.rectTransform);

        shopNoticeText = CreateText("ShopNotice", shopPanel.transform, 19f, Muted);
        shopNoticeText.fontStyle = FontStyles.Bold;
        shopNoticeText.characterSpacing = 1.5f;
        SetPointRect(shopNoticeText.rectTransform, new Vector2(0.5f, 0.795f), new Vector2(940f, 45f), Vector2.zero);

        for (int i = 0; i < 4; i++)
        {
            UpgradeId id = (UpgradeId)i;
            float anchorY = 0.67f - i * 0.15f;
            upgradeCards[id] = CreateUpgradeCard(id, new Vector2(0.5f, anchorY), UpgradeAccent(id));
        }

        shopFeedbackText = CreateText("Feedback", shopPanel.transform, 18f, Muted);
        shopFeedbackText.fontStyle = FontStyles.Bold;
        shopFeedbackText.characterSpacing = 1.6f;
        SetPointRect(shopFeedbackText.rectTransform, new Vector2(0.5f, 0.065f), new Vector2(900f, 42f), Vector2.zero);
        shopFeedbackText.text = "UPGRADES ARE PERMANENT • EFFECTS SNAPSHOT WHEN A NEW RUN STARTS";

        shopPanel.SetActive(false);
    }

    private void RefreshShop()
    {
        long coins = Math.Max(0L, profile?.coins ?? 0L);
        if (walletText != null)
            walletText.text = $"<size=16><color=#C7A94A>COINS</color></size>\n{coins:N0}";

        UpdateShopNotice();

        foreach (KeyValuePair<UpgradeId, UpgradeCardView> pair in upgradeCards)
        {
            UpgradeId id = pair.Key;
            UpgradeCardView card = pair.Value;
            UpgradeDefinitionData definition = UpgradeCatalog.Get(config, id);
            UpgradeTierDefinition current = UpgradeCatalog.CurrentTier(config, profile, id);
            UpgradeTierDefinition next = UpgradeCatalog.NextTier(config, profile, id);
            int tier = UpgradeCatalog.ClampTier(config, id, UpgradeCatalog.GetTier(profile, id));
            int maxTier = definition?.tiers?.Count ?? 0;
            string disabled = UpgradeCatalog.DisabledReason(config, profile, id, hasActiveRun);
            bool maxed = next == null;
            bool affordable = next != null && coins >= next.cost;
            bool available = string.IsNullOrEmpty(disabled);

            string currentEffect = current == null ? BaseEffect(id) : FormatEffect(id, current.effectValue);
            string nextEffect = next == null ? "MAX" : FormatEffect(id, next.effectValue);
            card.title.text = definition?.displayName?.ToUpperInvariant() ?? id.ToString().ToUpperInvariant();
            card.description.text = definition?.description ?? string.Empty;
            card.effect.text = maxed
                ? $"<color=#AAB3D0>CURRENT</color>  <b>{currentEffect}</b>"
                : $"<color=#AAB3D0>{currentEffect}</color>  <color=#{ColorUtility.ToHtmlStringRGB(card.accent)}>→</color>  <b>{nextEffect}</b>";
            card.tier.text = $"TIER {tier} / {maxTier}";
            SetFill(card.progressFill, maxTier <= 0 ? 0f : tier / (float)maxTier, card.accent);

            if (maxed)
            {
                card.status.text = "SYSTEM MAXED";
                card.status.color = Lime;
                SetButtonLabel(card.purchaseButton, "MAXED");
            }
            else if (hasActiveRun)
            {
                card.status.text = "LOCKED DURING ACTIVE RUN";
                card.status.color = Pink;
                SetButtonLabel(card.purchaseButton, "RUN\nACTIVE");
            }
            else if (!affordable)
            {
                card.status.text = $"NEED {Math.Max(0L, next.cost - coins):N0} MORE COINS";
                card.status.color = Gold;
                SetButtonLabel(card.purchaseButton, $"<size=19>INSTALL</size>\n<b>{next.cost:N0}</b> <size=15>COINS</size>");
            }
            else if (!available)
            {
                card.status.text = disabled.ToUpperInvariant();
                card.status.color = Danger;
                SetButtonLabel(card.purchaseButton, "UNAVAILABLE");
            }
            else
            {
                card.status.text = "READY TO INSTALL";
                card.status.color = card.accent;
                SetButtonLabel(card.purchaseButton, $"<size=19>INSTALL</size>\n<b>{next.cost:N0}</b> <size=15>COINS</size>");
            }

            card.purchaseButton.interactable = available;
            card.purchaseButton.image.color = available
                ? new Color(card.accent.r * 0.55f, card.accent.g * 0.55f, card.accent.b * 0.55f, 1f)
                : new Color(0.12f, 0.13f, 0.2f, 1f);
            card.icon.color = maxed
                ? new Color(0.58f, 0.68f, 0.8f, 0.8f)
                : Color.white;
            card.background.color = maxed
                ? new Color(0.04f, 0.05f, 0.1f, 1f)
                : RaisedSurface;
            card.outline.effectColor = new Color(card.accent.r, card.accent.g, card.accent.b, maxed ? 0.28f : 0.62f);
        }
    }

    private UpgradeCardView CreateUpgradeCard(UpgradeId id, Vector2 anchor, Color accent)
    {
        var view = new UpgradeCardView { accent = accent };
        GameObject card = CreatePanel(id + "Card", shopPanel.transform, RaisedSurface);
        view.root = card.GetComponent<RectTransform>();
        SetPointRect(view.root, anchor, new Vector2(960f, 260f), Vector2.zero);
        view.background = card.GetComponent<Image>();
        ApplyRoundedSprite(view.background);
        view.outline = card.AddComponent<Outline>();
        view.outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.62f);
        view.outline.effectDistance = new Vector2(2f, -2f);

        GameObject accentRail = CreatePanel("AccentRail", card.transform, accent);
        SetAnchoredRect(
            accentRail.GetComponent<RectTransform>(),
            new Vector2(0f, 0.08f),
            new Vector2(0.008f, 0.92f),
            new Vector2(2f, 0f),
            new Vector2(2f, 0f));
        accentRail.GetComponent<Image>().raycastTarget = false;

        GameObject frame = CreatePanel("IconFrame", card.transform, new Color(0.008f, 0.012f, 0.045f, 1f));
        SetPointRect(frame.GetComponent<RectTransform>(), new Vector2(0.12f, 0.5f), new Vector2(210f, 210f), Vector2.zero);

        Image frameImage = frame.GetComponent<Image>();
        frameImage.raycastTarget = false;
        ApplyRoundedSprite(frameImage);

        Outline frameOutline = frame.AddComponent<Outline>();
        frameOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.72f);
        frameOutline.effectDistance = new Vector2(2f, -2f);

        Sprite sprite = Resources.Load<Sprite>(UpgradeIconResourcePath(id));
        GameObject artwork = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        artwork.transform.SetParent(frame.transform, false);
        view.icon = artwork.GetComponent<Image>();
        view.icon.sprite = sprite;
        view.icon.color = Color.white;
        view.icon.preserveAspect = true;
        view.icon.raycastTarget = false;
        SetRect(view.icon.rectTransform, new Vector2(194f, 194f), Vector2.zero);

        if (sprite == null)
            Debug.LogWarning($"Missing upgrade icon at Resources/{UpgradeIconResourcePath(id)}");

        view.title = CreateText("Title", card.transform, 31f, Color.white);
        view.title.alignment = TextAlignmentOptions.MidlineLeft;
        view.title.fontStyle = FontStyles.Bold;
        view.title.characterSpacing = 0.8f;
        SetPointRect(view.title.rectTransform, new Vector2(0.43f, 0.76f), new Vector2(350f, 46f), Vector2.zero);

        GameObject tierChip = CreatePanel("TierChip", card.transform, new Color(0.025f, 0.035f, 0.085f, 1f));
        SetPointRect(tierChip.GetComponent<RectTransform>(), new Vector2(0.68f, 0.76f), new Vector2(132f, 38f), Vector2.zero);
        Image tierChipImage = tierChip.GetComponent<Image>();
        tierChipImage.raycastTarget = false;
        ApplyRoundedSprite(tierChipImage);
        view.tier = CreateText("Text", tierChip.transform, 16f, Muted);
        view.tier.fontStyle = FontStyles.Bold;
        view.tier.characterSpacing = 1.2f;
        Stretch(view.tier.rectTransform);

        view.description = CreateText("Description", card.transform, 21f, new Color(0.78f, 0.82f, 0.92f, 1f));
        view.description.alignment = TextAlignmentOptions.MidlineLeft;
        view.description.textWrappingMode = TextWrappingModes.NoWrap;
        SetPointRect(view.description.rectTransform, new Vector2(0.49f, 0.55f), new Vector2(470f, 38f), Vector2.zero);

        view.effect = CreateText("Effect", card.transform, 23f, Color.white);
        view.effect.alignment = TextAlignmentOptions.MidlineLeft;
        SetPointRect(view.effect.rectTransform, new Vector2(0.49f, 0.36f), new Vector2(470f, 40f), Vector2.zero);

        view.status = CreateText("Status", card.transform, 15f, accent);
        view.status.alignment = TextAlignmentOptions.MidlineLeft;
        view.status.fontStyle = FontStyles.Bold;
        view.status.characterSpacing = 1.4f;
        SetPointRect(view.status.rectTransform, new Vector2(0.49f, 0.2f), new Vector2(470f, 30f), Vector2.zero);

        CreateProgressTrack(
            "TierProgress",
            view.root,
            new Vector2(0.25f, 0.085f),
            new Vector2(0.75f, 0.125f),
            accent,
            out view.progressFill);

        view.purchaseButton = CreateButton("Purchase", card.transform, "INSTALL", accent);
        SetPointRect(view.purchaseButton.GetComponent<RectTransform>(), new Vector2(0.865f, 0.5f), new Vector2(210f, 122f), Vector2.zero);
        StyleOutlinedButton(view.purchaseButton, accent);
        TMP_Text purchaseLabel = view.purchaseButton.GetComponentInChildren<TMP_Text>();
        if (purchaseLabel != null)
            purchaseLabel.fontSize = 24f;

        UpgradeId capturedId = id;
        view.purchaseButton.onClick.AddListener(() =>
        {
            if (purchaseRequested != null && purchaseRequested(capturedId))
            {
                RefreshShop();
                PlayPurchaseFeedback(capturedId);
            }
        });
        return view;
    }

    private static Color UpgradeAccent(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth:
                return Pink;
            case UpgradeId.StartingReserve:
                return Cyan;
            case UpgradeId.GridStabilizer:
                return Lime;
            default:
                return Pink;
        }
    }

    private void UpdateShopNotice()
    {
        if (shopNoticeText == null)
            return;
        shopNoticeText.text = hasActiveRun
            ? "PURCHASES LOCKED  •  FINISH OR ABANDON THE ACTIVE RUN"
            : "SELECT A SYSTEM  •  REVIEW THE CHANGE  •  INSTALL PERMANENTLY";
        shopNoticeText.color = hasActiveRun ? Pink : Muted;
    }

    private void PlayPurchaseFeedback(UpgradeId id)
    {
        if (!upgradeCards.TryGetValue(id, out UpgradeCardView card))
            return;
        if (purchaseFeedbackCoroutine != null)
            StopCoroutine(purchaseFeedbackCoroutine);
        purchaseFeedbackCoroutine = StartCoroutine(PurchaseFeedbackRoutine(card));
    }

    private IEnumerator PurchaseFeedbackRoutine(UpgradeCardView card)
    {
        if (shopFeedbackText != null)
        {
            shopFeedbackText.text = $"{card.title.text} INSTALLED  •  NEXT RUN UPDATED";
            shopFeedbackText.color = card.accent;
        }

        float elapsed = 0f;
        const float duration = 0.24f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.42f
                ? Mathf.Lerp(1f, 1.025f, t / 0.42f)
                : Mathf.Lerp(1.025f, 1f, (t - 0.42f) / 0.58f);
            card.root.localScale = Vector3.one * scale;
            yield return null;
        }
        card.root.localScale = Vector3.one;

        float hold = 0f;
        while (hold < 1.15f)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }
        if (shopFeedbackText != null)
        {
            shopFeedbackText.text = "UPGRADES ARE PERMANENT • EFFECTS SNAPSHOT WHEN A NEW RUN STARTS";
            shopFeedbackText.color = Muted;
        }
        purchaseFeedbackCoroutine = null;
    }

    private static string UpgradeIconResourcePath(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth:
                return "UpgradeIcons/maximum-health";
            case UpgradeId.StartingReserve:
                return "UpgradeIcons/starting-reserve";
            case UpgradeId.GridStabilizer:
                return "UpgradeIcons/grid-stabilizer";
            default:
                return "UpgradeIcons/reverse-resistance";
        }
    }

    private string BaseEffect(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth:
                return $"{Mathf.Max(1, config?.baseHealth ?? 3)} HP";
            case UpgradeId.StartingReserve:
                return $"{Mathf.Max(0f, config?.baseStartingReserveSeconds ?? 15f):0}s";
            case UpgradeId.GridStabilizer:
                return "100% SPEED";
            default:
                return "+0s COOLDOWN";
        }
    }

    private static string FormatEffect(UpgradeId id, float value)
    {
        switch (id)
        {
            case UpgradeId.MaximumHealth:
                return $"{Mathf.RoundToInt(value)} HP";
            case UpgradeId.StartingReserve:
                return $"{value:0}s";
            case UpgradeId.GridStabilizer:
                return $"{value * 100f:0}% SPEED";
            default:
                return $"+{value:0}s COOLDOWN";
        }
    }

    private void CreateAbandonDialog()
    {
        abandonDialog = CreatePanel("AbandonConfirmation", canvasRoot, new Color(0.01f, 0.01f, 0.04f, 0.99f));
        Stretch(abandonDialog.GetComponent<RectTransform>());
        abandonMessage = CreateText("Message", abandonDialog.transform, 38f, Color.white);
        abandonMessage.textWrappingMode = TextWrappingModes.Normal;
        SetRect(abandonMessage.rectTransform, new Vector2(900f, 330f), new Vector2(0f, 180f));

        Button confirm = CreateButton("Confirm", abandonDialog.transform, "ABANDON", new Color(0.9f, 0.18f, 0.35f, 1f));
        SetRect(confirm.GetComponent<RectTransform>(), new Vector2(390f, 104f), new Vector2(-225f, -130f));
        confirm.onClick.AddListener(() =>
        {
            HideAbandonConfirmation();
            abandonConfirmed?.Invoke();
        });

        Button cancel = CreateButton("Cancel", abandonDialog.transform, "KEEP RUN", new Color(0.14f, 0.6f, 0.8f, 1f));
        SetRect(cancel.GetComponent<RectTransform>(), new Vector2(390f, 104f), new Vector2(225f, -130f));
        cancel.onClick.AddListener(HideAbandonConfirmation);
        abandonDialog.SetActive(false);
    }

    private void EnsureHapticsToggle()
    {
        if (hapticsToggle == null && settingsRoot != null)
        {
            GameObject toggleObject = new GameObject("HapticsToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(settingsRoot, false);
            SetRect(toggleObject.GetComponent<RectTransform>(), new Vector2(560f, 88f), new Vector2(0f, -250f));
            hapticsToggle = toggleObject.GetComponent<Toggle>();

            GameObject box = CreatePanel("Box", toggleObject.transform, new Color(0.08f, 0.09f, 0.18f, 1f));
            SetRect(box.GetComponent<RectTransform>(), new Vector2(64f, 64f), new Vector2(-220f, 0f));
            GameObject check = CreatePanel("Checkmark", box.transform, Cyan);
            RectTransform checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            hapticsToggle.targetGraphic = box.GetComponent<Image>();
            hapticsToggle.graphic = check.GetComponent<Image>();

            TMP_Text label = CreateText("Label", toggleObject.transform, 31f, Color.white);
            label.text = "HAPTICS";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            SetRect(label.rectTransform, new Vector2(390f, 70f), new Vector2(70f, 0f));
        }

        if (hapticsToggle != null)
        {
            hapticsToggle.onValueChanged.RemoveAllListeners();
            hapticsToggle.onValueChanged.AddListener(value => hapticsChanged?.Invoke(value));
        }
    }

    private void BindButtons()
    {
        Bind(startContinueButton, () => startOrContinueRequested?.Invoke());
        Bind(upgradesButton, () => upgradesRequested?.Invoke());
        Bind(settingsButton, () => settingsRequested?.Invoke());
        Bind(abandonButton, ShowAbandonConfirmation);
    }

    private static void Bind(Button button, Action callback)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback?.Invoke());
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return child;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        ApplyRoundedSprite(image);
        Button button = child.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.76f, 0.86f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.48f, 0.5f, 0.58f, 0.68f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        TMP_Text text = CreateText("Text", child.transform, 28f, Color.white);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, float size, Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text text = button == null ? null : button.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = value;
    }

    private void StyleOutlinedButton(Button button, Color accent)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
            ApplyRoundedSprite(image);
        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void ApplyRoundedSprite(Image image)
    {
        if (image == null || buttonSprite == null)
            return;
        image.sprite = buttonSprite;
        image.type = Image.Type.Sliced;
    }

    private void CreateProgressTrack(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color accent,
        out Image fill)
    {
        GameObject track = CreatePanel(objectName, parent, new Color(0.012f, 0.016f, 0.045f, 1f));
        SetAnchoredRect(track.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        Image trackImage = track.GetComponent<Image>();
        trackImage.raycastTarget = false;
        ApplyRoundedSprite(trackImage);

        GameObject fillObject = CreatePanel("Fill", track.transform, accent);
        fill = fillObject.GetComponent<Image>();
        fill.raycastTarget = false;
        ApplyRoundedSprite(fill);
        SetAnchoredRect(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void SetFill(Image fill, float amount, Color color)
    {
        if (fill == null)
            return;
        float clamped = Mathf.Clamp01(amount);
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(clamped, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fill.color = color;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetPointRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
