# Tap feedback and level transitions

## Ownership and tuning

`GameplayFeedbackController.Tap.cs` is the sole shared tap-feedback owner. Its edge priority is **Terminal > Damage > Success > None**; a lower-priority cue cannot replace a live higher-priority edge cue. `GameManager` commits input immediately, then calls the local cell and shared owner hooks. Feedback completion never changes health, targets, rewards, progression, saves, or input eligibility.

Tune these authored assets and classes:

- `Assets/Resources/NeonMotionSettings.asset` / `Assets/Scripts/Roguelite/NeonMotionSettings.cs`: cell tint and accents, vignette timing/intensity, 150 ms damage recoil at 0.004 viewport width, and success particle count/duration/travel.
- `Assets/Resources/LevelTransitionAnnouncementSettings.asset` / `Assets/Scripts/Roguelite/LevelTransitionAnnouncementSettings.cs`: announcement phases, the ten ordinary lines, and motion-challenge copy.
- `Assets/Scripts/GameSquare.cs`: owned local cell layers and outgoing-outline capture.

## Tap response

Wrong taps apply the current-palette-relative coral fill plus the cell-owned `DamageOutline`. Correct taps apply the green fill plus cell-owned `SuccessOutline` corner ticks (`CornerFraction` 0.08), not a hollow target-shaped frame. Both layers are non-raycasting, use shared settings, and clear on palette replacement, normalization, disable, and finite completion. They do not consume, recolor, or resize active or retiring role outlines.

Before a correct target advances, `GameManager.HandleCorrectTap` calls `CaptureSuccessPose`; `GameSquare.TryGetRenderedOutlineCorners` copies the live, transformed rendered perimeter. `PlayTapFeedback(true)` and `PlayConsumedFeedback()` then remain independent of the reused cell role.

The horizontal recoil moves `GameplayTapOffset`, the wrapper containing the complete gameplay panel and its UI raycast geometry. Its offset is normalized to wrapper width, returns exactly to zero, and has a backdrop for its small exposed edge. This keeps displayed cells and their hit regions together. Repeated damage retargets from the current offset rather than stacking amplitude.

`NeonTapParticles` is one UI mesh with a fixed pool of 48 glints. Defaults emit six short green/cyan glints for 0.22 seconds from the captured perimeter. It has no per-tap objects, caps decoration when slots are full, and uses its own decorative PRNG; target-selection gameplay RNG is never read or advanced.

Terminal feedback takes the shared owner, clears particles, and preserves the fatal damage cue without starting another one. Reserve exhaustion resets ordinary tap feedback. Settings/application pause stops presentation advancement; level replacement, navigation, and inactive gameplay reset the wrapper, edge, particles, and captured pose. Reduced Effects disables recoil and particles, and reduces edge/local intensity while preserving meaningful local color feedback.

## Level-complete announcement

`GameManager.LevelTransition.cs` prepares the next authoritative level before the presentation begins, locks input and simulation, and runs one 1.8-second transition. Grid/palette preparation uses the first 1.0 second. The announcement has a 0.2-second entrance, 1.4-second readable hold, and 0.2-second exit in that same window; grid morph, counters, and palette updates overlap it. Completion returns to play only after the single transition finishes, with no second introduction.

`LevelTransitionAnnouncementSelector` owns presentation-only copy selection. Its ten configured motivation lines use a shuffled bag without replacement and avoid an immediate boundary repeat. The selector uses its own `System.Random`, never gameplay RNG. The challenge is data-driven from the earliest valid authored Rotation, Move, or Scale configuration; Reverse and stationary/invalid motion do not qualify. In the current campaign it is the **Level 2 complete → Level 3 Rotation** boundary, with “Can you keep up as it rotates?” The challenge replaces that one ordinary line, does not consume the motivation bag, and repeats on the same boundary in every run. No separate existing-motion hint is added by this announcement path.

## Verification status

- Current focused EditMode result: **75 passed**.
- **53 runtime checks passed** at 720 × 1280, including a visible particle mesh from a translated/rotated/scaled target, correct touch raycasts during recoil, priority, input integrity, external pause, the actual L2 → L3 challenge, and onboarding replay isolation. Report and captures: `Artifacts/UI/TapFeedbackRenderCheck/720x1280/`.
- **5,021 grid-resize assertions passed**, including every different 2×2–5×5 pair, Reduced Effects, zero-duration transitions, and viewport refitting. Report: `Artifacts/LevelTransition/TapFeedbackFinal/405x900/GridResize/checks.txt`.
- **55 damage/run-ending** and **60 level-transition** checks passed, covering terminal persistence, fresh input, timer/resource freezing, checkpoint restoration, cancellation, and victory. Reports: `Artifacts/DamageEnding/TapFeedbackFinal/405x900/checks.txt` and `Artifacts/LevelTransition/TapFeedbackFinal/405x900/checks.txt`.
- **197 onboarding completion/presentation checks passed** at 405 × 900. Report: `Artifacts/UI/TapFeedbackFinal/405x900/runtime-checks.txt`.
- Actual runtime frame sequences and timestamps: `Artifacts/Motion/TapFeedbackAfter/405x900/`. The correct-tap, mistake, and level-complete folders include GIF previews. Screenshot readback affects capture cadence; these are presentation evidence, not device benchmarks.

Editor validation and simulated viewports do not establish physical-phone touch latency, Android/iOS haptics, or device performance. Those remain device checks.
