# Grid edge stability

## Current correction: remove the decorative cell boundary

The user still observed flicker after the antialiasing change below. Disabling the `PrecisionCellFrame` component on `CellBoundary` removed the flicker while the grid continued moving. `GameSquare.Setup` now omits that separate decorative layer. Filled cells define the grid, and `PrecisionOutline` and `RetiringTarget` still provide the gameplay target visuals. Tap feedback continues through the cell background tint.

The earlier synthetic coverage measurements demonstrated an improvement in that probe, but did not establish that the user's real gameplay flicker was resolved.

## Earlier antialiasing work

The September 8 recording exposed subpixel aliasing in the moving grid. The 1.2-unit hard rectangle borders became less than half a screen pixel wide in the small Game view. Scaling and translation made entire rows of those borders switch between covered and uncovered pixels; rotation produced stair-stepped edges.

`NeonGridAntialias` is a shared Resources material with analytic rectangle coverage. Its shader uses screen derivatives to smooth filled cells, full outlines, and retiring corner outlines. Outer-minus-inner coverage preserves thin border brightness. Mesh padding provides space for edge coverage without changing the RectTransform or its raycast surface. Both newly parented and existing canvases request the UV1 shape data. Unsupported shaders retain the original frame renderer and Image fill.

Level objectives, modifier speeds, motion math, role transitions, timers, and input rules are unchanged by this rendering fix.

## Reproduce the rendering check

With the Editor out of Play Mode, write this to `.utmp/neon-ui-command.json`:

```json
{"action":"flicker","stage":"After","width":405,"height":720,"full":false}
```

Repeat at 1080×1920. The existing QA bootstrap uses a new isolated profile. Output is ignored under `Artifacts/Flicker`, including native PNGs, the subpixel probe CSV, motion snapshots, and raycast checks. Capture readback limits frame cadence; these captures are not performance benchmarks.

The stationary probe translates a white 64×64 pixel frame with a 0.45-pixel border through 24 fractional-pixel positions at fixed 0° and 27° angles. Compare the temporal range of `roiMean`, divided by its temporal mean. At 405×720, the range fell from 56.63% to 0.275% for the axis-aligned border and from 2.63% to 0.064% at 27°. The CSV's other ROI statistics describe spatial variation within one image, not temporal flicker.

Actual Unity captures at both resolutions exercise Level 19 scaling, Level 3 rotation, Level 5 movement, and Level 100 combined motion with a target change. All captured target-center raycasts passed. Physical Android device rendering has not been measured.

Final regression validation: 46 isolated EditMode cases passed (0 failed), and 72 live runtime checks passed (0 failed). Results are under `Artifacts/UI/editmode-results.xml` and `Artifacts/Motion/FlickerFix/1080x1920/runtime-checks.txt`.
