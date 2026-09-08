#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Focused editor QA route for real gameplay HUD states and native navigation.</summary>
public static class NeonGameplayQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null || !NeonPresentationQAProfile.IsActive) throw new InvalidOperationException("Gameplay QA requires an isolated profile.");
        var service = Field<NeonSaveService>(gm, "saveService");
        if (service == null || service.DirectoryPath != NeonPresentationQAProfile.DirectoryPath) throw new InvalidOperationException("Non-isolated save profile refused.");
        var data = Field<SaveEnvelopeData>(gm, "saveData"); data.activeRun = null; gm.ShowMainMenu();
        yield return Wait(gm, "MainMenu", check);
        yield return Click(gm.startContinueButton, check, "start run");
        yield return Wait(gm, "Playing", check);
        var ui = gm.GetComponent<RogueliteUIController>(); var run = Field<ActiveRunData>(gm, "sessionRun");
        check(run != null && ui.IsGameplayReady, "real run reaches settled gameplay UI");
        gm.enabled = false;
        yield return CaptureAndFit(gm, capture, check, "01-normal-gameplay");
        int savedHealth = run.currentHealth, savedObjective = run.levelState.objectiveProgress; float savedReserve = run.currentReserveSeconds, savedTime = run.levelState.normalTimeRemaining;
        run.currentHealth = 1; Invoke(gm, "UpdateGameplayUI"); yield return CaptureAndFit(gm, capture, check, "02-low-health");
        run.levelState.normalTimeRemaining = 0f; run.currentReserveSeconds = 12.3f; run.levelState.reserveActive = true; Invoke(gm, "UpdateGameplayUI"); yield return CaptureAndFit(gm, capture, check, "03-reserve");
        run.levelState.reverseActive = true; ui.RefreshHud(1, 20, 12.3f, 12.3f, run.pendingCoins, 0, 160, 99.9f, 99.9f, false, true, false, 12); yield return CaptureAndFit(gm, capture, check, "04-reverse-HUD");
        ui.RefreshHud(20, 20, 9999.9f, 9999.9f, run.pendingCoins, 0, 160, 99.9f, 99.9f, false, false, false, 0); yield return CaptureAndFit(gm, capture, check, "05-capacity");
        check(!HasCellBoundary(gm), "No CellBoundary decoration exists");
        foreach (var graphic in gm.gameplayPanel.GetComponentsInChildren<NeonGameplayGraphic>(true)) check(!graphic.raycastTarget, "Gameplay decoration is noninteractive: " + graphic.name);
        run.currentHealth=savedHealth; run.levelState.objectiveProgress=savedObjective; run.currentReserveSeconds=savedReserve; run.levelState.normalTimeRemaining=savedTime; run.levelState.reserveActive=false; run.levelState.reverseActive=false; Invoke(gm,"UpdateGameplayUI"); gm.enabled = true;
        float frozen=run.levelState.normalTimeRemaining; yield return Click(gm.settingsButton,check,"pause settings"); yield return Wait(gm,"Settings",check); yield return new WaitForSecondsRealtime(.2f); check(Mathf.Approximately(frozen,run.levelState.normalTimeRemaining),"Settings freezes gameplay timer");
        yield return Click(gm.settingsCloseButton,check,"resume settings"); yield return Wait(gm,"Playing",check); yield return Click(gm.homeButton,check,"home"); yield return Wait(gm,"MainMenu",check); yield return Click(gm.startContinueButton,check,"continue run"); yield return Wait(gm,"Playing",check);
        var continued=Field<ActiveRunData>(gm,"sessionRun"); check(continued.currentHealth==savedHealth && continued.levelState.objectiveProgress==savedObjective && Mathf.Abs(continued.currentReserveSeconds-savedReserve)<.1f,"Continue restores health, objective, and reserve"); yield return Capture(capture,"06-resumed");
    }
    private static IEnumerator Capture(Func<string,IEnumerator> capture,string name){if(capture!=null){var r=capture(name);if(r!=null)yield return r;}}
    private static IEnumerator CaptureAndFit(GameManager gm,Func<string,IEnumerator> capture,Action<bool,string> check,string name){yield return Capture(capture,name);Canvas.ForceUpdateCanvases();foreach(var text in gm.gameplayPanel.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Gameplay label fits in "+name+": "+text.name);}}
    private static IEnumerator Wait(GameManager gm,string flow,Action<bool,string> check){float end=Time.realtimeSinceStartup+4;while(Time.realtimeSinceStartup<end){if(Field<object>(gm,"state").ToString()==flow&&Ready(gm,flow)){check(true,"Flow reaches "+flow+" and settles");yield break;}yield return null;}check(false,"Flow reaches "+flow+" and settles");throw new InvalidOperationException("Gameplay QA timeout: "+flow);}
    private static bool Ready(GameManager gm,string flow){GameObject p=flow=="MainMenu"?gm.mainMenuPanel:flow=="Settings"?gm.settingsPanel:flow=="Playing"?gm.gameplayPanel:null;if(p==null)return false;var m=p.GetComponent<NeonScreenMotion>();return m==null?p.activeInHierarchy:m.IsReady&&m.Opacity>=.999f&&(flow!="Playing"||gm.GetComponent<RogueliteUIController>().IsGameplayReady);}
    private static IEnumerator Click(Button b,Action<bool,string> check,string name){if(b==null)throw new InvalidOperationException("Missing "+name);Canvas.ForceUpdateCanvases();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.position),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);bool ok=hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b;check(ok,"Native raycast reaches "+name);if(!ok)throw new InvalidOperationException("Native raycast failed: "+name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);yield return null;}
    private static bool HasCellBoundary(GameManager gm){foreach(var tr in gm.gameplayPanel.GetComponentsInChildren<Transform>(true))if(tr.name.Contains("CellBoundary"))return true;return false;}
    private static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,Hidden).GetValue(o);} private static object Invoke(object o,string n,params object[] a){return o.GetType().GetMethod(n,Hidden).Invoke(o,a);}
}
#endif
