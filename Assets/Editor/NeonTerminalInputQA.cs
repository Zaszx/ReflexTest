using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime Input System coverage for the terminal report's fresh-input barrier.</summary>
public static class NeonTerminalInputQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run(GameManager gm, Action<bool, string> check)
    {
        Mouse mouse = null;
        Gamepad gamepad = null;
        Keyboard keyboard = null;
        Button reportButton = null;
        Button.ButtonClickedEvent originalClicks = null;
        bool clicksSwapped = false;
        int clickCount = 0;
        InputSettings inputSettings = null;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput = default;
        InputSettings.BackgroundBehavior originalBackgroundInput = default;
        bool originalRunInBackground = false;
        bool inputSettingsOverridden = false;
        try
        {
            Require(gm != null, check, "Terminal input QA has a GameManager.");
            Require(check != null, check, "Terminal input QA has a result callback.");
            var ui = Get<RogueliteUIController>(gm, "rogueliteUI");
            var save = Get<SaveEnvelopeData>(gm, "saveData");
            RunSummaryData summary = save != null && save.lastRunResult != null
                ? save.lastRunResult.ToSummary()
                : new RunSummaryData { reason = "HEALTH DEPLETED" };
            GameObject reportPanel = summary.campaignCompleted ? gm.successPanel : gm.failPanel;
            reportButton = summary.campaignCompleted ? gm.successNextButton : gm.failPrimaryButton;
            Require(ui != null && reportPanel != null && reportButton != null, check,
                "Terminal report UI and its primary button are available.");
            originalClicks = reportButton.onClick;
            var isolatedClicks = new Button.ButtonClickedEvent();
            isolatedClicks.AddListener(() => clickCount++);
            reportButton.onClick = isolatedClicks;
            clicksSwapped = true;

            var eventSystem = EventSystem.current;
            var module = eventSystem == null ? null : eventSystem.currentInputModule as InputSystemUIInputModule;
            Require(module != null && module.submit != null && module.submit.action != null, check,
                "EventSystem uses InputSystemUIInputModule with an actual Submit action.");

            // Presentation QA is launched from an Editor window, so the Game View
            // may not own focus. Route only this run's synthetic events to play mode.
            inputSettings = InputSystem.settings;
            originalEditorInput = inputSettings.editorInputBehaviorInPlayMode;
            originalBackgroundInput = inputSettings.backgroundBehavior;
            originalRunInBackground = InputSystem.runInBackground;
            inputSettingsOverridden = true;
            inputSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.runInBackground = true;

            mouse = InputSystem.AddDevice<Mouse>("NeonTerminalQaMouse");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            for (int frame = 0; frame < 3 && !mouse.leftButton.isPressed; frame++) yield return null;
            Require(mouse.leftButton.isPressed, check, "Synthetic mouse left press reached the Input System.");

            ui.ShowCommittedRunReportImmediate(summary);
            Canvas.ForceUpdateCanvases();
            CanvasGroup reportGroup = reportPanel.GetComponent<CanvasGroup>();
            Require(!ui.IsRunReportInputReady && reportGroup != null && !reportGroup.interactable &&
                !reportButton.IsInteractable(), check,
                "Held pointer remains blocked after the committed report is presentation-ready.");
            ExecuteEvents.Execute(reportButton.gameObject,
                new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
            Require(clickCount == 0, check,
                "The real report Button rejects pointer-click dispatch while its input layer is closed.");

            InputSystem.QueueStateEvent(mouse, new MouseState());
            for (int frame = 0; frame < 3 && mouse.leftButton.isPressed; frame++) yield return null;
            Require(!mouse.leftButton.isPressed, check, "Synthetic mouse release reached the Input System.");
            Require(!ui.IsRunReportInputReady && !reportGroup.interactable, check,
                "Pointer release frame remains blocked.");
            yield return null;
            Require(ui.IsRunReportInputReady && reportGroup.interactable && reportButton.IsInteractable(), check,
                "A later frame arms report controls and RefreshInputLayers updates the CanvasGroup.");
            ExecuteEvents.Execute(reportButton.gameObject,
                new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
            Require(clickCount == 1, check,
                "The real report Button accepts one fresh pointer-click dispatch after readiness.");

            gamepad = InputSystem.AddDevice<Gamepad>("NeonTerminalQaGamepad");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            for (int frame = 0; frame < 3 && !gamepad.buttonSouth.isPressed; frame++) yield return null;
            bool submitHeld = gamepad.buttonSouth.isPressed && module.submit.action.IsPressed();
            bool keyboardSubmit = false;
            if (!submitHeld)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                keyboard = InputSystem.AddDevice<Keyboard>("NeonTerminalQaKeyboard");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                for (int frame = 0; frame < 3 && !keyboard.enterKey.isPressed; frame++) yield return null;
                submitHeld = keyboard.enterKey.isPressed && module.submit.action.IsPressed();
                keyboardSubmit = true;
            }
            Require(submitHeld, check,
                "Synthetic " + (keyboardSubmit ? "keyboard Enter" : "gamepad South") +
                " is pressed through InputSystemUIInputModule.submit.");

            ui.ShowCommittedRunReportImmediate(summary);
            Canvas.ForceUpdateCanvases();
            reportGroup = reportPanel.GetComponent<CanvasGroup>();
            Require(!ui.IsRunReportInputReady && !reportGroup.interactable && !reportButton.IsInteractable(), check,
                "Held actual Submit action remains blocked on a newly ready report.");
            ExecuteEvents.Execute(reportButton.gameObject, new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);
            Require(clickCount == 1, check,
                "The real report Button rejects submit dispatch while its input layer is closed.");

            if (keyboardSubmit) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            else InputSystem.QueueStateEvent(gamepad, new GamepadState());
            for (int frame = 0; frame < 3 && module.submit.action.IsPressed(); frame++) yield return null;
            Require(!module.submit.action.IsPressed(), check,
                "Synthetic Submit release reached the Input System action.");
            Require(!ui.IsRunReportInputReady && !reportGroup.interactable, check,
                "Submit release frame remains blocked.");
            yield return null;
            Require(ui.IsRunReportInputReady && reportGroup.interactable && reportButton.IsInteractable(), check,
                "Fresh Submit can reach an armed report after release and the later-frame barrier.");
            ExecuteEvents.Execute(reportButton.gameObject, new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler);
            Require(clickCount == 2, check,
                "The real report Button accepts one fresh submit dispatch after readiness.");
        }
        finally
        {
            if (reportButton != null && clicksSwapped) reportButton.onClick = originalClicks;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (inputSettingsOverridden && inputSettings != null)
            {
                InputSystem.runInBackground = originalRunInBackground;
                inputSettings.backgroundBehavior = originalBackgroundInput;
                inputSettings.editorInputBehaviorInPlayMode = originalEditorInput;
            }
        }
    }

    private static void Require(bool condition, Action<bool, string> check, string message)
    {
        check?.Invoke(condition, message);
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Get<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Hidden);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }
}
