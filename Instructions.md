# NEON REFLEX — Responsiveness Fixes, Variable Outlines, Recovery Upgrades, and Enemies

Implement the following feature batch in the existing Unity project:

1. Remove the smiley feedback.
2. Eliminate the unresponsive gap after the level-complete popup disappears.
3. Replace reserve-activation vibration with animated visual feedback.
4. Improve the health display’s damage animation.
5. Animate consumed outlines differently in normal and Reverse gameplay.
6. Support a configurable, difficulty-dependent number of outlined cells per level.
7. Animate outgoing and incoming outlines during level transitions.
8. Add the permanently purchased Rebound upgrade.
9. Add the tiered Healing upgrade and collectible hearts.
10. Add configurable enemies to selected later levels.

Inspect the current implementation, make the changes, and verify the integrated game.

This is not a request for another visual redesign, a new game mode, or a new loadout system. Integrate the requested features into the existing campaign, shop, presentation, and save architecture.

---

## 1. Authority, Scope, and Initial Inspection

Read applicable repository instructions, including `AGENTS.md`.

Inspect:

- Grid construction and cell ownership.
- Target-sequence logic and outline animations.
- Correct-tap and wrong-tap event handling.
- Health, normal time, and reserve time.
- Level-transition phases and input locks.
- Existing smiley feedback and its creation paths.
- Upgrade definitions, purchases, and run snapshots.
- Gameplay motion and bounds.
- Save/restore, deterministic randomness, and debug isolation.
- Onboarding and practice-state separation.
- Existing feedback, audio, haptics, pooling, and cancellation.

Use the actual repository as authoritative for implementation details.

Do not assume an earlier class name, hierarchy, or implementation still applies.

Preserve unrelated changes in the working tree.

### These requirements intentionally supersede earlier rules

- Three active outlines are no longer mandatory.
- Healing becomes an explicit, purchased way to restore health.
- Rebound becomes an explicit, purchased exception to uninterrupted timer drain after a mistake.
- Reserve activation should no longer cause vibration.

Do not remove these new features because older documentation says health never regenerates or mistakes never pause timers.

Outside these explicit changes, preserve the established rules.

### Do not add unrelated earlier suggestions

Do not implement module slots, Pulse Lock, Overdrive, contracts, new currencies, or other previously discussed ideas as part of this task.

Rebound and Healing are permanent upgrades in the existing shop.

---

## 2. Defined First-Pass Interpretations

The following resolve unspecified interactions for this implementation. Keep their tunable values centralized and document them.

- A heart is collected by tapping its occupied grid cell.
- Each collected heart restores 1 health, never above current run maximum health.
- Rebound triggers only after a nonfatal wrong-grid tap.
- Enemy damage does not trigger Rebound.
- Rebound ends after its duration or the next accepted correct-outline tap, whichever happens first.
- Collecting hearts and destroying enemies do not end Rebound early.
- An enemy is consumed after its first successful collision with the grid.
- Destroying an enemy does not advance objective progress, advance outlines, or award currency.

These are first-pass design choices, not reasons to redesign other game systems.

Do not ask for ordinary tuning decisions. Populate editable conservative defaults, test them, and report the values used.

---

## 3. Remove Smiley Feedback

Identify and remove the smiley/face feedback added to gameplay.

Remove its:

- Runtime spawning.
- Presentation hooks.
- Unused serialized references.
- Associated animations.
- Obsolete configuration and assets where safe.

Do not merely hide an object while continuing to spawn or update it.

Do not remove unrelated functional icons:

- Health hearts.
- Settings icons.
- Upgrade icons.
- Enemy squares.
- Existing non-smiley neon particles.
- Motivational text.

Check generators and runtime UI builders so they do not recreate the removed smileys.

---

## 4. Fix the Gap Between Popup Disappearance and Gameplay Readiness

There is currently a period after the level-complete popup disappears when the board still does not respond.

Find the actual cause.

Inspect for:

- Trailing coroutine waits.
- Target-entry animations still owning an input lock.
- Multiple independently released transition flags.
- Transparent overlays intercepting raycasts.
- Delayed grid initialization.
- Debounce logic carrying over from the previous level.
- A second introduction sequence after the popup.
- HUD or animation callbacks releasing state in the wrong order.

Do not fix this by arbitrarily removing all input protections.

### Required readiness contract

Before the popup becomes fully invisible, the incoming level must have:

- Its correct cell collection and hit regions.
- Its authoritative target sequence.
- Fully visible, settled incoming outlines.
- Correct objective and timer values.
- Correct palette.
- Valid initial motion state and bounds.
- No obsolete animations capable of changing these values.

At the popup’s final disappearance, release the transition-owned gameplay lock and start the level in the same controlled state change.

There must be no additional wait afterward.

The first fresh pointer-down after the popup disappears must be accepted normally.

### Preserve legitimate protections

- Do not accept gameplay input before the transition has finished.
- Do not queue touches made during the popup.
- A held touch must not become an unintended tap in the next level.
- Settings or application suspension must still prevent gameplay from starting.
- A stale callback must not resume a failed run or gameplay behind another screen.

Use explicit pause/lock ownership rather than an unconditional “inputEnabled = true.”

Normal time, reserve, and gameplay modifiers resume with gameplay readiness—not earlier and not after an extra delay.

If initialization is genuinely not ready, keep the transition visibly active rather than hiding the popup and presenting an apparently playable but unresponsive board. Eliminate avoidable work so the configured duration is met in normal operation.

---

## 5. Reserve Activation: Visual Feedback Instead of Vibration

When normal level time reaches zero:

- Enter reserve mode immediately according to the existing timer rules.
- Remove the vibration specifically associated with that handoff.
- Preserve unrelated haptics settings and events.

Check all listeners so an indirect reserve event does not still produce vibration.

### Visual handoff

Animate the transition between normal-time and reserve presentation:

- Change the active label/state clearly.
- Transition the accent color and emphasis.
- Emphasize the remaining reserve value.
- Begin a repeating, restrained scale pulse on that value or its visual wrapper.

A provisional pulse could range from scale 1.0 to approximately 1.08–1.10, with a full cycle around 0.8 seconds.

Tune this in the actual HUD.

The pulse continues while reserve mode is active, drawing attention to the resource being used.

It must not:

- Scale the whole HUD.
- Reflow the layout.
- Change the actual timer value.
- Obscure the digits.
- Introduce flashing that dominates the board.
- Pause gameplay merely to show the transition.

The number remains an accurate display of current reserve. Do not tween it through fictitious values.

### Lifecycle

- Stop and settle the pulse when a new level returns to normal time.
- Hand presentation ownership to the terminal sequence when reserve reaches zero.
- Respect Settings and application pause.
- Prevent multiple pulse routines from accumulating.
- Use a restrained non-scaling emphasis under Reduced Effects.

During Rebound, reserve may remain the selected timer mode even though drain is temporarily held. Do not incorrectly switch back to normal-time mode.

---

## 6. Health Display Animation

Whenever health is lost, animate the health display itself.

The requested motion is:

1. Scale up.
2. Rotate slightly.
3. Return smoothly to its original scale and rotation.

Use a dedicated visual wrapper so layout and hit regions do not change.

Provisional starting values:

- Peak scale around 1.15–1.20.
- Rotation around 5 degrees.
- Total duration around 200–300 ms.

Tune for the current HUD.

### Logical behavior

Health changes immediately.

For example, 3 → 2 is committed and displayed immediately; the updated display then reacts.

Do not animate actual health through intermediate values.

### Coordination

Reuse existing damage feedback rather than adding a competing second effect.

- Wrong taps may retain their localized red reaction and screen feedback.
- Enemy impacts use damage feedback without pretending a wrong cell was tapped.
- Healing uses a positive treatment, not the damage animation.
- Fatal damage transitions into the existing run-ending sequence immediately.

Repeated damage should retarget or refresh from the current visual state without accumulating rotation or scale.

Return to the current layout’s correct baseline, not an obsolete cached transform.

Reduced Effects should retain a readable value change and lighter emphasis without rotation.

---

## 7. Correct-Tap Outline Exit Animations

The consumed outline currently appears to disappear immediately. Fix the ownership or teardown causing that.

Animate the outline graphic, not the underlying grid cell.

### Normal mode: consumed largest outline

On an accepted correct normal tap:

- Commit the sequence and objective immediately.
- Capture the consumed outline’s current rendered appearance.
- Let its outgoing visual scale up slightly.
- Dissolve it away quickly.

Use a restrained expansion, approximately 8–12% as a starting point, followed by or overlapping an actual dissolve/erosion treatment compatible with the current UI rendering.

Do not grow it dramatically over neighboring cells.

Its disappearance should begin promptly so it does not continue looking like the active largest target.

### Reverse mode: consumed smallest outline

On an accepted correct Reverse tap:

- Commit the sequence and objective immediately.
- Smoothly shrink the outgoing outline until it disappears.
- Use a short fade near the end if needed for a clean finish.

Do not apply the normal-mode expansion animation to Reverse consumption.

### Both modes

Use approximately 100–180 ms as initial effect durations, then tune.

Do not block input while these effects play.

The remaining outlines must animate toward their new ranks, and the replacement outline must appear without waiting for the outgoing graphic to finish.

### Immediate cell reuse

The consumed cell may be selected again for a new outline.

Support this without changing target-selection rules just to simplify animation.

Use a separate noninteractive outgoing visual or equivalent isolated ownership so the old effect cannot:

- Fade the new outline.
- Reset its scale.
- Restore an old color.
- Disable its cell.
- Run a stale completion callback against its new role.

Capture the outgoing rendered pose before the logical/view update invalidates it.

Reuse existing pooling where appropriate.

### Visual meaning

An outgoing expanded outline is a retiring effect, not a new target.

Make its dissolving state visually clear. Keep active targets legible and do not let decorative overshoot confuse the size ordering.

Verify the dissolve in the actual Canvas/material configuration. Do not claim it works based only on creating a shader asset.

---

## 8. Variable Number of Active Outlines

Add a per-level outline count independent of the grid dimensions.

The required supported range is **2–5 active outlines**, subject to available cells.

A 3×3 grid with two outlines still contains nine tappable cells; only two are active targets.

### Generalize the sequence

Represent active targets as an ordered collection from smallest to largest:

    targets[0] ... targets[K - 1]

where K is the configured outline count.

Every target occupies a distinct valid cell.

Normal play:

- Correct target is rank K - 1.
- Consume that target.
- Existing ranks 0 through K - 2 become ranks 1 through K - 1.
- Add a new smallest target at rank 0.

Reverse:

- Correct target is rank 0.
- Consume that target.
- Existing ranks 1 through K - 1 become ranks 0 through K - 2.
- Add a new largest target at rank K - 1.

Wrong taps do not change the collection, its order, or objective progress.

Do not leave gameplay logic dependent on exactly one Small, one Medium, and one Large field.

Update:

- Input validation.
- Target selection.
- Visual sizing.
- Reverse.
- Save/restore.
- Debug tools.
- Validation and tests.
- Relevant tutorial assumptions.

Compatibility adapters are acceptable where they simplify safe migration, but the new authoritative sequence must support K targets.

### Size readability

Use strictly increasing outline sizes.

For three outlines, preserve the existing authored sizes where practical.

For other counts, derive or configure a readable size ladder within the allowed visual range.

Five outlines must remain distinguishable on a phone.

Do not allow identical sizes, outline overlap that hides differences, or sizes that exceed cell boundaries.

Use bounded easing when active outlines change rank.

### Target occupancy

New target selection excludes:

- Other active targets.
- Cells reserved by active or retiring heart pickups.

The consumed cell may be reused when necessary and valid.

Handle capacity explicitly. Do not retry random selection indefinitely.

An added heart must never leave the sequence unable to create its next replacement target.

### Difficulty progression

Start with two outlines, introduce three after a few levels, and later mix in four and five.

Do not make every level use the maximum count after it is unlocked.

A provisional progression to start from is:

- Levels 1–5: two outlines.
- From Level 6: three become the usual baseline.
- From around Level 15: some levels use four.
- From around Level 35: some levels use five.
- Later levels still include lower-count configurations.

These are tuning defaults, not final design constraints. Adapt them conservatively to the actual campaign and document the chosen thresholds.

Preserve the existing first introductions of gameplay modifiers and avoid introducing several unfamiliar features at once.

Modify the relevant fields in existing fixed campaign data. Do not regenerate or overwrite unrelated handcrafted level settings.

Update any editor-generation workflow so future regeneration preserves the new structure.

---

## 9. Outline Animations During Level Transitions

When a level ends:

- Its active outlines smoothly scale down until they disappear.
- The next level’s outlines appear from almost zero scale and grow to their correct rank sizes.

This occurs while the existing level-complete popup is visible.

### Coordination with grid morphing

Preserve the existing structural transition for different grid sizes:

- Growing retains the overlapping cells and adds outer rows/columns.
- Shrinking removes outer cells and expands the retained portion.

Do not revert to two whole-grid crossfades.

Outline exits and entrances must be coordinated with the cell morph:

1. Retire the completed level’s outlines.
2. Morph or prepare the cell layout.
3. Reveal incoming outlines on the correctly positioned cells.
4. Settle everything before the popup finishes exiting.

Phases may overlap when visually coherent.

There must not be two sets of apparently active target outlines competing on the board.

The new targets are logically prepared before input resumes, even while their visuals are entering.

Do not use unbounded overshoot that reverses the visible size order.

### Completion barrier

By the time the popup disappears:

- All incoming outlines have reached their final visible sizes.
- The grid is ready.
- Input is released.
- The new level begins immediately.

Do not hide the popup and then start the target-entry animation.

Do not append extra waiting after it.

Preserve existing motivation lines, the single motion-introduction challenge, HUD count transitions, and palette blending.

---

## 10. Permanent Upgrade: Rebound

Add Rebound to the existing permanent shop.

**Name:** Rebound

**Description:**
Take a hit. Get a moment to regain control.

This is a **single permanent purchase**, not a consumable and not a multi-tier upgrade.

Use the existing currency and purchase validation.

Apply ownership/effect configuration to new runs through the existing upgrade-snapshot system. Preserve active-run purchase restrictions.

### Trigger

After a nonfatal wrong-grid tap:

- Apply the normal damage immediately.
- Leave the sequence unchanged.
- Begin the recovery window if Rebound is owned and not already active.

This applies in normal and Reverse gameplay.

Do not trigger it for:

- Ignored taps.
- Enemy collision damage.
- Fatal damage.
- Hearts expiring.
- Presentation-only demonstrations.

### Recovery window

During Rebound:

- Stop normal-time or reserve-time drain, whichever is active.
- Rotation, Move, and Scale operate at half their otherwise effective rate.
- Gameplay input remains active.
- Correct taps work normally.
- Further mistakes still cost health.

Default duration: **1.0 second**.

End it at the earlier of:

1. One second of active gameplay recovery time.
2. The next accepted correct-outline tap.

Heart collection and enemy destruction do not end it early.

### Return to normal rates

When the recovery condition ends:

- Release Rebound’s timer hold immediately.
- Smoothly restore motion to its normal effective rates over a short configurable interval.

Do not extend the timer pause merely to finish the visual rate recovery.

Calculate effective motion from:

    configured rate × existing upgrade effects × Rebound multiplier

Preserve rotation direction, movement direction, and scale range.

Do not restore a stale cached speed that ignores another current modifier or level change.

### Nonstacking behavior

Further mistakes during the same recovery window:

- Still deal damage.
- Do not restart or extend the one-second timer.
- Do not stack additional slowdown.

After the window ends, a later nonfatal wrong tap may activate it again.

If the run ends or the level completes, cancel Rebound safely.

### Separate clocks and pause reasons

Do not use a global time-scale freeze.

Rebound’s own duration must progress while resource drain is held.

During Rebound:

- Enemies continue moving and spawning normally.
- Heart lifetimes and spawn cooldowns continue normally.
- Reverse cooldown follows its normal active-gameplay behavior.

A real pause, Reverse announcement lock, level transition, or application suspension freezes these systems according to the existing game-state rules.

Removing Rebound’s hold must not resume a timer still paused by another reason.

Handle frame overshoot so the timer resumes for the appropriate remaining simulation interval rather than gaining an extra frame of free time.

---

## 11. Permanent Upgrade: Healing

Add Healing as a capped, tiered permanent upgrade.

Purchasing its first tier enables occasional collectible hearts.

Higher tiers increase how long hearts remain available.

Do not silently increase healing amount or spawn frequency with every tier.

Use the existing shop, currency, save system, and run-upgrade snapshots.

### Heart behavior

- A heart appears in a cell with no active outline.
- The cell is reserved while the pickup is present.
- No new target may occupy that cell during the pickup.
- Tapping that cell collects the heart.
- Collection restores 1 health, capped at the run’s maximum.
- Collection does not advance outlines or objective progress.
- Collection does not count as a correct-outline tap.
- Missing or allowing a heart to expire has no penalty.

Do not spawn hearts while health is already full.

If health becomes full while a heart is already present, collecting it may safely restore zero health, but must not cause damage or exceed the cap.

Do not allow a heart to revive an already terminated run.

### Brief, animated availability

Animate entrance, collection, and expiration.

Use a readable heart silhouette consistent with the neon UI—not a smiley.

Keep the entrance and exit short.

Suggested initial visible-lifetime tiers:

    0.75, 0.90, 1.05, 1.20, 1.35, 1.50 seconds

These are provisional values for six purchasable tiers, with the first tier unlocking the feature.

Measure the advertised visible lifetime from the point the pickup becomes clearly readable, excluding a very short entrance/exit.

Allow collection during entrance once the pickup has been presented as actionable.

On expiry, stop collection and animate it away. Until its retiring visual disappears, consume taps on that visual without passing them through as an accidental wrong-grid tap.

Release the reserved cell after its visible exit is complete, or use an equally safe detached-visual strategy that cannot obscure a new target.

### Low-frequency spawning

Use a low-frequency, bounded system rather than a per-frame random chance.

Suggested starting configuration:

- 3% chance after an accepted correct-outline tap when otherwise eligible.
- At least 15 seconds of active gameplay between spawn opportunities following a spawn.
- At most one active heart.
- At most two spawned hearts per level.
- Spawn only below maximum health.
- Spawn only when a valid unoccupied cell exists.
- Do not spawn from the final tap after level completion has been accepted.

The first-spawn eligibility delay should also be configurable.

Do not reroll every frame when a spawn fails.

Do not trigger additional chances from:

- Wrong taps.
- Enemy destruction.
- Heart collection.
- Rebound activation.

Keep all of this configurable and tune conservatively.

### Lifecycle and persistence

Heart lifetime and cooldowns use active gameplay time, not real-world elapsed time.

They pause during genuine gameplay pauses and level transitions, but not merely because Rebound holds the resource timer.

Save an active pickup’s cell, phase/remaining availability, and relevant spawn state.

Collecting a heart and removing it must be one coherent state change, so reload cannot repeatedly collect the same pickup.

At ordinary level completion, retire any remaining heart without reward or penalty. Do not carry pickups onto a differently sized next-level board.

Only remaining health carries forward.

---

## 12. New Feature: Enemies

Add small red square enemies to selected later campaign levels.

They spawn at the edges of the playable game area and move toward the grid.

The player can tap them to destroy them.

If an enemy reaches the grid, it deals one damage and is consumed.

### Configuration

Provide global defaults and per-level configuration or overrides for:

- Enemy enabled.
- Minimum campaign level.
- Spawn interval or interval range.
- Initial spawn delay.
- Movement speed.
- Enemy size.
- Maximum concurrent enemies.
- Allowed spawn edges if useful.
- Minimum spawn-to-grid clearance.
- Required reaction/travel allowance.

Use normalized gameplay-area units or another resolution-independent representation.

A conservative first-pass configuration could be:

- Enemies begin no earlier than around Level 25.
- Only selected later levels enable them.
- Initial delay around 2–3 seconds.
- Spawn interval around 6–9 seconds.
- Initially no more than two concurrent enemies.
- Slow movement with at least roughly two seconds of designed reaction opportunity.

These values are provisional and must remain editable.

Do not introduce enemies at the same moment as the first five-outline challenge unless specifically configured for a later combined challenge.

### Visual identity

Use a filled or otherwise unmistakable red square with a clear neon treatment.

Enemies must not look like another valid target outline.

Give them:

- A short entrance animation.
- Clear movement.
- A brief destruction animation when tapped.
- A distinct impact/retirement effect on collision.

Keep tap targets usable on a phone.

Do not add smiley faces to them.

### Spawn area

Use the gameplay arena excluding:

- Top HUD.
- Bottom navigation or controls.
- Safe-area exclusions.
- Other nonplayable UI.

Do not use the entire screen rectangle indiscriminately.

Spawn along valid arena edges, positioned so the enemy is visible and does not begin inside or overlapping the grid.

The next level’s enemies must not spawn while the level-complete popup is still present.

### Leave an approach corridor

Enemy-enabled levels need space around the grid.

Adjust the base fitted grid footprint and movement envelope as needed so incoming enemies have actual travel distance.

Account for:

- Maximum grid scale.
- Rotation extents.
- Grid movement toward an edge.
- Enemy dimensions.
- Minimum practical cell and outline readability.

A centered-grid measurement alone is insufficient: a moving or expanding grid must not eliminate the intended reaction space.

Use conservative maximum-envelope reasoning where appropriate, and validate candidate spawn positions.

For travel safety, consider relative closing motion rather than only dividing the initial gap by enemy speed.

Do not create an unreadably tiny grid to satisfy an impossible configuration.

If no safe configuration or spawn position is available:

- Reject or skip the spawn.
- Report a clear development validation warning.
- Do not spawn inside the grid or deal unavoidable immediate damage.

### Movement

Use a configurable constant movement speed.

For the first implementation, enemies steer toward the grid’s current authoritative center.

Do not parent enemies to the moving grid.

Movement continues during Rebound.

Pause enemy movement and spawning during all genuine gameplay pauses, announcements, transitions, and terminal states.

Prevent tunneling at low frame rates through appropriate swept checks or bounded simulation substeps.

### Collision with the grid

Treat the grid’s transformed outer rectangular footprint as the collision target, not individual active outlines.

An enemy reaching any part of the board structure counts as an impact.

Use collision geometry that respects the grid’s rotation and scale. A broad-phase axis-aligned bounds check alone must not cause visible false impacts beside a rotated corner.

Use a coherent coordinate space for enemy movement, grid geometry, and presentation.

Cosmetic screen shake must not cause artificial enemy collisions.

### On impact

Atomically:

1. Mark the enemy as consumed.
2. Remove it from active collision and input handling.
3. Apply exactly one health damage.
4. Play the appropriate impact and health feedback.
5. Retire its visual.

An enemy must never deal damage every frame while touching the grid.

Separate enemies may each deal damage. Do not add undocumented global invulnerability.

Fatal damage uses the existing terminal transaction and run-ending presentation.

Enemy damage does not trigger Rebound in this implementation.

### On enemy tap

Atomically:

- Mark it destroyed.
- Remove its collision behavior immediately.
- Play its destruction animation.

Do not:

- Advance objective taps.
- Advance the outline sequence.
- Grant coins.
- Roll for a heart.
- End Rebound early.
- Also process the same pointer-down as a wrong-grid tap.

An enemy cannot both be destroyed and apply impact damage afterward through a stale callback.

Once an impact is already committed, a later tap cannot undo it.

### Level ending and save/restore

On level completion or run termination:

- Stop enemy simulation immediately.
- Cancel pending spawn requests.
- Retire remaining visuals safely.
- Do not let an old enemy damage the next level.

Save active enemies, positions, relevant motion/spawn state, and deterministic randomness so closing the app does not simply remove threats.

Restore them coherently without spawning another enemy on top.

---

## 13. Shared Input, Occupancy, Damage, and Clock Rules

These features must integrate rather than independently subscribe to the same tap.

Use a clear input resolution order:

1. Modal/transition input blocking.
2. A valid enemy target.
3. A valid heart pickup cell.
4. Normal grid-cell handling.
5. Outside-grid input remains ignored.

Consume a pointer action once.

Retiring effects may absorb a tap where necessary to avoid click-through, but must not produce gameplay actions.

Do not let global “outside the grid” rejection prevent enemy taps in the approach corridor.

### Damage causes

Represent or preserve distinguishable damage causes:

- Wrong grid tap.
- Enemy impact.

Both update health and may terminate the run.

Only wrong-grid damage activates Rebound and receives wrong-cell feedback.

Healing is a separate resource operation, not negative damage passed through the same effect pipeline.

### Timing responsibilities

Keep these concepts separate:

- Whether gameplay is active.
- Whether normal/reserve resource drain is held by Rebound.
- The Rebound recovery clock.
- Gameplay motion multipliers.
- Heart lifetime and spawn clocks.
- Enemy movement and spawn clocks.
- Presentation animation clocks.

Do not use one boolean or global time scale for all of them.

### Occupancy and capacity

Target selection, heart spawning, and grid initialization must share consistent cell occupancy rules.

Enemies occupy arena space, not a reserved grid cell.

Validate that every configured outline count fits the grid and that pickups cannot deadlock target replacement.

---

## 14. Save Compatibility, Shop Integration, and Onboarding

Extend the current save architecture; do not create a parallel one.

Persist or safely reconstruct:

- Ordered target cell IDs and current outline count.
- Rebound ownership and active recovery state.
- Healing tier and effective run snapshot.
- Active heart and spawn limits/cooldowns.
- Active enemies and spawn schedule.
- Appropriate deterministic RNG state.

Use separate deterministic streams for new random systems where practical, so heart/enemy generation does not unpredictably perturb existing target and Reverse behavior.

### Legacy runs

Do not wipe player profiles.

Migrate old three-outline saves into an ordered three-element sequence.

When a campaign asset’s outline count changes, do not silently replace an already saved in-progress level’s sequence.

Preserve that active level’s saved effective count until its next boundary, then use current configuration as appropriate.

Missing new fields need safe defaults.

Enemy-enabled flags should not become true accidentally because old serialized data lacks a value.

Atomic snapshots must prevent duplicate heart collection, repeated enemy impacts, or lost terminal rewards.

### Shop

Add two matching cards:

- Rebound: locked/owned, one permanent purchase.
- Healing: current tier, next lifetime, price, and maximum tier.

Costs must be data-driven and chosen relative to the existing economy.

Do not sell additional Rebound tiers.

Do not apply new purchases retroactively to an active run.

### Onboarding

Keep first-time practice isolated.

- No enemies.
- No heart spawns.
- No accidental Rebound effect from the real profile.
- Practice health remains controlled by the tutorial.
- Early ignored taps stay ignored.

Update any assumptions that exactly three outlines must exist.

Use generic “largest outline” and “smallest outline” language.

The new enemy interaction should receive a concise introduction in the existing presentation flow when first encountered. Avoid another large tutorial framework, overlapping messages, or a second post-popup input delay.

---

## 15. Verification and Acceptance Tests

Add focused automated tests for pure rules and integration. Inspect actual motion in Unity where possible.

### Outline sequence

Test counts 2, 3, 4, and 5 in normal and Reverse modes.

Verify:

- Unique valid target cells.
- Correct rank advancement.
- Wrong taps leave the sequence unchanged.
- Heart-reserved cells are excluded.
- Full-capacity configurations do not loop indefinitely.
- Legacy saves migrate correctly.
- Rapid taps and immediate cell reuse do not leave stale visuals.

### Transition readiness

Verify:

- Outgoing outlines shrink away.
- Grid morph remains correct in both directions.
- Incoming outlines grow to final sizes before popup disappearance.
- A fresh tap immediately after disappearance is accepted.
- No invisible panel intercepts it.
- Timers and motion start at the correct moment.
- Settings/backgrounding cannot cause premature unlock.

### Reserve and health UI

Verify:

- Reserve activation does not vibrate.
- Reserve pulse runs only in the appropriate state.
- Pulse and health animation do not reflow layout.
- Repeated health loss does not accumulate transforms.
- Fatal damage still ends the run immediately.
- Correct largest/smallest exits use their distinct animations.

### Rebound

Verify:

- A nonfatal wrong tap activates it once.
- Timer drain pauses while motion continues at half effective speed.
- One second or a correct-outline tap ends it.
- Additional mistakes do not extend it.
- Heart/enemy taps do not end it early.
- Enemy damage does not activate it.
- Full pauses freeze its duration.
- Ending it does not clear other pause reasons.
- Level completion and terminal failure cancel it safely.

### Healing

Verify:

- No spawn before purchase or at full health.
- No overlap with active outlines.
- Brief tier-dependent availability.
- Collection restores exactly one health up to the cap.
- No objective progress or wrong-tap damage from collection.
- Expiry has no penalty.
- Spawn limits and cooldowns work.
- Save/reload cannot duplicate collection.
- Reserved cells are eventually released.

### Enemies

Verify:

- No enemies before the configured threshold or on disabled levels.
- No spawning in HUD/safe-area regions.
- Adequate approach space with combined grid motion.
- Valid collision against rotated/scaled boards.
- Tapping destroys without grid input leakage.
- Impact deals damage once and consumes the enemy.
- Rebound does not pause enemies.
- Low-frame-rate movement does not tunnel.
- Pause, transition, terminal state, and restore are safe.

### Combined stress cases

Test:

- Five outlines, a heart, and enemies.
- Reserve active while Rebound holds drain.
- Heart collection during Rebound.
- Enemy impact during Rebound.
- Fatal enemy impact near heart collection.
- Level completion with active pickup/enemies and outline effects.
- Return Home and resume during an active recovery window.
- Repeated transitions and rapid input.

Use isolated test profiles and debug fixtures, not real save manipulation.

Capture short clips or frame sequences to verify animation and readiness.

Do not claim tests, visual checks, or build results that were not actually performed.

---

## SUBAGENTS

You may use subagents when they genuinely help. You remain the creative and technical lead, responsible for the concept, core gameplay quality, integration, and final result.

Give delegated work clear boundaries, ownership, interfaces, and acceptance criteria. Avoid conflicting edits to shared scenes or project settings.

Choose models and reasoning levels that are actually available and suit the task. For example, GPT-5.6 Terra at Medium reasoning can be an option for straightforward implementation work, but it is not a requirement or a cap. Do not sacrifice quality merely to delegate to a cheaper model.

Review delegated results and verify the integrated game.

---

## 16. Final Report

Implement the complete batch, compiling and testing incrementally.

Report:

- The actual cause of the post-popup unresponsive gap.
- Smiley feedback removed.
- Animation and HUD changes.
- How the generalized target sequence works.
- Campaign outline-count and enemy progression applied.
- Rebound and Healing behavior and prices.
- Heart/enemy spawn and lifetime defaults.
- Input, clock, and damage integration.
- Save-format changes and migration behavior.
- Tests and runtime checks actually performed.
- Where all provisional values can be tuned.
- Any remaining limitations.

Do not leave requested gameplay systems as TODOs or nonfunctional shop cards.

The result should be immediately responsive after level transitions, visually clear during resource changes, and meaningfully expanded by variable outlines, recovery upgrades, and enemies—without breaking the existing run flow.