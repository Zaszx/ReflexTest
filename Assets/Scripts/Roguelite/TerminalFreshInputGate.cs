using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>Pure release/later-frame latch used by the terminal UI input gate.</summary>
public sealed class FreshInputReleaseGate
{
    private int releasedFrame = -1;

    public bool Active { get; private set; }
    public bool PresentationReady { get; private set; }
    public bool Ready { get; private set; }
    public bool AwaitingRelease => Active && !Ready && releasedFrame < 0;

    public void Begin(int frame, bool inputHeld)
    {
        Active = true;
        PresentationReady = false;
        Ready = false;
        releasedFrame = inputHeld ? -1 : frame;
    }

    public void MarkPresentationReady()
    {
        if (Active) PresentationReady = true;
    }

    public void Tick(int frame, bool inputHeld)
    {
        if (!Active || Ready) return;
        if (inputHeld)
        {
            releasedFrame = -1;
            return;
        }

        if (releasedFrame < 0)
        {
            releasedFrame = frame;
            return;
        }

        // Never enable report controls in the frame that accepted terminal input
        // or first observed its release. The following press is therefore fresh.
        if (PresentationReady && frame > releasedFrame) Ready = true;
    }

    public void Cancel()
    {
        Active = false;
        PresentationReady = false;
        Ready = false;
        releasedFrame = -1;
    }
}

/// <summary>
/// Polls every UI pointer and the EventSystem's actual submit action without
/// allocating. Once armed, ordinary held input no longer revokes readiness.
/// </summary>
public sealed class TerminalFreshInputGate : MonoBehaviour
{
    private readonly FreshInputReleaseGate gate = new FreshInputReleaseGate();
    private bool paused;

    public event Action BecameReady;

    public bool Active => gate.Active;
    public bool PresentationReady => gate.PresentationReady;
    public bool Ready => gate.Ready;
    public bool AwaitingRelease => gate.AwaitingRelease;

    public void BeginGate()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        gate.Begin(Time.frameCount, IsAnyTerminalInputHeld());
        paused = false;
        enabled = true;
    }

    public void MarkPresentationReady()
    {
        gate.MarkPresentationReady();
        Poll();
    }

    public void SetPaused(bool value) => paused = value;

    public void CancelGate()
    {
        gate.Cancel();
        paused = false;
        enabled = false;
    }

    private void Update()
    {
        Poll();
    }

    private void Poll()
    {
        if (paused || NeonMotion.ApplicationSuspended) return;
        bool wasReady = gate.Ready;
        gate.Tick(Time.frameCount, IsAnyTerminalInputHeld());
        if (!wasReady && gate.Ready)
        {
            enabled = false;
            BecameReady?.Invoke();
        }
    }

    private static bool IsAnyTerminalInputHeld()
    {
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed || mouse.forwardButton.isPressed || mouse.backButton.isPressed))
            return true;

        var pen = Pen.current;
        if (pen != null && (pen.tip.isPressed || pen.eraser.isPressed)) return true;

        var pointer = Pointer.current;
        if (pointer != null && pointer.press.isPressed) return true;

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].press.isPressed) return true;
        }

        var eventSystem = EventSystem.current;
        var inputModule = eventSystem == null ? null : eventSystem.currentInputModule as InputSystemUIInputModule;
        var submit = inputModule == null ? null : inputModule.submit;
        return submit != null && submit.action != null && submit.action.IsPressed();
    }
}
