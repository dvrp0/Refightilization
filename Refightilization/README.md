﻿
# Info

Allows players to respawn as a monster when they die. Highly configurable.

## Configuring for Single Player

If you ever want to mess with this mod while in single player, there are some config options to change.

| Key | Value | Reason |
|---|---|---|
|EndGameWhenEverybodyDead|false|The run would've ended after the first death.|
|RespawnDelay|0.0|The run would've ended because there are no players alive for a frame.|
|RespawnTeam|1|The run would've been softlocked because you can't charge the teleporter.|
|AdditionalRespawnTime|0.0|The run would've ended because there are no players alive for a frame.|

# Video

[![Youtube Teaser](https://img.youtube.com/vi/4BdSJ4V8CPI/0.jpg)](https://www.youtube.com/watch?v=4BdSJ4V8CPI)

# Issues

- Soft incompatibility with Ancient Scepter, players holding an Ancient Scepter would have it rerolled upon respawning.
- Artifact of Metamorphosis will spawn you as a random survivor as opposed to a random monster.

# Contact

If you've got *any* feedback or advice, feel free to reach out via Discord. You can find me at `Wonda#2183` in the modding server.

# Credits

Thanks to Nova and Arky for helpin' me scheme up the name for this thing.

Thanks to Fantab and Loke for inspiring this project to exist. 

Thanks to Cebe, Joe, JC, and PurpleKid for helping me test this mod out prior to release.

Thanks to Lux and Gaforb for providing tons of feedback to help make this mod better.

# Changelog

1.0.3 - Added item configs, added config for Monster Variants, added scaling respawn timer, prevented respawning as a recently spawned monster, caught bugs regarding disabling the mod, adjusted spawning to prevent softlocks, altered Affix code to give players Elite Aspects instead of buffing them, adjusted spawning to prevent being unable to pick up items, added functionality with some artifacts.

1.0.2 - Caught some NREs, fixed R2API dependencies.

1.0.1 - Updated README to provide some useful hints for using the mod.

1.0.0 - Initial Release