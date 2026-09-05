using UnityEngine;

public enum GameplayTimerTransition { None, EnteredReserve, ReserveDepleted }
public static class GameplayTimerRules
{
    public static GameplayTimerTransition Tick(ActiveRunData run, float deltaTime)
    {
        if (run == null || run.levelState == null || deltaTime <= 0f || float.IsNaN(deltaTime)) return GameplayTimerTransition.None;
        ActiveLevelStateData state = run.levelState;
        state.normalTimeRemaining = Mathf.Max(0f, state.normalTimeRemaining);
        run.currentReserveSeconds = Mathf.Max(0f, run.currentReserveSeconds);
        if (state.reserveActive)
        {
            run.currentReserveSeconds = Mathf.Max(0f, run.currentReserveSeconds - deltaTime);
            return run.currentReserveSeconds <= 0f ? GameplayTimerTransition.ReserveDepleted : GameplayTimerTransition.None;
        }
        if (deltaTime < state.normalTimeRemaining) { state.normalTimeRemaining -= deltaTime; return GameplayTimerTransition.None; }
        float overshoot = deltaTime - state.normalTimeRemaining;
        state.normalTimeRemaining = 0f; state.reserveActive = true;
        run.currentReserveSeconds = Mathf.Max(0f, run.currentReserveSeconds - overshoot);
        return run.currentReserveSeconds <= 0f ? GameplayTimerTransition.ReserveDepleted : GameplayTimerTransition.EnteredReserve;
    }
}
