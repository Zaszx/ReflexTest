# Profile debug tools

In Unity, open **Neon Reflex → Debug → Profile Tools**, then enter Play Mode in SampleScene. The window shows the current banked balance and all four permanent upgrade tiers.

- **+1,000 COINS** adds 1,000 banked coins per click and saves the profile. The balance caps at the supported maximum rather than overflowing.
- **RESET ALL UPGRADES** returns every permanent upgrade tier to zero and saves the profile. Coins, banking metadata and the active run are preserved. An existing run retains its starting upgrade snapshot; a new run uses the reset tiers.

The shop and menu refresh immediately after either action. The controls are disabled until the game initializes. Save failures use the game's existing retry path and are reported in the window's feedback and Unity Console.

The window lives under `Assets/Editor`. Its GameManager API is entirely inside `#if UNITY_EDITOR` in `GameManager.EditorDebug.cs`. Both controls and their mutation API are absent from all player builds, including development builds. They create no scene objects or runtime buttons.

The isolated editor QA command is `{"action":"profile-debug-tests","stage":"ProfileDebugAndDifficulty","width":720,"height":1280,"full":false}` through `.utmp/neon-ui-command.json`. It uses temporary QA storage and covers repeat grants, saved tiers and balance, reset idempotence, live wallet refresh, retained active-run data, baseline upgrades on the next run, coin overflow and early 4×4 grid creation.

Validation: 51 runtime checks passed at 720×1280, followed by 48 EditMode regression tests with no failures. Runtime results and captures are in `Artifacts/UI/ProfileDebugAndDifficulty/720x1280`; the test report is `Artifacts/UI/editmode-results.xml`. The asset diff independently confirms all 100 timers are exactly half their previous values, with no changed asset fields beyond time limits and the five grid sizes.

# Campaign difficulty update

All 100 campaign time limits were halved once. Correct-tap objectives, reserve time, modifiers, rewards, IDs and campaign ordering stay unchanged. Levels 6–10 now use 4×4 grids, bringing the campaign to 12 levels with 3×3 grids, 39 with 4×4 grids and 49 with 5×5 grids. The first five modifier introductions keep their 3×3 layouts.

The campaign generator uses the same timer multiplier and early 4×4 block, so regeneration preserves these changes. Saved runs keep their existing snapshot; newly initialized levels use the updated assets.
