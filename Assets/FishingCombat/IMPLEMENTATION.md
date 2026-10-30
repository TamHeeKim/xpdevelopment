# PoC combat restored

Source: TamHeeKim/xpdevelopment, commit 7269c72d42039cfab3031cffeba9e206aff5d201.
Open Fishing Combat > Open Combat Scene, then Play.

A/D: move. W: jump. LMB: original curved whip; hold to capture on tip contact; release to throw with actual velocity. RMB hold: reel. Space: original fishing. R: reset.

Combat, player, enemy, health and stage scripts now use the original PoC behavior. No assisted throw, circle capture, middle-button slam or new save system. Integration adaptations: new Input System, isolated namespace, scene-specific startup and editor reload.


Update: released targets retain collision damage for 2 seconds (ThrowDamageDuration). Original speed threshold 4.5 and damage formula retained. Enemy AI pauses during this interval; velocity is not overwritten. Repeated pair impacts have a 0.1 second cooldown.

Target assistance: while holding, a fast downward mouse swipe (0.8 screen heights/sec, mostly vertical) guides the held body toward the nearest enemy below it, inside the current rope length and player radius 7. Assistance lasts 0.35 sec with 0.65 sec cooldown; RMB cancels it. No target means original swing. On LMB release, an enemy within player radius 7 and 55 degrees of current velocity is selected by smallest angle. Speed is preserved; no homing after release. Stationary/slow release (<4.5) remains unchanged. Walls block target selection. These are provisional tuning values.

Tang enemy prototype: one enemy uses gas below distance 3 (chases 2 sec, cloud lasts 3 sec), shuriken at distance 3-5 (0.6 sec warning, speed 7, damage 12). Gas deals 3 damage each 0.5 sec and slows 40%, refreshed to 2 sec on contact. Held bottle instantly consumes one cloud; thrown filled bottle breaks for 40 area damage within radius 2. Returned shuriken adds 30 damage. Traps omitted. Heavy enemies require 0.8 sec capture; release, range loss or damage cancels. E saves/heals at start or x14; F9 loads saved checkpoint/catch. Death retains current catch and resets encounter. Inventory/currency remain outside implemented scope. All unfixed values are provisional.
