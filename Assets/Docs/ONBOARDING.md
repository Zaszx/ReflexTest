# Neon Reflex onboarding

The first-time onboarding is a short, isolated practice sequence on the normal gameplay screen. Its presentation is built by `RogueliteUIController.Onboarding`; `GameManager.Onboarding` owns all practice state, simulation gating, transitions, and completion behavior.

## Steps

1. **First tap** — only the largest outline is accepted. The first target receives cyan focus brackets and a pointer; other taps have no effect.
2. **Follow the sequence** — the player completes a short run of changing largest-outline targets. Incorrect taps still have no effect.
3. **Health** — the health HUD reveals, then practice begins with three health. Incorrect taps cost one health. Reaching zero shows the retry prompt and restarts only this section.
4. **Time and reserve** — the timer HUD reveals. A guided countdown reaches zero, shows reserve taking over, drains it briefly, then transitions to a second practice level to demonstrate fresh level time with retained health and reserve.
5. **Ready** — a centered card hands off to a new real Level 1 run, or returns to the preserved origin when replaying.

Instruction, HUD, focus, skip, and ready-card presentation use a retargetable UI fade clock. The controller advances that clock from GameManager, including a zero delta while Settings is open or the application is backgrounded.

## Practice isolation

Practice creates temporary cloned level definitions and a temporary `ActiveRunData` with a deterministic seed. It has no coin rewards and is never written as the active saved run. Save methods explicitly ignore onboarding activity. On exit, the temporary levels are released and either a fresh real run is created or the saved active run and level state are restored.

## Eligibility and persistence

`PlayerProfileData` stores `OnboardingStatus` as `NeverSeen`, `Skipped`, or `Completed`, plus `hasStartedRealRun`. `OnboardingProfileRules.ShouldOffer` offers the opening practice only to a truly clean profile: no active or completed run, coins, upgrades, banked-run id, or started real run. Finishing or skipping the opening practice records the durable outcome immediately before a real run is created. Replays leave the opening outcome unchanged.

## Replay and interruptions

Settings provides **How to Play**. During active onboarding it changes to **Skip Practice**. A replay preserves the current real-run snapshot, rebuilds temporary practice state, then uses the existing grid transition to restore the original run. Skip starts the real Level 1 for first-time onboarding, or returns to the replay origin. Home requests a clean practice exit. Settings and app backgrounding freeze the practice and presentation clocks; grid transitions gate interaction while their animation completes. Exit requests cancel pending work and ignore duplicate actions.

## Configuration

The runtime resource is [NeonOnboardingSettings.asset](../Resources/NeonOnboardingSettings.asset). Default values are:

- Follow-sequence taps: 5
- Health-practice taps: 4
- Guided level time: 3 seconds
- Starting practice reserve: 6 seconds
- Guided reserve drain: 2 seconds
- Visible zero-time hold before reserve: 0.35 seconds
- Instruction fade: 0.18 seconds

The same asset stores all step text and action labels. The UI receives its fade duration from `instructionFadeSeconds`.

## Validation

Unity Editor verification on 10 September 2026:

- **62 EditMode tests passed**, with no failures or skips, including profile eligibility, persistence and practice isolation tests. Report: `Artifacts/UI/editmode-results.xml`.
- **308 runtime assertions passed** at 405 × 900 with top/bottom safe-area insets. Report and five captured stages: `Artifacts/UI/OnboardingVerified/405x900-safe-insets/`.
- **197 runtime assertions passed** in the completion and presentation check at 720 × 1280. Report and five captured stages: `Artifacts/UI/OnboardingVerified/720x1280/`.
- Runtime coverage includes ignored early mistakes, successful-tap progression, health-only retry, the guided timer and reserve carry, real Level 1 handoff, save reload, every skip stage, duplicate exit requests, Home, Settings, background/focus interruptions, reduced effects, and replay from both menu and a paused moving 4 × 4 campaign grid. Completing or skipping replay preserves the saved run envelope byte-for-byte.
- Visible instructional text was checked for overflow and safe-area containment, with captured stages also inspected visually.

The Editor checks use an isolated QA profile and restore settings after the run. Physical Android-device behavior has not been tested for this change, and this task does not produce a new APK.
