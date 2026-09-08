# Seamless level transitions

Ordinary campaign and debug levels advance in the existing gameplay screen. A compact `LEVEL n COMPLETE` overlay accompanies one shared timeline: the remaining-tap display counts from zero to the next requirement, the normal timer interpolates to its new full value, and the grid changes its palette and target layout. There is no intermediate result page or Next button. The final configured level retains the existing victory and reward flow.

## Timing and presentation

Tune `levelTransitionDuration` (default **1 second**) and `levelTransitionEasing` (default **SmoothStep**) in `Assets/Resources/NeonMotionSettings.asset`. The integer count uses linear normalized time so its duration is independent of the tap requirement; the timer, palettes, grid transform and target crossfade use the selected bounded easing. The popup has a short entrance and exit within that same duration. Reduced Effects suppresses popup scale travel.

`GameManager.LevelTransition.cs` owns the clock and flow gate. `RogueliteUIController` renders the popup and temporary HUD values; routine HUD refreshes cannot overwrite them. `GameSquare` accepts externally driven palette and target changes. Matching grid dimensions reuse the cells and interpolate their displayed transform into the new layout. Different dimensions retain the actual outgoing grid while the prepared incoming grid fades in, then dispose of the outgoing objects. No second level introduction runs.

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
