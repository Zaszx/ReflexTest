using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemyState
{
    public bool initialized;
    public float spawnRemaining;
    public int nextEnemyId = 1;
    public uint randomState = 1;
    public List<EnemyInstanceState> active = new List<EnemyInstanceState>();
}

public enum EnemyPhase { Approaching, Destroying, Impacting }

[Serializable]
public sealed class EnemyInstanceState
{
    public int id;
    [Tooltip("Arena unit coordinates: (0,0) is bottom-left and (1,1) is top-right.")]
    public Vector2 normalizedPosition;
    [Tooltip("Normalized-arena-units per second.")]
    public Vector2 normalizedVelocity;
    public EnemyPhase phase;
    public float phaseElapsed;
}

/// <summary>Grid's actual world shape expressed in arena-local coordinates.</summary>
public struct EnemyGridObb
{
    public Vector2 center;
    public Vector2 axisX, axisY;
    public Vector2 halfExtents;
}

public static class EnemyRules
{
    private const float Epsilon = .00001f;

    public static EffectiveEnemySettings Resolve(EnemySettings defaults, EnemyLevelSettings level)
    {
        EnemySettings d = defaults;
        EffectiveEnemySettings result = new EffectiveEnemySettings
        {
            enabled = level != null && level.enabled,
            minimumCampaignLevel = d == null ? 25 : d.minimumCampaignLevel,
            initialSpawnDelay = d == null ? 3f : d.initialSpawnDelay,
            spawnIntervalMinimum = d == null ? 6f : d.spawnIntervalMinimum,
            spawnIntervalMaximum = d == null ? 9f : d.spawnIntervalMaximum,
            movementSpeedNormalized = d == null ? .08f : d.movementSpeedNormalized,
            sizeNormalized = d == null ? .055f : d.sizeNormalized,
            tapTargetScale = d == null ? 1.7f : Mathf.Clamp(d.tapTargetScale, 1f, 2.5f),
            maximumConcurrent = d == null ? 2 : d.maximumConcurrent,
            spawnEdges = d == null ? EnemySpawnEdges.All : d.spawnEdges,
            minimumSpawnClearanceNormalized = d == null ? .06f : d.minimumSpawnClearanceNormalized,
            requiredTravelSeconds = d == null ? 2f : d.requiredTravelSeconds,
            collisionSubstepNormalized = d == null ? .012f : d.collisionSubstepNormalized
        };
        if (level != null && level.overrideDefaults)
        {
            result.minimumCampaignLevel = level.minimumCampaignLevel;
            result.initialSpawnDelay = level.initialSpawnDelay;
            result.spawnIntervalMinimum = level.spawnIntervalMinimum;
            result.spawnIntervalMaximum = level.spawnIntervalMaximum;
            result.movementSpeedNormalized = level.movementSpeedNormalized;
            result.sizeNormalized = level.sizeNormalized;
            result.maximumConcurrent = level.maximumConcurrent;
            result.spawnEdges = level.spawnEdges;
            result.minimumSpawnClearanceNormalized = level.minimumSpawnClearanceNormalized;
            result.requiredTravelSeconds = level.requiredTravelSeconds;
        }
        result.minimumCampaignLevel = Mathf.Max(1, result.minimumCampaignLevel);
        result.initialSpawnDelay = Mathf.Max(0f, result.initialSpawnDelay);
        result.spawnIntervalMinimum = Mathf.Max(.01f, result.spawnIntervalMinimum);
        result.spawnIntervalMaximum = Mathf.Max(result.spawnIntervalMinimum, result.spawnIntervalMaximum);
        result.movementSpeedNormalized = Mathf.Max(0f, result.movementSpeedNormalized);
        result.sizeNormalized = Mathf.Clamp(result.sizeNormalized, .02f, .2f);
        result.maximumConcurrent = Mathf.Clamp(result.maximumConcurrent, 1, 8);
        if (result.spawnEdges == EnemySpawnEdges.None) result.spawnEdges = EnemySpawnEdges.All;
        result.minimumSpawnClearanceNormalized = Mathf.Max(0f, result.minimumSpawnClearanceNormalized);
        result.requiredTravelSeconds = Mathf.Max(.1f, result.requiredTravelSeconds);
        result.collisionSubstepNormalized = Mathf.Max(.001f, result.collisionSubstepNormalized);
        return result;
    }

    public static bool IsEnabledForLevel(EffectiveEnemySettings settings, int campaignLevel)
    {
        return settings.enabled && campaignLevel >= settings.minimumCampaignLevel;
    }

    /// <summary>Independent xorshift seed for one run/level enemy stream.</summary>
    public static uint CreateStreamSeed(int runSeed, int levelIndex)
    {
        unchecked
        {
            uint value = (uint)runSeed ^ 0xA511E9B3u;
            value ^= (uint)(levelIndex + 1) * 0x9E3779B9u;
            value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
            return value == 0 ? 1u : value;
        }
    }

    /// <summary>Repairs legacy/corrupt state deterministically before a controller binds it.</summary>
    public static void NormalizeState(EnemyState state, EffectiveEnemySettings settings)
    {
        if (state == null) return;
        if (state.active == null) state.active = new List<EnemyInstanceState>();
        if (state.randomState == 0) state.randomState = 1;
        if (float.IsNaN(state.spawnRemaining) || float.IsInfinity(state.spawnRemaining)) state.spawnRemaining = settings.initialSpawnDelay;
        state.spawnRemaining = Mathf.Clamp(state.spawnRemaining, 0f, Mathf.Max(settings.spawnIntervalMaximum, settings.initialSpawnDelay));
        HashSet<int> seenIds = new HashSet<int>();
        int greatestId = 0;
        // Keep the first valid record for a duplicate id. Serialized list order
        // is part of the restore contract, so a later corrupt duplicate must
        // never replace an earlier threat.
        for (int i = 0; i < state.active.Count; i++)
        {
            EnemyInstanceState enemy = state.active[i];
            if (enemy == null || enemy.id < 1 || !seenIds.Add(enemy.id) ||
                float.IsNaN(enemy.normalizedPosition.x) || float.IsNaN(enemy.normalizedPosition.y) ||
                float.IsInfinity(enemy.normalizedPosition.x) || float.IsInfinity(enemy.normalizedPosition.y) ||
                (enemy.phase != EnemyPhase.Approaching && enemy.phase != EnemyPhase.Destroying && enemy.phase != EnemyPhase.Impacting))
            {
                state.active.RemoveAt(i);
                i--;
                continue;
            }
            enemy.normalizedPosition = new Vector2(Mathf.Clamp01(enemy.normalizedPosition.x), Mathf.Clamp01(enemy.normalizedPosition.y));
            if (enemy.normalizedVelocity.sqrMagnitude < Epsilon || float.IsNaN(enemy.normalizedVelocity.x) || float.IsNaN(enemy.normalizedVelocity.y) ||
                float.IsInfinity(enemy.normalizedVelocity.x) || float.IsInfinity(enemy.normalizedVelocity.y)) enemy.normalizedVelocity = Vector2.right;
            else enemy.normalizedVelocity.Normalize();
            enemy.phaseElapsed = Mathf.Max(0f, float.IsNaN(enemy.phaseElapsed) || float.IsInfinity(enemy.phaseElapsed) ? 0f : enemy.phaseElapsed);
            greatestId = Mathf.Max(greatestId, enemy.id);
        }
        if (state.active.Count > settings.maximumConcurrent)
            state.active.RemoveRange(settings.maximumConcurrent, state.active.Count - settings.maximumConcurrent);
        state.nextEnemyId = Mathf.Max(greatestId + 1, Mathf.Max(1, state.nextEnemyId));
    }

    /// <summary>
    /// Space needed on each arena edge after the grid's worst transformed extent.
    /// Grid speed is normalized by arena short side; enemy speed is relative closure.
    /// </summary>
    public static float GetRequiredGridEdgeClearanceNormalized(EffectiveEnemySettings settings, float gridSpeedNormalized)
    {
        // Kept for simple stationary callers. A supplied speed is only useful
        // when no complete movement envelope is available.
        return GetEnemyApproachPaddingNormalized(settings) + Mathf.Max(0f, gridSpeedNormalized) * settings.requiredTravelSeconds;
    }

    /// <summary>Padding outside a complete future grid envelope. Do not add grid speed when using that envelope.</summary>
    public static float GetEnemyApproachPaddingNormalized(EffectiveEnemySettings settings)
    {
        return settings.sizeNormalized * .5f + settings.minimumSpawnClearanceNormalized +
            Mathf.Max(0f, settings.movementSpeedNormalized) * settings.requiredTravelSeconds;
    }

    public static float GetWorstGridRadialSpeedNormalized(float baseGridSideNormalized, float maximumGridScale,
        float rotationDegreesPerSecond, float maximumScaleChangePerSecond)
    {
        float baseRadius = Mathf.Max(0f, baseGridSideNormalized) * .70710678f;
        return Mathf.Abs(rotationDegreesPerSecond) * Mathf.Deg2Rad * baseRadius * Mathf.Max(.01f, maximumGridScale) +
            baseRadius * Mathf.Max(0f, maximumScaleChangePerSecond);
    }

    /// <summary>Bounds a proposed half footprint in arena units without shrinking below the caller's readable minimum.</summary>
    public static bool TryFitGridHalfExtents(Vector2 requestedHalfExtents, Vector2 minimumReadableHalfExtents,
        EffectiveEnemySettings settings, float gridSpeedNormalized, out Vector2 fittedHalfExtents)
    {
        float edge = GetRequiredGridEdgeClearanceNormalized(settings, gridSpeedNormalized);
        Vector2 maximum = Vector2.one * Mathf.Max(0f, .5f - edge);
        fittedHalfExtents = Vector2.Min(requestedHalfExtents, maximum);
        return fittedHalfExtents.x + Epsilon >= minimumReadableHalfExtents.x &&
               fittedHalfExtents.y + Epsilon >= minimumReadableHalfExtents.y;
    }

    /// <summary>
    /// Computes a square base-side that leaves an enemy approach corridor around
    /// its largest rotated/scaled footprint. The caller keeps its existing grid
    /// fit when this returns false, then skips enemies with a bounded warning.
    /// </summary>
    public static bool TryFitGridBaseSide(float arenaShortSide, int gridSize, float minimumCellPixels,
        float fixedPaddingNormalized, float maximumGridScale, float maximumRotationDegrees,
        float gridSpeedNormalized, EffectiveEnemySettings settings, out float baseGridSide)
    {
        arenaShortSide = Mathf.Max(1f, arenaShortSide);
        gridSize = Mathf.Max(2, gridSize);
        float radians = Mathf.Abs(maximumRotationDegrees) * Mathf.Deg2Rad;
        float rotationExtent = Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians));
        // A continuously rotating board eventually reaches 45 degrees even if
        // its authored speed is not itself 45, so callers should pass 45 then.
        rotationExtent = Mathf.Max(1f, rotationExtent);
        float corridor = GetRequiredGridEdgeClearanceNormalized(settings, gridSpeedNormalized);
        float halfAvailable = .5f - Mathf.Max(0f, fixedPaddingNormalized) - corridor;
        baseGridSide = Mathf.Max(0f, 2f * halfAvailable * arenaShortSide /
            (Mathf.Max(.01f, maximumGridScale) * rotationExtent));
        return baseGridSide / gridSize + Epsilon >= minimumCellPixels;
    }

    /// <summary>Motion-aware fitting for grids that can translate, rotate and scale in the enemy arena.</summary>
    public static bool TryFitGridBaseSideWithMotion(float arenaShortSide, int gridSize, float minimumCellPixels,
        float fixedPaddingNormalized, float maximumGridScale, float maximumRotationDegrees,
        float gridTranslationSpeedNormalized, float gridMovementEnvelopeNormalized, float rotationDegreesPerSecond,
        float maximumScaleChangePerSecond, EffectiveEnemySettings settings, out float baseGridSide)
    {
        baseGridSide = Mathf.Max(0f, arenaShortSide) * .5f;
        float radians = Mathf.Abs(maximumRotationDegrees) * Mathf.Deg2Rad;
        float rotationExtent = Mathf.Max(1f, Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians)));
        for (int i = 0; i < 3; i++)
        {
            // The complete translation envelope already includes every future
            // board centre. Adding its speed or radial rotation closure here
            // would reserve the same distance twice.
            float corridor = GetEnemyApproachPaddingNormalized(settings);
            float halfAvailable = .5f - Mathf.Max(0f, fixedPaddingNormalized) -
                Mathf.Max(0f, gridMovementEnvelopeNormalized) - corridor;
            baseGridSide = Mathf.Max(0f, 2f * halfAvailable * Mathf.Max(1f, arenaShortSide) /
                (Mathf.Max(.01f, maximumGridScale) * rotationExtent));
        }
        return baseGridSide / Mathf.Max(2, gridSize) + Epsilon >= minimumCellPixels;
    }

    public static EnemyGridObb CaptureGridObb(RectTransform grid, RectTransform arena)
    {
        Vector3[] corners = new Vector3[4];
        grid.GetWorldCorners(corners);
        Vector2 p0 = arena.InverseTransformPoint(corners[0]);
        Vector2 p1 = arena.InverseTransformPoint(corners[1]);
        Vector2 p3 = arena.InverseTransformPoint(corners[3]);
        Vector2 x = p3 - p0, y = p1 - p0;
        float width = x.magnitude, height = y.magnitude;
        return new EnemyGridObb
        {
            center = (p0 + p1 + (Vector2)arena.InverseTransformPoint(corners[2]) + p3) * .25f,
            axisX = width > Epsilon ? x / width : Vector2.right,
            axisY = height > Epsilon ? y / height : Vector2.up,
            halfExtents = new Vector2(width * .5f, height * .5f)
        };
    }

    public static bool SquareIntersectsObb(Vector2 squareCenter, float squareHalfSide, EnemyGridObb obb)
    {
        squareHalfSide = Mathf.Max(0f, squareHalfSide);
        Vector2 d = squareCenter - obb.center;
        // SAT: OBB axes and arena-local square axes. This catches rotated corners accurately.
        if (Mathf.Abs(Vector2.Dot(d, obb.axisX)) > obb.halfExtents.x + squareHalfSide * (Mathf.Abs(obb.axisX.x) + Mathf.Abs(obb.axisX.y))) return false;
        if (Mathf.Abs(Vector2.Dot(d, obb.axisY)) > obb.halfExtents.y + squareHalfSide * (Mathf.Abs(obb.axisY.x) + Mathf.Abs(obb.axisY.y))) return false;
        if (Mathf.Abs(d.x) > squareHalfSide + obb.halfExtents.x * Mathf.Abs(obb.axisX.x) + obb.halfExtents.y * Mathf.Abs(obb.axisY.x)) return false;
        if (Mathf.Abs(d.y) > squareHalfSide + obb.halfExtents.x * Mathf.Abs(obb.axisX.y) + obb.halfExtents.y * Mathf.Abs(obb.axisY.y)) return false;
        return true;
    }

    public static bool SweepSquareAgainstObb(Vector2 from, Vector2 to, float squareHalfSide, EnemyGridObb obb, float maximumStep)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(.0001f, maximumStep)), 1, 128);
        for (int i = 0; i <= steps; i++)
            if (SquareIntersectsObb(Vector2.Lerp(from, to, i / (float)steps), squareHalfSide, obb)) return true;
        return false;
    }

    /// <summary>Substep a square against a continuously moving/rotating/scaling OBB.</summary>
    public static bool SweepSquareAgainstMovingObb(Vector2 from, Vector2 to, float squareHalfSide,
        EnemyGridObb previous, EnemyGridObb current, float maximumStep, float authoredRotationDeltaDegrees = 0f)
    {
        float previousAngle = Mathf.Atan2(previous.axisX.y, previous.axisX.x) * Mathf.Rad2Deg;
        float currentAngle = Mathf.Atan2(current.axisX.y, current.axisX.x) * Mathf.Rad2Deg;
        float rotationDelta = Mathf.Abs(authoredRotationDeltaDegrees) > Mathf.Abs(Mathf.DeltaAngle(previousAngle, currentAngle)) + .01f
            ? authoredRotationDeltaDegrees : Mathf.DeltaAngle(previousAngle, currentAngle);
        float angularRadians = Mathf.Abs(rotationDelta) * Mathf.Deg2Rad;
        float radius = Mathf.Max(previous.halfExtents.magnitude, current.halfExtents.magnitude);
        float boardTravel = Vector2.Distance(previous.center, current.center) + angularRadians * radius +
            Vector2.Distance(previous.halfExtents, current.halfExtents);
        int steps = Mathf.Clamp(Mathf.CeilToInt((Vector2.Distance(from, to) + boardTravel) / Mathf.Max(.0001f, maximumStep)), 1, 128);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            if (SquareIntersectsObb(Vector2.Lerp(from, to, t), squareHalfSide, InterpolateObb(previous, current, t, rotationDelta))) return true;
        }
        return false;
    }

    private static EnemyGridObb InterpolateObb(EnemyGridObb from, EnemyGridObb to, float t, float rotationDeltaDegrees)
    {
        float firstAngle = Mathf.Atan2(from.axisX.y, from.axisX.x) * Mathf.Rad2Deg;
        float angle = (firstAngle + rotationDeltaDegrees * t) * Mathf.Deg2Rad;
        Vector2 x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float handedness = Mathf.Sign(from.axisX.x * from.axisY.y - from.axisX.y * from.axisY.x);
        if (Mathf.Approximately(handedness, 0f)) handedness = 1f;
        Vector2 y = new Vector2(-x.y * handedness, x.x * handedness);
        return new EnemyGridObb { center = Vector2.Lerp(from.center, to.center, t), axisX = x, axisY = y, halfExtents = Vector2.Lerp(from.halfExtents, to.halfExtents, t) };
    }

    public static bool TryChooseSpawn(EffectiveEnemySettings settings, EnemyGridObb grid, ref DeterministicRandom random,
        out Vector2 normalizedPosition, out Vector2 normalizedDirection)
    {
        normalizedPosition = Vector2.zero;
        normalizedDirection = Vector2.zero;
        float half = settings.sizeNormalized * .5f;
        EnemySpawnEdges[] edges = { EnemySpawnEdges.Left, EnemySpawnEdges.Right, EnemySpawnEdges.Bottom, EnemySpawnEdges.Top };
        int edgeOffset = random.Range(0, edges.Length);
        for (int attempt = 0; attempt < edges.Length * 3; attempt++)
        {
            EnemySpawnEdges edge = edges[(edgeOffset + attempt) % edges.Length];
            if ((settings.spawnEdges & edge) == 0) continue;
            float lane = Mathf.Lerp(half, 1f - half, random.NextFloat01());
            Vector2 candidate = edge == EnemySpawnEdges.Left ? new Vector2(half, lane) :
                edge == EnemySpawnEdges.Right ? new Vector2(1f - half, lane) :
                edge == EnemySpawnEdges.Bottom ? new Vector2(lane, half) : new Vector2(lane, 1f - half);
            Vector2 towardCenter = new Vector2(.5f, .5f) - candidate;
            if (towardCenter.sqrMagnitude < Epsilon) continue;
            Vector2 direction = towardCenter.normalized;
            float travel = EstimateApproachDistance(candidate, direction, grid, half, settings.collisionSubstepNormalized);
            if (travel + Epsilon < settings.minimumSpawnClearanceNormalized +
                settings.movementSpeedNormalized * settings.requiredTravelSeconds) continue;
            normalizedPosition = candidate;
            normalizedDirection = direction;
            return true;
        }
        return false;
    }

    /// <summary>Chooses a spawn against a grid OBB in arena-local coordinates, while persisting the result in arena units.</summary>
    public static bool TryChooseSpawnInArena(EffectiveEnemySettings settings, EnemyGridObb grid, Rect arenaRect,
        ref DeterministicRandom random, out Vector2 normalizedPosition, out Vector2 normalizedDirection)
    {
        normalizedPosition = Vector2.zero;
        normalizedDirection = Vector2.zero;
        float shortSide = Mathf.Max(1f, Mathf.Min(arenaRect.width, arenaRect.height));
        float half = settings.sizeNormalized * shortSide * .5f;
        EnemySpawnEdges[] edges = { EnemySpawnEdges.Left, EnemySpawnEdges.Right, EnemySpawnEdges.Bottom, EnemySpawnEdges.Top };
        int edgeOffset = random.Range(0, edges.Length);
        for (int attempt = 0; attempt < edges.Length * 3; attempt++)
        {
            EnemySpawnEdges edge = edges[(edgeOffset + attempt) % edges.Length];
            if ((settings.spawnEdges & edge) == 0) continue;
            float x = Mathf.Lerp(arenaRect.xMin + half, arenaRect.xMax - half, random.NextFloat01());
            float y = Mathf.Lerp(arenaRect.yMin + half, arenaRect.yMax - half, random.NextFloat01());
            Vector2 candidate = edge == EnemySpawnEdges.Left ? new Vector2(arenaRect.xMin + half, y) :
                edge == EnemySpawnEdges.Right ? new Vector2(arenaRect.xMax - half, y) :
                edge == EnemySpawnEdges.Bottom ? new Vector2(x, arenaRect.yMin + half) : new Vector2(x, arenaRect.yMax - half);
            Vector2 towardCenter = new Vector2(arenaRect.center.x, arenaRect.center.y) - candidate;
            if (towardCenter.sqrMagnitude < Epsilon) continue;
            Vector2 direction = towardCenter.normalized;
            float travel = EstimateApproachDistance(candidate, direction, grid, half, settings.collisionSubstepNormalized * shortSide);
            float required = (settings.minimumSpawnClearanceNormalized + settings.movementSpeedNormalized * settings.requiredTravelSeconds) * shortSide;
            if (travel + Epsilon < required) continue;
            normalizedPosition = new Vector2((candidate.x - arenaRect.xMin) / Mathf.Max(1f, arenaRect.width),
                (candidate.y - arenaRect.yMin) / Mathf.Max(1f, arenaRect.height));
            normalizedDirection = new Vector2(direction.x * shortSide / Mathf.Max(1f, arenaRect.width),
                direction.y * shortSide / Mathf.Max(1f, arenaRect.height)).normalized;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Chooses against an axis-aligned envelope covering every authored board
    /// pose. It tests all allowed edges in a bounded order, so a narrow arena
    /// can still use its long edges instead of producing unavoidable spawns.
    /// </summary>
    public static bool TryChooseSpawnAgainstEnvelope(EffectiveEnemySettings settings, Rect arenaRect,
        Vector2 fullEnvelopeHalfExtents, ref DeterministicRandom random, out Vector2 normalizedPosition, out Vector2 normalizedDirection)
    {
        normalizedPosition = Vector2.zero;
        normalizedDirection = Vector2.zero;
        float shortSide = Mathf.Max(1f, Mathf.Min(arenaRect.width, arenaRect.height));
        float physicalHalf = settings.sizeNormalized * shortSide * .5f;
        float padding = (settings.minimumSpawnClearanceNormalized + settings.movementSpeedNormalized * settings.requiredTravelSeconds) * shortSide + physicalHalf;
        Vector2 safeHalf = fullEnvelopeHalfExtents + Vector2.one * padding;
        Vector2 centre = arenaRect.center;
        EnemySpawnEdges[] edges = { EnemySpawnEdges.Left, EnemySpawnEdges.Right, EnemySpawnEdges.Bottom, EnemySpawnEdges.Top };
        int edgeOffset = random.Range(0, edges.Length);
        for (int attempt = 0; attempt < edges.Length * 3; attempt++)
        {
            EnemySpawnEdges edge = edges[(edgeOffset + attempt) % edges.Length];
            if ((settings.spawnEdges & edge) == 0) continue;
            float x = Mathf.Lerp(arenaRect.xMin + physicalHalf, arenaRect.xMax - physicalHalf, random.NextFloat01());
            float y = Mathf.Lerp(arenaRect.yMin + physicalHalf, arenaRect.yMax - physicalHalf, random.NextFloat01());
            Vector2 point = edge == EnemySpawnEdges.Left ? new Vector2(arenaRect.xMin + physicalHalf, y) :
                edge == EnemySpawnEdges.Right ? new Vector2(arenaRect.xMax - physicalHalf, y) :
                edge == EnemySpawnEdges.Bottom ? new Vector2(x, arenaRect.yMin + physicalHalf) : new Vector2(x, arenaRect.yMax - physicalHalf);
            if (Mathf.Abs(point.x - centre.x) <= safeHalf.x && Mathf.Abs(point.y - centre.y) <= safeHalf.y) continue;
            Vector2 towardCentre = centre - point;
            if (towardCentre.sqrMagnitude < Epsilon) continue;
            normalizedPosition = new Vector2((point.x - arenaRect.xMin) / Mathf.Max(1f, arenaRect.width), (point.y - arenaRect.yMin) / Mathf.Max(1f, arenaRect.height));
            Vector2 localDirection = towardCentre.normalized;
            normalizedDirection = new Vector2(localDirection.x * shortSide / Mathf.Max(1f, arenaRect.width), localDirection.y * shortSide / Mathf.Max(1f, arenaRect.height)).normalized;
            return true;
        }
        return false;
    }

    private static float EstimateApproachDistance(Vector2 origin, Vector2 direction, EnemyGridObb grid, float half, float step)
    {
        const float maxDistance = 2f;
        float increment = Mathf.Max(.002f, step);
        for (float distance = 0f; distance <= maxDistance; distance += increment)
            if (SquareIntersectsObb(origin + direction * distance, half, grid)) return distance;
        return 0f;
    }
}
