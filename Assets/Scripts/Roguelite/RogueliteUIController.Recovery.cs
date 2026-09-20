using UnityEngine;

public sealed partial class RogueliteUIController
{
    /// <summary>
    /// Call immediately after RefreshHud. The timer digits remain the selected normal/reserve value;
    /// this only identifies the temporary timer hold without changing resource state.
    /// </summary>
    public void SetReboundFeedback(bool active)
    {
        if (timerLabel == null) return;
        if (active)
        {
            timerLabel.text = "REBOUND · HOLD";
            timerLabel.color = PlayCyan;
            timerLabel.fontSize = 18;
            timerLabel.characterSpacing = 0;
        }
    }

    /// <summary>Call after the health value has been refreshed following heart collection.</summary>
    public void PlayHealthHealingFeedback(int amount = 1) => hudMotion?.PlayHealthHealing(amount);
}
