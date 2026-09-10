using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

/// <summary>The single runtime presentation builder. Serialized panel roots and the gameplay
/// grid survive; legacy children are replaced once. All displayed state comes from GameManager.</summary>
public sealed partial class RogueliteUIController : MonoBehaviour
{
    private NeonTheme T => NeonTheme.T;
    private GameManager gm;
    private RectTransform canvas, menu, play, settings, shop;
    private GameObject shopPanel, abandonDialog;
    private Button abandonButton;
    private TMP_Text menuState, menuResources, menuWallet, health, reserve, pending, rule, timerLabel, objective, modifiers, debugBadge;
    private TMP_Text wallet, shopNotice, shopFeedback, abandonMessage, settingsHeading;
    private Image healthFill, timerFill, progressFill, ruleRail;
    private Toggle hapticsToggle, reducedToggle;
    private Action startRequested, abandonConfirmed, upgradesRequested, settingsRequested, shopClosed;
    private Action<bool> hapticsChanged;
    private Func<UpgradeId,bool> purchaseRequested;
    private GameConfig config;
    private PlayerProfileData profile;
    private bool hasActiveRun;
    private long pendingCoins;
    private long displayedPending = long.MinValue;
    private bool displayedDebug;
    private int cachedLevel;
    private string modifierText;
    private readonly Dictionary<UpgradeId, UpgradeView> cards = new Dictionary<UpgradeId, UpgradeView>();
    private ResultView successView, failView;
    private float feedbackUntil;
    private NeonHudMotion hudMotion;
    private bool levelTransitionActive;
    private LevelData transitionNextLevel;
    private Color gameplayPrimary = Color.white, gameplayAccent = Color.white, transitionSourcePrimary, transitionSourceAccent;
    private Color transitionTimerFrom, transitionTimerLabelFrom, transitionTimerFillFrom;
    private TMP_Text transitionTitle, transitionSubtitle;
    private CanvasGroup transitionGroup;
    private RectTransform transitionRect;
    private LevelTransitionAnnouncementSettings transitionAnnouncementSettings;
    private float transitionAnnouncementDuration;
    private readonly Dictionary<GameObject, NeonScreenMotion> screens = new Dictionary<GameObject, NeonScreenMotion>();
    private sealed class UpgradeView
    {
        public TMP_Text title, description, tier, benefit, status, price;
        public Button buy;
        public Image progress;
        public readonly List<Image> segments = new List<Image>();
        public NeonUpgradeGraphic purchaseFrame, buttonFrame;
        public Color accent;
        public NeonPurchaseMotion motion;
    }
    private sealed class ResultView
    {
        public TMP_Text eyebrow, title, description, amount, amountLabel, leftLabel, leftValue, rightLabel, rightValue, footer;
        public NeonShape emblem;
        public TMP_Text reason;
        public NeonRunReportGraphic reportEmblem;
        public Material headlineGlow;
        public NeonResultMotion motion;
    }
    public bool IsShopVisible => WantsVisible(shopPanel);
    public bool IsAbandonConfirmationVisible => WantsVisible(abandonDialog);
    public bool IsSettingsVisible => CoversInput(gm.settingsPanel);
    public bool IsGameplayReady => screens.TryGetValue(gm.gameplayPanel, out var motion) && motion.IsReady;

    public void Initialize(GameManager manager)
    {
        gm = manager; canvas = gm.mainMenuPanel.transform.parent as RectTransform;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) { scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0; }
        // Preserve the authored grid and flash references before retiring old gameplay decoration.
        RectTransform bounds = gm.gridContainer.parent as RectTransform;
        bounds.SetParent(canvas,false);
        if (gm.flashOverlay != null) gm.flashOverlay.transform.SetParent(canvas,false);
        menu = ResetPanel(gm.mainMenuPanel);
        play = ResetPanel(gm.gameplayPanel);
        settings = ResetPanel(gm.settingsPanel);
        RectTransform success = ResetPanel(gm.successPanel), fail = ResetPanel(gm.failPanel);
        bounds.SetParent(play,false); Fill(bounds,22,254,22,490);
        var boundsImage = bounds.GetComponent<Image>(); if(boundsImage!=null) { boundsImage.color=Color.clear; boundsImage.raycastTarget=false; }
        if (gm.flashOverlay != null) { gm.flashOverlay.transform.SetParent(play,false); Fill(gm.flashOverlay.rectTransform); gm.flashOverlay.transform.SetAsLastSibling(); }
        CreateMenu(); CreateHud(); CreateSettings();
        successView = CreateResult(success,true); failView = CreateResult(fail,false);
        CreateShop(); CreateAbandonDialog();
        foreach (var motion in screens.Values) motion.SetImmediate(false);
    }

    private RectTransform ResetPanel(GameObject panel)
    {
        panel.SetActive(true);
        foreach(Transform child in panel.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var bg=panel.GetComponent<Image>();if(bg==null)bg=panel.AddComponent<Image>();bg.sprite=null;
        bg.color=panel==gm.settingsPanel ? new Color(.004f,.011f,.022f,.9f) : T.Background;bg.raycastTarget=true;
        foreach(var effect in panel.GetComponents<BaseMeshEffect>()) Destroy(effect);
        Fill((RectTransform)panel.transform);
        var safe=Rect("SafeContent",panel.transform); Fill(safe); safe.gameObject.AddComponent<NeonSafeArea>();
        RegisterScreen(panel, safe, panel == gm.gameplayPanel ? NeonScreenMotion.Kind.Gameplay :
            panel == gm.settingsPanel ? NeonScreenMotion.Kind.Modal :
            panel == gm.successPanel || panel == gm.failPanel ? NeonScreenMotion.Kind.Result : NeonScreenMotion.Kind.Page);
        return safe;
    }

    private void RegisterScreen(GameObject panel, RectTransform visual, NeonScreenMotion.Kind kind)
    {
        var motion = panel.AddComponent<NeonScreenMotion>();
        motion.Initialize(visual, kind);
        motion.Settled += RefreshInputLayers;
        screens.Add(panel, motion);
    }
    private bool WantsVisible(GameObject panel) => panel != null && screens.TryGetValue(panel, out var motion) && motion.WantsVisible;
    private bool CoversInput(GameObject panel) => panel != null && screens.TryGetValue(panel, out var motion) && motion.CoversInput;
    private void SetVisible(GameObject panel, bool visible, Action onHidden = null)
    {
        if (panel != null && screens.TryGetValue(panel, out var motion)) motion.SetVisible(visible, onHidden);
        else onHidden?.Invoke();
    }
    private void SetBasePanels(GameObject destination)
    {
        SetVisible(gm.mainMenuPanel, destination == gm.mainMenuPanel);
        SetVisible(gm.gameplayPanel, destination == gm.gameplayPanel);
        SetVisible(gm.successPanel, destination == gm.successPanel);
        SetVisible(gm.failPanel, destination == gm.failPanel);
        SetVisible(gm.settingsPanel, false);
        if (destination != null) destination.transform.SetAsLastSibling();
    }
    public void ShowBaseScreen(GameObject destination)
    {
        CancelRunEnding();
        SetBasePanels(destination);
        HideShop();
        HideAbandonConfirmation();
        RefreshInputLayers();
    }
    public void ShowSettings()
    {
        SetVisible(gm.settingsPanel, true);
        RefreshInputLayers();
    }
    public void DismissSettings(Action onHidden)
    {
        SetVisible(gm.settingsPanel, false, onHidden);
        RefreshInputLayers();
    }
    public void SetGameplayFeedbackPaused(bool paused) { if (!levelTransitionActive || paused) hudMotion?.SetPaused(paused); }
    public void PlayHealthDamageFeedback() => hudMotion?.PlayHealthDamage(1);
    public void BeginTerminalHudFeedback(bool reserveFailure)
    {
        hudMotion?.BeginTerminalPresentation(reserveFailure);
        terminalTimerTint = gm.timerText.color;
        terminalDialTint = timeDial.color;
    }
    public void RenderTerminalHudShutdown(float amount)
    {
        if (timerGlow != null)
        {
            Color glow = terminalTimerTint;
            glow.a = .48f * (1f - Mathf.Clamp01(amount));
            timerGlow.SetColor("_UnderlayColor", glow);
        }
        Color dial = terminalDialTint;
        dial.a *= 1f - Mathf.Clamp01(amount) * .8f;
        timeDial.color = dial;
    }
    public void EndTerminalHudFeedback()
    {
        hudMotion?.EndTerminalPresentation();
        SetTimerGlow(terminalTimerTint);
        timeDial.color = terminalDialTint;
    }
    public void NormalizeGameplayFeedback() { if (!levelTransitionActive) hudMotion?.NormalizeImmediate(); }
    public void SettleGameplayEntrance()
    {
        if (screens.TryGetValue(gm.gameplayPanel, out var motion)) motion.SettleVisible();
    }
    private TMP_Text Txt(string name,Transform parent,string value,float size,float x,float y,float w,float h,Color? color=null,float ax=0,float ay=1)
    {
        var t=Text(name,parent,value,size,color??T.Text); At(t.rectTransform,ax,ay,x+w*.5f,y-h*.5f,w,h); return t;
    }
    private TMP_Text Eyebrow(Transform parent,string text,float y=70)
    { var t=Txt("Eyebrow",parent,text,T.labelSize,T.pageMargin,-y,900,40,T.Muted); t.characterSpacing=3; return t; }
    private void Hairline(Transform parent,float y)
    { var r=Rule("Divider",parent); r.rectTransform.anchorMin=new Vector2(0,1); r.rectTransform.anchorMax=new Vector2(1,1); r.rectTransform.offsetMin=new Vector2(T.pageMargin,-y-T.ruleWidth);r.rectTransform.offsetMax=new Vector2(-T.pageMargin,-y); }
    private Button WideButton(string name,Transform parent,string label,float bottom,bool primary)
    {
        var b=Button(name,parent,label,primary); var r=(RectTransform)b.transform;
        r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);
        r.offsetMin=new Vector2(T.pageMargin,bottom);r.offsetMax=new Vector2(-T.pageMargin,bottom+(primary?136:T.controlHeight));
        if(primary) { var arrow=Shape("Forward",b.transform,NeonShape.Kind.Arrow,T.Background,4); At(arrow.rectTransform,1,.5f,-62,0,44,44); }
        return b;
    }

    private Image Track(string name,Transform parent,Vector2 topLeft,float width,float height)
    {
        var track=Rule(name,parent); At(track.rectTransform,0,1,topLeft.x+width*.5f,topLeft.y,width,height);
        var fill=Rule("Fill",track.transform,T.Primary); Fill(fill.rectTransform); return fill;
    }
    private static void SetFill(Image image,float amount,Color color)
    { if(image==null)return; image.color=color; var r=image.rectTransform; r.anchorMax=new Vector2(Mathf.Clamp01(amount),1);r.offsetMin=r.offsetMax=Vector2.zero; }

    public void BindCallbacks(Action startOrContinue,Action abandon,Action openUpgrades,Action openSettings,Action closeShop,Action<bool> changeHaptics,Func<UpgradeId,bool> purchaseUpgrade)
    {
        startRequested=startOrContinue;abandonConfirmed=abandon;upgradesRequested=openUpgrades;settingsRequested=openSettings;shopClosed=closeShop;hapticsChanged=changeHaptics;purchaseRequested=purchaseUpgrade;
        gm.startContinueButton.onClick.AddListener(()=>startRequested?.Invoke());
        gm.upgradesButton.onClick.AddListener(()=>upgradesRequested?.Invoke());
        gm.menuSettingsButton.onClick.AddListener(()=>settingsRequested?.Invoke());
    }
    public void UpdateLevelContext(LevelData level,int count)
    {
        hudMotion.NormalizeImmediate();
        cachedLevel=level.levelNumber;
        gm.levelText.text=$"{cachedLevel:00}<size=30><color=#7393A7> / {count}</color></size>";
        var list=new List<string>(); if(!Mathf.Approximately(level.rotateSpeed,0))list.Add("ROTATE");if(level.scaleEnabled)list.Add("SCALE");if(level.movementEnabled)list.Add("MOVE");
        modifierText=string.Join(" · ",list); modifiers.text=modifierText;
        ApplyGameplayTheme(level.textPrimaryColor,level.outlineColor);
    }

    public void BeginLevelTransition(int completedLevelNumber, LevelData next, int campaignCount, ActiveRunData run, bool debug,
        float sourceNormalTime, string announcementSubline = null, float totalDuration = 0f)
    {
        if (next == null || run == null) return;
        transitionNextLevel = next; levelTransitionActive = true;
        transitionTimerFrom = gm.timerText.color; transitionTimerLabelFrom = timerLabel.color; transitionTimerFillFrom = timerFill.color;
        hudMotion.NormalizeImmediate(); hudMotion.SetPaused(true);
        transitionSourcePrimary = gameplayPrimary; transitionSourceAccent = gameplayAccent;
        cachedLevel = next.levelNumber;
        gm.levelText.text = $"{cachedLevel:00}<size=30><color=#7393A7> / {campaignCount}</color></size>";
        var list = new List<string>(); if (!Mathf.Approximately(next.rotateSpeed, 0)) list.Add("ROTATE"); if (next.scaleEnabled) list.Add("SCALE"); if (next.movementEnabled) list.Add("MOVE");
        modifierText = string.Join(" · ", list); modifiers.text = modifierText;
        rule.text = "LARGEST OUTLINE"; rule.color = ruleRail.color = PlayCyan;
        health.SetText("{0}<color=#7393A7> / {1}  HEALTH</color>", Mathf.Max(0, run.currentHealth), run.upgrades.maxHealth);
        health.color = run.currentHealth <= 1 ? T.Danger : PlayWhite;
        reserve.SetText("<size=23>RESERVE</size>  <b>{0:1}s</b>", Mathf.Max(0, run.currentReserveSeconds)); reserve.color = PlayGold;
        pending.text = debug ? "PRACTICE SESSION" : $"+{Math.Max(0, run.pendingCoins):N0} PENDING COINS";
        displayedPending = run.pendingCoins; displayedDebug = debug;
        debugBadge.gameObject.SetActive(debug); SetFill(progressFill, 0f, PlayCyan); SetFill(healthFill, run.currentHealth / (float)Mathf.Max(1, run.upgrades.maxHealth), run.currentHealth <= 1 ? T.Danger : PlayLime);
        RefreshHudGraphics(run.currentHealth, run.upgrades.maxHealth, 0, false, false);
        hudMotion.SetState(run.currentHealth, run.upgrades.maxHealth, 0, Mathf.Max(1, next.requiredCorrectClicks), false);
        transitionTitle.text = completedLevelNumber < 0 ? "BACK TO YOUR RUN" :
            completedLevelNumber == 0 ? $"LEVEL {next.levelNumber:00}" : $"LEVEL {completedLevelNumber} COMPLETE";
        transitionAnnouncementDuration = totalDuration > 0f ? totalDuration : NeonMotion.T.levelTransitionDuration;
        ConfigureTransitionAnnouncement(announcementSubline);
        transitionGroup.alpha = 0f; transitionRect.localScale = NeonTheme.ReducedEffects ? Vector3.one : Vector3.one * .96f; transitionGroup.gameObject.SetActive(true);
        RenderLevelTransition(0f, 0f, sourceNormalTime);
    }

    public void RenderLevelTransition(float normalizedProgress, float easedProgress)
    { RenderLevelTransition(normalizedProgress, easedProgress, transitionDisplayedSourceTime); }

    private float transitionDisplayedSourceTime;
    private void RenderLevelTransition(float normalizedProgress, float easedProgress, float sourceTime)
    {
        if (!levelTransitionActive || transitionNextLevel == null) return;
        normalizedProgress = Mathf.Clamp01(normalizedProgress); easedProgress = Mathf.Clamp01(easedProgress); transitionDisplayedSourceTime = sourceTime;
        int required = Mathf.Max(1, transitionNextLevel.requiredCorrectClicks);
        float preparationDuration = Mathf.Max(0f, NeonMotion.T.levelTransitionPreparationDuration);
        float preparation = preparationDuration <= 0f ? 1f : Mathf.Clamp01(normalizedProgress * transitionAnnouncementDuration / preparationDuration);
        int shown = Mathf.Clamp(Mathf.FloorToInt(required * preparation + .0001f), 0, required);
        objective.SetText("<color=#7393A7><size=25>LEFT</size></color>  {0}<color=#7393A7>/{1}</color>", shown, required);
        float time = Mathf.Lerp(sourceTime, transitionNextLevel.timeLimit, easedProgress);
        timerLabel.text = "TIME"; timerLabel.fontSize = 23; timerLabel.characterSpacing = 3;
        timerLabel.color = Color.Lerp(transitionTimerLabelFrom, PlayMuted, easedProgress);
        gm.timerText.SetText("{0:1}<size=32>s</size>", Mathf.Max(0f, time));
        gm.timerText.color = Color.Lerp(transitionTimerFrom, transitionNextLevel.timeLimit <= 5f ? PlayGold : PlayCyan, easedProgress);
        SetTimerGlow(gm.timerText.color);
        float timeRatio = time / Mathf.Max(.01f, transitionNextLevel.timeLimit);
        SetFill(timerFill, timeRatio, Color.Lerp(transitionTimerFillFrom, PlayCyan, easedProgress));
        timeDial.value = Mathf.Clamp01(timeRatio); timeDial.SetVerticesDirty();
        ApplyGameplayTheme(Color.Lerp(transitionSourcePrimary, transitionNextLevel.textPrimaryColor, easedProgress), Color.Lerp(transitionSourceAccent, transitionNextLevel.outlineColor, easedProgress));
        transitionTitle.color = PlayWhite;
        if (transitionSubtitle != null) transitionSubtitle.color = PlayWhite;
        float phaseDuration = transitionAnnouncementSettings == null ? 0f : transitionAnnouncementSettings.PhaseDuration;
        float entranceEnd = phaseDuration <= 0f ? 0f : Mathf.Clamp01(transitionAnnouncementSettings.entranceDuration / phaseDuration);
        float exitStart = phaseDuration <= 0f ? 1f : Mathf.Clamp01((transitionAnnouncementSettings.entranceDuration + transitionAnnouncementSettings.holdDuration) / phaseDuration);
        float entrance = entranceEnd <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, entranceEnd, normalizedProgress));
        float exit = exitStart >= 1f ? (normalizedProgress >= 1f ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(exitStart, 1f, normalizedProgress));
        transitionGroup.alpha = entrance * (1f - exit); transitionRect.localScale = NeonTheme.ReducedEffects ? Vector3.one : Vector3.one * Mathf.Lerp(.96f, 1f, entrance) * Mathf.Lerp(1f, .98f, exit);
    }

    private void ConfigureTransitionAnnouncement(string subline)
    {
        if (transitionAnnouncementSettings == null)
        {
            transitionAnnouncementSettings = Resources.Load<LevelTransitionAnnouncementSettings>("LevelTransitionAnnouncementSettings");
            if (transitionAnnouncementSettings == null) transitionAnnouncementSettings = ScriptableObject.CreateInstance<LevelTransitionAnnouncementSettings>();
        }
        bool hasSubline = !string.IsNullOrWhiteSpace(subline);
        if (transitionSubtitle == null)
        {
            transitionSubtitle = Text("TransitionSubtitle", transitionRect, string.Empty, 36, PlayWhite, TextAlignmentOptions.Center);
            transitionSubtitle.fontStyle = FontStyles.Normal;
            transitionSubtitle.enableAutoSizing = true; transitionSubtitle.fontSizeMin = 32; transitionSubtitle.fontSizeMax = 36;
            transitionSubtitle.textWrappingMode = TextWrappingModes.Normal;
        }
        // Keep the structural morph visible below the HUD-side announcement.
        if (hasSubline) At(transitionRect, .5f, 1f, 0, -604, 940, 220);
        else At(transitionRect, .5f, .5f, 0, -70, 840, 132);
        transitionTitle.fontSize = hasSubline ? 54 : 48; transitionTitle.enableAutoSizing = false;
        transitionTitle.fontStyle = FontStyles.Bold; transitionTitle.alignment = TextAlignmentOptions.Center;
        At(transitionTitle.rectTransform, .5f, .5f, 0, hasSubline ? 35 : 0, 780, 70);
        transitionSubtitle.text = hasSubline ? subline.Trim() : string.Empty;
        transitionSubtitle.gameObject.SetActive(hasSubline);
        if (hasSubline) At(transitionSubtitle.rectTransform, .5f, .5f, 0, -48, 820, 96);
    }

    public void EndLevelTransition()
    {
        if (!levelTransitionActive) return;
        RenderLevelTransition(1f, 1f, transitionNextLevel.timeLimit);
        ApplyGameplayTheme(transitionNextLevel.textPrimaryColor, transitionNextLevel.outlineColor);
        hudMotion.NormalizeImmediate(); hudMotion.SetPaused(false);
        transitionGroup.alpha = 0f; transitionGroup.gameObject.SetActive(false); transitionNextLevel = null; levelTransitionActive = false;
    }
    public void RefreshHud(int currentHealth,int maximumHealth,float reserveSeconds,float maximumReserveSeconds,long runPending,int progress,int required,float remaining,float timeLimit,bool reserveActive,bool reverseActive,bool debug,int reverseRemaining=0)
    {
        if (levelTransitionActive) return;
        health.SetText("{0}<color=#7393A7> / {1}  HEALTH</color>",Mathf.Max(0,currentHealth),maximumHealth);
        health.color=currentHealth<=1?T.Danger:PlayWhite;
        reserve.SetText(reserveActive?"<size=23>LEVEL TIME EXHAUSTED</size>":"<size=23>RESERVE</size>  <b>{0:1}s</b>",Mathf.Max(0,reserveSeconds));
        reserve.color=PlayGold;
        gm.timerText.SetText("{0:1}<size=32>s</size>",Mathf.Max(0,reserveActive?reserveSeconds:remaining));
        gm.timerText.color=reserveActive||remaining<=5?PlayGold:PlayCyan;
        SetTimerGlow(gm.timerText.color);
        timerLabel.text=reserveActive?"RESERVE · DRAINING":"TIME";timerLabel.color=reserveActive?PlayGold:PlayMuted;
        timerLabel.fontSize=reserveActive?18:23;timerLabel.characterSpacing=reserveActive?0:3;
        objective.SetText("<color=#7393A7><size=25>LEFT</size></color>  {0}<color=#7393A7>/{1}</color>",Mathf.Max(0,required-progress),required);
        progressFill.color = reverseActive ? PlayPink : PlayCyan;
        float timeRatio=(reserveActive?reserveSeconds:remaining)/Mathf.Max(.01f,reserveActive?maximumReserveSeconds:timeLimit);
        SetFill(timerFill,timeRatio,reserveActive?PlayGold:PlayCyan);
        SetFill(healthFill,currentHealth/(float)Mathf.Max(1,maximumHealth),currentHealth<=1?T.Danger:PlayLime);
        rule.text="LARGEST OUTLINE";rule.color=ruleRail.color=reverseActive?PlayPink:PlayCyan;
        // The rule stays upright; this count is the real remaining Reverse sequence.
        if (reverseActive)
        {
            rule.text = $"<size=20>REVERSE · {Mathf.Max(0, reverseRemaining)} LEFT</size>\nSMALLEST OUTLINE";
        }
        if(displayedPending!=runPending||displayedDebug!=debug)
        {
            pending.text=debug?"PRACTICE SESSION":$"+{Math.Max(0,runPending):N0} PENDING COINS";
            displayedPending=runPending;displayedDebug=debug;
        }
        debugBadge.gameObject.SetActive(debug);
        RefreshHudGraphics(currentHealth,maximumHealth,timeRatio,reserveActive,reverseActive);
        hudMotion.SetState(currentHealth, maximumHealth, progress, required, reserveActive);
    }
    public void ApplyGameplayTheme(Color primary,Color accent)
    {
        gameplayPrimary = primary; gameplayAccent = accent;
        gm.gameplayPanel.GetComponent<Image>().color=PlayBackground;
        if(Camera.main!=null)Camera.main.backgroundColor=PlayBackground;
        if(gm.levelText!=null)gm.levelText.color=PlayWhite;
        if(rule!=null) { rule.color=PlayCyan; ruleRail.color=PlayCyan; }
    }

    public void RefreshInputLayers()
    {
        // A newly enabled Graphic has depth -1 until Unity renders it. Disable
        // the underlay immediately so a second touch cannot pass through during
        // that frame. This changes input only, never simulation or pause state.
        if (gm == null) return;
        bool dialog=CoversInput(abandonDialog), paused=CoversInput(gm.settingsPanel);
        bool covered=dialog||paused||IsShopVisible;
        SetInput(gm.mainMenuPanel,!covered);SetInput(gm.gameplayPanel,!covered);
        SetInput(gm.successPanel,!covered);SetInput(gm.failPanel,!covered);
        SetInput(gm.settingsPanel,!dialog);SetInput(shopPanel,!dialog&&!paused);
        SetInput(abandonDialog,true);
        if (paused) gm.settingsPanel.transform.SetAsLastSibling();
        if (dialog) abandonDialog.transform.SetAsLastSibling();
    }
    private void SetInput(GameObject panel,bool allowed)
    {
        if (panel != null && screens.TryGetValue(panel, out var motion)) motion.SetInputAllowed(allowed && TerminalAllowsInput(panel));
    }

    private ResultView CreateResult(RectTransform root,bool success)
    {
        if (!success) return CreateRunReport(root);
        var v=new ResultView();v.eyebrow=Eyebrow(root,"CAMPAIGN / RUN REPORT");
        v.emblem=Shape("ResultMark",root,success?NeonShape.Kind.Check:NeonShape.Kind.Reticle,success?T.Primary:T.Danger,4);At(v.emblem.rectTransform,0,1,130,-211,128,128);
        v.title=Txt("ResultTitle",root,"",T.headingSize,T.pageMargin,-320,940,210);v.title.fontStyle=FontStyles.Bold;v.title.textWrappingMode=TextWrappingModes.Normal;
        v.description=Txt("ResultDescription",root,"",32,T.pageMargin,-565,940,118,T.Muted);v.description.textWrappingMode=TextWrappingModes.Normal;
        v.amountLabel=Txt("AmountLabel",root,"",24,T.pageMargin,-756,940,44,T.Muted);v.amountLabel.characterSpacing=3;
        v.amount=Txt("Amount",root,"",112,T.pageMargin,-806,940,140,T.Primary);v.amount.fontStyle=FontStyles.Bold;
        Hairline(root,1000);
        v.leftLabel=Txt("LeftLabel",root,"",24,T.pageMargin,-1050,460,40,T.Muted);
        v.leftValue=Txt("LeftValue",root,"",45,T.pageMargin,-1105,460,72);
        v.rightLabel=Txt("RightLabel",root,"",24,558,-1050,458,40,T.Muted);
        v.rightValue=Txt("RightValue",root,"",45,558,-1105,458,72);
        v.footer=Txt("ResultFooter",root,"",28,T.pageMargin,-1220,940,112,T.Muted);v.footer.textWrappingMode=TextWrappingModes.Normal;
        var primary=WideButton("ResultPrimary",root,"UPGRADES",210,true);var home=WideButton("ResultHome",root,"RETURN HOME",72,false);
        if(success){gm.successLevelText=v.title;gm.successNextButton=primary;gm.successMenuButton=home;}
        else {gm.failLevelText=v.title;gm.failReasonText=v.description;gm.failPrimaryButton=primary;gm.failMenuButton=home;}
        v.motion = root.gameObject.AddComponent<NeonResultMotion>();
        v.motion.Add(v.emblem.rectTransform, 0); v.motion.Add(v.title.rectTransform, 0);
        v.motion.Add(v.amount.rectTransform, 1); v.motion.Add(v.amountLabel.rectTransform, 1);
        v.motion.Add(v.leftValue.rectTransform, 2); v.motion.Add(v.rightValue.rectTransform, 2);
        return v;
    }
    public void ShowLevelComplete(ActiveRunData run,int completed,int next,long reward,bool debug=false)
    {
        var v=successView;v.motion.NormalizeImmediate();v.emblem.kind=NeonShape.Kind.Check;At(v.emblem.rectTransform,0,1,130,-211,128,128);v.emblem.SetVerticesDirty();v.title.fontSize=T.headingSize;v.eyebrow.text=debug?"SANDBOX / LEVEL COMPLETE":"CAMPAIGN / LEVEL COMPLETE";
        v.title.text=$"LEVEL {completed:00}\nCOMPLETE.";v.description.text=debug?"Practice complete. Your real run is untouched.":$"Next up: Level {next:00}.\nYour health and reserve carry forward.";
        v.emblem.color=T.Primary;v.amount.color=T.Primary;v.amountLabel.text=debug?"PRACTICE SESSION":"LEVEL REWARD / PENDING";v.amount.text=debug?"WELL PLAYED":$"+{reward:N0}<size=34> COINS</size>";
        v.amount.fontSize=debug?66:112;v.leftLabel.text="HEALTH CARRIED";v.leftValue.text=$"{run.currentHealth} / {run.upgrades.maxHealth}";
        v.rightLabel.text="RESERVE CARRIED";v.rightValue.text=$"{run.currentReserveSeconds:0.0}<size=28>s</size>";
        v.footer.text=debug?"No coins or progression changed.":$"{run.pendingCoins:N0} pending run coins\nBanked when this run ends.";
        ShowBaseScreen(gm.successPanel); v.motion.Play();
    }
    public void ShowRunSummary(RunSummaryData summary)
    {
        if (!summary.campaignCompleted) { ShowFailureReport(summary); return; }
        var v=summary.campaignCompleted?successView:failView;bool win=summary.campaignCompleted;
        v.motion.NormalizeImmediate();
        v.eyebrow.text=win?"NEON REFLEX / CAMPAIGN COMPLETE":"CAMPAIGN / RUN REPORT";
        v.title.text=win?"CAMPAIGN\nCONQUERED.":summary.reason=="RUN ABANDONED"?"RUN\nCLOSED.":"RUN\nENDED.";
        v.title.fontSize=win?78:T.headingSize;
        v.emblem.kind=win?NeonShape.Kind.Reticle:NeonShape.Kind.Reverse;
        At(v.emblem.rectTransform,win?1:0,1,win?-198:130,-211,win?220:128,win?220:128);
        v.emblem.SetVerticesDirty();
        v.description.text=win?"Every level cleared. Precision, all the way.":summary.reason=="RUN ABANDONED"?"You ended this run.\nYour completed levels still count.":summary.reason.Contains("HEALTH")?"Health depleted.\nTake what you earned. Come back stronger.":"Reserve depleted.\nTake what you earned. Come back stronger.";
        v.emblem.color=win?T.Primary:summary.reason=="RUN ABANDONED"?T.Muted:summary.reason.Contains("HEALTH")?T.Danger:T.Reserve;
        v.amountLabel.text="COINS EARNED / BANKED";v.amount.text=$"+{summary.totalEarned:N0}";v.amount.fontSize=112;v.amount.color=T.Primary;
        v.leftLabel.text="LEVEL REACHED";v.leftValue.text=summary.highestLevelEntered.ToString("00");v.rightLabel.text="LEVELS COMPLETED";v.rightValue.text=summary.levelsCompleted.ToString("00");
        v.footer.text=win?$"{summary.runLevelRewards:N0} level rewards + {summary.completionBonus:N0} completion bonus\nPermanent balance: {summary.newWalletBalance:N0} coins":$"Permanent balance: {summary.newWalletBalance:N0} coins\nPut your earnings into your next run.";
        ShowBaseScreen(win ? gm.successPanel : gm.failPanel); v.motion.Play();
    }

    public void ShowDebugRunEnded(string reason,ActiveRunData run,int level)
    {
        ShowPracticeReport(reason, run, level);
    }

    private string Effect(UpgradeId id,float value)
    {
        switch(id){case UpgradeId.MaximumHealth:return $"{Mathf.Clamp(Mathf.RoundToInt(value),1,config.maximumHealthCap)} HP";case UpgradeId.StartingReserve:return $"{value:0.#}s";case UpgradeId.GridStabilizer:return $"{value*100:0}% speed";default:return $"+{value:0.#}s cooldown";}
    }
    void Update()
    { if(feedbackUntil>0){feedbackUntil=Mathf.Max(0,feedbackUntil-NeonMotion.Delta());if(feedbackUntil<=0){shopFeedback.text="PERMANENT BENEFITS. APPLIED TO NEW RUNS.";shopFeedback.color=ShopMuted;}} }
    public void HideShop(bool notify=false)
    { feedbackUntil=0;if(shopFeedback!=null){shopFeedback.text="PERMANENT BENEFITS. APPLIED TO NEW RUNS.";shopFeedback.color=ShopMuted;}SetVisible(shopPanel,false);RefreshInputLayers();if(notify)shopClosed?.Invoke(); }

    private void CreateAbandonDialog()
    {
        abandonDialog=Panel("AbandonConfirmation",canvas,T.Background,true).gameObject;Fill((RectTransform)abandonDialog.transform);
        var safe=Rect("SafeContent",abandonDialog.transform);Fill(safe);safe.gameObject.AddComponent<NeonSafeArea>();
        RegisterScreen(abandonDialog, safe, NeonScreenMotion.Kind.Modal);
        var body=Rect("Dialog",safe);At(body,.5f,.5f,0,0,952,850);
        var glyph=Shape("EndGlyph",body,NeonShape.Kind.Reverse,T.Reserve,4);At(glyph.rectTransform,0,1,46,-38,72,72);
        Txt("Title",body,"END THIS\nRUN?",86,0,-143,930,210).fontStyle=FontStyles.Bold;
        abandonMessage=Txt("Explanation",body,"",34,0,-387,930,175,T.Muted);abandonMessage.textWrappingMode=TextWrappingModes.Normal;
        var confirm=Button("ConfirmAbandon",body,"END RUN & BANK COINS",true);At((RectTransform)confirm.transform,.5f,0,0,204,952,120);confirm.image.color=T.Reserve;
        confirm.onClick.AddListener(()=>{HideAbandonConfirmation();abandonConfirmed?.Invoke();});
        var cancel=Button("KeepRun",body,"KEEP RUN");At((RectTransform)cancel.transform,.5f,0,0,62,952,112);cancel.onClick.AddListener(HideAbandonConfirmation);
    }
    public void ShowAbandonConfirmation()
    { abandonMessage.text=$"Your run will end. {pendingCoins:N0} pending coins from completed levels will be banked.\nThe current level earns no coins.";SetVisible(abandonDialog,true);RefreshInputLayers(); }
    public void HideAbandonConfirmation(){SetVisible(abandonDialog,false);RefreshInputLayers();}
}
