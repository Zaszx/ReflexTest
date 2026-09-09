using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Focused capture and navigation checks using the existing isolated profile runner.</summary>
public static class NeonSettingsQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (!NeonPresentationQAProfile.IsActive || Get<NeonSaveService>(gm, "saveService").DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Settings capture requires isolated saves.");
        bool reducedBefore = NeonTheme.ReducedEffects;
        var ui = Get<RogueliteUIController>(gm, "rogueliteUI");
        try
        {
            gm.ReturnToMainMenu(); gm.StartNewRun(); yield return Wait(gm, "Playing");
            gm.OpenSettings(); yield return new WaitForSecondsRealtime(.3f);
            var run = Get<ActiveRunData>(gm, "sessionRun"); string frozen = JsonUtility.ToJson(run);
            NeonTheme.ReducedEffects = false; ui.SetHaptics(true);
            check(Get<TMP_Text>(ui, "settingsHeading").text == "PAUSED", "Gameplay opens the PAUSED heading.");
            check(gm.settingsCloseButton.GetComponentInChildren<TMP_Text>().text == "RESUME", "Gameplay offers RESUME.");
            var card = Get<RectTransform>(ui, "settingsCard"); var parent = (RectTransform)card.parent;
            var corners = new Vector3[4]; card.GetWorldCorners(corners);
            foreach (Vector3 corner in corners) check(parent.rect.Contains(parent.InverseTransformPoint(corner)), "Settings card fits the safe content area.");
            foreach (TMP_Text label in card.GetComponentsInChildren<TMP_Text>())
            { label.ForceMeshUpdate(); check(!label.isTextOverflowing, "Settings text fits: " + label.name); }
            yield return capture("01-paused-reference");
            var reduced = Get<Toggle>(ui, "reducedToggle");
            Click(gm.hapticToggle.gameObject); Click(reduced.gameObject);
            yield return new WaitForSecondsRealtime(.2f);
            check(!gm.hapticToggle.isOn && reduced.isOn && NeonTheme.ReducedEffects, "Both switches respond to Toggle pointer-click events.");
            check(JsonUtility.ToJson(run) == frozen, "Settings interactions leave health, timers, targets and motion state frozen.");
            yield return capture("02-switches-reversed");
            Click(reduced.gameObject); ui.SetHaptics(true);
            Click(gm.settingsCloseButton.gameObject);
            check(Flow(gm) == "Settings", "Resume keeps the game paused until the modal exit completes.");
            yield return Wait(gm, "Playing");
            check(!gm.settingsPanel.activeSelf, "Resume removes the settings input layer.");
            gm.OpenSettings(); yield return new WaitForSecondsRealtime(.3f);
            string checkpoint = JsonUtility.ToJson(run);
            Click(card.Find("SettingsHome").gameObject);
            yield return Wait(gm, "MainMenu"); yield return new WaitForSecondsRealtime(.3f);
            var data = Get<SaveEnvelopeData>(gm, "saveData");
            check(data.activeRun == run && JsonUtility.ToJson(run) == checkpoint, "Return Home preserves the active run checkpoint without banking it.");
            check(!gm.settingsPanel.activeSelf, "Return Home removes the modal and its raycast layer.");
            gm.OpenSettings(); yield return new WaitForSecondsRealtime(.3f);
            ui.SetHaptics(true);
            check(Get<TMP_Text>(ui, "settingsHeading").text == "SETTINGS", "Menu opens SETTINGS instead of PAUSED.");
            check(gm.settingsCloseButton.GetComponentInChildren<TMP_Text>().text == "DONE", "Menu settings offers DONE.");
            yield return capture("03-menu-settings");
            Click(gm.settingsCloseButton.gameObject); yield return Wait(gm, "MainMenu");
        }
        finally { NeonTheme.ReducedEffects = reducedBefore; gm.ReturnToMainMenu(); }
    }
    private static void Click(GameObject target) => ExecuteEvents.Execute(target,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    private static IEnumerator Wait(GameManager gm, string expected)
    {
        float deadline = Time.realtimeSinceStartup + 4;
        while (Flow(gm) != expected && Time.realtimeSinceStartup < deadline) yield return null;
        if (Flow(gm) != expected) throw new InvalidOperationException("Expected " + expected + "; got " + Flow(gm));
        yield return null;
    }
    private static string Flow(GameManager gm) => Get<object>(gm, "state").ToString();
    private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Hidden).GetValue(o);
}
