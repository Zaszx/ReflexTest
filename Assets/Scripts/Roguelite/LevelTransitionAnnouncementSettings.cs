using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Presentation-only copy and timing for the in-place level-complete announcement.</summary>
[CreateAssetMenu(fileName = "LevelTransitionAnnouncementSettings", menuName = "NeonReflex/Level Transition Announcement Settings")]
public sealed class LevelTransitionAnnouncementSettings : ScriptableObject
{
    [Header("Readable window / seconds")]
    [Min(0f)] public float entranceDuration = .2f;
    [Min(0f)] public float holdDuration = 1.4f;
    [Min(0f)] public float exitDuration = .2f;

    [Header("Ordinary motivation (shuffled without replacement)")]
    [TextArea] public List<string> motivationLines = new List<string>
    {
        "Good job!",
        "Keep going!",
        "Nicely done!",
        "Keep it up!",
        "Stay sharp!",
        "You’ve got this!",
        "Keep the rhythm!",
        "On to the next!",
        "Stay focused!",
        "Next challenge!"
    };

    [Header("First motion challenge")]
    [TextArea] public string rotationChallenge = "Can you keep up as it rotates?";
    [TextArea] public string moveChallenge = "Can you keep up as it moves?";
    [TextArea] public string scaleChallenge = "Can you keep up as it changes size?";
    [TextArea] public string combinedMotionChallenge = "Can you keep up with the changing grid?";

    public float PhaseDuration => Mathf.Max(0f, entranceDuration) + Mathf.Max(0f, holdDuration) + Mathf.Max(0f, exitDuration);

    internal string ChallengeFor(MotionChallengeKind kind)
    {
        switch (kind)
        {
            case MotionChallengeKind.Rotation: return rotationChallenge;
            case MotionChallengeKind.Move: return moveChallenge;
            case MotionChallengeKind.Scale: return scaleChallenge;
            case MotionChallengeKind.Combined: return combinedMotionChallenge;
            default: return string.Empty;
        }
    }
}

public enum MotionChallengeKind { None, Rotation, Move, Scale, Combined }

/// <summary>Describes the one campaign boundary eligible for a motion challenge.</summary>
public readonly struct MotionChallengeBoundary
{
    public readonly int completedLevelNumber;
    public readonly int enteringLevelNumber;
    public readonly MotionChallengeKind kind;

    public bool Exists => kind != MotionChallengeKind.None && completedLevelNumber > 0;

    public MotionChallengeBoundary(int completedLevelNumber, int enteringLevelNumber, MotionChallengeKind kind)
    {
        this.completedLevelNumber = completedLevelNumber;
        this.enteringLevelNumber = enteringLevelNumber;
        this.kind = kind;
    }
}

/// <summary>Immutable transition copy, chosen once by the flow owner before it starts animating.</summary>
public readonly struct LevelTransitionAnnouncement
{
    public readonly string Subline;
    public readonly bool IsMotionChallenge;

    public bool HasSubline => !string.IsNullOrWhiteSpace(Subline);

    public LevelTransitionAnnouncement(string subline, bool isMotionChallenge)
    {
        Subline = subline ?? string.Empty;
        IsMotionChallenge = isMotionChallenge;
    }
}

/// <summary>
/// Owns cosmetic line order only. It never reads or advances gameplay random state and is recreated per run.
/// </summary>
public sealed class LevelTransitionAnnouncementSelector
{
    private readonly LevelTransitionAnnouncementSettings settings;
    private readonly System.Random random;
    private readonly List<string> bag = new List<string>();
    private string lastMotivation;

    public LevelTransitionAnnouncementSelector(LevelTransitionAnnouncementSettings settings, int? presentationSeed = null)
    {
        this.settings = settings != null ? settings : ScriptableObject.CreateInstance<LevelTransitionAnnouncementSettings>();
        random = presentationSeed.HasValue ? new System.Random(presentationSeed.Value) : new System.Random();
    }

    public MotionChallengeBoundary FindBoundary(CampaignDefinition campaign)
    {
        if (campaign == null) return default;
        for (int index = 0; index < campaign.LevelCount; index++)
        {
            MotionChallengeKind kind = ActiveMotionKind(campaign.GetLevel(index));
            if (kind == MotionChallengeKind.None) continue;

            // There is no completed-level announcement before the first campaign level.
            if (index == 0) return default;
            LevelData previous = campaign.GetLevel(index - 1);
            LevelData entering = campaign.GetLevel(index);
            return new MotionChallengeBoundary(previous == null ? 0 : previous.levelNumber,
                entering == null ? 0 : entering.levelNumber, kind);
        }
        return default;
    }

    public LevelTransitionAnnouncement Select(CampaignDefinition campaign, int completedLevelNumber, bool isCampaignTransition)
    {
        if (!isCampaignTransition || completedLevelNumber <= 0)
            return new LevelTransitionAnnouncement(string.Empty, false);

        MotionChallengeBoundary boundary = FindBoundary(campaign);
        if (boundary.Exists && boundary.completedLevelNumber == completedLevelNumber)
            return new LevelTransitionAnnouncement(settings.ChallengeFor(boundary.kind), true);

        return new LevelTransitionAnnouncement(NextMotivation(), false);
    }

    private string NextMotivation()
    {
        if (bag.Count == 0) RefillBag();
        if (bag.Count == 0) return string.Empty;
        int index = bag.Count - 1;
        string selected = bag[index];
        bag.RemoveAt(index);
        lastMotivation = selected;
        return selected;
    }

    private void RefillBag()
    {
        bag.Clear();
        if (settings.motivationLines != null)
        {
            for (int i = 0; i < settings.motivationLines.Count; i++)
            {
                string line = settings.motivationLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    line = line.Trim();
                    if (!bag.Contains(line)) bag.Add(line);
                }
            }
        }

        for (int i = bag.Count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            string value = bag[i]; bag[i] = bag[swap]; bag[swap] = value;
        }

        // The first dequeue is the final list item. Put a different line there when possible.
        if (bag.Count > 1 && bag[bag.Count - 1] == lastMotivation)
        {
            int swap = random.Next(bag.Count - 1);
            string value = bag[bag.Count - 1]; bag[bag.Count - 1] = bag[swap]; bag[swap] = value;
        }
    }

    private static MotionChallengeKind ActiveMotionKind(LevelData level)
    {
        if (level == null) return MotionChallengeKind.None;
        bool rotation = IsFinite(level.rotateSpeed) && !Mathf.Approximately(level.rotateSpeed, 0f);
        bool scale = level.scaleEnabled && IsFinite(level.minimumGridScale) && IsFinite(level.maximumGridScale) &&
            IsFinite(level.scaleCycleDuration) && level.minimumGridScale > 0f &&
            level.maximumGridScale > level.minimumGridScale &&
            !Mathf.Approximately(level.maximumGridScale, level.minimumGridScale) && level.scaleCycleDuration > 0f;
        bool move = level.movementEnabled && IsFinite(level.movementSpeedNormalized) &&
            !Mathf.Approximately(level.movementSpeedNormalized, 0f);
        int activeCount = (rotation ? 1 : 0) + (scale ? 1 : 0) + (move ? 1 : 0);
        if (activeCount != 1) return activeCount > 1 ? MotionChallengeKind.Combined : MotionChallengeKind.None;
        return rotation ? MotionChallengeKind.Rotation : scale ? MotionChallengeKind.Scale : MotionChallengeKind.Move;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
