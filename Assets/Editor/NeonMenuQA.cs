using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small native-menu regression route. It uses the real EventSystem path and an isolated QA save.</summary>
public static class NeonMenuQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null) throw new ArgumentNullException("gm");
        if (!NeonPresentationQAProfile.IsActive)
            throw new InvalidOperationException("Menu QA requires an isolated NeonPresentationQAProfile before mutations.");
        var service = Field<NeonSaveService>(gm, "saveService");
        if (service == null || service.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Menu QA refused to mutate a non-isolated save profile.");
        var data = Field<SaveEnvelopeData>(gm, "saveData");
        data.profile.coins = 992;
        data.activeRun = null;
        gm.ShowMainMenu();
        yield return WaitFor(gm, "MainMenu", 4f, check);
        yield return Capture(capture, "01-menu-fresh");
        CheckMenu(gm, check, false);

        yield return Click(gm.upgradesButton, check, "menu upgrades");
        yield return WaitFor(gm, "Shop", 4f, check);
        gm.ReturnToMainMenu(); yield return WaitFor(gm, "MainMenu", 4f, check);
        yield return Click(gm.menuSettingsButton, check, "menu settings");
        yield return WaitFor(gm, "Settings", 4f, check);
        gm.CloseSettings(); yield return WaitFor(gm, "MainMenu", 4f, check);
        yield return Click(gm.menuSettingsButton, check, "menu settings repeated");
        yield return WaitFor(gm, "Settings", 4f, check);
        gm.CloseSettings(); yield return WaitFor(gm, "MainMenu", 4f, check);

        yield return Click(gm.startContinueButton, check, "start run");
        yield return WaitFor(gm, "Playing", 4f, check);
        data = Field<SaveEnvelopeData>(gm, "saveData");
        var run = Field<ActiveRunData>(gm, "sessionRun");
        if (run == null) throw new InvalidOperationException("Native start did not create a run.");
        run.currentHealth = 2; run.currentReserveSeconds = 7.25f;
        int required = gm.campaign.GetLevel(run.currentLevelIndex).requiredCorrectClicks;
        run.levelState.objectiveProgress = Mathf.Min(2, Mathf.Max(1, required));
        gm.ReturnToMainMenu(); yield return WaitFor(gm, "MainMenu", 4f, check);
        yield return Capture(capture, "02-menu-active-run");
        CheckMenu(gm, check, true);
        string before = JsonUtility.ToJson(data.activeRun);
        ActiveRunData savedBeforeContinue = JsonUtility.FromJson<ActiveRunData>(before);
        yield return Click(gm.startContinueButton, check, "continue run");
        yield return WaitFor(gm, "Playing", 4f, check);
        var continued = Field<ActiveRunData>(gm, "sessionRun");
        check(continued != null, "Continue creates a live run");
        if (continued != null)
        {
            check(continued.currentLevelIndex == savedBeforeContinue.currentLevelIndex, "Continue preserves level index");
            check(continued.levelState.smallIndex == savedBeforeContinue.levelState.smallIndex &&
                  continued.levelState.mediumIndex == savedBeforeContinue.levelState.mediumIndex &&
                  continued.levelState.largeIndex == savedBeforeContinue.levelState.largeIndex, "Continue preserves target sequence");
            check(continued.randomState == savedBeforeContinue.randomState, "Continue preserves deterministic random state");
            check(continued.currentHealth == 2 && Mathf.Abs(continued.currentReserveSeconds - 7.25f) < .01f, "Continue preserves health and reserve");
            check(continued.levelState.objectiveProgress == 2, "Continue preserves objective progress");
        }
        gm.ReturnToMainMenu(); yield return WaitFor(gm, "MainMenu", 4f, check);

        Button abandon = FindButton("AbandonRun");
        if (abandon == null) { check(false, "AbandonRun button exists"); throw new InvalidOperationException("Menu QA requires AbandonRun."); }
        else
        {
            yield return Click(abandon, check, "abandon run");
            Button keep = FindButton("KeepRun");
            if (keep == null) { check(false, "KeepRun button exists"); throw new InvalidOperationException("Abandon dialog did not create KeepRun."); }
            yield return WaitForButtonActive(keep, true, 4f, check, "abandon dialog");
            yield return Click(keep, check, "keep run");
            yield return WaitForButtonActive(keep, false, 4f, check, "abandon dialog dismissal");
            check(data.activeRun != null, "Canceling abandonment keeps the saved run");
            yield return Capture(capture, "03-menu-abandon-cancel");
        }
        Button wallet = FindButton("WalletUpgrades");
        if (wallet == null) { check(false, "WalletUpgrades button exists"); throw new InvalidOperationException("Menu QA requires WalletUpgrades."); }
        else
        {
            yield return Click(wallet, check, "wallet upgrades");
            yield return WaitFor(gm, "Shop", 4f, check);
            gm.ReturnToMainMenu(); yield return WaitFor(gm, "MainMenu", 4f, check);
        }
        check(before != null && before.Length > 0, "Active run snapshot captured before native continue");
    }

    private static IEnumerator Capture(Func<string, IEnumerator> capture, string label)
    { if (capture != null) { var routine = capture(label); if (routine != null) yield return routine; } }

    private static IEnumerator Click(Button button, Action<bool, string> check, string label)
    {
        if (button == null) { check(false, "Missing button: " + label); yield break; }
        Canvas.ForceUpdateCanvases();
        var eventSystem = EventSystem.current;
        var data = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
        var hits = new List<RaycastResult>(); eventSystem.RaycastAll(data, hits);
        bool top = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
        check(top, "Native raycast reaches " + label);
        if (top)
        {
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        yield return null;
    }

    private static IEnumerator WaitFor(GameManager gm, string flow, float seconds, Action<bool, string> check)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            if (Field<object>(gm, "state").ToString() == flow && ScreenReady(gm, flow))
            { yield return null; check(true, "Flow reaches " + flow + " and settles"); yield break; }
            yield return null;
        }
        check(false, "Flow reaches " + flow + " within 4 seconds");
    }

    private static IEnumerator WaitForButtonActive(Button button, bool expected, float seconds, Action<bool, string> check, string label)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            bool visible = button != null && button.gameObject.activeInHierarchy;
            NeonScreenMotion motion = button == null ? null : button.GetComponentInParent<NeonScreenMotion>();
            bool settled = !expected || (motion != null && motion.IsReady && motion.Opacity >= .999f);
            if (visible == expected && settled) { check(true, label + " visibility settles"); yield break; }
            yield return null;
        }
        check(false, label + " visibility settles");
    }

    private static void CheckMenu(GameManager gm, Action<bool, string> check, bool active)
    {
        check(gm.startContinueButton != null && gm.startContinueButton.gameObject.activeInHierarchy, "Start/continue button displayed");
        check(gm.upgradesButton != null && gm.upgradesButton.gameObject.activeInHierarchy, "Upgrades button displayed");
        check(gm.menuSettingsButton != null && gm.menuSettingsButton.gameObject.activeInHierarchy, "Settings button displayed");
        if (active)
        {
            TMP_Text text = gm.startContinueButton.GetComponentInChildren<TMP_Text>();
            check(text != null && text.text.Contains("CONTINUE RUN"), "Active menu says CONTINUE RUN");
        }
        foreach (TMP_Text text in gm.mainMenuPanel.GetComponentsInChildren<TMP_Text>(false))
            check(!text.isTextOverflowing, "Menu label fits: " + text.name);
    }

    private static Button FindButton(string name)
    { foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (b.name == name) return b; return null; }

    private static bool ScreenReady(GameManager gm, string flow)
    {
        GameObject panel = flow == "MainMenu" ? gm.mainMenuPanel : flow == "Settings" ? gm.settingsPanel :
            flow == "Playing" ? gm.gameplayPanel : flow == "Shop" ? Field<GameObject>(gm.GetComponent<RogueliteUIController>(), "shopPanel") : null;
        if (panel == null) return false;
        var motion = panel.GetComponent<NeonScreenMotion>();
        return motion == null ? panel.activeInHierarchy : motion.IsReady && motion.Opacity >= .999f;
    }
    private static T Field<T>(object target, string name) where T : class
    { return typeof(T).IsValueType ? null : (T)target.GetType().GetField(name, Hidden).GetValue(target); }
}
