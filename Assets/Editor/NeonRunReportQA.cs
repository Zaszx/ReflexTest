#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Editor-only validation for health, timeout, abandon, and practice reports.</summary>
public static class NeonRunReportQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null || !NeonPresentationQAProfile.IsActive)
            throw new InvalidOperationException("Run report QA requires isolated profile storage.");
        var service = Field<NeonSaveService>(gm, "saveService");
        if (service == null || service.DirectoryPath != NeonPresentationQAProfile.DirectoryPath)
            throw new InvalidOperationException("Non-isolated save profile refused.");
        var data = Field<SaveEnvelopeData>(gm, "saveData");
        data.profile.coins = 799;
        data.activeRun = null;
        gm.ShowMainMenu();
        yield return Wait(gm, "MainMenu", check);
        yield return Click(gm.startContinueButton, check, "start run");
        yield return Wait(gm, "Playing", check);
        var run = Field<ActiveRunData>(gm, "sessionRun");
        run.currentLevelIndex = 7;
        run.currentLevelId = gm.campaign.GetLevel(7).stableId;
        run.levelsCompleted = 7;
        run.pendingCoins = 168;
        run.currentHealth = 1;
        run.levelState.objectiveProgress = 0;
        Invoke(gm, "UpdateGameplayUI");
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        yield return Wait(gm, "RunSummary", check);
        yield return CaptureAndFit(gm, capture, check, "01-health-depleted");
        check(data.activeRun == null && data.profile.coins == 967, "Health report clears active run and banks 168 coins exactly once");
        var disk = service.Load(gm.gameConfig);
        check(disk.activeRun == null && disk.profile.coins == 967, "Persisted report transaction has no active run and wallet 967");
        CheckReport(gm, check, "RUN OVER", "HEALTH DEPLETED", true);
        check(ReportLabel(gm, "LeftValue") == "08" && ReportLabel(gm, "RightValue") == "07" && ReportLabel(gm, "Amount") == "+168", "Report shows actual level 08, seven completed and 168 earned");
        long wallet = data.profile.coins;
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        check(data.profile.coins == wallet, "Repeated health terminal callback cannot double-bank");
        yield return Click(gm.failPrimaryButton, check, "report upgrades");
        yield return Wait(gm, "Shop", check);
        gm.ReturnToMainMenu();
        yield return Wait(gm, "MainMenu", check);
        data.activeRun = null;
        yield return Click(gm.startContinueButton, check, "timeout run");
        yield return Wait(gm, "Playing", check);
        run = Field<ActiveRunData>(gm, "sessionRun");
        run.levelState.normalTimeRemaining = 0f;
        run.levelState.reserveActive = true;
        run.currentReserveSeconds = .02f;
        run.pendingCoins = 0;
        Invoke(gm, "UpdateGameplayUI");
        yield return new WaitForSecondsRealtime(.15f);
        yield return Wait(gm, "RunSummary", check);
        yield return CaptureAndFit(gm, capture, check, "02-out-of-time");
        CheckReport(gm, check, "RUN OVER", "OUT OF TIME", false);
        check(data.profile.coins == wallet, "Out-of-time with zero pending coins leaves wallet unchanged");
        yield return Click(gm.failMenuButton, check, "timeout return home");
        yield return Wait(gm, "MainMenu", check);
        var summary = new RunSummaryData
        {
            reason = "HEALTH DEPLETED",
            totalEarned = long.MaxValue,
            newWalletBalance = long.MaxValue,
            highestLevelEntered = 100,
            levelsCompleted = 99,
            runLevelRewards = long.MaxValue,
            completionBonus = long.MaxValue
        };
        gm.GetComponent<RogueliteUIController>().ShowRunSummary(summary);
        yield return CaptureAndFit(gm, capture, check, "03-long-values");
        check(data.profile.coins == wallet, "Long-value report presentation does not mutate wallet");
        yield return Click(gm.failMenuButton, check, "long-value return home");
        yield return Wait(gm, "MainMenu", check);
        data.activeRun = null;
        yield return Click(gm.startContinueButton, check, "abandon run");
        yield return Wait(gm, "Playing", check);
        Invoke(gm, "ConfirmAbandonRun");
        yield return Wait(gm, "RunSummary", check);
        yield return CaptureAndFit(gm, capture, check, "04-abandoned");
        CheckReport(gm, check, "RUN CLOSED", "RUN ABANDONED", true);
        yield return Click(gm.failPrimaryButton, check, "abandon upgrades");
        yield return Wait(gm, "Shop", check);
        gm.ReturnToMainMenu();
        yield return Wait(gm, "MainMenu", check);
        var before = JsonUtility.ToJson(data);
        gm.StartDebugLevel(0);
        yield return Wait(gm, "Playing", check);
        Invoke(gm, "FailRun", "HEALTH DEPLETED");
        yield return Wait(gm, "RunSummary", check);
        yield return CaptureAndFit(gm, capture, check, "05-practice");
        check(JsonUtility.ToJson(data) == before, "Practice report leaves isolated profile envelope unchanged");
        check(Text(gm.failLevelText).Contains("PRACTICE") && Text(gm.failPrimaryButton.GetComponentInChildren<TMP_Text>()) == "RETURN TO MENU", "Practice report title and primary navigation are correct");
        yield return Click(gm.failPrimaryButton, check, "practice return home");
        yield return Wait(gm, "MainMenu", check);
    }

    private static void CheckReport(GameManager gm, Action<bool, string> c, string title, string cause, bool red)
    {
        c(Text(gm.failLevelText).Contains(title), "Report title contains " + title);
        c(Text(gm.failReasonText).Contains(cause), "Report cause contains " + cause);
        var graphic = FindReportGraphic(gm.failPanel);
        c(graphic != null, "Run report emblem exists");
        if (graphic != null)
        {
            var kind = graphic.GetType().GetField("kind", BindingFlags.Instance | BindingFlags.Public);
            c(kind != null && kind.GetValue(graphic).ToString().Contains(red ? "ShatteredSquare" : "EmptyHourglass"), "Report emblem matches failure cause");
        }
    }

    private static IEnumerator CaptureAndFit(GameManager gm, Func<string, IEnumerator> cap, Action<bool, string> c, string n)
    {
        if (cap != null)
        {
            var r = cap(n);
            if (r != null)
                yield return r;
        }

        Canvas.ForceUpdateCanvases();
        foreach (var t in gm.failPanel.GetComponentsInChildren<TMP_Text>(false))
        {
            t.ForceMeshUpdate();
            if (t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                c(!t.isTextOverflowing, "Report text fits: " + n + ": " + t.name);
        }
    }

    private static Component FindReportGraphic(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<Component>(true))
            if (c.GetType().Name == "NeonRunReportGraphic" && c.gameObject.name == "RunOverEmblem")
                return c;
        return null;
    }

    private static string ReportLabel(GameManager gm, string name)
    {
        foreach (var t in gm.failPanel.GetComponentsInChildren<TMP_Text>())
            if (t.name == name)
                return Text(t);
        return string.Empty;
    }

    private static string Text(TMP_Text t) => t == null ? string.Empty : System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]*>", string.Empty);
    private static IEnumerator Wait(GameManager gm, string flow, Action<bool, string> c)
    {
        float e = Time.realtimeSinceStartup + 4;
        while (Time.realtimeSinceStartup < e)
        {
            if (Field<object>(gm, "state").ToString() == flow && Ready(gm, flow))
            {
                c(true, "Flow reaches " + flow);
                yield break;
            }

            yield return null;
        }

        c(false, "Flow reaches " + flow);
        throw new InvalidOperationException("Run report QA timeout: " + flow);
    }

    private static bool Ready(GameManager gm, string f)
    {
        GameObject p = f == "MainMenu" ? gm.mainMenuPanel : f == "Playing" ? gm.gameplayPanel : f == "RunSummary" ? gm.failPanel : f == "Shop" ? Field<GameObject>(gm.GetComponent<RogueliteUIController>(), "shopPanel") : null;
        if (p == null)
            return true;
        var m = p.GetComponent<NeonScreenMotion>();
        return m == null ? p.activeInHierarchy : m.IsReady && m.Opacity >= .999f;
    }

    private static IEnumerator Click(Button b, Action<bool, string> c, string n)
    {
        if (b == null)
            throw new InvalidOperationException("Missing " + n);
        Canvas.ForceUpdateCanvases();
        var p = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, b.transform.position),
            button = PointerEventData.InputButton.Left
        };
        var h = new List<RaycastResult>();
        EventSystem.current.RaycastAll(p, h);
        bool ok = h.Count > 0 && h[0].gameObject.GetComponentInParent<Button>() == b;
        c(ok, "Native raycast reaches " + n);
        if (!ok)
            throw new InvalidOperationException("Native raycast failed: " + n);
        ExecuteEvents.ExecuteHierarchy(h[0].gameObject, p, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(h[0].gameObject, p, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(h[0].gameObject, p, ExecuteEvents.pointerClickHandler);
        yield return null;
    }

    private static T Field<T>(object o, string n)
    {
        return (T)o.GetType().GetField(n, Hidden).GetValue(o);
    }

    private static object Invoke(object o, string n, params object[] a)
    {
        return o.GetType().GetMethod(n, Hidden).Invoke(o, a);
    }
}
#endif
