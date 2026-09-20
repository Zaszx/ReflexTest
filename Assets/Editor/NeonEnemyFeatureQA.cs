#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Bounded live-scene checks for the Level 25 enemy feature. Caller supplies isolated storage/capture.</summary>
public static class NeonEnemyFeatureQA
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run(GameManager gm, Func<string, IEnumerator> capture, Action<bool, string> check)
    {
        if (gm == null) throw new ArgumentNullException(nameof(gm));
        const int Level25Index = 24;
        try
        {
            gm.enabled = true;
            gm.ReturnToMainMenu();
            gm.StartDebugLevel(Level25Index);
            yield return WaitFor(gm, "Playing", check);

            ActiveRunData run = Field<ActiveRunData>(gm, "sessionRun");
            LevelData level = Field<LevelData>(gm, "activeLevel");
            NeonEnemyController enemies = Field<NeonEnemyController>(gm, "enemyController");
            check(level != null && level.levelNumber == 25 && level.enemies != null && level.enemies.enabled,
                "The actual selected Level 25 configuration enables enemies.");
            check(enemies != null && enemies.IsConfigured, "Level 25 binds an active enemy controller.");
            if (enemies == null || !enemies.IsConfigured) yield break;

            // Disable the component so this is exactly the requested manual
            // active-time simulation, free from a real-time Update race. Use
            // the full owner substep so grid motion and enemy corridor checks
            // share the same actual integration path.
            gm.enabled = false;
            for (int i = 0; i < 31; i++) Invoke(gm, "TickActiveGameplay", .1f);
            EnemyState enemyState = enemies.State;
            check(enemyState.active.Count > 0, "A safe Level 25 enemy actually spawns after the three-second initial delay.");
            if (enemyState.active.Count == 0) yield break;

            EnemyInstanceState first = enemyState.active[0];
            check(IsOutsideCurrentGrid(first, enemies, gm), "The observed enemy spawn is outside the current transformed grid footprint.");
            yield return Capture(capture, "enemy-corridor");

            int objective = run.levelState.objectiveProgress;
            long coins = run.pendingCoins;
            NeonEnemyView view = FindView(gm, first.id);
            check(view != null && view.gameObject.activeInHierarchy, "Spawned enemy owns a visible pooled UI view.");
            if (view != null)
            {
                Canvas.ForceUpdateCanvases();
                PointerEventData pointer = new PointerEventData(EventSystem.current)
                {
                    pointerId = 8125,
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, view.transform.TransformPoint(new Vector3(((RectTransform)view.transform).rect.width * .65f, 0, 0)))
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<NeonEnemyView>() == view,
                    "Enemy touch area extends beyond its small visual for phone input.");
                if (hits.Count > 0) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                check(pointer.used && first.phase == EnemyPhase.Destroying && run.levelState.objectiveProgress == objective && run.pendingCoins == coins,
                    "A manual enemy pointer-down destroys it, consumes the pointer, and does not leak into objective or coins.");
            }

            // Retire the tapped visual before the collision checks, while
            // retaining the same real level/controller setup.
            enemies.Tick(.2f);
            string frozen = JsonUtility.ToJson(enemyState);
            gm.enabled = true;
            gm.OpenSettings();
            yield return WaitFor(gm, "Settings", check);
            yield return new WaitForSecondsRealtime(.16f);
            check(JsonUtility.ToJson(enemyState) == frozen,
                "Settings popup pauses enemy spawn and motion clocks without a catch-up mutation.");
            gm.CloseSettings();
            yield return WaitFor(gm, "Playing", check);

            gm.enabled = false;
            run.currentHealth = Mathf.Max(2, run.upgrades.maxHealth);
            run.upgrades.reboundOwned = true;
            ReboundRules.Cancel(run.levelState.rebound);
            int healthBeforeImpact = run.currentHealth;
            AddOverlappingEnemy(enemies, 901);
            enemies.Tick(.02f);
            int healthAfterImpact = run.currentHealth;
            enemies.Tick(.1f);
            check(healthAfterImpact == healthBeforeImpact - 1 && run.currentHealth == healthAfterImpact && !run.levelState.rebound.active,
                "A nonfatal enemy impact applies one damage and never activates Rebound.");

            run.currentHealth = 1;
            AddOverlappingEnemy(enemies, 902);
            enemies.Tick(.02f);
            check(Flow(gm) == "RunEnding" || Flow(gm) == "RunSummary",
                "A fatal enemy impact enters the terminal flow atomically.");
            check(!enemies.IsConfigured || enemies.State.active.Count == 0,
                "The terminal transaction cancels active enemy collision/input state exactly once.");

            yield return CombinedLevel70(gm, check);
        }
        finally
        {
            gm.enabled = true;
            gm.ReturnToMainMenu();
        }
    }

    private static void AddOverlappingEnemy(NeonEnemyController controller, int id)
    {
        EnemyState state = controller.State;
        state.active.Clear();
        state.active.Add(new EnemyInstanceState
        {
            id = id,
            normalizedPosition = new Vector2(.5f, .5f),
            normalizedVelocity = Vector2.right,
            phase = EnemyPhase.Approaching
        });
        state.nextEnemyId = Mathf.Max(state.nextEnemyId, id + 1);
    }

    private static IEnumerator CombinedLevel70(GameManager gm, Action<bool, string> check)
    {
        const int Level70Index = 69;
        gm.enabled = true;
        gm.ReturnToMainMenu();
        gm.StartDebugLevel(Level70Index);
        yield return WaitFor(gm, "Playing", check);
        ActiveRunData run = Field<ActiveRunData>(gm, "sessionRun");
        LevelData level = Field<LevelData>(gm, "activeLevel");
        NeonEnemyController enemies = Field<NeonEnemyController>(gm, "enemyController");
        check(level.levelNumber == 70 && level.outlineCount == 5 && enemies != null && enemies.IsConfigured,
            "The authored Level 70 combined challenge has five outlines and enabled enemies.");
        if (enemies == null || !enemies.IsConfigured) yield break;

        ActiveLevelStateData state = run.levelState;
        run.upgrades.reboundOwned = true;
        run.upgrades.healingTier = Mathf.Max(1, run.upgrades.healingTier);
        run.currentHealth = Mathf.Max(1, run.upgrades.maxHealth - 1);
        state.rebound.active = true;
        state.rebound.activeRemainingSeconds = 1f;
        state.rebound.motionRecoveryRemainingSeconds = 0f;
        state.reserveActive = true;
        state.normalTimeRemaining = 0f;
        run.currentReserveSeconds = 5f;
        int heartCell = FindUnoccupiedCell(state, Field<List<GameSquare>>(gm, "instantiatedSquares").Count);
        state.heart = new HeartStateData { cellIndex = heartCell, phase = HeartPhase.Available, phaseRemainingSeconds = 2f, visibleRemainingSeconds = 2f };
        Invoke(gm, "RenderHeart");
        enemies.State.active.Clear();
        enemies.State.active.Add(new EnemyInstanceState { id = 1701, normalizedPosition = new Vector2(.04f, .04f), normalizedVelocity = Vector2.one.normalized, phase = EnemyPhase.Approaching });
        Vector2 enemyBefore = enemies.State.active[0].normalizedPosition;
        float heartBefore = state.heart.phaseRemainingSeconds;
        gm.enabled = false;
        Invoke(gm, "TickActiveGameplay", .2f);
        check(enemies.State.active.Count > 0 && enemies.State.active[0].normalizedPosition != enemyBefore &&
            state.heart.phaseRemainingSeconds < heartBefore && Mathf.Approximately(run.currentReserveSeconds, 5f),
            "During Rebound the enemy and heart clocks advance while reserve drain remains held.");

        float reboundBeforeHeart = state.rebound.activeRemainingSeconds;
        List<GameSquare> squares = Field<List<GameSquare>>(gm, "instantiatedSquares");
        squares[heartCell].OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 8170 });
        check(state.rebound.active && Mathf.Approximately(state.rebound.activeRemainingSeconds, reboundBeforeHeart),
            "Heart collection does not end or extend Rebound.");

        run.currentHealth = Mathf.Max(2, run.upgrades.maxHealth);
        float reboundBeforeImpact = state.rebound.activeRemainingSeconds;
        AddOverlappingEnemy(enemies, 1702);
        enemies.Tick(.01f);
        check(state.rebound.active && state.rebound.activeRemainingSeconds <= reboundBeforeImpact,
            "A nonfatal enemy impact during Rebound does not extend its recovery window.");

        // Recreate both transient resources, then complete with the actual
        // current largest target to exercise the level-boundary cancellation.
        state.heart = new HeartStateData { cellIndex = FindUnoccupiedCell(state, squares.Count), phase = HeartPhase.Available, phaseRemainingSeconds = 2f, visibleRemainingSeconds = 2f };
        enemies.State.active.Clear();
        enemies.State.active.Add(new EnemyInstanceState { id = 1703, normalizedPosition = new Vector2(.04f, .04f), normalizedVelocity = Vector2.one.normalized, phase = EnemyPhase.Approaching });
        state.objectiveProgress = level.requiredCorrectClicks - 1;
        state.reverseActive = false;
        int largest = state.targetIndices[state.targetIndices.Length - 1];
        gm.enabled = true;
        squares[largest].OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 8171 });
        yield return null;
        check(Flow(gm) == "LevelComplete" || Flow(gm) == "LevelTransition" || Flow(gm) == "RunSummary",
            "The valid final largest-outline tap enters the level completion flow.");
        check(enemies.State.active.Count == 0 && !state.heart.IsReserved,
            "Level completion clears prior enemies and heart state before the next level can begin.");
    }

    private static int FindUnoccupiedCell(ActiveLevelStateData state, int count)
    {
        HashSet<int> occupied = new HashSet<int>(state.targetIndices ?? Array.Empty<int>());
        for (int i = 0; i < count; i++) if (!occupied.Contains(i)) return i;
        throw new InvalidOperationException("Combined QA needs one unoccupied heart cell.");
    }

    private static bool IsOutsideCurrentGrid(EnemyInstanceState enemy, NeonEnemyController controller, GameManager gm)
    {
        RectTransform arena = Field<RectTransform>(gm, "boundsRoot");
        RectTransform grid = Field<RectTransform>(gm, "rotationScaleRoot");
        if (arena == null || grid == null || enemy == null) return false;
        Rect rect = arena.rect;
        float shortSide = Mathf.Max(1f, Mathf.Min(rect.width, rect.height));
        Vector2 point = new Vector2(rect.xMin + enemy.normalizedPosition.x * rect.width, rect.yMin + enemy.normalizedPosition.y * rect.height);
        EnemyGridObb obb = EnemyRules.CaptureGridObb(grid, arena);
        EffectiveEnemySettings settings = Field<EffectiveEnemySettings>(controller, "settings");
        float half = settings.sizeNormalized * shortSide * .5f;
        return !EnemyRules.SquareIntersectsObb(point, half, obb);
    }

    private static NeonEnemyView FindView(GameManager gm, int id)
    {
        RectTransform arena = Field<RectTransform>(gm, "boundsRoot");
        if (arena == null) return null;
        NeonEnemyView[] views = arena.GetComponentsInChildren<NeonEnemyView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            int viewId = Field<int>(views[i], "enemyId");
            if (viewId == id) return views[i];
        }
        return null;
    }

    private static IEnumerator Capture(Func<string, IEnumerator> capture, string name)
    {
        if (capture == null) yield break;
        IEnumerator routine = capture(name);
        if (routine != null) yield return routine;
    }

    private static IEnumerator WaitFor(GameManager gm, string target, Action<bool, string> check)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Flow(gm) == target) yield break;
            yield return null;
        }
        check(false, "Enemy QA timed out waiting for " + target + ".");
        throw new InvalidOperationException("Enemy QA timed out waiting for " + target + ".");
    }

    private static string Flow(GameManager gm) => Field<object>(gm, "state").ToString();
    private static object Invoke(object owner, string name, params object[] arguments)
    {
        MethodInfo method = owner.GetType().GetMethod(name, Hidden);
        if (method == null) throw new MissingMethodException(owner.GetType().Name, name);
        return method.Invoke(owner, arguments);
    }
    private static T Field<T>(object owner, string name)
    {
        FieldInfo field = owner.GetType().GetField(name, Hidden);
        if (field == null) throw new MissingFieldException(owner.GetType().Name, name);
        return (T)field.GetValue(owner);
    }
}
#endif
