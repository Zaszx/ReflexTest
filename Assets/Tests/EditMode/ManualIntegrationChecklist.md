# Neon Reflex manual integration checks

These behaviours deliberately remain manual because `GameManager` owns them through
private scene/UI state and has no injectable input or terminal-transition seam.

- On the final valid tap of a level, confirm input locks and level completion occurs
  before any Reverse trigger or sequence follow-up presentation.
- During normal and active Reverse gameplay, tap a wrong square and verify exactly one
  health is lost while objective progress, target indices, and Reverse remaining taps
  do not change. Repeat with one health remaining and verify no subsequent callback
  advances the sequence or starts Reverse.
- Verify grid-external and all input-locked states ignore taps with no health, progress,
  target, Reverse, or feedback change.
- Open Settings, background the app, enter Reverse transitions, and show level/run
  summaries; confirm timer, cooldown, rotation, scale, movement, and gameplay input
  stop and resume from exactly the persisted state.
- Complete and fail/abandon runs in the scene to confirm rewards bank once, pending
  coins are not spendable, and the next level resets only normal time while retaining
  health and reserve.
- Test combined rotation, scale, and movement at multiple aspect ratios and confirm the
  visible grid stays contained.
