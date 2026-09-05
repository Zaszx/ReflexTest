using UnityEngine;

public static class GridMotionMath
{
    public static Vector2 TransformedAabbHalfExtents(float baseSide, float uniformScale, float angleDegrees)
    {
        float half = Mathf.Max(0f, baseSide) * Mathf.Max(0f, uniformScale) * .5f;
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2((Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians))) * half, (Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians))) * half);
    }
    public static float AdvanceTrianglePhase(ref float phase, ref int direction, float minimum, float maximum, float halfCycleSeconds, float deltaTime)
    {
        minimum = Mathf.Max(0f, minimum); maximum = Mathf.Max(minimum, maximum);
        if (maximum <= minimum || halfCycleSeconds <= 0f || deltaTime <= 0f) { phase = Mathf.Clamp01(phase); direction = direction < 0 ? -1 : 1; return Mathf.Lerp(minimum, maximum, phase); }
        phase = Mathf.Clamp01(float.IsNaN(phase) ? 0f : phase); direction = direction < 0 ? -1 : 1;
        float travel = deltaTime / halfCycleSeconds;
        // Encode the direction in the unfolded [0, 2) triangle-wave phase.
        // Adding signed travel directly to the folded phase loses a restored
        // descending direction as soon as it is away from an endpoint.
        float unfolded = direction > 0 ? phase : 2f - phase;
        float period = Mathf.Repeat(unfolded + travel, 2f);
        phase = period <= 1f ? period : 2f - period;
        direction = period < 1f ? 1 : -1;
        return Mathf.Lerp(minimum, maximum, phase);
    }
    public static Vector2 ClampPosition(Vector2 position, Vector2 direction, Vector2 boundsHalf, Vector2 objectHalf, out Vector2 correctedDirection)
    {
        correctedDirection = NormalizeSafe(direction);
        Vector2 room = Vector2.Max(Vector2.zero, boundsHalf - objectHalf);
        if (room.x <= 0f) { position.x = 0f; correctedDirection.x = Mathf.Abs(correctedDirection.x); }
        else
        {
            if (position.x > room.x && correctedDirection.x > 0f) correctedDirection.x = -correctedDirection.x;
            else if (position.x < -room.x && correctedDirection.x < 0f) correctedDirection.x = -correctedDirection.x;
            position.x = Mathf.Clamp(position.x, -room.x, room.x);
        }
        if (room.y <= 0f) { position.y = 0f; correctedDirection.y = Mathf.Abs(correctedDirection.y); }
        else
        {
            if (position.y > room.y && correctedDirection.y > 0f) correctedDirection.y = -correctedDirection.y;
            else if (position.y < -room.y && correctedDirection.y < 0f) correctedDirection.y = -correctedDirection.y;
            position.y = Mathf.Clamp(position.y, -room.y, room.y);
        }
        correctedDirection = NormalizeSafe(correctedDirection);
        return position;
    }
    public static Vector2 AdvanceStableMovement(ref Vector2 position, ref Vector2 direction, float speed, float deltaTime, Vector2 boundsHalf, Vector2 objectHalf)
    {
        position = ClampPosition(position, direction, boundsHalf, objectHalf, out direction);
        if (speed <= 0f || deltaTime <= 0f || float.IsNaN(speed) || float.IsNaN(deltaTime)) return position;
        Vector2 room = Vector2.Max(Vector2.zero, boundsHalf - objectHalf);
        direction = NormalizeSafe(direction);
        position.x = ReflectAxis(position.x, ref direction.x, speed * deltaTime, room.x);
        position.y = ReflectAxis(position.y, ref direction.y, speed * deltaTime, room.y);
        direction = NormalizeSafe(direction);
        return position;
    }
    private static float ReflectAxis(float position, ref float component, float distance, float room)
    {
        if (room <= 0f) { component = Mathf.Abs(component); return 0f; }
        float normalized = Mathf.Clamp(position, -room, room) + room;
        float unbounded = normalized + component * distance;
        float span = 2f * room, period = span * 2f;
        float folded = Mathf.Repeat(unbounded, period);
        bool forward = folded <= span;
        float reflected = forward ? folded : period - folded;
        float sign = component >= 0f ? 1f : -1f;
        // Direction after the movement depends on parity of wall crossings.
        if (!forward) sign = -sign;
        component = Mathf.Abs(component) * sign;
        if (Mathf.Approximately(reflected, 0f) && component < 0f) component = -component;
        else if (Mathf.Approximately(reflected, span) && component > 0f) component = -component;
        return reflected - room;
    }
    private static Vector2 NormalizeSafe(Vector2 value)
    {
        if (float.IsNaN(value.x) || float.IsNaN(value.y) || value.sqrMagnitude < .000001f) return new Vector2(.70710678f, .70710678f);
        return value.normalized;
    }
}
