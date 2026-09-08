# Neon Reflex presentation

This document records the original UI redesign and its validation. The subsequent animation pass, current motion owners and separate runtime recordings are documented in [MOTION_DESIGN.md](MOTION_DESIGN.md).

## Main menu reference update (September 9, 2026)

The main menu follows the supplied cyberpunk reference: a full-bleed neon corridor and reflective floor, blue/pink vector wordmark, gently turning nested luminous squares, a compact banked-coin wallet, and three stacked glowing controls. Start/Continue, Upgrades and Settings keep their existing callbacks. The wallet plus opens Upgrades. An active run adds a compact level/health/reserve/pending-coins readout and the existing End Active Run action; canceling its confirmation keeps the run. Development builds retain the small Practice link.

`RogueliteUIController.Menu.cs` owns layout and bindings; `NeonMenuGraphic.cs` draws the live lettering, iconography and continuous glow falloff. The existing `NeonOpticalMotion` respects Reduced Effects and application suspension. The background uses an aspect-preserving fill while interactive elements respect `NeonSafeArea`. Background artwork is `Assets/Resources/MenuArt/neon-corridor.png`; its exact built-in image-generation prompt is in [MENU_BACKGROUND_PROMPT.md](MENU_BACKGROUND_PROMPT.md). Text, buttons, coin values and the square motif are not baked into that image.

The isolated editor command `{"action":"menu-capture","stage":"MenuReview","width":720,"height":1280,"full":false}` captures fresh, active and abandonment-canceled menu states. `NeonMenuQA` drives native UI raycasts for all primary controls, the wallet, Continue and cancellation. It checks saved level/progress/resources/target sequence/PRNG preservation and text overflow. The bridge supports `safeTop` and `safeBottom` pixel insets for additional viewport checks. QA uses a temporary isolated profile, not the player's save.

## Upgrade shop reference update (September 8, 2026)

The upgrade shop now follows the supplied neon-card reference: four chamfered navy cards, green/gold/cyan/violet category colors, large glowing vector symbols, current-to-next benefits, discrete tier segments, and separate coin-price/purchase panels. The heading and wallet are compact enough to show all four cards on a portrait phone; shorter viewports retain the existing scroll behavior. Affordable, insufficient-funds, active-run and maximum-tier states remain explicit. Values and tier counts use the configured catalog, so they need not equal illustrative numbers in the reference.

`Assets/Scripts/Roguelite/RogueliteUIController.Shop.cs` owns the shop layout, palette and binding. `NeonUpgradeGraphic.cs` draws its card frames, icon sockets and symbols as native UI meshes with soft stroke layers; it has no update loop or external image dependency. Other screens retain their existing theme. Purchase callbacks still use `GameManager` and `UpgradeCatalog`, including their active-run restriction and persistence. No balance, save format or gameplay changes are part of this shop update.

The isolated editor bridge accepts `{"action":"shop-capture","stage":"UpgradeReview","width":720,"height":1280,"full":false}` through `.utmp/neon-ui-command.json`. It captures affordable, purchased, unaffordable, active-run, maxed and scrolled states, checks label overflow and verifies real purchase cost/tier changes and rejection during a run. Evidence is saved under `Artifacts/UI/<stage>/<resolution>`; the player's profile is not used.

The interface uses an optical-instrument direction: ink green surfaces, warm white typography, acid-lime actions and precise geometric outlines. Cyan identifies normal targets, violet identifies Reverse, amber identifies reserve use, and coral identifies damage. The nested title mark comes from the game's size-reading mechanic. Controls and typography are native uGUI/TMP; the geometric marks are native UI meshes.

## Gameplay reference update (September 9, 2026)

The gameplay screen follows the supplied portrait reference: chamfered navy panels, cyan time and target cues, an amber reserve strip, lime health segments, a static bracket around the board, a faint perspective floor, and cyan Home/magenta Pause controls. Actual health, time, reserve, pending coins, required taps and remaining Reverse taps come from the run; illustrative reference values are not hard-coded. LEFT shows remaining taps out of the level's required total, counting down during play and retaining the existing count-up during the in-place level transition. The screenshot viewer's Edit/share controls are excluded.

Gameplay HUD decoration is built from native uGUI meshes in `Assets/Scripts/Roguelite/NeonGameplayGraphic.cs`. It supplies static panel, board, icon, clock, floor and segmented health visuals; callers update values and mark the graphic dirty, with no per-frame graphic update loop. `RogueliteUIController.Gameplay.cs` owns the layout and keeps the existing GameManager callbacks and HUD values. Gameplay panels use their own palette; the menu, upgrade shop and settings retain their existing builders.

Grid cells use the analytic `NeonGridAntialias` shader and `NeonGridRendering` payload for coverage-aware borders and fills. The redesigned gameplay presentation does not add a CellBoundary or PrecisionCellFrame decorative path. Grid difficulty, target roles, movement, scale, rotation, Reverse behavior and hit rectangles remain authored by the existing gameplay systems.

The isolated editor bridge accepts `{"action":"gameplay-capture","stage":"GameplayReview","width":720,"height":1280,"full":false}`. `NeonGameplayQA` captures normal gameplay, low health, reserve drain, a Reverse HUD fixture, maximum-value typography and a resumed run. It checks active text overflow, noninteractive decoration, and native Pause/Resume/Home/Continue navigation with resource retention. Safe-inset checks use `safeTop` and `safeBottom` in pixels. Captures are under `Artifacts/UI/GameplayFinal`; all profile writes use temporary QA storage.

For this update, gameplay capture/navigation checks passed 110 assertions at 720×1280 and at 405×900 with simulated 44/24-pixel top/bottom safe insets. The 720×1280 runtime motion suite passed 72 checks and the transition suite passed 60 checks. At 401×660 the scale, rotation, movement and combined-modifier recordings each sampled 60 frames with zero target-raycast failures and verified that their expected motion advanced. These editor recordings are under `Artifacts/Motion/GameplayFinal`, `Artifacts/LevelTransition/GameplayFinal` and `Artifacts/Flicker/GameplayFinal`; they do not claim physical Android-device validation. The final normal-state preview is `Artifacts/UI/GameplayFinal/720x1280/01-normal-gameplay.png`.

## Ownership and tuning

- `Assets/Resources/NeonPresentationTheme.asset` contains the shared palette, font reference, type sizes, spacing, line treatment and presentation timing. The bundled TMP font travels with the project.
- `Assets/Scripts/Roguelite/RogueliteUIController.cs` is the single runtime screen builder. It preserves the existing scene's panel and grid roots, replaces legacy panel children once, and reconnects the resulting controls through `GameManager`. Edit this builder rather than making competing changes to runtime-generated children.
- `NeonStyle.cs` provides reusable typography, buttons, safe-area layout and panel entrances. `NeonShape.cs` contains the shared geometric icon family.
- `GameSquare.cs` draws immediate, discrete Small/Medium/Large outlines and cell-local tap feedback. Feedback does not change the target dimensions or the cell's hit rectangle.
- `GameplayFeedbackController.cs` owns the rule-change announcement within the existing Reverse entrance/exit windows. The upright persistent rule indicator belongs to the HUD.

The HUD palette remains stable between levels. A small contribution from each level's authored palette remains on the cells/targets through `NeonTheme.LevelTarget`; loading a level cannot restore the old global interface colors. The screen's actual laid-out bounds drive grid fitting, including subsequent viewport or safe-area changes. Fitting preserves configured scale ranges and motion phases.

`GameManager`, `GameplayTimerRules`, `GridSequenceRules`, `GridMotionMath`, `UpgradeCatalog`, `RunEconomyRules` and `NeonSaveService` remain authoritative. Presentation code does not own reward transactions, timer state, input locks or an independent wallet. Shop descriptions and current/next benefits come from the configured upgrade definitions. Grid Stabilizer affects rotation, movement and scale; Reverse Resistance adds post-Reverse cooldown.

## Changed project files

| Area | Main files |
| --- | --- |
| Screens and shared design | `Roguelite/RogueliteUIController.cs`, `Roguelite/NeonStyle.cs`, `Roguelite/NeonShape.cs`, `Roguelite/NeonTheme.cs`, `Resources/NeonPresentationTheme.asset` |
| Runtime integration | `GameManager.cs` |
| Grid and rule-change feedback | `GameSquare.cs`, `GameplayFeedbackController.cs` |
| Practice navigation | `DebugLevelSelectController.cs` |
| Necessary persistence/report corrections | `Roguelite/NeonSaveService.cs`, `Roguelite/RunEconomyRules.cs` |
| Isolated capture and regression evidence | `Editor/NeonPresentationQA.cs`, `Roguelite/NeonPresentationQAProfile.cs`, `Tests/EditMode/NeonReflexRulesEditModeTests.cs` |

Script paths in this table are under `Assets/Scripts` unless an `Editor`, `Resources` or `Tests` prefix is shown. Scene and prefab references are preserved: `SampleScene`, the square prefab, campaign definition, 100 configured level assets, `GameConfig` balance and project settings are unchanged. No campaign generation is needed to use the redesign.

## Necessary integration corrections

The run summary previously counted the next available level as entered when a run ended between levels. `RunEconomyRules` now reports the last level actually entered while retaining completed-level counts and rewards.

Transition completion is also recorded correctly if Settings opens on an animation's final yielded frame. The controller records the state to resume without advancing simulation behind Settings, preventing a completed intro or Reverse transition from leaving the run locked. Cancelling the level intro resets its decorative text scale.

Unity can report a newly enabled modal graphic with depth -1 until its first rendered frame. The UI now disables underlying panels' input immediately when Settings, the shop or the abandon dialog opens, then restores it when the covering panel closes. This removes the first-frame input gap without depending on animation completion or changing simulation pause ownership. QA checks both immediate underlay blocking and the modal's settled native raycast order.

Regression tests also exposed a pre-existing Unity serialization defect: serializing a null inline `ActiveRunData` emitted a default empty object, which the loader rejected. Wallet-only and freshly banked saves could consequently reload as a fresh profile. `NeonSaveService` now omits `activeRun` when no run exists and recognizes the exact legacy empty representation. Real active-run identity and structure validation remain intact. Tests cover wallet/upgrades/bank-marker preservation, legacy compatibility and backup recovery for a damaged real run. No save-version migration, campaign regeneration, reward rebalance or player-data reset is involved.

The editor's rapid-purchase capture also exposed occasional `File.Replace` failures with the message "Unable to remove the file to be replaced." Later saves and disk-reload assertions succeeded; the source of the transient file sharing could not be established from that message. Atomic replacement now retries `IOException` up to three attempts with 8 ms between attempts. Persistent failures still propagate to the controller's existing dirty-save path, and an I/O error does not switch to a non-atomic overwrite. The pre-existing unsupported-platform fallback remains unchanged.

## Repeatable isolated QA

Open `SampleScene` and use **Neon Reflex > Presentation QA > Capture After (isolated)**. The explicit editor harness creates a unique profile below the project's `.utmp` directory before `GameManager.Start`. In this session, save writes use that directory; legacy migration and preference writes are suppressed. The harness does not save scenes, regenerate the campaign or edit the player's real profile. It uses the real Unity Canvas, scene controller and gameplay methods with isolated fixture data to reach otherwise lengthy campaign states.

Use **Neon Reflex > Presentation QA > Run regression tests (no player preferences)** for the focused suite. The existing legacy PlayerPrefs migration test is deliberately excluded because it writes the real preference keys during execution, even though it later restores them. The other save tests use temporary directories.

`Assets/Editor/NeonPresentationQA.cs` also accepts explicit capture commands through `.utmp/neon-ui-command.json`, including dimensions and simulated top/bottom safe-area insets. The harness saves native runtime PNGs, accompanying text/layout snapshots and `runtime-checks.txt` under `Artifacts/UI/After/<viewport>`. The before baseline is under `Artifacts/UI/Before/1080x1920`. These are Unity runtime captures, not browser recreations.

For an interactive isolated preview, write the following to `.utmp/neon-ui-command.json` while the Editor is out of Play Mode:

```json
{"action":"preview","stage":"Preview","full":false,"width":1080,"height":1920,"previewState":"menu"}
```

The main menu remains interactive in Play Mode using a fresh isolated profile. Exit with the Editor's Play/Stop control, or write `{"action":"stop"}` to the same command file. A normal Play Mode launch outside this explicit QA workflow uses the game's regular player profile.

At delivery, the Editor was left on this [fresh 0-coin menu preview](../../Artifacts/UI/Preview/1080x1920/01-fresh-menu.png) at 1080×1920 in the isolated session.

## Verification evidence

The final focused EditMode run on 2026-09-05 at 02:06:30 UTC completed with **25 passed, 0 failed, 0 skipped**; see [the XML test result](../../Artifacts/UI/editmode-results.xml). The separately excluded migration test is not included in that count.

The final capture, regression, timing and preview window produced no new save-failed messages, C# compilation errors, exceptions or presentation-QA errors in the Editor log.

The final 1080×1920 runtime suite recorded **41 passing assertions, 0 failures and 29 PNG states**. In addition to progression and persistence checks, it exercised pointer-down through the actual UI raycaster, exactly one tier/cost change per purchase-button click, all three final-frame pause boundaries, immediate underlay blocking before a modal renders, and settled modal raycast order. Its active TMP text snapshots reported no overflow.

The same 41 assertions passed at each of these six layouts. The 29 captured states were rendered at every layout, for 174 after screenshots; the baseline contains four before screenshots.

| Runtime viewport | Simulated safe-area insets | Assertions | PNG states | Evidence |
| --- | --- | --- | --- | --- |
| 720×1280 | None | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/720x1280/runtime-checks.txt) |
| 1080×1920 | None | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/1080x1920/runtime-checks.txt) |
| 1080×2340 | None | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/1080x2340/runtime-checks.txt) |
| 1080×2400 | None | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/1080x2400/runtime-checks.txt) |
| 1080×1920 | Top 120 px, bottom 96 px | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/1080x1920-safe-insets/runtime-checks.txt) |
| 1080×2400 | Top 120 px, bottom 96 px | 41 pass / 0 fail | 29 | [checks](../../Artifacts/UI/After/1080x2400-safe-insets/runtime-checks.txt) |

Runtime assertion details are recorded per captured viewport in `runtime-checks.txt`. These checks cover discrete outline sizes, valid/wrong input, pause semantics, purchase validation, carried resources, next-level normal time, terminal idempotence, persistence, campaign completion, debug isolation and transformed bounds. Captured states include fresh/active menus, shop states, gameplay resources, Reverse, results and utility panels. Check the recorded assertion and capture files for the exact execution evidence; a screenshot of a fixture does not by itself prove a transaction or pause behavior.

Before evidence: [main menu](../../Artifacts/UI/Before/1080x1920/01-fresh-menu.png), [gameplay](../../Artifacts/UI/Before/1080x1920/04-normal-gameplay.png).

Implemented runtime evidence: [main menu](../../Artifacts/UI/After/1080x1920/01-fresh-menu.png), [gameplay](../../Artifacts/UI/After/1080x1920/04-normal-gameplay.png), [upgrade shop](../../Artifacts/UI/After/1080x1920/12-shop-affordable.png), [active Reverse with combined modifiers](../../Artifacts/UI/After/1080x1920/21-reverse-combined-maximum-scale.png), [reserve use](../../Artifacts/UI/After/1080x1920/07-reserve-active.png), [level complete](../../Artifacts/UI/After/1080x1920/16-level-complete.png), [campaign complete](../../Artifacts/UI/After/1080x1920/19-campaign-complete.png), [pause/settings](../../Artifacts/UI/After/1080x1920/05-settings-over-gameplay.png), [abandon confirmation](../../Artifacts/UI/After/1080x1920/10-abandon-confirmation.png).

A separate active Level 100 editor sample at 1080×1920 used a 2-second warmup and 10.005-second measurement: 679 frames, **14.442 ms median, 17.684 ms p95, 21.606 ms maximum**, and 67.87 observed frames/second. Rotation, scale, movement and the level timer remained active. This was Unity 6000.3.11f1 on an i7-6700HQ/GTX 950M, with targetFrameRate -1 and vSync 0. The sample includes Editor/harness overhead and has no device or before/after baseline comparison. See the [measurement context](../../Artifacts/UI/Performance/1080x1920/editor-performance.txt) and [raw frame times](../../Artifacts/UI/Performance/1080x1920/editor-frame-times.csv); GPU timing and UI allocation attribution were not measured.

No Android device performance, APK build, platform haptic hardware or physical system-gesture behavior is claimed by the editor captures. The preset viewport tests and simulated safe areas exercise layout and bounds inside Unity.

One measured touchability limit remains: at 720×1280, the configured Level 100 5×5 grid with combined modifiers and 43° rotation has a cell edge of **52.65 physical pixels at minimum scale and 61.94 pixels at maximum scale**. Both are below the configured 64-pixel target. The cells remain contained and visually discrete; the existing modifier ranges and level difficulty were retained. This is an explicit physical-device touchability validation item, not a claimed handset pass.

## Run report reference update

`RogueliteUIController.RunReport.cs` builds the failure report over the existing neon corridor: a fractured red square for **HEALTH DEPLETED**, an amber empty hourglass for **OUT OF TIME**, a lime banked-earnings card, paired level statistics, permanent balance, and Upgrades / Return Home buttons. The timeout report appears when both normal time and reserve are exhausted. Abandoned runs say **RUN CLOSED / RUN ABANDONED**; practice says **PRACTICE OVER / NO COINS BANKED** and retains its Return to Menu action. Campaign victory retains its existing presentation.

`NeonRunReportGraphic` provides native noninteractive card and icon meshes. `NeonRunReportLayout` fits a 1080×1800 artboard uniformly inside the safe area while cropping the full-bleed background independently. Result entrances use the existing motion/reduced-effects system; action buttons remain outside noninteractive animation groups. `NeonReportTextGlow.mat` references the existing TMP distance-field shader and its glow variant for build inclusion. TMP owns each label's material instance and font atlas.

Reports display `RunSummaryData` after the existing terminal transaction; the presentation never credits coins or edits the saved run. `NeonRunReportQA` exercises real isolated GameManager sessions, persisted 168-coin banking (799 → 967), repeated terminal callbacks, natural reserve expiry, native button raycasts, long numeric values, abandoned runs, and practice profile isolation. Run it through the existing command bridge with `action: run-report-capture`. Captures and checks are under `Artifacts/UI/RunReportFinal`, including health, timeout, long-value, abandoned, and practice screenshots per viewport.

The final version passed **125 runtime checks at 720×1280** and **125 at 405×900 with 48 px top / 32 px bottom safe-area insets**. Both sets contain five actual Unity PNGs and text/control snapshots with no report text overflow. These are Editor layout and interaction checks, not an Android device or APK test.
