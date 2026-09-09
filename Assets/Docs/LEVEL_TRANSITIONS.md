# Seamless level transitions

Ordinary campaign and debug levels advance in the existing gameplay screen. A compact `LEVEL n COMPLETE` overlay accompanies one shared timeline: the remaining-tap display counts from zero to the next requirement, the normal timer interpolates to its new full value, and the grid changes its palette and target layout. There is no intermediate result page or Next button. The final configured level retains the existing victory and reward flow.

## Timing and presentation

Tune `levelTransitionDuration` (default **1 second**) and `levelTransitionEasing` (default **SmoothStep**) in `Assets/Resources/NeonMotionSettings.asset`. The integer count uses linear normalized time so its duration is independent of the tap requirement; the timer, palettes, grid transform and target crossfade use the selected bounded easing. The popup has a short entrance and exit within that same duration. Reduced Effects suppresses popup scale travel.

`GameManager.LevelTransition.cs` owns the clock and flow gate. `RogueliteUIController` renders the popup and temporary HUD values; routine HUD refreshes cannot overwrite them. `GameSquare` accepts externally driven palette and target changes. Matching grid dimensions reuse the cells and interpolate their displayed transform into the new layout. No second level introduction runs.

Different dimensions use `GameManager.GridResizeTransition.cs`. The top-left `min(oldSize, newSize)` square keeps the actual `GameSquare` objects fully opaque. For growth, the shared cells shrink and move into the top-left part of the new layout over the first 60% of the timeline; new rows and columns reveal outward during the last 40%. For shrinkage, outside rows and columns disappear from the outside inward during the first 40%; the surviving cells then expand to fill the new board during the remaining 60%. Multiple rows/columns use staggered square shells, so a 2×2 ↔ 5×5 jump follows the same mechanism as 3×3 ↔ 4×4. The corner is relative to the board as it rotates.

The existing grid transform blends from its last rotation, scale and movement position into the next level's prepared pose during the resize phase. A visual-only fit prevents intermediate rotated bounds from extending outside the gameplay area. Cell layout is temporarily driven explicitly; its normalized coordinates also support viewport refits. The ordinary `GridLayoutGroup` resumes at the exact endpoint. New cells alone are instantiated; removed cells alone are retired. Navigation/cancellation immediately settles this same endpoint and cleans up removed cells. Reduced Effects retains the structural resize and fades but suppresses the additional spawn/removal scale effect. The former two-board crossfade is removed.

The existing background and HUD semantic colors remain consistent. Level-specific colors blend through the cell surfaces, target outlines and popup accent. The decorative `CellBoundary` removed in the flicker correction stays removed.

## State, input and persistence

Completing the old objective awards its reward once, advances the level index, initializes the next level's zero objective, full normal timer, target sequence, PRNG and modifiers, and saves that prepared checkpoint before animating. Health and reserve carry forward exactly; upgrade bonuses are not reapplied. Reserve completion starts the normal timer display at zero. Presentation never modifies earned progress, consumes time, advances modifiers or awards resources.

`FlowState.LevelTransition` rejects gameplay taps. A same-frame readiness guard also rejects taps on the frame the transition ends. Input remains pointer-down based, so holding a press does not queue an action. Settings and application suspension freeze both the timeline and simulation; the first resumed frame cannot catch up elapsed background time.

Home cancels and settles the presentation before navigation. Continue loads the prepared next level directly. New transition checkpoints have `betweenLevels = false`; the existing loader still handles older saves with `betweenLevels = true`. No save schema migration is required. Final victory banks through the existing terminal guard; debug runs never mutate the real profile.

## Difficulty update

All 104 existing level assets have exactly doubled `requiredCorrectClicks`: 100 campaign levels plus four legacy assets. The campaign now ranges from 30 to 160 required taps; legacy requirements are 120, 140, 150 and 170. `CampaignTestGenerator` generates the doubled campaign values too. Other level fields, including timers and modifier speeds, are unchanged in this pass.

## Verification

Native Unity 6.3 editor validation on September 8, 2026:

- 48 isolated EditMode cases passed, including target presentation, palette replacement and unchanged hit rectangles.
- 64 dedicated transition checks passed: exact next checkpoint and PRNG, count-up without earned progress, fresh normal time, carried resources, duplicate completion/tap rejection, matching cell reuse, changed dimensions, Settings, backgrounding, Home, disk reload/Continue, final victory and debug isolation.
- 72 existing runtime motion checks passed after adapting ordinary completion to automatic advance.
- Recorded 720 x 1280 native sequences exercise a 20-tap timer decrease from a grid rotated by 37 degrees and a 160-tap transition from reserve with changed grid dimensions/modifiers. Identical and strongly different palettes were inspected. Final captures and timestamp CSVs are under `Artifacts/LevelTransition/Final/720x1280`; the runs reached playable state at 0.980 and 0.999 seconds from their initial captured frame.
- Independently compared all 104 assets with their previous requirements: zero doubling mismatches and one changed field per asset.

The editor command bridge accepts `level-transition-tests` and `level-transition-capture` through `.utmp/neon-ui-command.json`, for example:

```json
{"action":"level-transition-capture","stage":"Review","width":720,"height":1280,"full":false}
```

Run with the editor outside Play mode. Every QA session uses a fresh profile under `.utmp`; it does not touch the player's profile. Capture frames are read during the sequence and encoded afterward to avoid PNG compression distorting the animation clock. Generated evidence and temporary profiles are ignored by Git. Validation was in the desktop Unity editor; physical Android rendering and touch have not been tested in this pass.

## Retained-cell resize verification (September 9, 2026)

The resize suite exercises all 12 ordered dimension changes among 2×2, 3×3, 4×4 and 5×5, plus Reduced Effects, zero duration, an interrupted shrinking run, and a mid-transition viewport refit. It checks retained object identity, row-major coordinates, initial world-corner continuity, staged row/column visibility, opaque shared cells, unchanged authoritative state and disk checkpoints, input gating, and an exact handoff from animated geometry to Unity's normal layout. Rotation, scale and movement are enabled in these fixtures. Settings/backgrounding and Home/Continue are also exercised while cells are being removed.

- 720×1280: all 14 base/special fixtures passed, with 4,980 per-cell, corner and state assertions. Native snapshots at 0%, 25%, 50%, 75% and 100% were inspected for 3×3 → 4×4, 4×4 → 3×3, 2×2 → 5×5 and 5×5 → 2×2, plus reduced/instant variants. Evidence: `Artifacts/LevelTransition/RetainedCellsVisual/720x1280/GridResize`.
- 405×900 with 48 px top / 32 px bottom safe-area insets: all 16 fixtures passed, including shrinking-run cancellation and the viewport refit, with 5,021 per-cell, corner and state assertions. Evidence: `Artifacts/LevelTransition/RetainedCellsFinalSafe/405x900/GridResize/checks.txt`.
- The existing 60 transition regression checks passed, including same-size reuse, reward idempotence, carried health/reserve, Settings, suspension, Home, disk reload/Continue, campaign victory and debug isolation. Evidence: `Artifacts/LevelTransition/RetainedCellsRegression/720x1280/checks.txt`.

The bridge also accepts `grid-resize-tests` and `grid-resize-capture`. The latter captures deterministic milestone images with simulation disabled while presentation is explicitly advanced; these images show geometry at exact progress values, not measured frame pacing. All validation above uses isolated Unity Editor sessions, not physical Android hardware.
