using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NeonStyle;

public sealed partial class RogueliteUIController
{
    private RectTransform menuMotif;
    private static readonly Color MenuBlue = NeonTheme.Hex("30CFFF");
    private static readonly Color MenuPink = NeonTheme.Hex("FF48DC");
    private static readonly Color MenuLime = NeonTheme.Hex("D4FF67");
    private static readonly Color MenuIce = NeonTheme.Hex("A9D4FF");

    private static NeonMenuGraphic MenuArt(string name,Transform parent,NeonMenuGraphic.Kind kind,Color tint)
    {
        var rect=Rect(name,parent);var graphic=rect.gameObject.AddComponent<NeonMenuGraphic>();
        graphic.kind=kind;graphic.color=tint;graphic.raycastTarget=false;return graphic;
    }

    private void CreateMenu()
    {
        var background=Rect("NeonCorridor",gm.mainMenuPanel.transform);
        background.SetAsFirstSibling();Fill(background);
        var art=background.gameObject.AddComponent<RawImage>();
        art.texture=Resources.Load<Texture2D>("MenuArt/neon-corridor");art.raycastTarget=false;
        if(art.texture!=null)
        {
            var fit=background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectRatio=art.texture.width/(float)art.texture.height;
            fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        var walletFrame=MenuArt("WalletFrame",menu,NeonMenuGraphic.Kind.Button,MenuIce*.65f);
        At(walletFrame.rectTransform,1,1,-200,-80,336,100);walletFrame.fillTint=.03f;
        var coin=UpgradeArt("Coin",walletFrame.transform,NeonUpgradeGraphic.Kind.Coin,ShopGold);
        At(coin.rectTransform,0,.5f,48,0,49,49);
        menuWallet=Txt("PermanentBalance",walletFrame.transform,"0",34,88,-25,146,50,MenuIce);
        menuWallet.enableAutoSizing=true;menuWallet.fontSizeMin=21;menuWallet.fontSizeMax=34;
        var walletPlus=Button("WalletUpgrades",walletFrame.transform,"",false);
        walletPlus.image.color=Color.clear;At((RectTransform)walletPlus.transform,1,.5f,-49,0,80,80);
        var plusFrame=MenuArt("PlusFrame",walletPlus.transform,NeonMenuGraphic.Kind.Button,MenuIce*.75f);
        Fill(plusFrame.rectTransform);plusFrame.fillTint=.08f;
        var plus=MenuArt("Plus",walletPlus.transform,NeonMenuGraphic.Kind.Plus,MenuIce);
        At(plus.rectTransform,.5f,.5f,0,0,37,37);
        walletPlus.onClick.AddListener(()=>upgradesRequested?.Invoke());

        var logo=Rect("NeonReflexWordmark",menu);At(logo,.5f,.81f,0,0,744,255);logo.localScale=Vector3.one*.88f;
        var neon=MenuArt("NeonWordmark",logo,NeonMenuGraphic.Kind.NeonWord,NeonTheme.Hex("77CFFF"));
        At(neon.rectTransform,.5f,1,0,-59,722,118);
        var reflex=MenuArt("ReflexWordmark",logo,NeonMenuGraphic.Kind.ReflexWord,MenuPink);
        At(reflex.rectTransform,.5f,0,0,59,722,118);

        menuMotif=Rect("OpticalSignature",menu);At(menuMotif,.5f,.56f,0,0,460,460);
        menuMotif.localRotation=Quaternion.Euler(0,0,-20);
        var outer=MenuArt("OuterSquare",menuMotif,NeonMenuGraphic.Kind.Frame,MenuBlue);Fill(outer.rectTransform);outer.strokeWidth=4;
        var middle=MenuArt("MiddleSquare",menuMotif,NeonMenuGraphic.Kind.Frame,MenuPink);At(middle.rectTransform,.5f,.5f,0,0,292,292);middle.strokeWidth=3.5f;
        var inner=MenuArt("InnerSquare",menuMotif,NeonMenuGraphic.Kind.Frame,NeonTheme.Hex("A64DFF"));At(inner.rectTransform,.5f,.5f,0,0,110,110);inner.strokeWidth=3;
        menuMotif.gameObject.AddComponent<NeonOpticalMotion>();

        var actions=Rect("MenuActions",menu);At(actions,.5f,.38f,0,0,798,548);actions.pivot=new Vector2(.5f,1);
        gm.startContinueButton=MenuButton("StartContinue",actions,"START RUN",MenuLime,NeonMenuGraphic.Kind.Play,-93,186,true);
        gm.upgradesButton=MenuButton("Upgrades",actions,"UPGRADES",MenuBlue,NeonMenuGraphic.Kind.Bars,-284,164,false);
        gm.menuSettingsButton=MenuButton("Settings",actions,"SETTINGS",MenuIce,NeonMenuGraphic.Kind.Gear,-465,164,false);

        menuState=Txt("RunState",menu,"",25,130,58,820,38,MenuIce,0,.42f);menuState.alignment=TextAlignmentOptions.Center;
        menuResources=Txt("RunResources",menu,"",23,100,15,880,55,MenuIce,0,.42f);menuResources.alignment=TextAlignmentOptions.Center;
        menuResources.textWrappingMode=TextWrappingModes.Normal;
        menuState.outlineColor=menuResources.outlineColor=Color.black;
        menuState.outlineWidth=menuResources.outlineWidth=.18f;
        abandonButton=Button("AbandonRun",menu,"END ACTIVE RUN",false);
        At((RectTransform)abandonButton.transform,.5f,0,0,68,570,75);
        abandonButton.image.color=Color.clear;
        var abandonLabel=abandonButton.GetComponentInChildren<TMP_Text>();abandonLabel.color=MenuIce;abandonLabel.fontSize=23;
        abandonLabel.characterSpacing=2;abandonLabel.alignment=TextAlignmentOptions.Center;
        abandonButton.onClick.AddListener(ShowAbandonConfirmation);
    }

    private Button MenuButton(string name,Transform parent,string label,Color tint,NeonMenuGraphic.Kind icon,float y,float height,bool primary)
    {
        var button=Button(name,parent,label,false);At((RectTransform)button.transform,.5f,1,0,y,798,height);button.image.color=Color.clear;
        var frame=MenuArt("LuminousButton",button.transform,NeonMenuGraphic.Kind.Button,tint);
        Fill(frame.rectTransform);frame.transform.SetAsFirstSibling();frame.fillTint=primary ? .17f : .035f;
        var symbol=MenuArt("ButtonIcon",button.transform,icon,primary?Color.white:tint);
        At(symbol.rectTransform,0,.5f,156,0,primary?75:76,primary?75:76);
        var text=button.GetComponentInChildren<TMP_Text>();text.fontSize=primary?52:43;text.characterSpacing=2;
        text.color=primary?Color.white:MenuIce;text.alignment=TextAlignmentOptions.Center;
        Fill(text.rectTransform,230,0,54,0);
        button.GetComponent<NeonPressFeedback>().CaptureRestPose();
        return button;
    }

    public void RefreshMenu(bool activeRun,int currentLevelNumber,long balance,long pendingAmount,ActiveRunData run=null)
    {
        RefreshInputLayers();hasActiveRun=activeRun;pendingCoins=Math.Max(0,pendingAmount);
        ButtonText(gm.startContinueButton,activeRun?"CONTINUE RUN":"START RUN");abandonButton.gameObject.SetActive(activeRun);
        menuWallet.text=$"{Math.Max(0,balance):N0}";
        menuState.gameObject.SetActive(activeRun);menuResources.gameObject.SetActive(activeRun);
        menuState.text=$"LEVEL {Mathf.Max(1,currentLevelNumber):00}  /  RUN IN PROGRESS";
        menuResources.text=run!=null?$"{run.currentHealth}/{run.upgrades.maxHealth} HEALTH  ·  {run.currentReserveSeconds:0.0}s RESERVE  ·  {pendingCoins:N0} PENDING COINS":$"{pendingCoins:N0} PENDING COINS";
        menuMotif.anchorMin=menuMotif.anchorMax=new Vector2(.5f,activeRun ? .59f : .56f);
        menuMotif.localScale=Vector3.one*(activeRun ? .78f : 1f);
    }
}
