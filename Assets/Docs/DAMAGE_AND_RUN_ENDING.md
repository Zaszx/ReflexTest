# Damage feedback and run ending

The previous failure path immediately reset feedback and changed base screens. Its generic gameplay exit and report entrance ran independently, so the final mistake disappeared into a sudden screen change.

## Ownership

`GameManager.HandleMistake` still commits exactly one health loss and refreshes the authoritative HUD before starting one cell reaction, one HUD reaction, and the existing vignette/haptic. The role-defining outline and cell hitbox do not move. A reusable inner coral frame identifies the wrong cell. The heart recoils, lost health remains briefly as a ghost, and one reusable floating label aggregates overlapping events up to 99. Ordinary input and simulation remain live.

`RunEconomyRules.TryBankAndClearActiveRun` creates a copied result, updates the wallet, and clears the active run in the same envelope. `GameManager` saves that envelope before starting the terminal presentation. The result includes the run ID and a pending-report flag; legacy envelopes without it remain supported. A reload restores the committed report directly. Leaving the report acknowledges it, and starting a new run clears it. No animation callback banks, saves, deducts health, or advances a level.

`GameManager.RunEnding` owns one terminal clock and generation guard. The gameplay model stops immediately, while the actual grid hierarchy remains at its visible pose. Cells retain their current target/retiring geometry and dim only their cosmetic colors. Obsolete Reverse announcements stop. The report uses external control of the existing screen/result motion components, with grouped hero, details, and button reveals. Outgoing gameplay is hidden only once the report covers it. Navigation cancels these owners before normalizing hidden children.

Health failure retains the final hit feedback; it does not start a second health reaction. Reserve failure clears health cosmetics and gives the zero timer an amber cue. Campaign completion and abandonment retain their existing flows.

The terminal clock runs while gameplay is terminal, but pauses for Settings or application suspension. The shared clock suppresses the first resumed frame and caps cosmetic frame steps. `TerminalFreshInputGate` reads pointers and the current UI submit action, waits for release and a later frame, and requires the report to be visually ready before enabling its controls.

## Tuning

Use `Assets/Resources/NeonMotionSettings.asset` (definitions in `Assets/Scripts/Roguelite/NeonMotionSettings.cs`). Defaults:

| Presentation | Timing / strength |
| --- | --- |
| Wrong cell | 160 ms, coral opacity 0.82 |
| Heart recoil / lost health | 200 ms, compression 0.91 |
| Floating damage | 260 ms, 14 canvas units of rise |
| Aggregation window | 180 ms |
| Supporting vignette | 200 ms, opacity 0.055 |
| Shared terminal sequence | 900 ms |
| Grid shutdown | 13–62% of terminal timeline |
| Report hero / panel | 33–78% |
| Earnings and statistics | 61–89% |
| Actions | 72–100% |
| Reserve exhaustion cue | 220 ms, amber |

Reduced effects reduce accents and remove heart compression through the existing preference. Zero-duration tracks settle immediately without bypassing the fresh-input barrier.

## Verification tools

All runtime scenarios use `NeonPresentationQAProfile`, never the real profile. The Editor bridge accepts a JSON command at `.utmp/neon-ui-command.json`:

```json
{"action":"damage-ending-capture","stage":"After","width":405,"height":900}
```

`damage-ending-tests` runs the assertions without frame capture. `tests`, `motion-tests`, `level-transition-tests`, and `grid-resize-tests` run existing regression suites. Status is written to `.utmp/neon-ui-status.json`. The damage suite writes checks, actual frame timestamps, event timestamps, and native Unity screenshots under `Artifacts/DamageEnding/<stage>/<resolution>/`. Screenshot readback can reduce cadence; timestamps report the actual capture times.

These harnesses are Editor-only. Physical-device rendering, haptic feel, and device performance require an Android device pass.

## Verified in Unity Editor (2026-09-09)

- Final EditMode run: 58 passed, zero failures. This covers rules, campaign definitions, motion, local damage, fresh-input gating, and legacy/result save recovery. The existing PlayerPrefs migration test was deliberately excluded to avoid touching real preferences. XML: `Artifacts/UI/editmode-results.xml`.
- 55 damage/terminal runtime assertions passed at 405×900 and again at 720×1280 with 40/32-pixel safe-area insets. These include immediate damage and unchanged target rules, rapid aggregation, frozen motion poses, terminal idempotency, disk reload, modal/background suspension at three phases, debug isolation, stale-callback cancellation, reduced effects, and zero duration.
- Real Input System synthetic mouse and Gamepad South events verified held-input detection. The actual report Button handlers rejected pointer/submit dispatch while gated and accepted fresh dispatch after release and a later frame. QA temporarily routes synthetic devices into the unfocused Game View, then restores input settings and the exact original button event object.
- 72 existing motion runtime assertions passed, including Reverse, input timing, resources, target role sizes, interrupted navigation, and zero-duration cleanup.
- 60 existing level-transition assertions and 5,021 grid-resize assertions passed, including all distinct 2×2 through 5×5 size pairs, retained-cell geometry, cancellation, safe-area/viewport refits, and zero-duration behavior. Results are under `Artifacts/LevelTransition/DamageEndingRegression/720x1280/` (grid checks in `GridResize/`).
- Native frame sequences for single, rapid, fatal-after-recent-hit, and reserve damage were inspected at both portrait sizes. Rotation, scaling, and movement were active; rapid/fatal clips also used active Reverse. No blank handoff, visible grid reset, second report entrance, or lingering damage wash was observed. Frame timestamps show the terminal presentation finishing approximately 0.9 seconds after acceptance.

Evidence: `Artifacts/DamageEnding/After/405x900/` and `Artifacts/DamageEnding/After/720x1280/`. Each scenario includes PNG frames, `timestamps.csv`, `events.csv`, a `preview.gif`, and a contact sheet. Only after-change captures were obtained for this implementation; older motion captures elsewhere in Artifacts are not a matched baseline for this task.
