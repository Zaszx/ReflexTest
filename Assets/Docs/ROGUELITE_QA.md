# Neon Reflex Roguelite QA

## Provisional balance

Balance remains deliberately editable. Base values live in `GameConfig`: health 3 (cap 20), reserve 15 seconds, and the Reverse baseline. Upgrade tiers and costs live in the four `UpgradeDefinitionData` fields on `GameConfig`; `UpgradeDefaults` supplies health 4–20, reserve 20/25/30/35/40/45 seconds, stabilizer multipliers .90/.80/.70/.60/.50, and Reverse cooldown bonuses 2/4/6/8/10 seconds. Campaign reward, timing, and modifier plateaus are generated in `CampaignTestGenerator.Configure` and remain editable per `LevelData` asset.

## Campaign generation

Use **Neon Reflex > Generate Missing Campaign** to create the campaign only when it is absent. It never overwrites existing assets. **Regenerate Campaign (Replace Generated Assets)** requires confirmation and replaces only the 100 known generator-owned assets (stable IDs `neon-reflex-level-001` through `100`) and the generated campaign definition. The first five levels introduce base, Reverse, rotation, scale, and movement separately.

## Persistence

The single envelope is `neon_reflex_save.json` under `Application.persistentDataPath`, with `.bak` retained before a replacement and `.tmp` used during writes. Profile and active run remain separate data models but are saved together, allowing banking plus run clearing to be atomic. Malformed/corrupt resources are normalized conservatively and never create coins. One-time migration deletes only legacy `CampaignLevel`, `TimedHighScore`, and `SpeedBestTime`, preserves `HapticsEnabled`, then marks the profile migrated and saves it.

Transient presentations are canonicalized on restore: a level intro resumes as unlocked gameplay, Reverse entrance resumes as active Reverse with its badge/instruction, and Reverse exit resumes as normal gameplay with its already-started cooldown. The saved timer, objective, target indices, resources, transform state, and deterministic random state are retained; transition animations are not replayed.

## Manual QA checklist — not yet executed

- [ ] Start a clean profile; confirm Level 1, health 3, and reserve 15 seconds.
- [ ] Tap a wrong normal target; confirm exactly one health is lost and the sequence/objective do not move.
- [ ] Trigger Reverse and repeat the wrong-target test; confirm the sequence, objective, and Reverse count do not move.
- [ ] Let normal time expire; confirm an immediate, unmistakable Reserve transition with frame overshoot charged correctly.
- [ ] Complete a level in Reserve; confirm remaining reserve carries forward and normal time resets.
- [ ] Complete a level during normal time; confirm unused normal time is discarded rather than converted.
- [ ] Open Settings during normal play, active Reverse, Reverse entrance, and Reverse exit; confirm all simulation and presentation progress pauses and resumes exactly.
- [ ] Use Home during a level and between levels; confirm Continue Run appears and restores the same run.
- [ ] Background/close and reopen the app; confirm no offline timer drain.
- [ ] Confirm restored objective progress and exact Small/Medium/Large cells.
- [ ] Confirm restored Reverse state/cooldown and rotation, scale phase/direction, and movement position/direction.
- [ ] Test rotation alone in both configured directions.
- [ ] Test scale alone through both endpoints with no jump.
- [ ] Test movement alone, including horizontal, vertical, and corner reflections.
- [ ] Test every pair of rotation, scale, and movement modifiers.
- [ ] Test rotation, scale, and movement together during normal and active Reverse gameplay.
- [ ] Confirm every transformed grid corner remains inside gameplay bounds in all motion tests.
- [ ] Fail through health depletion; confirm input locks and only prior completed-level rewards bank.
- [ ] Fail through reserve depletion; confirm the same reward rule and one terminal event.
- [ ] Abandon a run through the confirmation; confirm the same reward treatment as failure.
- [ ] Open the shop during an active run; confirm all purchases are disabled with the active-run explanation.
- [ ] End a run, purchase each upgrade, and confirm exact cost deduction and next-run-only effects.
- [ ] Confirm Maximum Health caps at 20.
- [ ] Confirm maximum Grid Stabilizer slows but does not stop or reverse configured motion.
- [ ] Confirm maximum Reverse Resistance increases cooldown but does not eliminate Reverse or shorten its duration to zero.
- [ ] Use the development level selector with and without a saved real run; confirm it changes no save, wallet, pending coins, or tiers.
- [ ] Complete the final configured level; confirm final-level reward plus the separate completion bonus bank exactly once.
- [ ] Start after campaign completion; confirm the next real run begins at Level 1.
- [ ] Confirm no Timed or Speed mode UI or gameplay remains.
- [ ] Open `SampleScene`; confirm there are no missing references or recurring Console exceptions.
- [ ] Verify the menu, HUD, settings, summaries, shop, and grid at two or more portrait aspect ratios, including a device safe area; confirm readable touch targets and no clipping.
- [ ] Corrupt the primary save after producing a backup; confirm backup recovery warns, does not crash, and does not invent currency.
- [ ] Regenerate only through the confirmation command; confirm missing-generation never overwrites an existing manually edited campaign.
