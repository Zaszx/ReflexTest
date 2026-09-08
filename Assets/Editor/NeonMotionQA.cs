using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editor-only actual runtime motion evidence, using the existing isolated profile bootstrap.</summary>
public static class NeonMotionQA
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class Cue
    {
        public float time;
        public string name;
        public Action action;
        public Cue(float seconds, string description, Action callback) { time = seconds; name = description; action = callback; }
    }
    private sealed class Frame
    {
        public Texture2D texture;
        public double elapsed;
        public string snapshot;
    }

    private static HashSet<string> selectedClips;
    public static IEnumerator Run(GameManager gm, string stage,bool full=true,string clips=null)
    {
        selectedClips=string.IsNullOrWhiteSpace(clips)?null:new HashSet<string>(clips.Split(','));
        if (!NeonPresentationQAProfile.IsActive || Get<NeonSaveService>(gm, "saveService").DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Motion QA requires isolated profile storage.");
        string directory = ConfigurationDirectory(stage);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "README.txt"), "Actual Unity runtime frame sequences. Requested capture interval 1/30s; timestamps.csv records actual frame/event elapsed time. Screenshot readback can reduce capture cadence. Texture encoding occurs after each recorded clip. Game simulation and unscaled presentation clocks remain live while recording. All saves are isolated beneath " + NeonPresentationQAProfile.DirectoryPath + ".\n");
        gm.ReturnToMainMenu();
        Invoke(gm, "OnApplicationFocus", true);
        yield return new WaitForSecondsRealtime(.35f);
        yield return Record(gm, directory, "01-screen-navigation", 3f,
            new Cue(.25f,"menu to shop",gm.OpenUpgradeShop),
            new Cue(1f,"shop to menu",gm.ReturnToMainMenu),
            new Cue(1.55f,"open settings",gm.OpenSettings),
            new Cue(2.3f,"close settings",gm.CloseSettings));

        gm.StartDebugLevel(0);
        yield return new WaitForSecondsRealtime(.65f);
        RunData(gm).levelState.reverseCooldownRemaining = 999;
        yield return Record(gm, directory, "02-normal-correct-taps", 2f,
            new Cue(.25f,"correct 1",()=>Tap(gm,true)),
            new Cue(.75f,"correct 2",()=>Tap(gm,true)),
            new Cue(1.25f,"correct 3",()=>Tap(gm,true)));
        yield return Record(gm, directory, "03-mistake-health", 1.25f,
            new Cue(.25f,"wrong medium cell",()=>Tap(gm,false)));

        gm.ReturnToMainMenu();
        int reverseIndex = 0;
        for (int i=0;i<gm.campaign.LevelCount;i++) if(gm.campaign.GetLevel(i).reverseEnabled) { reverseIndex=i; break; }
        gm.StartDebugLevel(reverseIndex);
        yield return new WaitForSecondsRealtime(.65f);
        yield return Record(gm, directory, "04-reverse-sequence", 3.6f,
            new Cue(.2f,"reverse entrance",()=>BeginReverse(gm)),
            new Cue(1.2f,"reverse correct 1",()=>Tap(gm,true)),
            new Cue(1.65f,"reverse correct 2",()=>Tap(gm,true)),
            new Cue(2.1f,"reverse correct 3 and exit",()=>Tap(gm,true)));

        gm.ReturnToMainMenu();
        gm.StartNewRun();
        yield return new WaitForSecondsRealtime(.65f);
        var run=RunData(gm);
        run.levelState.reverseCooldownRemaining=999;
        run.levelState.objectiveProgress=gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks-1;
        Invoke(gm,"UpdateGameplayUI");
        yield return Record(gm,directory,"05-level-complete",1.5f,
            new Cue(.25f,"final correct tap",()=>Tap(gm,true)));
        gm.ReturnToMainMenu();
        Invoke(gm,"ConfirmAbandonRun");
        gm.ReturnToMainMenu();
        Get<SaveEnvelopeData>(gm,"saveData").profile.coins=10000;
        gm.OpenUpgradeShop();
        yield return new WaitForSecondsRealtime(.4f);
        yield return Record(gm,directory,"06-shop-purchase",1.5f,
            new Cue(.25f,"purchase maximum health",()=>PurchaseFirst(gm)));
        if(stage!="Before" && full)
        {
            gm.ReturnToMainMenu();gm.StartDebugLevel(gm.campaign.LevelCount-1);yield return Ready(gm);
            RunData(gm).levelState.reverseCooldownRemaining=999;
            yield return Record(gm,directory,"07-rapid-combined-modifiers",1.5f,
                new Cue(.25f,"four immediate correct taps",()=>{for(int i=0;i<4;i++)Tap(gm,true);}),
                new Cue(.55f,"four more immediate correct taps",()=>{for(int i=0;i<4;i++)Tap(gm,true);}));
            gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
            RunData(gm).currentHealth=1;Invoke(gm,"UpdateGameplayUI");
            yield return Record(gm,directory,"08-fatal-damage",1.5f,new Cue(.25f,"fatal wrong tap",()=>Tap(gm,false)));
            gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
            run=RunData(gm);run.levelState.normalTimeRemaining=.3f;run.currentReserveSeconds=.8f;Invoke(gm,"UpdateGameplayUI");
            yield return Record(gm,directory,"09-reserve-entry-and-expiry",1.7f);
            gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
            RunData(gm).levelState.reverseCooldownRemaining=999;
            yield return Record(gm,directory,"10-settings-during-target-feedback",1.7f,
                new Cue(.25f,"correct tap",()=>Tap(gm,true)),new Cue(.28f,"settings during target morph",gm.OpenSettings),new Cue(.9f,"close settings and resume",gm.CloseSettings));
            gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
            run=RunData(gm);run.levelState.reverseCooldownRemaining=999;
            run.randomState=NeonSaveService.EncodeRandomState(FindReuseSeed(run.levelState,Get<List<GameSquare>>(gm,"instantiatedSquares").Count,false));
            yield return Record(gm,directory,"11-immediate-consumed-cell-reuse",1.25f,new Cue(.25f,"consume Large and immediately reuse cell as Small",()=>Tap(gm,true)));
            gm.ReturnToMainMenu();gm.StartDebugLevel(gm.campaign.LevelCount-1);yield return Ready(gm);
            run=RunData(gm);run.levelState.reverseActive=true;run.levelState.reverseCorrectTapsRemaining=12;
            gm.GetComponent<GameplayFeedbackController>().RestoreReverseActive();Invoke(gm,"UpdateGameplayUI");
            yield return Record(gm,directory,"12-rapid-reverse",1.5f,
                new Cue(.25f,"four immediate Reverse taps",()=>{for(int i=0;i<4;i++)Tap(gm,true);}),
                new Cue(.55f,"four more immediate Reverse taps",()=>{for(int i=0;i<4;i++)Tap(gm,true);}));
        }
    }

    private static IEnumerator Record(GameManager gm,string root,string name,float duration,params Cue[] cues)
    {
        if(selectedClips!=null && !selectedClips.Contains(name))
        {
            // Preserve setup transactions and preceding sequence progress while
            // leaving the unselected clip's existing evidence files untouched.
            foreach(Cue setupCue in cues)setupCue.action();
            yield break;
        }
        string directory=Path.Combine(root,name); Directory.CreateDirectory(directory);
        // A revised capture replaces only its own generated frame sequence.
        foreach(string previousFrame in Directory.GetFiles(directory,"frame-*.png"))File.Delete(previousFrame);
        var frames=new List<Frame>(128);
        var events=new List<string>{"actual_elapsed_seconds,event"};
        int cue=0;
        double start=Time.realtimeSinceStartupAsDouble;
        double next=0;
        gm.enabled=true;
        while(Time.realtimeSinceStartupAsDouble-start<duration)
        {
            double elapsed=Time.realtimeSinceStartupAsDouble-start;
            while(cue<cues.Length && elapsed>=cues[cue].time)
            {
                cues[cue].action();
                events.Add(elapsed.ToString("F6",CultureInfo.InvariantCulture)+","+cues[cue].name);
                cue++;
            }
            if(elapsed>=next)
            {
                yield return new WaitForEndOfFrame();
                frames.Add(new Frame{texture=ScreenCapture.CaptureScreenshotAsTexture(),elapsed=Time.realtimeSinceStartupAsDouble-start,snapshot=Snapshot(gm)});
                next+=1d/30d;
            }
            else yield return null;
        }
        gm.enabled=false;
        var timestamps=new List<string>{"frame,elapsed_seconds,flow,health,objective,normal_seconds,small_index,medium_index,large_index,reverse,rendered_roles,reserve_seconds,unscaled_frame_seconds"};
        for(int i=0;i<frames.Count;i++)
        {
            File.WriteAllBytes(Path.Combine(directory,"frame-"+i.ToString("D4")+".png"),frames[i].texture.EncodeToPNG());
            timestamps.Add(i+","+frames[i].elapsed.ToString("F6",CultureInfo.InvariantCulture)+","+frames[i].snapshot);
            UnityEngine.Object.Destroy(frames[i].texture);
            yield return null;
        }
        File.WriteAllLines(Path.Combine(directory,"timestamps.csv"),timestamps);
        File.WriteAllLines(Path.Combine(directory,"events.csv"),events);
        File.WriteAllLines(Path.Combine(directory,"layout.txt"),ActiveTextLayout());
        gm.enabled=true;
        File.WriteAllText(Path.Combine(root,"progress.txt"),name+" complete: "+frames.Count+" actual rendered frames.");
    }

    private static string Snapshot(GameManager gm)
    {
        var run=RunData(gm);
        var roles=new List<string>();
        foreach(var square in Get<List<GameSquare>>(gm,"instantiatedSquares"))
        {
            if(square==null) continue;
            roles.Add(square.gridX+"_"+square.gridY+":"+square.currentLitSize+":"+square.outlineImage.transform.localScale.x.ToString("F3",CultureInfo.InvariantCulture)+":"+(square.outlineImage.gameObject.activeSelf?"1":"0")+":"+ProbeFloat(square,"RenderedAlpha",1).ToString("F3",CultureInfo.InvariantCulture)+":"+ProbeFloat(square,"RetiringAlpha",0).ToString("F3",CultureInfo.InvariantCulture));
        }
        return Get<object>(gm,"state")+","+(run==null?"0,0,0,-1,-1,-1,false":run.currentHealth+","+run.levelState.objectiveProgress+","+run.levelState.normalTimeRemaining.ToString("F6",CultureInfo.InvariantCulture)+","+run.levelState.smallIndex+","+run.levelState.mediumIndex+","+run.levelState.largeIndex+","+run.levelState.reverseActive)+","+string.Join(";",roles)+","+(run==null?0:run.currentReserveSeconds).ToString("F6",CultureInfo.InvariantCulture)+","+Time.unscaledDeltaTime.ToString("F6",CultureInfo.InvariantCulture);
    }

    private static void Tap(GameManager gm,bool correct)
    {
        var state=RunData(gm).levelState;
        var squares=Get<List<GameSquare>>(gm,"instantiatedSquares");
        int index=correct?(state.reverseActive?state.smallIndex:state.largeIndex):state.mediumIndex;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,squares[index].transform.position)};
        ExecuteEvents.Execute(squares[index].gameObject,pointer,ExecuteEvents.pointerDownHandler);
    }
    private static void BeginReverse(GameManager gm)
    {
        var run=RunData(gm); run.levelState.reverseCooldownRemaining=0;
        uint seed=1;
        for(;seed<100000;seed++){var random=new DeterministicRandom(seed);if(random.NextFloat01()<=gm.gameConfig.reverseTriggerChancePerCorrectClick)break;}
        run.randomState=NeonSaveService.EncodeRandomState(seed);
        Invoke(gm,"TryBeginReverse");
        run.levelState.reverseCorrectTapsRemaining=3;
        Invoke(gm,"UpdateGameplayUI");
    }
    private static void PurchaseFirst(GameManager gm)
    {
        foreach(var button in Get<GameObject>(gm.GetComponent<RogueliteUIController>(),"shopPanel").GetComponentsInChildren<Button>())
            if(button.name=="Purchase" && button.interactable){button.onClick.Invoke();return;}
        throw new InvalidOperationException("No eligible rendered purchase button found.");
    }
    private static readonly List<string> checks=new List<string>();
    private static string checkPath;
    public static IEnumerator RunChecks(GameManager gm,string stage)
    {
        checks.Clear();
        string directory=ConfigurationDirectory(stage);
        Directory.CreateDirectory(directory);
        checkPath=Path.Combine(directory,"runtime-checks.txt");File.WriteAllText(checkPath,string.Empty);
        Check(NeonPresentationQAProfile.IsActive && Get<NeonSaveService>(gm,"saveService").DirectoryPath==NeonPresentationQAProfile.DirectoryPath,"Profile storage is isolated before the first GameManager frame.");
        var data=Get<SaveEnvelopeData>(gm,"saveData");
        string realBefore=JsonUtility.ToJson(data);
        Invoke(gm,"OnApplicationFocus",true);
        gm.StartDebugLevel(gm.campaign.LevelCount-1);
        yield return Ready(gm);
        yield return null;
        System.Threading.Thread.Sleep(180); // Explicit editor-only foreground hitch fixture.
        yield return null;
        Check(Time.unscaledDeltaTime>.1f && Mathf.Abs(NeonMotion.TransitionDelta()-Time.unscaledDeltaTime)<.00001f && Mathf.Abs(NeonMotion.Delta()-.1f)<.00001f,"An active180ms hitch retains full established transition-clock elapsed time while cosmetic catch-up is capped at100ms.");
        var run=RunData(gm);
        run.levelState.reverseCooldownRemaining=999;
        int before=run.levelState.objectiveProgress;
        int cellCount=Get<List<GameSquare>>(gm,"instantiatedSquares").Count;
        int graphicCount=gm.gameplayPanel.GetComponentsInChildren<Graphic>(true).Length;
        CheckPointerSurface(gm,"Normal input");
        for(int i=0;i<8;i++)Tap(gm,true);
        Check(run.levelState.objectiveProgress==before+8 && Flow(gm)=="Playing","Eight normal pointer-downs in one frame commit all eight objectives without a presentation lock.");
        int shownRemaining = gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks - run.levelState.objectiveProgress;
        var objectiveLabel = Get<TMPro.TMP_Text>(gm.GetComponent<RogueliteUIController>(), "objective");
        objectiveLabel.ForceMeshUpdate(); // GetParsedText reads the last generated mesh, not pending SetText data.
        string displayedRemaining = System.Text.RegularExpressions.Regex.Match(objectiveLabel.GetParsedText(), @"\d+").Value;
        Check(displayedRemaining == shownRemaining.ToString(), "Objective text presents the latest remaining count immediately while its progress fill eases.");
        CheckRoles(gm,"Normal rapid retargets");
        float timer=run.levelState.normalTimeRemaining;
        yield return new WaitForSecondsRealtime(.2f);
        Check(run.levelState.normalTimeRemaining<timer-.05f,"Normal time continues draining while target feedback settles.");
        CheckSettled(gm,"Normal rapid retargets");
        Check(gm.gameplayPanel.GetComponentsInChildren<Graphic>(true).Length==graphicCount && Get<List<GameSquare>>(gm,"instantiatedSquares").Count==cellCount,"Rapid taps retain the same cell and graphic pool counts.");
        Tap(gm,true);
        gm.gameplayPanel.SetActive(false);
        CheckSettled(gm,"Actual runtime panel disable");
        gm.gameplayPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(.2f);
        CheckSettled(gm,"Actual runtime re-enable without stale effects");

        gm.ReturnToMainMenu(); gm.StartDebugLevel(gm.campaign.LevelCount-1); yield return Ready(gm);
        run=RunData(gm); run.levelState.reverseActive=true; run.levelState.reverseCorrectTapsRemaining=12;
        gm.GetComponent<GameplayFeedbackController>().RestoreReverseActive(); Invoke(gm,"UpdateGameplayUI");
        before=run.levelState.objectiveProgress;
        for(int i=0;i<8;i++)Tap(gm,true);
        Check(run.levelState.objectiveProgress==before+8 && run.levelState.reverseCorrectTapsRemaining==4 && Flow(gm)=="Playing","Eight Reverse pointer-downs in one frame commit shrinking progression and all eight Reverse counts.");
        CheckRoles(gm,"Reverse rapid retargets");
        CheckNoOverflow("Dense Level100 Reverse progress HUD");
        yield return new WaitForSecondsRealtime(.25f); CheckSettled(gm,"Reverse rapid retargets");

        foreach(bool reverse in new[]{false,true})
        {
            gm.ReturnToMainMenu(); gm.StartDebugLevel(gm.campaign.LevelCount-1); yield return Ready(gm);
            run=RunData(gm); run.levelState.reverseActive=reverse;run.levelState.reverseCorrectTapsRemaining=8;run.levelState.reverseCooldownRemaining=999;
            int consumed=reverse?run.levelState.smallIndex:run.levelState.largeIndex;
            int instance=Get<List<GameSquare>>(gm,"instantiatedSquares")[consumed].GetInstanceID();
            uint seed=FindReuseSeed(run.levelState,Get<List<GameSquare>>(gm,"instantiatedSquares").Count,reverse);
            run.randomState=NeonSaveService.EncodeRandomState(seed);
            Tap(gm,true);
            var reused=Get<List<GameSquare>>(gm,"instantiatedSquares")[consumed];
            Check((reverse?run.levelState.largeIndex:run.levelState.smallIndex)==consumed && reused.GetInstanceID()==instance && reused.gameObject.activeInHierarchy,(reverse?"Reverse":"Normal")+" immediate consumed-cell reuse preserves the existing active input object.");
            Check(reused.currentLitSize==(reverse?GameSquare.LitSize.Large:GameSquare.LitSize.Small) && ProbeFloat(reused,"RenderedAlpha",1)>.1f,(reverse?"Reverse":"Normal")+" reused cell immediately displays its latest valid role independently of retirement.");
            yield return new WaitForSecondsRealtime(.3f);
            CheckSettled(gm,(reverse?"Reverse":"Normal")+" reused cell");
        }

        gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
        run=RunData(gm);run.upgrades.maxHealth=20;run.currentHealth=20;run.levelState.reverseCooldownRemaining=999;
        string sequence=Sequence(run);before=run.levelState.objectiveProgress;
        for(int i=0;i<12;i++)Tap(gm,false);
        Check(run.currentHealth==8 && run.levelState.objectiveProgress==before && Sequence(run)==sequence,"Twelve mistakes in one frame each remove one health without moving sequence or objective.");
        var hudUI=gm.GetComponent<RogueliteUIController>();
        Check(System.Text.RegularExpressions.Regex.Replace(Get<TMPro.TMP_Text>(hudUI,"health").text,"<[^>]*>",string.Empty).TrimStart().StartsWith("8") && Mathf.Abs(Get<Image>(hudUI,"healthFill").rectTransform.anchorMax.x-.4f)<.001f,"Health text and primary fill display the exact latest8/20 immediately, independent of trailing damage feedback.");
        timer=run.levelState.normalTimeRemaining;
        yield return new WaitForSecondsRealtime(.12f);
        Check(run.levelState.normalTimeRemaining<timer-.02f && Flow(gm)=="Playing","Damage feedback adds neither invulnerability nor a timer pause.");
        gm.OpenSettings();string pausedVisual=Visuals(gm);timer=run.levelState.normalTimeRemaining;
        Tap(gm,false);yield return new WaitForSecondsRealtime(.18f);
        Check(run.currentHealth==8 && run.levelState.normalTimeRemaining==timer && Visuals(gm)==pausedVisual,"Settings during damage freezes gameplay and target presentation and rejects grid input.");
        gm.CloseSettings();yield return Ready(gm);
        CheckPointerSurface(gm,"Input after modal exit");
        run.currentHealth=1;Tap(gm,false);long banked=data.profile.coins;
        Check(run.currentHealth==0 && Flow(gm)=="RunSummary","Fatal damage ends the run immediately before its cosmetic cue completes.");
        Tap(gm,true);Tap(gm,false);yield return new WaitForSecondsRealtime(.4f);
        Check(run.levelState.objectiveProgress==before && data.profile.coins==banked,"Post-fatal taps and late effects neither advance objectives nor duplicate banking.");

        gm.ReturnToMainMenu();gm.StartDebugLevel(gm.campaign.LevelCount-1);yield return Ready(gm);
        run=RunData(gm);run.levelState.reverseCooldownRemaining=999;
        float angle=run.levelState.rotationAngle,phase=run.levelState.scalePhase;Vector2 position=run.levelState.movementPosition;
        Tap(gm,true);
        Check(run.levelState.rotationAngle==angle && run.levelState.scalePhase==phase && run.levelState.movementPosition==position,"A target tap never writes authoritative rotation, scale phase, or movement position.");
        gm.OpenSettings();pausedVisual=Visuals(gm);timer=run.levelState.normalTimeRemaining;
        yield return new WaitForSecondsRealtime(.2f);
        Check(Visuals(gm)==pausedVisual && run.levelState.normalTimeRemaining==timer,"Settings during target retargeting freezes at a coherent current frame.");
        gm.CloseSettings();yield return Ready(gm);yield return new WaitForSecondsRealtime(.18f);
        Check(run.levelState.rotationAngle!=angle && run.levelState.scalePhase!=phase,"Gameplay Rotation and Scale resume independently of target effects.");
        CheckSettled(gm,"Target pause and resume");
        BeginReverse(gm);gm.OpenSettings();timer=run.levelState.normalTimeRemaining;
        yield return new WaitForSecondsRealtime(.25f);
        Check(Flow(gm)=="Settings" && run.levelState.normalTimeRemaining==timer,"Settings during Reverse announcement preserves the established locked state and timer pause.");
        gm.CloseSettings();yield return Ready(gm);
        Check(run.levelState.reverseActive,"Paused Reverse entrance resumes to the authoritative Reverse rule.");

        gm.ReturnToMainMenu();gm.StartDebugLevel(0);yield return Ready(gm);
        run=RunData(gm);run.levelState.normalTimeRemaining=.02f;run.currentReserveSeconds=2;Tap(gm,true);
        yield return new WaitForSecondsRealtime(.12f);
        Check(run.levelState.reserveActive && run.currentReserveSeconds<1.98f && Flow(gm)=="Playing","Reserve activation and drain occur while target feedback is still active.");
        run.currentReserveSeconds=.01f;Tap(gm,false);yield return null;yield return null;
        Check(Flow(gm)=="RunSummary","Zero reserve ends the run without waiting for damage or resource tweens.");
        gm.ReturnToMainMenu();
        Check(JsonUtility.ToJson(data)==realBefore,"All debug motion fixtures leave the isolated real-profile envelope unchanged.");

        gm.StartNewRun();yield return Ready(gm);run=RunData(gm);run.levelState.reverseCooldownRemaining=999;
        Tap(gm,true);gm.ReturnToMainMenu();
        var saved=Get<NeonSaveService>(gm,"saveService").Load(gm.gameConfig).activeRun;
        string savedSequence=Sequence(saved);int savedHealth=saved.currentHealth;float savedNormal=saved.levelState.normalTimeRemaining;
        gm.ContinueRun();
        Check(RunData(gm).levelState.normalTimeRemaining==savedNormal,"Continue reconstructs the saved timer without consuming the concealed entry period.");
        yield return Ready(gm);run=RunData(gm);
        Check(Sequence(run)==savedSequence && run.currentHealth==savedHealth,"Home during feedback and Continue restore exact authoritative sequence and health.");
        CheckSettled(gm,"Restored run");
        long wallet=data.profile.coins;
        Check(!(bool)Invoke(gm,"TryPurchaseUpgrade",UpgradeId.MaximumHealth) && data.profile.coins==wallet,"Animation never bypasses the active-run purchase restriction.");
        run.levelState.objectiveProgress=gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks-1;Tap(gm,true);
        long reward=run.pendingCoins;
        Check(Flow(gm)=="LevelTransition" && reward>0,"Final pointer-down commits completion, reward and the prepared next level before presentation.");
        Tap(gm,true);Check(run.pendingCoins==reward,"A second pointer-down during the in-place transition cannot pay the reward again.");
        yield return Ready(gm);
        Check(RunData(gm).levelState.objectiveProgress==0 && RunData(gm).pendingCoins==reward,"Next-level entry supersedes obsolete effects and retains exactly one earned reward.");
        CheckSettled(gm,"Next level after automatic in-place transition");
        gm.ReturnToMainMenu();Invoke(gm,"ConfirmAbandonRun");wallet=data.profile.coins;
        gm.ReturnToMainMenu();gm.OpenUpgradeShop();yield return new WaitForSecondsRealtime(.3f);
        Check(data.activeRun==null && data.profile.coins==wallet,"Interrupting abandonment-result entrance retains the already banked balance and cleared run.");
        data.profile.coins=100000;
        gm.GetComponent<RogueliteUIController>().RefreshShop(gm.gameConfig,data.profile,false);
        int tier=data.profile.maximumHealthTier;wallet=data.profile.coins;long expectedCost=0;
        for(int i=0;i<4;i++)
        {
            expectedCost+=UpgradeCatalog.NextTier(gm.gameConfig,data.profile,UpgradeId.MaximumHealth).cost;
            PurchaseFirst(gm);
        }
        Check(data.profile.maximumHealthTier==tier+4 && data.profile.coins==wallet-expectedCost,"Four rapid purchase-button clicks use each latest authoritative price and commit exactly four upgrades.");
        wallet=data.profile.coins;tier=data.profile.maximumHealthTier;gm.ReturnToMainMenu();yield return new WaitForSecondsRealtime(.4f);
        Check(data.profile.coins==wallet && data.profile.maximumHealthTier==tier,"Interrupted purchase effects never duplicate transactions through late callbacks.");

        for(int i=0;i<8;i++){gm.OpenUpgradeShop();gm.ReturnToMainMenu();gm.OpenSettings();gm.CloseSettings();}
        yield return new WaitForSecondsRealtime(.6f);
        Check(Flow(gm)=="MainMenu","Repeated overlapping navigation requests settle to the latest requested menu state.");
        CheckScreens(gm,"Repeated navigation");
        gm.OpenSettings();yield return CheckModalContrast(gm,"Settings entrance");
        gm.CloseSettings();yield return CheckModalContrast(gm,"Settings exit");
        yield return new WaitForSecondsRealtime(.2f);
        var ui=gm.GetComponent<RogueliteUIController>();
        gm.OpenUpgradeShop();yield return null;
        var shop=Get<GameObject>(ui,"shopPanel").GetComponent<NeonScreenMotion>();
        Invoke(gm,"OnApplicationPause",true);float opacity=shop.Opacity;
        yield return new WaitForSecondsRealtime(.2f);
        Check(shop.Opacity==opacity,"Backgrounding during screen entry freezes presentation without consuming the background interval.");
        Invoke(gm,"OnApplicationPause",false);yield return new WaitForSecondsRealtime(.35f);
        Check(shop.IsReady && Mathf.Approximately(shop.Opacity,1),"Screen entry resumes to its exact final opacity after application resume.");
        shop.gameObject.SetActive(false);
        Check(!shop.IsReady,"An externally disabled visible panel does not report gameplay or navigation readiness.");
        gm.OpenUpgradeShop();yield return new WaitForSecondsRealtime(.3f);
        Check(shop.IsReady && shop.gameObject.activeInHierarchy,"Showing a desired-visible panel after external disable reactivates and settles it.");
        shop.enabled=false;int hiddenCalls=0;
        shop.SetVisible(false,()=>hiddenCalls++);
        Check(!shop.gameObject.activeSelf && !shop.IsAnimating && hiddenCalls==1,"A disabled optional screen owner hides immediately and invokes its completion once.");
        shop.SetVisible(true);
        Check(shop.IsReady && Mathf.Approximately(shop.Opacity,1),"A disabled optional screen owner reopens at its exact visible state without awaiting Update.");
        shop.enabled=true;
        gm.ReturnToMainMenu();yield return new WaitForSecondsRealtime(.3f);
        gm.OpenSettings();yield return new WaitForSecondsRealtime(.22f);gm.CloseSettings();
        gm.settingsPanel.SetActive(false);yield return new WaitForSecondsRealtime(.25f);
        Check(Flow(gm)=="MainMenu","External disable during modal dismissal still completes its guarded navigation continuation.");
        gm.StartDebugLevel(0);yield return Ready(gm);gm.OpenSettings();yield return new WaitForSecondsRealtime(.2f);
        gm.CloseSettings();gm.ReturnToMainMenu();gm.OpenUpgradeShop();yield return new WaitForSecondsRealtime(.35f);
        Check(Flow(gm)=="Shop" && !gm.gameplayPanel.activeInHierarchy,"An obsolete Settings-close callback cannot resume gameplay after a newer shop navigation.");
        CheckScreens(gm,"External disable and stale callback recovery");
        gm.ReturnToMainMenu();yield return new WaitForSecondsRealtime(.3f);

        var original=NeonMotion.T;
        var testSettings=UnityEngine.Object.Instantiate(original);
        var settingsField=typeof(NeonMotion).GetField("settings",BindingFlags.Static|BindingFlags.NonPublic);
        bool reduced=NeonTheme.ReducedEffects;
        try
        {
            foreach(var field in typeof(NeonMotionSettings).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(field.FieldType==typeof(float) && (field.Name.EndsWith("Duration") || field.Name=="resultStagger" || field.Name.EndsWith("Fade")))field.SetValue(testSettings,0f);
            settingsField.SetValue(null,testSettings);NeonTheme.ReducedEffects=true;
            gm.OpenUpgradeShop();gm.ReturnToMainMenu();gm.OpenSettings();gm.CloseSettings();
            yield return null;
            Check(Flow(gm)=="MainMenu","Zero-duration reduced-effect navigation completes without a callback lock.");
            gm.StartDebugLevel(gm.campaign.LevelCount-1);yield return Ready(gm);
            run=RunData(gm);run.levelState.reverseCooldownRemaining=999;Tap(gm,true);
            CheckRoles(gm,"Zero-duration target change");CheckSettled(gm,"Zero-duration target change");
            angle=run.levelState.rotationAngle;yield return new WaitForSecondsRealtime(.12f);
            Check(run.levelState.rotationAngle!=angle,"Reduced effects with zero cosmetic durations retains gameplay Rotation.");
            gm.ReturnToMainMenu();yield return null;CheckScreens(gm,"Zero-duration navigation");
        }
        finally { settingsField.SetValue(null,original);NeonTheme.ReducedEffects=reduced;UnityEngine.Object.Destroy(testSettings); }
        CheckNoOverflow("Final settled menu");
        File.WriteAllLines(Path.Combine(directory,"runtime-checks.txt"),checks);
    }

    private static IEnumerator Ready(GameManager gm)
    {
        float deadline=Time.realtimeSinceStartup+3;
        while(Flow(gm)!="Playing" && Time.realtimeSinceStartup<deadline)yield return null;
        if(Flow(gm)!="Playing")throw new InvalidOperationException("Motion QA gameplay readiness timed out in "+Flow(gm));
        yield return null;
    }
    private static uint FindReuseSeed(ActiveLevelStateData state,int count,bool reverse)
    {
        int consumed=reverse?state.smallIndex:state.largeIndex;
        for(uint seed=1;seed<100000;seed++)
        {
            var random=new DeterministicRandom(seed);var next=new GridSequenceState(state.smallIndex,state.mediumIndex,state.largeIndex);
            if(reverse)GridSequenceRules.AdvanceReverse(ref next,count,ref random);else GridSequenceRules.AdvanceNormal(ref next,count,ref random);
            if((reverse?next.large:next.small)==consumed)return seed;
        }
        throw new InvalidOperationException("No deterministic immediate-reuse seed found.");
    }
    private static void CheckRoles(GameManager gm,string label)
    {
        var run=RunData(gm);var squares=Get<List<GameSquare>>(gm,"instantiatedSquares");var state=run.levelState;
        bool correct=true;for(int i=0;i<squares.Count;i++)correct&=squares[i].currentLitSize==(i==state.smallIndex?GameSquare.LitSize.Small:i==state.mediumIndex?GameSquare.LitSize.Medium:i==state.largeIndex?GameSquare.LitSize.Large:GameSquare.LitSize.None);
        Check(correct,label+": every logical cell role matches the latest authoritative snapshot.");
        Check(squares[state.smallIndex].outlineImage.transform.localScale.x<squares[state.mediumIndex].outlineImage.transform.localScale.x && squares[state.mediumIndex].outlineImage.transform.localScale.x<squares[state.largeIndex].outlineImage.transform.localScale.x,label+": active Small < Medium < Large remains true immediately after retargeting.");
    }
    private static void CheckSettled(GameManager gm,string label)
    {
        var level=gm.campaign.GetLevel(RunData(gm).currentLevelIndex);bool settled=true;
        foreach(var square in Get<List<GameSquare>>(gm,"instantiatedSquares"))
        {
            float expected=square.currentLitSize==GameSquare.LitSize.Small?level.smallScale:square.currentLitSize==GameSquare.LitSize.Medium?level.mediumScale:square.currentLitSize==GameSquare.LitSize.Large?level.fullScale:0;
            settled&=square.currentLitSize==GameSquare.LitSize.None?!square.outlineImage.gameObject.activeSelf:Mathf.Abs(square.outlineImage.transform.localScale.x-expected)<.001f && ProbeFloat(square,"RenderedAlpha",1)>.99f;
            settled&=ProbeFloat(square,"RetiringAlpha",0)<.001f;
        }
        Check(settled,label+": settled targets have exact configured sizes, full active opacity, and no obsolete retirement effect.");
    }
    private static void CheckScreens(GameManager gm,string label)
    {
        bool stable=true;foreach(var motion in UnityEngine.Object.FindObjectsByType<NeonScreenMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            stable&=!motion.IsAnimating && (motion.WantsVisible?Mathf.Approximately(motion.Opacity,1):!motion.gameObject.activeSelf);
            var visual=Get<RectTransform>(motion,"visual");var origin=Get<Vector2>(motion,"origin");stable&=Vector2.Distance(visual.anchoredPosition,origin)<.001f;
        }
        Check(stable,label+": screens settle to exact origins/opacity with no invisible active overlay.");
    }
    private static void CheckPointerSurface(GameManager gm,string label)
    {
        Canvas.ForceUpdateCanvases();var run=RunData(gm);var squares=Get<List<GameSquare>>(gm,"instantiatedSquares");
        var square=squares[run.levelState.reverseActive?run.levelState.smallIndex:run.levelState.largeIndex];
        var hits=new List<RaycastResult>();var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,square.transform.position)};
        EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<GameSquare>()==square,label+": rendered target center resolves through the actual GraphicRaycaster to its authoritative cell.");
        hits.Clear();pointer.position=new Vector2(1,1);EventSystem.current.RaycastAll(pointer,hits);
        Check(!hits.Exists(hit=>hit.gameObject.GetComponentInParent<GameSquare>()!=null),label+": outside-grid point has no target input surface.");
    }
    private static IEnumerator CheckModalContrast(GameManager gm,string label)
    {
        var motion=gm.settingsPanel.GetComponent<NeonScreenMotion>();var backdrop=gm.settingsPanel.GetComponent<Image>();var input=gm.settingsPanel.GetComponent<CanvasGroup>();
        bool valid=true,observed=false;float deadline=Time.realtimeSinceStartup+.5f;
        while(motion.IsAnimating && Time.realtimeSinceStartup<deadline)
        {
            float opacity=motion.Opacity;
            valid&=opacity>=0 && opacity<=1;
            if(opacity>=.25f){observed=true;valid&=Mathf.Abs(backdrop.color.a-1)<.001f && input.blocksRaycasts;}
            yield return null;
        }
        Check(valid && observed,label+": readable content has an opaque backdrop and modal input coverage throughout its bounded fade.");
    }
    private static string Visuals(GameManager gm)
    {
        var result=new List<string>();foreach(var square in Get<List<GameSquare>>(gm,"instantiatedSquares"))result.Add(square.outlineImage.transform.localScale.x.ToString("F5")+"/"+ProbeFloat(square,"RenderedAlpha",1).ToString("F5")+"/"+ProbeFloat(square,"RetiringAlpha",0).ToString("F5"));return string.Join(";",result);
    }
    private static float ProbeFloat(object instance,string property,float fallback)
    {var probe=instance.GetType().GetProperty(property);return probe==null?fallback:Convert.ToSingle(probe.GetValue(instance));}
    private static string Flow(GameManager gm)=>Get<object>(gm,"state").ToString();
    private static string ConfigurationDirectory(string stage)=>Path.Combine(Application.dataPath,"..","Artifacts","Motion",stage,Screen.width+"x"+Screen.height+(NeonSafeArea.PreviewSafeArea.HasValue?"-safe-insets":""));
    private static List<string> ActiveTextLayout()
    {
        Canvas.ForceUpdateCanvases();var lines=new List<string>{"Screen "+Screen.width+"x"+Screen.height+"; preview safe area "+(NeonSafeArea.PreviewSafeArea.HasValue?NeonSafeArea.PreviewSafeArea.Value.ToString():"full screen")};
        foreach(var label in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            if(label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)){label.ForceMeshUpdate();lines.Add(label.name+" | "+label.text.Replace("\n"," / ")+" | "+label.rectTransform.rect+" | overflow "+label.isTextOverflowing);}
        return lines;
    }
    private static void CheckNoOverflow(string context){var lines=ActiveTextLayout();Check(!lines.Exists(line=>line.Contains("overflow True")),context+": active TMP labels have no text overflow.");}
    private static string Sequence(ActiveRunData run)=>run.levelState.smallIndex+","+run.levelState.mediumIndex+","+run.levelState.largeIndex;
    private static void Check(bool condition,string description){string line=(condition?"PASS: ":"FAIL: ")+description;checks.Add(line);if(!string.IsNullOrEmpty(checkPath))File.AppendAllText(checkPath,line+Environment.NewLine);if(!condition)Debug.LogError("Motion QA: "+description);}
    private static ActiveRunData RunData(GameManager gm)=>Get<ActiveRunData>(gm,"sessionRun");
    private static T Get<T>(object instance,string name)=>(T)instance.GetType().GetField(name,Hidden).GetValue(instance);
    private static object Invoke(object instance,string name,params object[] values)=>instance.GetType().GetMethod(name,Hidden).Invoke(instance,values);
}
