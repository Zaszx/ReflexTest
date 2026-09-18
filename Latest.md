The smileys didnt work good. We need to remove them.

After the player advances a level, the board becomes unresponsive for a moment. So, there is a time that "level complete" popup is gone but the level is still unresponsive.
That isn't good, we don't want players to feel this way. The game should become responsive as soon as the level complete popup disappears.

When the level time is exhausted and it switches to reserve time, instead of giving a vibration, we need to have a visual effect.
Maybe the switch between level time and reserve time can have an animation, where the remaining reserve time scales up and down constantly to take attention.

Similarly, when a wrong tap is done and health is lost, there needs to be a visual animation on the health display ui.
The remaining health should scale up and rotate slightly, then scale down back to its place. Similar in a way with a lot of other app ui.

When a correct square is tapped, it seems to be disappearing immediately without any animation. The correct square should slightly scale up and dissolve if it's the biggest square,
and scale down to the point it completely disappears if it's the smallest square (reverse mode)

Also, as a difficulty increase, in later levels, make the number of outlined squares in the board variable. Maybe it should start as 2, proceed into 3 after a few levels, and then
after some point some levels could have 4 or even 5. Not all levels have to have 4 after some point, but some can. Make this scale with difficulty.


After a level is done, the outlined squares on the level disappear and new ones appear. We need to have an animation for that.
The squares on the passed level should scale down to the point that they disappear, and the outlined squares on the new level should start with scale almost zero and scale up.
This should happen while the "level complete" popup is there. By the time the popup is gone, everything has to be ready.



Upgrade:

Rebound — help the player recover after a mistake

One off upgrade.

Shop description:

Take a hit. Get a moment to regain control.

After a nonfatal mistake, grid motion briefly slows, and level (or reserve) timer stops draining, giving the player a short opportunity to recover their orientation.

For example: Timer stops, Rotation, Scale, and Move operate at half speed for roughly one second OR the user makes a correct tap, then smoothly return to their normal effective rates.

Health still drops immediately. The sequence remains unchanged.


Upgrade:

Healing - help the player regain health

When bought, some heart shapes may appear on the board where there are no square outlines active. While a heart is active, there can be no square outlines on that square.
The heart stays on only for a very brief moment, make this very short. The more this is upgraded, the longer the heart stays. 
Heart should appear - disappear with animation.
Heart can't heal above max health.
The chance of a heart appearing is low. Dont make too many appear. 





New feature: enemies

After a certain level, red square shapes (enemies) will start spawning. They will spawn from the edges of the game screen (excluding ui part), and will slowly move towards the 
grid structure. If it touches the grid structure, player takes one damage. If the player taps on one, it gets destroyed (with an animation).

Make this configurable. Enemy freqıemcy should also be configurable. Enemies should only start to appear after a certain level. If enemies are enabled in a level, 
the grid should not be too big, and there should be enough space left from the edges of the screen so that the enemies dont hit the grid immediately, they have time to travel so
the player can react to them.
