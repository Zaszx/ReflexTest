using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A root-driven enemy simulation. It deliberately has no Update: the game flow
/// supplies only active gameplay time, so pauses and transitions cannot advance it.
/// </summary>
public sealed class NeonEnemyController : MonoBehaviour
{
    private const float EntranceSeconds = .14f;
    private const float DestroySeconds = .16f;
    private const float ImpactSeconds = .13f;
    private readonly List<NeonEnemyView> views = new List<NeonEnemyView>();
    private readonly Dictionary<int, NeonEnemyView> viewById = new Dictionary<int, NeonEnemyView>();
    private readonly List<int> removalIds = new List<int>();
    private EnemyState state;
    private EffectiveEnemySettings settings;
    private RectTransform arena, grid;
    private Action damageCallback;
    private Action destroyedCallback;
    private Func<bool> tapGate;
    private bool configured, warnedUnsafe;
    private EnemyGridObb previousGridObb;
    private bool hasPreviousGridObb, hasMotionEnvelope;
    private Vector2 maximumGridHalfExtentsArena, gridTranslationEnvelopeArena;
    private float authoredRotationDegreesPerSecond;

    public EnemyState State => state;
    public bool IsConfigured => configured;

    public void Configure(EnemyState persistedState, EnemyLevelSettings levelSettings, EnemySettings defaultSettings,
        RectTransform gameplayArena, RectTransform gridFootprint, Action onImpactDamage, Func<bool> canAcceptEnemyTap,
        uint deterministicSeed = 1, Action onDestroyed = null, int campaignLevel = int.MaxValue)
    {
        // Rebinding after a save/load must not mutate the supplied snapshot.
        // CancelLevel is reserved for a real level boundary.
        configured = false;
        damageCallback = null;
        tapGate = null;
        ResetVisuals();
        state = persistedState ?? new EnemyState();
        settings = EnemyRules.Resolve(defaultSettings, levelSettings);
        EnemyRules.NormalizeState(state, settings);
        arena = gameplayArena;
        grid = gridFootprint;
        damageCallback = onImpactDamage;
        tapGate = canAcceptEnemyTap;
        destroyedCallback = onDestroyed;
        configured = arena != null && grid != null && EnemyRules.IsEnabledForLevel(settings, campaignLevel);
        warnedUnsafe = false;
        hasPreviousGridObb = false;
        hasMotionEnvelope = false;
        if (!configured) { ResetVisuals(); return; }
        if (!state.initialized)
        {
            state.initialized = true;
            state.randomState = deterministicSeed == 0 ? 1u : deterministicSeed;
            state.spawnRemaining = settings.initialSpawnDelay;
        }
        RestoreViews();
    }

    /// <summary>
    /// Supplies a conservative static envelope for every future grid pose in
    /// arena-local units. maximumGridHalfExtentsArena includes max scale and
    /// rotation; translationEnvelopeArena is the authored centre travel room.
    /// </summary>
    public void SetMotionEnvelope(Vector2 maximumGridHalfExtents, Vector2 translationEnvelope, float rotationDegreesPerSecond = 0f)
    {
        maximumGridHalfExtentsArena = Vector2.Max(Vector2.zero, maximumGridHalfExtents);
        gridTranslationEnvelopeArena = Vector2.Max(Vector2.zero, translationEnvelope);
        authoredRotationDegreesPerSecond = rotationDegreesPerSecond;
        hasMotionEnvelope = true;
    }

    public void Tick(float activeDt)
    {
        if (!configured || state == null || activeDt <= 0f || arena == null || grid == null) return;
        activeDt = Mathf.Min(activeDt, .25f);
        EnemyGridObb gridObb = EnemyRules.CaptureGridObb(grid, arena);
        EnemyGridObb startGridObb = hasPreviousGridObb ? previousGridObb : gridObb;
        Rect arenaRect = arena.rect;
        float shortSide = Mathf.Max(1f, Mathf.Min(arenaRect.width, arenaRect.height));
        float half = settings.sizeNormalized * shortSide * .5f;
        for (int i = 0; i < state.active.Count; i++)
        {
            EnemyInstanceState enemy = state.active[i];
            if (enemy == null) continue;
            enemy.phaseElapsed += activeDt;
            if (enemy.phase != EnemyPhase.Approaching)
            {
                if (enemy.phaseElapsed >= (enemy.phase == EnemyPhase.Destroying ? DestroySeconds : ImpactSeconds)) removalIds.Add(enemy.id);
                UpdateView(enemy, arenaRect, shortSide);
                continue;
            }
            Vector2 from = ToArenaPoint(enemy.normalizedPosition, arenaRect);
            Vector2 desired = gridObb.center - from;
            if (desired.sqrMagnitude > .0001f) enemy.normalizedVelocity = ToNormalizedDirection(desired.normalized, arenaRect, shortSide);
            Vector2 to = from + ToArenaDirection(enemy.normalizedVelocity, arenaRect, shortSide) * (settings.movementSpeedNormalized * shortSide * activeDt);
            if (EnemyRules.SweepSquareAgainstMovingObb(from, to, half, startGridObb, gridObb,
                settings.collisionSubstepNormalized * shortSide, authoredRotationDegreesPerSecond * activeDt))
            {
                enemy.phase = EnemyPhase.Impacting;
                enemy.phaseElapsed = 0f;
                // Commit state and disable raycast/collision before damage. A
                // terminal callback therefore cannot leave a live old enemy.
                UpdateView(enemy, arenaRect, shortSide);
                damageCallback?.Invoke();
                if (!configured || state == null) { hasPreviousGridObb = false; return; }
            }
            else
            {
                enemy.normalizedPosition = ToNormalizedPoint(to, arenaRect);
                UpdateView(enemy, arenaRect, shortSide);
            }
        }
        RemoveRetired();
        if (!configured || state == null) { hasPreviousGridObb = false; return; }
        AdvanceSpawning(activeDt, gridObb, arenaRect);
        previousGridObb = gridObb;
        hasPreviousGridObb = true;
    }

    /// <summary>Root may call this before grid resolution when it centrally owns pointer routing.</summary>
    public bool TryConsumePointer(PointerEventData eventData)
    {
        if (!configured || eventData == null) return false;
        for (int i = state.active.Count - 1; i >= 0; i--)
        {
            EnemyInstanceState enemy = state.active[i];
            if (enemy == null) continue;
            NeonEnemyView view;
            if (viewById.TryGetValue(enemy.id, out view) && view != null &&
                view.ContainsPointer(eventData))
            {
                if (enemy.phase != EnemyPhase.Approaching || (CanAcceptTap() && TryDestroy(enemy.id))) { eventData.Use(); return true; }
            }
        }
        return false;
    }

    internal bool TryConsumeViewPointer(int enemyId)
    {
        if (!configured || state == null) return false;
        for (int i = 0; i < state.active.Count; i++)
        {
            EnemyInstanceState enemy = state.active[i];
            if (enemy == null || enemy.id != enemyId) continue;
            // A retiring visual remains an inert pointer shield for its short
            // fade, so an impact cannot become a same-frame grid tap.
            return enemy.phase != EnemyPhase.Approaching || (CanAcceptTap() && TryDestroy(enemyId));
        }
        return false;
    }

    /// <summary>Central input dispatch can call this only after its own gameplay gate has accepted the pointer.</summary>
    public bool TryDestroyEnemy(int enemyId)
    {
        return CanAcceptTap() && TryDestroy(enemyId);
    }

    public void CancelLevel()
    {
        configured = false;
        damageCallback = null;
        destroyedCallback = null;
        tapGate = null;
        hasPreviousGridObb = false;
        hasMotionEnvelope = false;
        authoredRotationDegreesPerSecond = 0f;
        if (state != null && state.active != null) state.active.Clear();
        ResetVisuals();
    }

    public void ResetVisuals()
    {
        for (int i = 0; i < views.Count; i++) if (views[i] != null) views[i].gameObject.SetActive(false);
        viewById.Clear();
    }

    private void RestoreViews()
    {
        ResetVisuals();
        Rect arenaRect = arena.rect;
        float shortSide = Mathf.Max(1f, Mathf.Min(arenaRect.width, arenaRect.height));
        for (int i = 0; i < state.active.Count; i++) if (state.active[i] != null) UpdateView(state.active[i], arenaRect, shortSide);
    }

    private void AdvanceSpawning(float activeDt, EnemyGridObb gridObb, Rect arenaRect)
    {
        state.spawnRemaining -= activeDt;
        if (state.spawnRemaining > 0f || state.active.Count >= settings.maximumConcurrent) return;
        DeterministicRandom random = new DeterministicRandom(state.randomState);
        Vector2 position, direction;
        bool spawned = hasMotionEnvelope
            ? EnemyRules.TryChooseSpawnAgainstEnvelope(settings, arenaRect, maximumGridHalfExtentsArena + gridTranslationEnvelopeArena,
                ref random, out position, out direction)
            : EnemyRules.TryChooseSpawnInArena(settings, gridObb, arenaRect, ref random, out position, out direction);
        if (spawned)
        {
            state.active.Add(new EnemyInstanceState { id = state.nextEnemyId++, normalizedPosition = position, normalizedVelocity = direction, phase = EnemyPhase.Approaching });
        }
        else if (!warnedUnsafe)
        {
            warnedUnsafe = true;
            Debug.LogWarning("Enemy spawn skipped: arena and moving grid leave no safe two-second approach corridor.", this);
        }
        state.randomState = random.State;
        state.spawnRemaining = Mathf.Lerp(settings.spawnIntervalMinimum, settings.spawnIntervalMaximum, random.NextFloat01());
        state.randomState = random.State;
    }

    private bool CanAcceptTap() { return configured && tapGate != null && tapGate(); }

    private bool TryDestroy(int enemyId)
    {
        if (!configured || state == null) return false;
        for (int i = 0; i < state.active.Count; i++)
        {
            EnemyInstanceState enemy = state.active[i];
            if (enemy == null || enemy.id != enemyId || enemy.phase != EnemyPhase.Approaching) continue;
            enemy.phase = EnemyPhase.Destroying;
            enemy.phaseElapsed = 0f;
            UpdateView(enemy, arena.rect, Mathf.Max(1f, Mathf.Min(arena.rect.width, arena.rect.height)));
            destroyedCallback?.Invoke();
            return true;
        }
        return false;
    }

    private void RemoveRetired()
    {
        for (int removeIndex = 0; removeIndex < removalIds.Count; removeIndex++)
        {
            int id = removalIds[removeIndex];
            for (int i = state.active.Count - 1; i >= 0; i--) if (state.active[i] != null && state.active[i].id == id) state.active.RemoveAt(i);
            NeonEnemyView view;
            if (viewById.TryGetValue(id, out view) && view != null) view.gameObject.SetActive(false);
            viewById.Remove(id);
        }
        removalIds.Clear();
    }

    private void UpdateView(EnemyInstanceState enemy, Rect arenaRect, float shortSide)
    {
        NeonEnemyView view = GetView(enemy.id);
        RectTransform rect = (RectTransform)view.transform;
        rect.SetParent(arena, false);
        float size = settings.sizeNormalized * shortSide;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = ToArenaPoint(enemy.normalizedPosition, arenaRect);
        float scale = 1f, alpha = 1f;
        if (enemy.phase == EnemyPhase.Approaching) scale = Mathf.Lerp(.35f, 1f, Mathf.Clamp01(enemy.phaseElapsed / EntranceSeconds));
        else if (enemy.phase == EnemyPhase.Destroying) { float t = Mathf.Clamp01(enemy.phaseElapsed / DestroySeconds); scale = 1f + t * .45f; alpha = 1f - t; }
        else { float t = Mathf.Clamp01(enemy.phaseElapsed / ImpactSeconds); scale = 1f + t * .25f; alpha = 1f - t; }
        rect.localScale = Vector3.one * scale;
        view.SetColor(enemy.phase == EnemyPhase.Impacting ? new Color(1f, .5f, .5f, alpha) : new Color(1f, .08f, .18f, alpha));
        view.SetTapTargetScale(settings.tapTargetScale);
        view.gameObject.SetActive(true);
    }

    private NeonEnemyView GetView(int id)
    {
        NeonEnemyView existing;
        if (viewById.TryGetValue(id, out existing) && existing != null) return existing;
        NeonEnemyView view = null;
        for (int i = 0; i < views.Count; i++) if (views[i] != null && !views[i].gameObject.activeSelf) { view = views[i]; break; }
        if (view == null)
        {
            GameObject go = new GameObject("NeonEnemy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(NeonEnemyView));
            view = go.GetComponent<NeonEnemyView>();
            views.Add(view);
        }
        view.Bind(this, id);
        viewById[id] = view;
        return view;
    }

    private static Vector2 ToArenaPoint(Vector2 normalized, Rect rect) => new Vector2(rect.xMin + normalized.x * rect.width, rect.yMin + normalized.y * rect.height);
    private static Vector2 ToNormalizedPoint(Vector2 point, Rect rect) => new Vector2((point.x - rect.xMin) / Mathf.Max(1f, rect.width), (point.y - rect.yMin) / Mathf.Max(1f, rect.height));
    private static Vector2 ToArenaDirection(Vector2 normalizedDirection, Rect rect, float shortSide) => new Vector2(normalizedDirection.x * rect.width / shortSide, normalizedDirection.y * rect.height / shortSide).normalized;
    private static Vector2 ToNormalizedDirection(Vector2 localDirection, Rect rect, float shortSide) => new Vector2(localDirection.x * shortSide / rect.width, localDirection.y * shortSide / rect.height).normalized;
}
