using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

/// <summary>The single runtime presentation builder. Serialized panel roots and the gameplay
/// grid survive; legacy children are replaced once. All displayed state comes from GameManager.</summary>
public sealed class RogueliteUIController : MonoBehaviour
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
    private sealed class UpgradeView
    {
        public TMP_Text title, description, tier, benefit, status;
        public Button buy;
        public Image progress;
        public Color accent;
    }
    private sealed class ResultView
    {
        public TMP_Text eyebrow, title, description, amount, amountLabel, leftLabel, leftValue, rightLabel, rightValue, footer;
        public NeonShape emblem;
    }
    public bool IsShopVisible => shopPanel != null && shopPanel.activeSelf;
    public bool IsAbandonConfirmationVisible => abandonDialog != null && abandonDialog.activeSelf;

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
        bounds.SetParent(play,false); Fill(bounds,40,234,40,476);
        var boundsImage = bounds.GetComponent<Image>(); if(boundsImage!=null) { boundsImage.color=Color.clear; boundsImage.raycastTarget=false; }
        if (gm.flashOverlay != null) { gm.flashOverlay.transform.SetParent(play,false); Fill(gm.flashOverlay.rectTransform); gm.flashOverlay.transform.SetAsLastSibling(); }
        CreateMenu(); CreateHud(); CreateSettings();
        successView = CreateResult(success,true); failView = CreateResult(fail,false);
        CreateShop(); CreateAbandonDialog();
        gm.mainMenuPanel.SetActive(true); gm.gameplayPanel.SetActive(false); gm.settingsPanel.SetActive(false);
        gm.successPanel.SetActive(false); gm.failPanel.SetActive(false);
    }

    private RectTransform ResetPanel(GameObject panel)
    {
        panel.SetActive(true);
        foreach(Transform child in panel.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var bg=panel.GetComponent<Image>();if(bg==null)bg=panel.AddComponent<Image>();bg.sprite=null;bg.color=T.Background;bg.raycastTarget=true;
        foreach(var effect in panel.GetComponents<BaseMeshEffect>()) Destroy(effect);
        Fill((RectTransform)panel.transform);
        var safe=Rect("SafeContent",panel.transform); Fill(safe); safe.gameObject.AddComponent<NeonSafeArea>();
        safe.gameObject.AddComponent<NeonPanelEntrance>();
        return safe;
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

    private void CreateMenu()
    {
        Eyebrow(menu,"A GAME OF SIZE & INSTINCT");
        menuWallet=Txt("PermanentBalance",menu,"",26,T.pageMargin,-124,952,44,T.Muted); menuWallet.alignment=TextAlignmentOptions.MidlineRight;
        var neon=Txt("NeonWordmark",menu,"NEON",T.displaySize,T.pageMargin,-194,910,180); neon.fontStyle=FontStyles.Bold; neon.characterSpacing=-7;
        var reflex=Txt("ReflexWordmark",menu,"REFLEX",T.displaySize,T.pageMargin,-350,955,170,T.Primary); reflex.fontStyle=FontStyles.Bold; reflex.characterSpacing=-8;
        Txt("Manifesto",menu,"Find the largest.\nTrust your reflex.",34,T.pageMargin,-558,670,98,T.Text).textWrappingMode=TextWrappingModes.Normal;
        // The offset nested frames are the game's size-reading mechanic made into a mark.
        var motif=Shape("OpticalSignature",menu,NeonShape.Kind.Reticle,T.Target,3);
        At(motif.rectTransform,.66f,.54f,0,0,430,430); motif.rectTransform.localRotation=Quaternion.Euler(0,0,-14);
        motif.gameObject.AddComponent<NeonOpticalMotion>();
        var serial=Txt("CampaignLabel",menu,"CAMPAIGN\n"+gm.campaign.LevelCount+" LEVELS",24,T.pageMargin,-30,340,76,T.Muted,0,.43f); serial.characterSpacing=3;
        menuState=Txt("RunState",menu,"",38,T.pageMargin,628,930,54,T.Text,0,0); menuState.fontStyle=FontStyles.Bold;
        menuResources=Txt("RunResources",menu,"",27,T.pageMargin,560,930,90,T.Muted,0,0); menuResources.textWrappingMode=TextWrappingModes.Normal;
        gm.startContinueButton=WideButton("StartContinue",menu,"START RUN",310,true);
        gm.upgradesButton=Button("Upgrades",menu,"UPGRADES"); At((RectTransform)gm.upgradesButton.transform,.25f,0,27,222,466,108);
        gm.menuSettingsButton=Button("Settings",menu,"SETTINGS"); At((RectTransform)gm.menuSettingsButton.transform,.75f,0,-27,222,466,108);
        abandonButton=Button("AbandonRun",menu,"END ACTIVE RUN"); At((RectTransform)abandonButton.transform,.5f,0,0,116,560,76);
        abandonButton.image.color=T.Background; var abandonLabel=abandonButton.GetComponentInChildren<TMP_Text>(); abandonLabel.color=T.Muted; abandonLabel.fontSize=24; abandonLabel.alignment=TextAlignmentOptions.Center;
        abandonButton.onClick.AddListener(ShowAbandonConfirmation);
        Txt("MenuFooter",menu,"PRECISION UNDER PRESSURE",22,T.pageMargin,54,820,30,T.Muted,0,0).characterSpacing=3;
    }

    private void CreateHud()
    {
        Eyebrow(play,"CAMPAIGN",58);
        gm.levelText=Txt("Level",play,"01",86,T.pageMargin,-116,470,110); gm.levelText.fontStyle=FontStyles.Bold;
        objective=Txt("Objective",play,"",28,T.pageMargin,-240,500,48,T.Muted); gm.remainingText=objective;
        progressFill=Track("ObjectiveProgress",play,new Vector2(64,-309),436,5);
        timerLabel=Txt("TimerLabel",play,"LEVEL TIME",24,600,-68,416,44,T.Muted); timerLabel.alignment=TextAlignmentOptions.MidlineRight;
        gm.timerText=Txt("Timer",play,"",104,558,-99,458,140); gm.timerText.alignment=TextAlignmentOptions.MidlineRight;
        gm.timerText.overflowMode=TextOverflowModes.Overflow;
        gm.timerText.fontStyle=FontStyles.Bold;
        timerFill=Track("TimeProgress",play,new Vector2(650,-244),366,5);
        reserve=Txt("Reserve",play,"",28,540,-268,476,49,T.Reserve); reserve.alignment=TextAlignmentOptions.MidlineRight;
        health=Txt("Health",play,"",30,T.pageMargin,-340,470,54,T.Text);
        var healthIcon=Shape("HealthGlyph",play,NeonShape.Kind.Health,T.Primary,3); At(healthIcon.rectTransform,0,1,84,-364,35,35);
        health.rectTransform.anchoredPosition+=new Vector2(55,0); health.rectTransform.sizeDelta-=new Vector2(55,0);
        healthFill=Track("HealthProgress",play,new Vector2(650,-365),366,5);
        Hairline(play,414);
        ruleRail=Rule("RuleRail",play,T.Target); At(ruleRail.rectTransform,0,1,69,-457,8,38);
        rule=Txt("TargetRule",play,"LARGEST OUTLINE",30,92,-433,600,52,T.Target); rule.fontStyle=FontStyles.Bold;
        modifiers=Txt("Modifiers",play,"",23,520,-435,496,48,T.Muted);modifiers.alignment=TextAlignmentOptions.MidlineRight;
        pending=Txt("PendingEarnings",play,"",25,T.pageMargin,193,952,46,T.Muted,0,0);
        debugBadge=Txt("Sandbox",play,"DEBUG SANDBOX / NO PROGRESSION",22,T.pageMargin,234,952,38,T.Reserve,0,0);
        gm.homeButton=Button("Home",play,"HOME"); At((RectTransform)gm.homeButton.transform,0,0,242,100,356,104);
        gm.settingsButton=Button("Pause",play,"PAUSE"); At((RectTransform)gm.settingsButton.transform,1,0,-242,100,356,104);
        var pause=Shape("PauseGlyph",gm.settingsButton.transform,NeonShape.Kind.Pause,T.Text,5); At(pause.rectTransform,1,.5f,-50,0,36,36);
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
    public void RefreshMenu(bool activeRun,int currentLevelNumber,long balance,long pendingAmount,ActiveRunData run=null)
    {
        RefreshInputLayers();
        hasActiveRun=activeRun;pendingCoins=Math.Max(0,pendingAmount);
        ButtonText(gm.startContinueButton,activeRun?"CONTINUE RUN":"START RUN"); abandonButton.gameObject.SetActive(activeRun);
        menuWallet.text=$"{Math.Max(0,balance):N0}  BANKED COINS";
        menuState.text=activeRun?$"YOUR RUN · LEVEL {Mathf.Max(1,currentLevelNumber):00}":"ONE RUN. MAKE IT COUNT.";
        menuResources.text=activeRun && run!=null
            ? $"{run.currentHealth}/{run.upgrades.maxHealth} health  ·  {run.currentReserveSeconds:0.0}s reserve\n{pendingCoins:N0} pending coins from completed levels"
            : activeRun?$"{pendingCoins:N0} pending coins · Pick up where you left off.":"Begin at Level 1. Carry your health and reserve\nthrough the campaign. Every completed level pays.";
    }
    public void UpdateLevelContext(LevelData level,int count)
    {
        cachedLevel=level.levelNumber;
        gm.levelText.text=$"{cachedLevel:00}<size=30><color=#9EAFAD> / {count}</color></size>";
        var list=new List<string>(); if(!Mathf.Approximately(level.rotateSpeed,0))list.Add("ROTATE");if(level.scaleEnabled)list.Add("SCALE");if(level.movementEnabled)list.Add("MOVE");
        modifierText=string.Join(" · ",list); modifiers.text=modifierText;
        ApplyGameplayTheme(level.textPrimaryColor,level.outlineColor);
    }
    public void RefreshHud(int currentHealth,int maximumHealth,float reserveSeconds,float maximumReserveSeconds,long runPending,int progress,int required,float remaining,float timeLimit,bool reserveActive,bool reverseActive,bool debug)
    {
        health.SetText("<mspace=22>{0}</mspace><color=#9EAFAD> / {1} HEALTH</color>",Mathf.Max(0,currentHealth),maximumHealth);
        health.color=currentHealth<=1?T.Danger:T.Text;
        reserve.SetText(reserveActive?"LEVEL TIME EXHAUSTED":"RESERVE  <mspace=19>{0:1}</mspace>s",Mathf.Max(0,reserveSeconds));
        reserve.color=T.Reserve;
        gm.timerText.SetText("<mspace=65>{0:1}</mspace><size=32>s</size>",Mathf.Max(0,reserveActive?reserveSeconds:remaining));
        gm.timerText.color=reserveActive?T.Reserve:remaining<=5?T.Reserve:T.Text;
        timerLabel.text=reserveActive?"RESERVE · DRAINING":"LEVEL TIME";timerLabel.color=reserveActive?T.Reserve:T.Muted;
        objective.SetText("{0} <color=#9EAFAD>/ {1} TARGETS</color>",progress,required);
        SetFill(progressFill,progress/(float)Mathf.Max(1,required),reverseActive?T.Reverse:T.Primary);
        SetFill(timerFill,(reserveActive?reserveSeconds:remaining)/Mathf.Max(.01f,reserveActive?maximumReserveSeconds:timeLimit),reserveActive?T.Reserve:T.Target);
        SetFill(healthFill,currentHealth/(float)Mathf.Max(1,maximumHealth),currentHealth<=1?T.Danger:T.Primary);
        rule.text=reverseActive?"REVERSE / SMALLEST":"LARGEST OUTLINE";rule.color=ruleRail.color=reverseActive?T.Reverse:T.Target;
        if(displayedPending!=runPending||displayedDebug!=debug)
        {
            pending.text=debug?"PRACTICE SESSION":$"+{Math.Max(0,runPending):N0} PENDING COINS";
            displayedPending=runPending;displayedDebug=debug;
        }
        debugBadge.gameObject.SetActive(debug);
    }
    public void ApplyGameplayTheme(Color primary,Color accent)
    {
        gm.gameplayPanel.GetComponent<Image>().color=T.Background;
        if(Camera.main!=null)Camera.main.backgroundColor=T.Background;
        if(gm.levelText!=null)gm.levelText.color=T.Text;
    }

    private void CreateSettings()
    {
        Eyebrow(settings,"NEON REFLEX / PREFERENCES");
        settingsHeading=Txt("Title",settings,"SETTINGS",T.headingSize,T.pageMargin,-202,940,114);settingsHeading.fontStyle=FontStyles.Bold;
        Txt("Intro",settings,"Make yourself comfortable.",34,T.pageMargin,-334,900,60,T.Muted);
        Hairline(settings,464);
        hapticsToggle=SettingRow(settings,"Haptic feedback","A tactile cue on supported devices.",530,false);
        hapticsToggle.onValueChanged.AddListener(v=>hapticsChanged?.Invoke(v));gm.hapticToggle=hapticsToggle;gm.sfxVolumeSlider=null;
        reducedToggle=SettingRow(settings,"Reduced effects","Quieter transitions and feedback.\nGrid motion and game rules stay the same.",790,true);
        reducedToggle.SetIsOnWithoutNotify(NeonTheme.ReducedEffects);reducedToggle.onValueChanged.AddListener(v=>NeonTheme.ReducedEffects=v);
        Txt("SettingsNote",settings,"Your run waits here.\nHealth, time and motion pause together.",30,T.pageMargin,-1170,910,120,T.Muted).textWrappingMode=TextWrappingModes.Normal;
        gm.settingsCloseButton=WideButton("CloseSettings",settings,"DONE",116,true);
    }
    private Toggle SettingRow(Transform parent,string title,string description,float y,bool reduced)
    {
        Txt("SettingTitle",parent,title,38,T.pageMargin,-y,690,70).fontStyle=FontStyles.Bold;
        var d=Txt("SettingDescription",parent,description,29,T.pageMargin,-y-84,720,114,T.Muted);d.textWrappingMode=TextWrappingModes.Normal;
        var bg=Panel(reduced?"ReducedEffectsToggle":"HapticsToggle",parent,T.Raised,true);At(bg.rectTransform,1,1,-150,-y-46,174,96);
        var toggle=bg.gameObject.AddComponent<Toggle>();toggle.targetGraphic=bg;toggle.navigation=new Navigation{mode=Navigation.Mode.None};
        var on=Panel("On",bg.transform,T.Primary);Fill(on.rectTransform,6,6,6,6);toggle.graphic=on;
        var label=Text("State",bg.transform,"OFF",27,T.Text,TextAlignmentOptions.Center);Fill(label.rectTransform);
        toggle.onValueChanged.AddListener(v=>{label.text=v?"ON":"OFF";label.color=v?T.Background:T.Text;});
        return toggle;
    }
    public void SetHaptics(bool enabled)
    {
        hapticsToggle.SetIsOnWithoutNotify(enabled);
        var text=hapticsToggle.GetComponentInChildren<TMP_Text>();text.text=enabled?"ON":"OFF";text.color=enabled?T.Background:T.Text;
        if(reducedToggle!=null){bool on=NeonTheme.ReducedEffects;reducedToggle.SetIsOnWithoutNotify(on);var rt=reducedToggle.GetComponentInChildren<TMP_Text>();rt.text=on?"ON":"OFF";rt.color=on?T.Background:T.Text;}
    }
    public void RefreshSettings(bool duringGameplay)
    { settingsHeading.text=duringGameplay?"PAUSED":"SETTINGS";ButtonText(gm.settingsCloseButton,duringGameplay?"RESUME":"DONE");RefreshInputLayers(); }

    public void RefreshInputLayers()
    {
        // A newly enabled Graphic has depth -1 until Unity renders it. Disable
        // the underlay immediately so a second touch cannot pass through during
        // that frame. This changes input only, never simulation or pause state.
        bool dialog=IsAbandonConfirmationVisible, paused=gm.settingsPanel.activeSelf;
        bool covered=dialog||paused||IsShopVisible;
        SetInput(gm.mainMenuPanel,!covered);SetInput(gm.gameplayPanel,!covered);
        SetInput(gm.successPanel,!covered);SetInput(gm.failPanel,!covered);
        SetInput(gm.settingsPanel,!dialog);SetInput(shopPanel,!dialog&&!paused);
        SetInput(abandonDialog,true);
    }
    private static void SetInput(GameObject panel,bool allowed)
    {
        if(panel==null)return;
        var group=panel.GetComponent<CanvasGroup>();if(group==null)group=panel.AddComponent<CanvasGroup>();
        group.interactable=allowed;group.blocksRaycasts=allowed;
    }

    private ResultView CreateResult(RectTransform root,bool success)
    {
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
        return v;
    }
    public void ShowLevelComplete(ActiveRunData run,int completed,int next,long reward,bool debug=false)
    {
        var v=successView;v.emblem.kind=NeonShape.Kind.Check;At(v.emblem.rectTransform,0,1,130,-211,128,128);v.emblem.SetVerticesDirty();v.title.fontSize=T.headingSize;v.eyebrow.text=debug?"SANDBOX / LEVEL COMPLETE":"CAMPAIGN / LEVEL COMPLETE";
        v.title.text=$"LEVEL {completed:00}\nCOMPLETE.";v.description.text=debug?"Practice complete. Your real run is untouched.":$"Next up: Level {next:00}.\nYour health and reserve carry forward.";
        v.emblem.color=T.Primary;v.amount.color=T.Primary;v.amountLabel.text=debug?"PRACTICE SESSION":"LEVEL REWARD / PENDING";v.amount.text=debug?"WELL PLAYED":$"+{reward:N0}<size=34> COINS</size>";
        v.amount.fontSize=debug?66:112;v.leftLabel.text="HEALTH CARRIED";v.leftValue.text=$"{run.currentHealth} / {run.upgrades.maxHealth}";
        v.rightLabel.text="RESERVE CARRIED";v.rightValue.text=$"{run.currentReserveSeconds:0.0}<size=28>s</size>";
        v.footer.text=debug?"No coins or progression changed.":$"{run.pendingCoins:N0} pending run coins\nBanked when this run ends.";
    }
    public void ShowRunSummary(RunSummaryData summary)
    {
        var v=summary.campaignCompleted?successView:failView;bool win=summary.campaignCompleted;
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
    }

    public void ShowDebugRunEnded(string reason,ActiveRunData run,int level)
    {
        var v=failView;v.eyebrow.text="DEBUG SANDBOX / PRACTICE REPORT";
        v.title.text="PRACTICE\nCOMPLETE.";v.description.text=reason=="HEALTH DEPLETED"?"Health depleted. Your real run is untouched.":"Reserve depleted. Your real run is untouched.";
        v.amountLabel.text="SANDBOX / NO REWARDS";v.amount.text="NO COINS BANKED";v.amount.fontSize=58;v.amount.color=T.Muted;
        v.leftLabel.text="LEVEL PRACTICED";v.leftValue.text=level.ToString("00");v.rightLabel.text="CORRECT TARGETS";v.rightValue.text=run.levelState.objectiveProgress.ToString();
        v.footer.text="No coins, upgrades or saved run state changed.";v.emblem.color=T.Muted;
    }

    private void CreateShop()
    {
        shopPanel=Panel("PermanentUpgrades",canvas,T.Background,true).gameObject;Fill((RectTransform)shopPanel.transform);
        shop=Rect("SafeContent",shopPanel.transform);Fill(shop);shop.gameObject.AddComponent<NeonSafeArea>();shop.gameObject.AddComponent<NeonPanelEntrance>();
        Eyebrow(shop,"NEON REFLEX / PERMANENT UPGRADES");
        Txt("ShopTitle",shop,"BUILD YOUR\nNEXT RUN.",80,T.pageMargin,-166,900,195).fontStyle=FontStyles.Bold;
        wallet=Txt("BankedCoins",shop,"",30,T.pageMargin,-376,940,54,T.Primary);
        shopNotice=Txt("ShopNotice",shop,"",27,T.pageMargin,-440,940,76,T.Muted);shopNotice.textWrappingMode=TextWrappingModes.Normal;
        var viewport=Panel("UpgradeViewport",shop,Color.clear,true);Fill(viewport.rectTransform,T.pageMargin,240,T.pageMargin,550);
        viewport.gameObject.AddComponent<RectMask2D>();
        var list=Rect("UpgradeList",viewport.transform);list.anchorMin=new Vector2(0,1);list.anchorMax=new Vector2(1,1);list.pivot=new Vector2(.5f,1);list.sizeDelta=new Vector2(0,1120);list.anchoredPosition=Vector2.zero;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.content=list;scroll.viewport=viewport.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
        for(int i=0;i<4;i++)cards[(UpgradeId)i]=CreateUpgrade((UpgradeId)i,list,i);
        shopFeedback=Txt("PurchaseFeedback",shop,"PERMANENT BENEFITS. APPLIED TO NEW RUNS.",23,T.pageMargin,220,940,54,T.Muted,0,0);
        var back=WideButton("ShopBack",shop,"RETURN HOME",64,false);back.onClick.AddListener(()=>HideShop(true));shopPanel.SetActive(false);
    }
    private UpgradeView CreateUpgrade(UpgradeId id,Transform parent,int index)
    {
        var v=new UpgradeView{accent=id==UpgradeId.MaximumHealth?T.Primary:id==UpgradeId.StartingReserve?T.Reserve:id==UpgradeId.GridStabilizer?T.Target:T.Reverse};
        var row=Rect(id.ToString(),parent);Box(row,0,1-(index+1)*.25f,1,1-index*.25f);
        var line=Rule("Divider",row);Box(line.rectTransform,0,1,1,1);line.rectTransform.sizeDelta=new Vector2(0,2);
        var icon=Shape("UpgradeGlyph",row,(NeonShape.Kind)((int)NeonShape.Kind.Health+index),v.accent,3);At(icon.rectTransform,0,1,35,-61,60,60);
        v.title=Txt("Title",row,"",34,100,-20,670,57);v.title.fontStyle=FontStyles.Bold;
        v.tier=Txt("Tier",row,"",23,100,-76,550,35,T.Muted);
        v.description=Txt("Description",row,"",27,100,-131,790,43,T.Muted);
        v.benefit=Txt("Benefit",row,"",30,100,-188,540,54,v.accent);
        v.status=Txt("Status",row,"",21,100,-242,790,32,T.Muted);
        v.buy=Button("Purchase",row,"",false);At((RectTransform)v.buy.transform,1,1,-119,-213,236,96);
        var bt=v.buy.GetComponentInChildren<TMP_Text>();bt.fontSize=28;bt.alignment=TextAlignmentOptions.Center;Fill(bt.rectTransform,8,0,8,0);
        v.progress=Track("TierProgress",row,new Vector2(0,-132),70,4);
        v.buy.onClick.AddListener(()=>
        {
            if(purchaseRequested!=null&&purchaseRequested(id))
            { RefreshShop();v.status.text="INSTALLED / NEXT RUN UPDATED";v.status.color=v.accent;shopFeedback.text=v.title.text+" INSTALLED";shopFeedback.color=v.accent;feedbackUntil=Time.unscaledTime+1.8f; }
        });return v;
    }
    public void ShowShop(GameConfig gameConfig,PlayerProfileData player,bool activeRun)
    { config=gameConfig;profile=player;hasActiveRun=activeRun;RefreshShop();shopPanel.SetActive(true);shopPanel.transform.SetAsLastSibling();RefreshInputLayers(); }
    public void RefreshShop(GameConfig gameConfig,PlayerProfileData player,bool activeRun)
    { config=gameConfig;profile=player;hasActiveRun=activeRun;RefreshShop(); }
    private void RefreshShop()
    {
        long coins=Math.Max(0,profile?.coins??0);wallet.text=$"{coins:N0} <size=24>BANKED COINS</size>";
        shopNotice.text=hasActiveRun?"RUN ACTIVE / Purchases paused.\nFinish or end your run to make upgrades.":"Spend completed-level earnings on a stronger start.\nEvery upgrade stays with you.";shopNotice.color=hasActiveRun?T.Reserve:T.Muted;
        foreach(var pair in cards)
        {
            UpgradeId id=pair.Key;var v=pair.Value;var d=UpgradeCatalog.Get(config,id);var current=UpgradeCatalog.CurrentTier(config,profile,id);var next=UpgradeCatalog.NextTier(config,profile,id);
            int tier=UpgradeCatalog.ClampTier(config,id,UpgradeCatalog.GetTier(profile,id));int max=d?.tiers?.Count??0;
            v.title.text=d?.displayName??id.ToString();v.tier.text=$"TIER {tier:00} / {max:00}";
            v.description.text=d?.description??"";
            float value=current?.effectValue??(id==UpgradeId.MaximumHealth?config.baseHealth:id==UpgradeId.StartingReserve?config.baseStartingReserveSeconds:id==UpgradeId.GridStabilizer?1:0);
            v.benefit.text=next==null?Effect(id,value):$"<color=#9EAFAD>{Effect(id,value)}</color>  →  {Effect(id,next.effectValue)}";
            SetFill(v.progress,tier/(float)Mathf.Max(1,max),v.accent);
            bool allowed=string.IsNullOrEmpty(UpgradeCatalog.DisabledReason(config,profile,id,hasActiveRun));v.buy.interactable=allowed;
            v.buy.image.color=allowed?v.accent:T.Raised;v.buy.GetComponentInChildren<TMP_Text>().color=allowed?T.Background:T.Muted;
            ButtonText(v.buy,next==null?"MAX TIER":hasActiveRun?"RUN ACTIVE":$"{next.cost:N0}\n<size=20>COINS / UPGRADE</size>");
            v.status.text=next==null?"MAXIMUM BENEFIT REACHED":hasActiveRun?"AVAILABLE AFTER THIS RUN":coins<next.cost?$"{next.cost-coins:N0} MORE COINS NEEDED":"READY TO UPGRADE";
            v.status.color=allowed?v.accent:T.Muted;
        }
    }
    private string Effect(UpgradeId id,float value)
    {
        switch(id){case UpgradeId.MaximumHealth:return $"{Mathf.Clamp(Mathf.RoundToInt(value),1,config.maximumHealthCap)} HP";case UpgradeId.StartingReserve:return $"{value:0.#}s";case UpgradeId.GridStabilizer:return $"{value*100:0}% speed";default:return $"+{value:0.#}s cooldown";}
    }
    void Update()
    { if(feedbackUntil>0&&Time.unscaledTime>feedbackUntil){feedbackUntil=0;shopFeedback.text="PERMANENT BENEFITS. APPLIED TO NEW RUNS.";shopFeedback.color=T.Muted;} }
    public void HideShop(bool notify=false)
    { feedbackUntil=0;if(shopFeedback!=null){shopFeedback.text="PERMANENT BENEFITS. APPLIED TO NEW RUNS.";shopFeedback.color=T.Muted;}if(shopPanel!=null)shopPanel.SetActive(false);RefreshInputLayers();if(notify)shopClosed?.Invoke(); }

    private void CreateAbandonDialog()
    {
        abandonDialog=Panel("AbandonConfirmation",canvas,T.Background,true).gameObject;Fill((RectTransform)abandonDialog.transform);
        var safe=Rect("SafeContent",abandonDialog.transform);Fill(safe);safe.gameObject.AddComponent<NeonSafeArea>();
        var body=Rect("Dialog",safe);At(body,.5f,.5f,0,0,952,850);
        var glyph=Shape("EndGlyph",body,NeonShape.Kind.Reverse,T.Reserve,4);At(glyph.rectTransform,0,1,46,-38,72,72);
        Txt("Title",body,"END THIS\nRUN?",86,0,-143,930,210).fontStyle=FontStyles.Bold;
        abandonMessage=Txt("Explanation",body,"",34,0,-387,930,175,T.Muted);abandonMessage.textWrappingMode=TextWrappingModes.Normal;
        var confirm=Button("ConfirmAbandon",body,"END RUN & BANK COINS",true);At((RectTransform)confirm.transform,.5f,0,0,204,952,120);confirm.image.color=T.Reserve;
        confirm.onClick.AddListener(()=>{HideAbandonConfirmation();abandonConfirmed?.Invoke();});
        var cancel=Button("KeepRun",body,"KEEP RUN");At((RectTransform)cancel.transform,.5f,0,0,62,952,112);cancel.onClick.AddListener(HideAbandonConfirmation);
        abandonDialog.SetActive(false);
    }
    public void ShowAbandonConfirmation()
    { abandonMessage.text=$"Your run will end. {pendingCoins:N0} pending coins from completed levels will be banked.\nThe current level earns no coins.";abandonDialog.SetActive(true);abandonDialog.transform.SetAsLastSibling();RefreshInputLayers(); }
    public void HideAbandonConfirmation(){if(abandonDialog!=null)abandonDialog.SetActive(false);RefreshInputLayers();}
}
