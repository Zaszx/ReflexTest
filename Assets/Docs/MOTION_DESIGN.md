# Neon Reflex motion design

This pass keeps the existing optical interface and adds short, interruptible motion around authoritative state changes. Taps, damage, timer transitions, purchases and banking commit through the existing game systems. The visual layer follows the latest state; completing an effect never awards coins, advances a sequence or authorizes another tap.

## Motion ownership and tuning

`Assets/Resources/NeonMotionSettings.asset` is the shared tuning asset. `NeonMotionSettings.cs` defines the fields, bounded easing and presentation clock. The default easing is cubic out, with Linear and SmoothStep alternatives; no curve overshoots the role or layout bounds. The old presentation theme's press/entrance timing fields and `NeonPanelEntrance` were replaced by this owner. `NeonPresentationTheme.asset` still owns the palette, typography and line treatment.

| Interaction | Default timing | Presentation owner |
| --- | --- | --- |
| Surviving target role change | 110 ms; interrupted retarget 65 ms | `GameSquare` |
| New target / consumed target ghost | 100 ms / 90 ms | `GameSquare` |
| Correct / wrong cell acknowledgement | 160 ms / 200 ms | `GameSquare` |
| Lost health / objective fill / reserve activation | 200 ms / 120 ms / 220 ms | `NeonHudMotion` |
| Button label press / release | 60 ms / 120 ms | `NeonPressFeedback` |
| Page entrance / exit | 220 ms / 160 ms | `NeonScreenMotion` |
| Modal entrance / exit | 180 ms / 140 ms | `NeonScreenMotion` |
| Purchase confirmation | 280 ms | Per-card `NeonPurchaseMotion`, one shared `NeonWalletMotion` |
| Result details | 320 ms, 45 ms stagger | `NeonResultMotion` |

Travel amounts, target appearance scale/alpha, ghost contraction, role separation and feedback strength are in the same asset. `modalBackdropLead` controls how early the modal's ink background covers the underlay; the default 4 reaches full backdrop opacity at 25% content opacity. The existing level introduction remains 450 ms. Reverse retains its existing 760 ms entrance and 590 ms exit windows; the shared fade/travel settings shape that announcement within those windows. Timing changes here do not modify Grid Stabilizer, Reverse cooldowns, timers or the save schema.

## Targets and fast input

`GameManager` validates and commits the tap first. It then captures the consumed target's current visual and assigns every cell once from the final logical sequence. This removes the old intermediate clear-all presentation step. `GameSquare.currentLitSize` changes immediately; neither its pointer-down callback nor the sequence rules wait for visual interpolation.

Small, Medium and Large have disjoint visual bands derived from the configured sizes and their midpoints. The separation is `targetRoleGap` times the smaller adjacent configured size gap. On reassignment, a target may make a small immediate move into its new semantic band; it then eases to the exact configured size. This prioritizes readable role order during rapid input. New Small targets begin at 82% of their configured size within their band. A new Large starts at its full size and fades into emphasis, so it never passes through a misleading Small/Medium cue.

Every cell preallocates one separate, noninteractive retiring corner graphic. A consumed target fades in that graphic while its active outline can already represent a new role on the same cell. The retiring graphic has a distinct shape and restrained lime tint, with 35% starting alpha and up to 8% contraction. New effects replace the one current effect; there is no list of pending tweens or completion callback capable of hiding a later target.

`NormalizePresentation` settles the current logical role and clears transient tint/ghost state. Its optional `false` argument clears feedback while keeping the current role tween. Disable, level replacement, restore and navigation use normalization. The target frame and ghost are cosmetic children; the cell hit rectangle and authoritative Rotation/Scale/Move transforms remain unchanged.

## Health, time, purchases and results

Health numbers and the primary health fill update immediately. Rapid losses merge one trailing segment spanning only the lost portion of the bar; a small health-label offset restores to its original position without accumulating displacement. The wrong cell receives coral fill/boundary feedback and keeps its logical role. The correct cell receives a different local acknowledgement plus its consumed-target ghost. Fatal damage still ends the run synchronously and can overlap the result entrance.

The objective number uses the real current count while its fill retargets from the current rendered position toward the latest count. A new objective or restored snapshot snaps to its correct baseline. Timer numbers and timer fill have no display lag. Reserve activation changes the real state immediately and adds a short amber underline cue. Hidden HUD snapshots and normalization do not replay damage or reserve activation when the panel returns.

Successful purchases refresh the real tier, next price, benefit and wallet before playing a card wash, tier/benefit underline and filled-track glint. Each row has one feedback owner; all rows share one wallet accent owner. These helpers never write numbers or call the economy service. Result values are the immutable summary already returned after banking; entrance choreography changes opacity and position only. Interrupting either presentation cannot transact again.

## Navigation, pause and cancellation

`RogueliteUIController` builds and registers one `NeonScreenMotion` per production screen. A new visibility request retargets from the current opacity and offset; it replaces the prior destination instead of queuing visits. Outgoing pages stop accepting input immediately. Modal input coverage remains until their exit has completed, including the first enabled frame before Unity assigns a valid Graphic depth. Modal content and backdrop have independent opacity: the background covers before incoming text becomes prominent and remains opaque until departing text is almost gone. This avoids overlapping legible Settings and menu headings without adding transition time. Incoming gameplay fades without translating its SafeContent or any grid-bound ancestor.

Only `GameManager` decides when simulation can resume. Fresh and next-level entrances overlap the existing introduction; Continue uses `LevelIntro` until the reconstructed gameplay screen is ready. Settings keeps its authoritative paused state throughout dismissal. Its hidden callback must match the current request version and expected flow state. A re-open or navigation invalidates that callback. Existing intro/Reverse presentation tokens also guard their completions, including Settings opening on the final yielded frame.

An externally disabled screen can be reactivated by the same visibility request, including the shop's repeated-open path. If external disabling interrupts a modal already being dismissed, its presentation owner reports the hidden continuation once; the controller still validates the request before resuming. A disabled motion component settles navigation immediately. These paths avoid an inactive screen waiting for an Update it cannot receive.

| Clock / effect | Settings open | Application pause or focus loss |
| --- | --- | --- |
| Existing gameplay simulation | Suspended by flow state | Suspended by combined pause/focus state |
| Target, wrong-cell, health, objective and reserve feedback | Frozen at current presentation | Frozen |
| Reverse announcement | Frozen by the external pause reason; its own transition lock allows its animation | Frozen |
| Screens, modal dismissal, buttons, menu ornament, purchase and results | Animate so paused navigation remains usable | Frozen |

`NeonMotion.Delta` uses unscaled frame time for cosmetic presentation, accepts an explicit external-pause flag, clamps an active cosmetic step to 100 ms and skips the application-resume frame. The existing intro and Reverse window timelines use `NeonMotion.TransitionDelta`: it has the same external-pause/application/resume guards but consumes the full active unscaled frame delta. Thus an active frame hitch above 100 ms does not unnecessarily lengthen those established gameplay locks. Application pause and focus loss are combined rather than letting one resume signal clear the other. No animation samples absolute wall-clock deadlines or catches up a background interval. Intro/Reverse controllers preserve the distinction between their own transition lock and an external suspension.

Disabling a component restores its intended layout and clears its transient effects. Gameplay restore reconstructs logical roles/resources and saved grid motion, then starts from a coherent cosmetic baseline. Decorative interpolation progress is never serialized. Zero-duration effects settle immediately. Reduced effects uses the existing preference: decorative travel and health-label displacement are removed, and accent intensity is multiplied by the central reduced strength; gameplay Rotation, Scale, Move and Reverse remain active.

## Changed files

| Area | Main files |
| --- | --- |
| Shared timing and clock | `Assets/Scripts/Roguelite/NeonMotionSettings.cs`, `Assets/Resources/NeonMotionSettings.asset` |
| Screens, input and results | `Assets/Scripts/Roguelite/NeonScreenMotion.cs`, `Assets/Scripts/Roguelite/RogueliteUIController.cs`, `Assets/Scripts/GameManager.cs` |
| Target and Reverse motion | `Assets/Scripts/GameSquare.cs`, `Assets/Scripts/GameplayFeedbackController.cs` |
| HUD, purchase and controls | `Assets/Scripts/Roguelite/NeonHudMotion.cs`, `Assets/Scripts/Roguelite/NeonStyle.cs` |
| Superseded timing removal | `Assets/Scripts/Roguelite/NeonTheme.cs`, `Assets/Resources/NeonPresentationTheme.asset` |
| Isolated recording and regression | `Assets/Editor/NeonMotionQA.cs`, `Assets/Editor/NeonPresentationQA.cs`, `Assets/Tests/EditMode/GameSquareMotionEditModeTests.cs`, `Assets/Tests/EditMode/NeonHudMotionEditModeTests.cs`, test assembly references |

New scripts/assets include Unity metadata. The scene, square prefab, campaign definition, 100 level assets, `GameConfig`, save/economy rules and player profile are unchanged by this motion pass. The prior UI redesign and its distinct evidence are documented in [PRESENTATION_REDESIGN.md](PRESENTATION_REDESIGN.md).

## Isolated reproduction and evidence

The existing editor QA bootstrap creates a unique profile under `.utmp` before `GameManager.Start`. In this explicit session, disk saves stay in that directory and legacy migration/preference writes are suppressed. No scene or player profile is overwritten. Fixtures reach campaign and economy states through the real controller and UI, with development-only state setup where needed. The capture fixture explicitly marks the game foreground before recording so that OS focus on Codex does not freeze the capture. Separate lifecycle checks inject focus/pause callbacks into the real controller; they do not establish mobile OS lifecycle behavior.

With `SampleScene` open and the Editor outside Play Mode, write a command to `.utmp/neon-ui-command.json`:

```json
{"action":"motion","stage":"After","full":false,"width":720,"height":1280}
```

The runtime recorder captures native Unity rendered frames and writes `timestamps.csv` and `events.csv` beside each frame sequence under `Artifacts/Motion/After/<viewport>`. The baseline has six sequences and 223 PNG frames under [Before/720x1280](../../Artifacts/Motion/Before/720x1280). The local [motion player](../../Artifacts/Motion/index.html) uses the recorded timestamps for playback and can compare the paired interactions. `Artifacts/Motion/build-player.py` rebuilds that artifact after new recordings.

```json
{"action":"motion-tests","stage":"After","width":720,"height":1280}
```

The integration command exercises motion lifecycle against the isolated runtime. The existing `tests` command also selects the new target and HUD EditMode test classes. Test settings are cloned and restored so cases do not mutate the authored motion asset. The legacy PlayerPrefs migration test remains deliberately excluded because it writes real preference keys.

## Verification evidence

The final focused EditMode run at **2026-09-05 11:52:41 UTC passed 44 tests, with 0 failed and 0 skipped**: 25 existing gameplay/economy/save cases, 16 target-motion cases and 3 HUD cases. See [the XML result](../../Artifacts/UI/editmode-results.xml). Target cases include 1,000 rapid assignments in each rule direction, distinct role bands, same-cell reuse, external pause, disable/restore and zero durations. Explicit 1/120 s, 1/60 s, 1/30 s and 100 ms presentation steps check both rule orders and exact final states. HUD cases check rapid lost-segment merging without writing primary-health/timer geometry, hidden snapshots without replay and immediate zero-duration final states. These deterministic step tests do not establish device frame rates.

The final isolated runtime matrix recorded **72 passing checks and 0 failures at each of three viewports, 216 passing checks total**:

| Viewport | Simulated safe-area insets | Completed (UTC, 2026-09-05) | Runtime evidence |
| --- | --- | --- | --- |
| 720×1280 | None | 11:41:30 | [72 passing checks](../../Artifacts/Motion/After/720x1280/runtime-checks.txt) |
| 1080×1920 | None | 11:48:24 | [72 passing checks](../../Artifacts/Motion/After/1080x1920/runtime-checks.txt) |
| 1080×2400 | Top 120 px, bottom 96 px | 11:51:01 | [72 passing checks](../../Artifacts/Motion/After/1080x2400-safe-insets/runtime-checks.txt) |

The matrix exercised:

- Eight accepted pointer-downs in one frame in both Normal and Reverse, immediate reuse of the consumed cell, unchanged pooled object counts, and strictly ordered/settled target roles.
- Twelve mistakes in one frame with exact latest 8/20 health, unchanged objective/sequence, continued timer drain, immediate fatal failure and immediate Reserve expiry.
- Settings during target/damage/Reverse feedback, native raycast input before and after dismissal, home/Continue with exact saved resources, application suspension during screen entry, overlapping navigation and settled layout/opacity. The expanded checks also cover external screen disabling, recovery of a closing modal and rejection of an obsolete Settings-close callback after newer navigation.
- Immediate level reward and next-level entry, ignored post-terminal taps, interruption of banked-result presentation, active-run purchase restrictions, and four rapid purchase-button transactions using the latest price each time.
- Zero-duration navigation and target feedback with reduced effects, preserved gameplay Rotation, and no invisible overlay at settled navigation states.

The final suite additionally checks modal backdrop contrast on entry and exit, the disabled optional screen-owner fallback, and a forced 180 ms active hitch: its measured unscaled frame delta is retained by `TransitionDelta` while cosmetic `Delta` remains capped at 100 ms. Active TMP labels, including the dense-grid Reverse counter, are checked for overflow.

The updated legacy presentation regression also passed **91 checks with 0 failures and 29 screenshots** at **11:53:39 UTC**. It retains the existing progression, save reload, economy, grid bounds, viewport resize and final-yield Settings pause assertions, with explicit readiness waits for the new transitions. See [the regression checks](../../Artifacts/UI/MotionRegression/1080x1920/runtime-checks.txt) and [native screenshots](../../Artifacts/UI/MotionRegression/1080x1920). Its foreground fixture now initializes before menu captures, matching the motion harness.

Actual 720×1280 mistake and purchase frame sequences were also inspected at the event, during feedback and after settling. The mistake frames show the current 2/3 value and primary fill immediately while the lost segment fades, with the same target sequence. Purchase feedback already displays the committed wallet/tier/next price in its first captured frame, then settles without restoring old values.

Frame inspection exposed legible Settings and menu headings overlapping during the first modal crossfade. The modal backdrop/content split was refined and re-recorded. The [refined entrance](../../Artifacts/Motion/After/720x1280/01-screen-navigation/frame-0048.png), [refined exit](../../Artifacts/Motion/After/720x1280/01-screen-navigation/frame-0070.png) and [pause during target feedback](../../Artifacts/Motion/After/720x1280/10-settings-during-target-feedback/frame-0010.png) were inspected in native runtime output: prominent modal text now sits against the clean ink background without underlying headings or grid text showing through. Entry/exit timings and pause ownership remain unchanged.

The completed 720×1280 after recording contains **12 frame sequences and 661 PNG frames**. Its first six pair with the baseline: [screen navigation](../../Artifacts/Motion/After/720x1280/01-screen-navigation), [normal targets](../../Artifacts/Motion/After/720x1280/02-normal-correct-taps), [mistake/health](../../Artifacts/Motion/After/720x1280/03-mistake-health), [Reverse](../../Artifacts/Motion/After/720x1280/04-reverse-sequence), [level completion](../../Artifacts/Motion/After/720x1280/05-level-complete), and [purchase](../../Artifacts/Motion/After/720x1280/06-shop-purchase). Additional sequences cover rapid input with combined modifiers, fatal damage, Reserve entry/expiry, Settings during target feedback, immediate consumed-cell reuse and rapid Reverse taps.

The final recording inventory contains **6 before sequences and 24 after sequences**, with **223 before and 1,230 after PNG frames**. Every timestamp CSV row count matches its PNG count exactly; there are no stale surplus frames. Active TMP layout dumps in the after recordings report no text overflow.

The [machine-readable validation summary](../../Artifacts/Motion/validation-summary.json) records the final runs and capture inventory. The player was rebuilt for all 30 sequences; its JavaScript syntax check passed and the local server returned HTTP 200. Its browser controls were not tested through browser automation. Native frame sequences and timestamp/event CSVs are the primary visual evidence.

| Recording set | Sequences | PNG frames | Actual capture rate across clips |
| --- | --- | --- | --- |
| [Before / 720×1280](../../Artifacts/Motion/Before/720x1280) | 6 | 223 | 16.10–18.15 Hz |
| [After / 720×1280](../../Artifacts/Motion/After/720x1280) | 12 | 661 | 29.66–30.02 Hz |
| [After / 1080×1920](../../Artifacts/Motion/After/1080x1920) | 6 | 323 | 22.99–25.69 Hz |
| [After / 1080×2400 with safe insets](../../Artifacts/Motion/After/1080x2400-safe-insets) | 6 | 246 | 16.47–19.36 Hz |

The capture process performs synchronous GPU readback, so its actual recorded cadence can be lower than the requested 30 Hz. The recorder's scheduling was tightened after the baseline. These capture rates are not a before/after game-performance comparison; recorded timestamps, rather than a fixed frame-rate assumption, describe playback. No phone capture, device touch test, GPU timing or standalone player performance claim is implied by editor frame sequences.

## Editor timing and preview

A separate Editor sample at **11:54:23 UTC** ran isolated Level 100 with Rotation, Scale and Move at 1080×1920. After 2 seconds of warmup, 576 frames over 10.012 seconds measured median **17.106 ms**, p95 **20.840 ms**, mean **17.391 ms** and maximum **26.088 ms**, or 57.53 observed frames/s. The normal timer decreased by 10.017 seconds, rotation advanced from 46.30° to −33.22°, and the final flow was Playing. See the [timing report](../../Artifacts/Motion/Performance/1080x1920/editor-performance.txt) and [raw frame times](../../Artifacts/Motion/Performance/1080x1920/editor-frame-times.csv).

The sample used Unity 6000.3.11f1 on an i7-6700HQ / GTX 950M, with targetFrameRate −1 and vSync 0. No pointer input, screenshot readback or disk work occurred inside the measured loop. It measures active modifier gameplay in the Editor, including Editor/harness overhead; it is not a rapid-tap stress benchmark or a measured before/after performance comparison. Process-wide GC collection deltas were 0/0/0, which does not establish zero UI allocations. GPU timings, profiler allocation attribution and device performance were not measured.

For an interactive fresh menu in the same isolated workflow, stop Play Mode and write:

```json
{"action":"preview","stage":"Preview","full":false,"width":1080,"height":1920,"previewState":"menu"}
```

Use the Editor Play/Stop control or write `{"action":"stop"}` to `.utmp/neon-ui-command.json` to exit. The delivery session is left on the [fresh menu preview](../../Artifacts/UI/Preview/1080x1920/01-fresh-menu.png), confirmed ready at **11:54:54 UTC**. A normal Play Mode launch outside this explicit QA workflow uses the regular player profile.

The final verification log segment through preview contained no C# compiler errors, unhandled gameplay exceptions, QA failures or save-write failures. Expected corrupt-save and rejected-purchase fixture messages remain, along with unrelated Unity licensing/cloud service messages; this is not a claim that the entire Editor log is empty or error-free.

The existing dense-grid touch-size limitation also remains: the prior UI pass measured a 52.65 px minimum cell edge for the combined 5×5 modifiers at 720×1280, below the configured 64 px guideline. This motion pass preserves that authoritative geometry; see the [prior layout measurement and limitations](PRESENTATION_REDESIGN.md). Target interpolation does not enlarge the cell's hit area.
