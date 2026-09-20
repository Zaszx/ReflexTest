# Variable outlines, recovery, and enemies

Implemented September 19, 2026. This extends the existing campaign, shop, save envelope, and in-place transitions.

## Responsiveness and presentation

The old transition owner set `transitionInputReadyFrame` when finishing, and `OnSquareClicked` rejected **every** pointer-down on that frame. The grid and popup could already have settled while this extra gate still rejected a fresh tap. That gate is removed. Live raycast checks also exposed newly added cells whose graphics had not yet registered when an expanding transition completed within one frame. The flow owner now finishes the layout, rebuilds the canvas, and checks that all cell graphics have a valid raycast depth and are not culled. Until then the announcement stays visible and clocks/input remain held. Once ready, it hides the announcement, switches to Playing, clears pointer claims, and refreshes UI input in one handoff. Real PointerDown events are used; held touches are not replayed or queued. Settings and application suspension remain separate gates.

The popup remains 1.8 seconds by default. Its first second prepares the board: outgoing outlines shrink over preparation 0–18%, the existing cells/layout morph over 18–72%, and incoming outlines grow over 72–100%. Incoming targets are fully settled before the popup disappears. The existing grow/shrink grid-shell transition and first-motion/motivation announcements remain. Removed cells also retire their outlines.

Normal consumed outlines expand 10% and use a local-coordinate erosion shader over 0.12 seconds. Reverse consumed outlines shrink to zero over the same interval. Active targets and retiring meshes are separate; four reusable exit meshes per cell bound rapid-input decoration. Rendering uses the existing antialiased UI shader. Cell boundaries removed in the earlier flicker fix are not recreated.

Reserve activation no longer calls the haptic function. The displayed reserve digits pulse from 1 to 1.08 over a 0.8-second cycle. Reduced Effects keeps their scale fixed. The digits still show the authoritative value. Health loss uses a separate visual wrapper, peaking at 1.17 scale and 5 degrees over 0.25 seconds; repeated hits retarget without accumulating transforms. Healing has its own positive cue. These wrappers do not resize layout or input geometry.

Smiley spawning, hooks, shader, settings, and imported duplicate PNGs are removed. Original artist files remain in the root `FeedbackSmileys/` folder. The existing success particles remain available but disabled.

## Targets and campaign

`ActiveLevelStateData.targetIndices` is ordered smallest to largest. Normal consumes the final element, promotes survivors, and inserts a new smallest; Reverse consumes the first, demotes survivors, and appends a new largest. Target ranks are unique, exclude the reserved heart cell, and prefer a cell other than the consumed one. At full capacity the consumed cell is a valid fallback. Invalid capacity is rejected without loops or RNG advancement.

Two through five outlines are supported. Three retains the authored small/medium/full values; other counts use a strictly increasing ladder between the endpoints. The saved effective count remains authoritative until the next level boundary.

All 100 authored campaign assets and the generator were updated without changing their objectives, time budgets, rewards, or existing motion/Reverse modifiers:

- Levels 1–5: two outlines.
- Levels 6–14: three outlines.
- Four-outline challenges begin at level 15.
- Five-outline challenges begin at level 35; later levels retain a mixture of three, four, and five.
- Totals: 5 two-outline, 61 three-outline, 24 four-outline, and 10 five-outline levels.
- Enemies: level 25, then every fifth level from 30 through 100, excluding 35. The first enemy announcement says “RED SQUARES APPROACH · TAP TO DESTROY”.

## Permanent upgrades

| Upgrade | Price | Effect |
| --- | --- | --- |
| Rebound | 120 coins, one purchase | After a nonfatal wrong-grid tap, hold normal/reserve drain for 1 second and run grid motion at half its otherwise effective rate. |
| Healing tiers 1–6 | 80, 125, 185, 260, 350, 455 coins | Unlock hearts, then increase their readable lifetime to 0.75, 0.90, 1.05, 1.20, 1.35, 1.50 seconds. |

Purchases use the existing wallet and are captured only for new runs. The shop has six matching cards and a scrollable list. Rebound is locked/owned with no repeat tiers. Existing editor-only reset/add-coins tools also cover the new upgrades.

Rebound does not lock input, pause hearts/enemies, or pause Reverse cooldown. Further mistakes still hurt and do not extend it. Correct-outline input ends it immediately; heart/enemy taps do not. Enemy damage never activates it. Motion returns over 0.2 seconds after the resource hold has ended. Expiry-crossing frames drain the exact remaining resource interval. Settings, backgrounding, and announcements freeze the active recovery clock.

Hearts roll once after eligible nonfinal correct-outline taps: 3% chance, 5-second first eligibility delay, 15-second cooldown after spawning, maximum two per level, and only below full health. One heart can reserve an unoutlined cell. Entrance is 0.08 seconds; the advertised lifetime starts after entrance. Collection restores one health up to the run cap and does not advance targets. Collected or expired hearts retire for 0.15 seconds; their cell stays reserved and absorbs taps during that fade. Pickup phases, cooldown, count, and independent RNG state persist.

## Enemies, input, and clocks

Global defaults: minimum level 25, first spawn after 3 active seconds, subsequent interval 6–9 seconds, at most two enemies, speed 0.08 of the arena's short side per second, visual size 0.055, all arena edges allowed, clearance 0.06, and at least two seconds of designed approach time. A 1.7× touch area makes the small visual easier to tap. The enemy is a filled red core with a red halo, distinct from target outlines.

The arena excludes HUD, navigation, and safe-area reservations. Fitting reserves space against the full maximum-scale, rotated and translated grid envelope; candidates are checked against that full future envelope. This bounds closing grid motion without double-counting its travel. Narrow edges can be rejected while longer edges remain available. An impossible fit keeps readable grid cells and skips unsafe spawns with a development warning.

Enemies steer toward the current authoritative center. They are siblings of the grid, not its children. Swept collision uses the true transformed rectangle and interpolates old/new grid poses, including rapid rotation. The root uses bounded active simulation steps. Cosmetic shake cancels out in the common arena coordinate space. Impact commits the consumed phase before one damage callback; destruction commits before its save callback. Neither action can subsequently fire the other.

Input priority is modal/transition gate, enemy, reserved heart cell, normal grid, then ignored outside input. Cosmetic images do not receive gameplay input. A consumed action cannot also advance a target or cause wrong-cell damage. Enemy taps grant neither coins nor target progress.

Resource drain, recovery, grid motion, heart lifetime/spawning, enemy simulation, and cosmetic animation have separate responsibilities. Global time scale is not used to implement Rebound. Actual level boundaries and terminal transactions cancel old pickups/threats before the next board runs. Home/Continue and onboarding replay preserve the original run instead of consuming its features.

## Saves and tuning

The existing JSON envelope receives additive fields for ordered targets/count, upgrade ownership and effective snapshots, Rebound state, heart state/spawn bookkeeping, and enemy state/spawn RNG. Legacy three-index runs migrate to a three-element ordered sequence; legacy endpoint fields stay synchronized for compatibility. Missing new purchases remain off. Legacy profiles and balances are not wiped. New random systems use separate deterministic streams. Atomic collection, impact, and terminal saves prevent replaying rewards or damage on reload.

Tuning locations:

- `Assets/Levels/Campaign/LevelNNN.asset`: outline count, enemy enable/override settings, existing difficulty settings.
- `Assets/Resources/RecoverySettings.asset`: Rebound timing/multiplier, heart chance/delay/cooldown/limits/animation, health wrapper and reserve pulse.
- `Assets/GameConfig.asset`: Rebound price and Healing tier prices/lifetimes, existing upgrade economy and minimum touch size.
- `Assets/Resources/EnemySettings.asset`: enemy threshold, size, touch margin, speed, scheduling, concurrency, edges, clearance and reaction time.
- `Assets/Resources/NeonMotionSettings.asset`: outline exits, health duration, preparation and popup durations.
- `Assets/Editor/CampaignTestGenerator.cs`: matching defaults for an explicitly regenerated campaign.

## Verification

Unity 6.3 LTS compiled the runtime, editor helpers, tests, and UI shader. The automated EditMode suite passed 116 cases after fixing sequence-selection and bounded-exit-pool regressions found by the first run. The final isolated runtime pass passed 292 checks with zero failures. Final captures/checks are recorded under `Artifacts/UI/FeatureBatchFinal/540x1200/`; machine-readable EditMode results are `Artifacts/UI/editmode-results.xml`.

Runtime coverage includes all target counts and both directions, grow/shrink transitions, same-frame fresh input and real UI raycasts on every new cell, Settings/background locks, exact Rebound overshoot and half-speed motion, heart collection/retirement/save/Continue, real level-25 enemy spawning, impacts, expanded enemy touch targets, and the level-70 combined five-outline/recovery/pickup/enemy boundary. Onboarding regression coverage uses isolated storage; replay restores the frozen Rebound/retiring-heart level state byte-for-byte. Frame sequences capture normal/Reverse exits and transition preparation; stills show hearts, enemy corridors, and all six shop cards.

No Android APK was built for this task and no physical-phone test is claimed. The economy, spawn rates, and touch sizes are conservative starting values for device playtesting. Configurations with no safe approach corridor deliberately skip enemy spawns instead of shrinking the board below its configured readable minimum.
