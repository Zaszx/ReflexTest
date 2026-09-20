using System;

/// <summary>Ordered active cells, from the smallest outline to the largest.</summary>
public struct GridSequenceState
{
    public int[] targets;

    public int Count => targets == null ? 0 : targets.Length;
    // Compatibility adapters for legacy three-target save and debug paths.
    public int small => Count > 0 ? targets[0] : -1;
    public int medium => Count > 0 ? targets[Count / 2] : -1;
    public int large => Count > 0 ? targets[Count - 1] : -1;

    public GridSequenceState(int[] targets) => this.targets = targets == null ? null : (int[])targets.Clone();
    public GridSequenceState(int small, int medium, int large) => targets = new[] { small, medium, large };
}

public static class GridSequenceRules
{
    public const int MinimumOutlineCount = 2;
    public const int MaximumOutlineCount = 5;

    public static bool Validate(GridSequenceState state, int cellCount)
    {
        if (cellCount < MinimumOutlineCount || state.targets == null ||
            state.targets.Length < MinimumOutlineCount || state.targets.Length > MaximumOutlineCount ||
            state.targets.Length > cellCount)
            return false;
        for (int i = 0; i < state.targets.Length; i++)
        {
            int target = state.targets[i];
            if (target < 0 || target >= cellCount) return false;
            for (int other = 0; other < i; other++) if (state.targets[other] == target) return false;
        }
        return true;
    }

    /// <summary>Legacy three-target initialization.</summary>
    public static bool Initialize(int cellCount, ref DeterministicRandom rng, out GridSequenceState state) =>
        Initialize(cellCount, 3, ref rng, out state);

    public static bool Initialize(int cellCount, int outlineCount, ref DeterministicRandom rng,
        out GridSequenceState state, int reservedCell = -1)
    {
        state = default;
        if (!IsSupportedCount(outlineCount) || cellCount < outlineCount || !IsReservedCellValid(reservedCell, cellCount)) return false;
        if (cellCount - (reservedCell >= 0 ? 1 : 0) < outlineCount) return false;

        int[] selected = new int[outlineCount];
        for (int rank = 0; rank < outlineCount; rank++)
            selected[rank] = PickAvailable(cellCount, selected, rank, reservedCell, -1, ref rng);
        state = new GridSequenceState(selected);
        return true;
    }

    /// <summary>Promotes every remaining rank and inserts a new smallest rank.</summary>
    public static bool AdvanceNormal(ref GridSequenceState state, int cellCount, ref DeterministicRandom rng,
        int reservedCell = -1)
    {
        if (!Validate(state, cellCount) || !IsReservedCellValid(reservedCell, cellCount) ||
            (reservedCell >= 0 && Contains(state.targets, state.Count, reservedCell))) return false;
        return Advance(ref state, cellCount, ref rng, reservedCell, state.targets[state.targets.Length - 1], true);
    }

    /// <summary>Demotes every remaining rank and appends a new largest rank.</summary>
    public static bool AdvanceReverse(ref GridSequenceState state, int cellCount, ref DeterministicRandom rng,
        int reservedCell = -1)
    {
        if (!Validate(state, cellCount) || !IsReservedCellValid(reservedCell, cellCount) ||
            (reservedCell >= 0 && Contains(state.targets, state.Count, reservedCell))) return false;
        return Advance(ref state, cellCount, ref rng, reservedCell, state.targets[0], false);
    }

    private static bool Advance(ref GridSequenceState state, int cellCount, ref DeterministicRandom rng,
        int reservedCell, int consumed, bool normal)
    {
        int count = state.targets.Length;
        int[] kept = new int[count - 1];
        if (normal) Array.Copy(state.targets, 0, kept, 0, count - 1);
        else Array.Copy(state.targets, 1, kept, 0, count - 1);

        // Prefer an unconsumed free cell. At full capacity, the just-consumed
        // cell is the only legal replacement and is intentionally allowed.
        int replacement = PickAvailable(cellCount, kept, kept.Length, reservedCell, consumed, ref rng);
        if (replacement < 0)
        {
            if (consumed == reservedCell || Contains(kept, kept.Length, consumed)) return false;
            replacement = consumed;
        }

        int[] next = new int[count];
        if (normal)
        {
            next[0] = replacement;
            Array.Copy(kept, 0, next, 1, kept.Length);
        }
        else
        {
            Array.Copy(kept, next, kept.Length);
            next[count - 1] = replacement;
        }
        state = new GridSequenceState(next);
        return true;
    }

    private static int PickAvailable(int cellCount, int[] occupied, int occupiedCount, int reservedCell,
        int avoidedCell, ref DeterministicRandom rng)
    {
        int choices = 0;
        for (int cell = 0; cell < cellCount; cell++)
            if (cell != reservedCell && cell != avoidedCell && !Contains(occupied, occupiedCount, cell)) choices++;
        if (choices == 0) return -1;
        int pick = rng.Range(0, choices);
        for (int cell = 0; cell < cellCount; cell++)
            if (cell != reservedCell && cell != avoidedCell && !Contains(occupied, occupiedCount, cell) && pick-- == 0) return cell;
        return -1;
    }

    private static bool Contains(int[] values, int count, int value)
    {
        for (int i = 0; i < count; i++) if (values[i] == value) return true;
        return false;
    }

    private static bool IsSupportedCount(int count) => count >= MinimumOutlineCount && count <= MaximumOutlineCount;
    private static bool IsReservedCellValid(int reservedCell, int cellCount) => reservedCell >= -1 && reservedCell < cellCount;
}
