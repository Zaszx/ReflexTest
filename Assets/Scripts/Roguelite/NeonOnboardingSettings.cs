using System;
using UnityEngine;

public enum OnboardingStep
{
    None, FirstTap, FollowSequence, HealthIntro, HealthPractice, HealthRetry,
    TimeIntro, TimeCountdown, ReserveIntro, ReserveCountdown, ReserveCarry,
    NextLevel, CarrySummary, Ready, Exiting, Returning
}

[Serializable]
public sealed class OnboardingPrompt
{
    public string title;
    [TextArea(1, 3)] public string body;
    public string action;
    public OnboardingPrompt(string title, string body = "", string action = "")
    { this.title = title; this.body = body; this.action = action; }
}

/// <summary>Practice-only content and timing. Campaign assets and upgrade rules are never changed.</summary>
[CreateAssetMenu(menuName = "NeonReflex/Onboarding Settings")]
public sealed class NeonOnboardingSettings : ScriptableObject
{
    [Header("Short interactive sections")]
    [Min(1)] public int followCorrectTaps = 5;
    [Min(1)] public int healthCorrectTaps = 4;
    [Header("Guided timer demonstration / seconds")]
    [Min(.5f)] public float levelSeconds = 3f;
    [Min(3)] public float reserveSeconds = 6f;
    [Min(.5f)] public float reserveDrainSeconds = 2f;
    [Range(.1f, 1f)] public float zeroTimeHoldSeconds = .35f;
    [Header("Presentation / seconds")]
    [Range(.08f, .4f)] public float instructionFadeSeconds = .18f;

    public OnboardingPrompt firstTap = new OnboardingPrompt("Tap the <color=#59EFFF>largest outline.</color>");
    public OnboardingPrompt follow = new OnboardingPrompt("Good! Keep tapping the <color=#59EFFF>largest outline.</color>");
    public OnboardingPrompt healthIntro = new OnboardingPrompt("Mistakes cost <color=#ACF35F>1 health.</color>", "Zero health ends the run.\nHealth carries between levels.", "NEXT");
    public OnboardingPrompt healthPractice = new OnboardingPrompt("Keep following the <color=#59EFFF>largest outline.</color>", "Mistakes cost 1 health. You've got this.");
    public OnboardingPrompt healthRetry = new OnboardingPrompt("Out of practice health.", "In a real run, zero health ends it.\nLet's try just this part again.", "TRY AGAIN");
    public OnboardingPrompt timeIntro = new OnboardingPrompt("Complete the tap goal before <color=#FFD05B>level time</color> runs out.", "Each level starts with fresh time.", "WATCH TIMER");
    public OnboardingPrompt timeCountdown = new OnboardingPrompt("Watch <color=#FFD05B>level time</color> reach zero.", "A short demonstration — no tapping needed.");
    public OnboardingPrompt reserveIntro = new OnboardingPrompt("When level time runs out, <color=#FFD05B>reserve takes over.</color>", "Watch the shared reserve start to drain.", "WATCH RESERVE");
    public OnboardingPrompt reserveCountdown = new OnboardingPrompt("Your <color=#FFD05B>reserve</color> is now being used.");
    public OnboardingPrompt reserveCarry = new OnboardingPrompt("<color=#FFD05B>Reserve</color> carries between levels.", "It does not refill automatically.\nEmpty reserve ends the run.", "NEXT");
    public OnboardingPrompt nextLevel = new OnboardingPrompt("Next level. <color=#59EFFF>Fresh time.</color>", "Watch health and reserve stay the same.");
    public OnboardingPrompt carrySummary = new OnboardingPrompt("Fresh time. Same <color=#ACF35F>health</color> and <color=#FFD05B>reserve.</color>", "Your resources carry through the run.", "GOT IT");
    public OnboardingPrompt ready = new OnboardingPrompt("You're ready.", "Start your first run.", "START RUN");

    public OnboardingPrompt Prompt(OnboardingStep step)
    {
        switch (step)
        {
            case OnboardingStep.FirstTap: return firstTap;
            case OnboardingStep.FollowSequence: return follow;
            case OnboardingStep.HealthIntro: return healthIntro;
            case OnboardingStep.HealthPractice: return healthPractice;
            case OnboardingStep.HealthRetry: return healthRetry;
            case OnboardingStep.TimeIntro: return timeIntro;
            case OnboardingStep.TimeCountdown: return timeCountdown;
            case OnboardingStep.ReserveIntro: return reserveIntro;
            case OnboardingStep.ReserveCountdown: return reserveCountdown;
            case OnboardingStep.ReserveCarry: return reserveCarry;
            case OnboardingStep.NextLevel: return nextLevel;
            case OnboardingStep.CarrySummary: return carrySummary;
            default: return ready;
        }
    }
}
