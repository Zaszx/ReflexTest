using System;
using UnityEngine;

/// <summary>Small xorshift PRNG. Pass this struct by ref when consuming values.</summary>
[Serializable]
public struct DeterministicRandom
{
    private uint state;
    public uint State { get => state == 0 ? 1u : state; set => state = value == 0 ? 1u : value; }
    public DeterministicRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 1u; }
    public DeterministicRandom(uint savedState) { state = savedState == 0 ? 1u : savedState; }
    public uint NextUInt() { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state = x == 0 ? 1u : x; return state; }
    public float NextFloat01() => (NextUInt() & 0x00ffffffu) / 16777216f;
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
    }
    public Vector2 NextValidUnitDirection(float minAbsAxis = .22f)
    {
        minAbsAxis = Mathf.Clamp(minAbsAxis, 0f, .7071067f);
        for (int i = 0; i < 16; i++)
        {
            float angle = NextFloat01() * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (Mathf.Abs(direction.x) >= minAbsAxis && Mathf.Abs(direction.y) >= minAbsAxis) return direction;
        }
        // Four deterministic diagonals remain valid for every supported threshold.
        int sign = Range(0, 4);
        return new Vector2((sign & 1) == 0 ? 1f : -1f, (sign & 2) == 0 ? 1f : -1f).normalized;
    }
}
