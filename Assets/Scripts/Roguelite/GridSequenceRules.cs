using UnityEngine;

public struct GridSequenceState
{
    public int small, medium, large;
    public GridSequenceState(int small, int medium, int large) { this.small = small; this.medium = medium; this.large = large; }
}

public static class GridSequenceRules
{
    public static bool Validate(GridSequenceState state, int count) => count >= 3 && state.small >= 0 && state.medium >= 0 && state.large >= 0 && state.small < count && state.medium < count && state.large < count && state.small != state.medium && state.small != state.large && state.medium != state.large;
    public static bool Initialize(int cellCount, ref DeterministicRandom rng, out GridSequenceState state)
    {
        state = default;
        if (cellCount < 3) return false;
        int a = rng.Range(0, cellCount), b = PickUnused(cellCount, a, -1, ref rng), c = PickUnused(cellCount, a, b, ref rng);
        state = new GridSequenceState(a, b, c); return true;
    }
    public static bool AdvanceNormal(ref GridSequenceState state, int count, ref DeterministicRandom rng)
    {
        if (!Validate(state, count) || count < 4) return false;
        // Keep the two promoted roles and the just-consumed cell unavailable.
        int nextSmall = PickUnused(count, state.small, state.medium, ref rng, state.large);
        state = new GridSequenceState(nextSmall, state.small, state.medium); return true;
    }
    public static bool AdvanceReverse(ref GridSequenceState state, int count, ref DeterministicRandom rng)
    {
        if (!Validate(state, count) || count < 4) return false;
        int nextLarge = PickUnused(count, state.medium, state.large, ref rng, state.small);
        state = new GridSequenceState(state.medium, state.large, nextLarge); return true;
    }
    private static int PickUnused(int count, int excludedA, int excludedB, ref DeterministicRandom rng, int excludedC = -1)
    {
        int choices = count - (excludedA >= 0 ? 1 : 0) - (excludedB >= 0 && excludedB != excludedA ? 1 : 0);
        if (excludedC >= 0 && excludedC != excludedA && excludedC != excludedB) choices--;
        if (choices <= 0) return -1;
        int pick = rng.Range(0, choices);
        for (int i = 0; i < count; i++) if (i != excludedA && i != excludedB && i != excludedC && pick-- == 0) return i;
        return 0;
    }
}
