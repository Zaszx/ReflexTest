using NUnit.Framework;

public sealed class TerminalFreshInputGateEditModeTests
{
    [Test]
    public void HeldTerminalInputRequiresReleaseAndLaterFrame()
    {
        var gate = new FreshInputReleaseGate();
        gate.Begin(10, true);
        gate.MarkPresentationReady();

        gate.Tick(11, true);
        Assert.That(gate.Ready, Is.False);
        Assert.That(gate.AwaitingRelease, Is.True);

        gate.Tick(12, false);
        Assert.That(gate.Ready, Is.False);
        gate.Tick(13, false);
        Assert.That(gate.Ready, Is.True);

        gate.Tick(14, true);
        Assert.That(gate.Ready, Is.True, "Once armed, normal held input stays enabled.");
    }

    [Test]
    public void ZeroDurationPresentationCannotArmInStartingFrame()
    {
        var gate = new FreshInputReleaseGate();
        gate.Begin(20, false);
        gate.MarkPresentationReady();

        gate.Tick(20, false);
        Assert.That(gate.Ready, Is.False);
        gate.Tick(21, false);
        Assert.That(gate.Ready, Is.True);
    }

    [Test]
    public void PresentationMustBeReadyBeforeReleasedInputCanArm()
    {
        var gate = new FreshInputReleaseGate();
        gate.Begin(30, false);
        gate.Tick(31, false);
        Assert.That(gate.Ready, Is.False);

        gate.MarkPresentationReady();
        gate.Tick(31, false);
        Assert.That(gate.Ready, Is.True);
    }

    [Test]
    public void CancelClearsEveryGateState()
    {
        var gate = new FreshInputReleaseGate();
        gate.Begin(40, true);
        gate.MarkPresentationReady();
        gate.Cancel();

        Assert.That(gate.Active, Is.False);
        Assert.That(gate.PresentationReady, Is.False);
        Assert.That(gate.Ready, Is.False);
        Assert.That(gate.AwaitingRelease, Is.False);
    }
}
