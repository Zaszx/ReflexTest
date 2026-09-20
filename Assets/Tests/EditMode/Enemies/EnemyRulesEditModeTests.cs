using NUnit.Framework;
using UnityEngine;

public sealed class EnemyRulesEditModeTests
{
    [Test]
    public void RotatedBoardCornerDoesNotUseFalseAabbImpact()
    {
        EnemyGridObb board = new EnemyGridObb
        {
            center = Vector2.zero,
            axisX = new Vector2(.70710678f, .70710678f),
            axisY = new Vector2(-.70710678f, .70710678f),
            halfExtents = new Vector2(.5f, .5f)
        };
        // Inside the rotated board's broad-phase AABB, outside its actual corner.
        Assert.That(EnemyRules.SquareIntersectsObb(new Vector2(.64f, .64f), .02f, board), Is.False);
        Assert.That(EnemyRules.SquareIntersectsObb(Vector2.zero, .02f, board), Is.True);
    }

    [Test]
    public void SweptSquareDetectsAHighSpeedBoardImpact()
    {
        EnemyGridObb board = new EnemyGridObb { center = Vector2.zero, axisX = Vector2.right, axisY = Vector2.up, halfExtents = new Vector2(.2f, .2f) };
        Assert.That(EnemyRules.SweepSquareAgainstObb(new Vector2(-1f, 0f), new Vector2(1f, 0f), .03f, board, .01f), Is.True);
    }

    [Test]
    public void FastFullBoardRotationIsSweptEvenWhenStartAndEndAxesMatch()
    {
        EnemyGridObb board = new EnemyGridObb { center = Vector2.zero, axisX = Vector2.right, axisY = Vector2.up, halfExtents = new Vector2(.8f, .04f) };
        Assert.That(EnemyRules.SweepSquareAgainstMovingObb(new Vector2(0f, .6f), new Vector2(0f, .6f), .03f, board, board, .01f, 360f), Is.True);
    }

    [Test]
    public void SpawnRandomStreamRestoresExactly()
    {
        DeterministicRandom first = new DeterministicRandom(917);
        DeterministicRandom restored = new DeterministicRandom(first.State);
        for (int i = 0; i < 12; i++) Assert.That(restored.NextUInt(), Is.EqualTo(first.NextUInt()));
    }

    [Test]
    public void NormalizeStateRepairsInvalidSaveFieldsWithoutCreatingThreats()
    {
        EnemyState state = new EnemyState { randomState = 0, spawnRemaining = float.NaN, nextEnemyId = 0 };
        state.active.Add(new EnemyInstanceState { id = 4, normalizedPosition = new Vector2(2f, -.5f), normalizedVelocity = Vector2.zero });
        state.active.Add(new EnemyInstanceState { id = 4, normalizedPosition = Vector2.one, normalizedVelocity = Vector2.right });
        EffectiveEnemySettings settings = EnemyRules.Resolve(null, new EnemyLevelSettings { enabled = false });
        EnemyRules.NormalizeState(state, settings);
        Assert.That(state.randomState, Is.EqualTo(1u));
        Assert.That(state.active.Count, Is.EqualTo(1));
        Assert.That(state.active[0].normalizedPosition, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(state.nextEnemyId, Is.GreaterThan(4));
    }

    [Test]
    public void SaveRestoreRetainsMultipleThreatsAndEnemySpawnStream()
    {
        EnemyState state = new EnemyState { initialized = true, spawnRemaining = 2.75f, nextEnemyId = 9, randomState = EnemyRules.CreateStreamSeed(77, 24) };
        state.active.Add(new EnemyInstanceState { id = 7, normalizedPosition = new Vector2(.08f, .71f), normalizedVelocity = new Vector2(.7f, -.7f), phase = EnemyPhase.Approaching });
        state.active.Add(new EnemyInstanceState { id = 8, normalizedPosition = new Vector2(.92f, .31f), normalizedVelocity = new Vector2(-.6f, .8f), phase = EnemyPhase.Approaching });
        DeterministicRandom beforeSave = new DeterministicRandom(state.randomState);
        beforeSave.NextUInt();
        state.randomState = beforeSave.State;

        EnemyState restored = JsonUtility.FromJson<EnemyState>(JsonUtility.ToJson(state));
        EffectiveEnemySettings settings = EnemyRules.Resolve(null, new EnemyLevelSettings { enabled = true });
        EnemyRules.NormalizeState(restored, settings);
        DeterministicRandom expectedNext = new DeterministicRandom(state.randomState);
        DeterministicRandom restoredNext = new DeterministicRandom(restored.randomState);
        Assert.That(restored.active.Count, Is.EqualTo(2));
        Assert.That(restored.active[0].id, Is.EqualTo(7));
        Assert.That(restored.active[1].id, Is.EqualTo(8));
        Assert.That(restored.spawnRemaining, Is.EqualTo(2.75f));
        Assert.That(restoredNext.NextUInt(), Is.EqualTo(expectedNext.NextUInt()));
    }

    [Test]
    public void DestroyedAndImpactingEnemiesCannotDealDamageTwice()
    {
        GameObject arenaObject = new GameObject("Arena", typeof(RectTransform));
        GameObject gridObject = new GameObject("Grid", typeof(RectTransform));
        GameObject controllerObject = new GameObject("Enemies", typeof(NeonEnemyController));
        EnemySettings defaults = ScriptableObject.CreateInstance<EnemySettings>();
        try
        {
            RectTransform arena = (RectTransform)arenaObject.transform;
            arena.sizeDelta = new Vector2(100f, 100f);
            RectTransform grid = (RectTransform)gridObject.transform;
            grid.SetParent(arena, false); grid.sizeDelta = new Vector2(40f, 40f);
            EnemyState state = new EnemyState { initialized = true, spawnRemaining = 999f };
            state.active.Add(new EnemyInstanceState { id = 1, normalizedPosition = new Vector2(.3f, .5f), normalizedVelocity = Vector2.right, phase = EnemyPhase.Approaching });
            int damage = 0;
            NeonEnemyController controller = controllerObject.GetComponent<NeonEnemyController>();
            controller.Configure(state, new EnemyLevelSettings { enabled = true }, defaults, arena, grid, () => damage++, () => true);
            controller.Tick(.1f);
            controller.Tick(.1f);
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(state.active[0].phase, Is.EqualTo(EnemyPhase.Impacting));
            Assert.That(controller.TryDestroyEnemy(1), Is.False);

            state.active.Clear();
            state.active.Add(new EnemyInstanceState { id = 2, normalizedPosition = new Vector2(.3f, .5f), normalizedVelocity = Vector2.right, phase = EnemyPhase.Approaching });
            Assert.That(controller.TryDestroyEnemy(2), Is.True);
            controller.Tick(.1f);
            Assert.That(damage, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(defaults);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(gridObject);
            Object.DestroyImmediate(arenaObject);
        }
    }

    [Test]
    public void UnsafeGridFitIsRejectedWithoutMakingTheGridTiny()
    {
        EffectiveEnemySettings settings = EnemyRules.Resolve(null, new EnemyLevelSettings { enabled = true, overrideDefaults = true, movementSpeedNormalized = .08f, sizeNormalized = .055f, minimumSpawnClearanceNormalized = .06f, requiredTravelSeconds = 2f });
        Vector2 fit;
        Assert.That(EnemyRules.TryFitGridHalfExtents(new Vector2(.42f, .42f), new Vector2(.3f, .3f), settings, .2f, out fit), Is.False);
    }
}
