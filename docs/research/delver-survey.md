# Donor survey: Delver (com.interrupt.dungeoneer)

Delver is a Java/libGDX first-person roguelike dungeon crawler. `rusty-dungeon` adapts its
gameplay concepts and content shapes onto the `rusty-engine` SDK; nothing else transfers.

## Purpose and provenance

Two donor roots were inspected read-only; nothing under them was modified:

- `/home/agent/research/delver-engine` — Java source and engine-stub assets. Java paths
  below are relative to `/home/agent/research/delver-engine/core/src/main/java/`. 343 Java
  files under `core/`, 413 across the checkout including `desktop/` and `editor/`
  **[donor]** `/home/agent/research/delver-engine/core/src/main/java/`.
- `/home/agent/research/delver-game/delver.jar` — the compiled commercial distribution
  (2021-10-22 build) carrying the full content set; contents verified with
  `unzip -l /home/agent/research/delver-game/delver.jar` **[data]** `delver.jar`. Data
  paths in this note are jar paths unless prefixed `assets/` (meaning
  `/home/agent/research/delver-engine/assets/`).

Provenance tags used throughout both research notes: **[donor]** = a behavior documented at
the named donor source path or class; **[data]** = observed in the named donor data file;
**[uncertain]** = not directly verified. A claim without a path is unverified. Naming: the
root Java package is `com.interrupt.dungeoneer`, not "com.interrupt.dungeoneering" — older
writeups using the longer spelling are wrong **[donor]** `com/interrupt/dungeoneer/` layout.

### License note

- No LICENSE or COPYING file exists anywhere in `/home/agent/research/delver-engine`
  (`find /home/agent/research/delver-engine -iname "*license*"` returns nothing). Do not
  assume reuse terms from the checkout itself.
- Upstream `Interrupt/delverengine` tag v1.1.0 `README.md` points to `LICENSE.txt`: a
  zlib-style "Delver Open Source license", Copyright (C) 2018 Chad Cuddigan, five clauses
  (origin not misrepresented, altered versions marked, modifications redistributed,
  relicensing right retained by the author, notice preserved), cited from
  `raw.githubusercontent.com/Interrupt/delverengine/refs/tags/v1.1.0/LICENSE.txt`
  **[uncertain]** — external fetch, not in the local tree.
- The earlier survey's "MIT (Interrupting Bacon)" characterization
  (`/tmp/dungeon-survey/delver.md:9`) matches no file in the tree and is treated as
  incorrect **[uncertain]**. The same upstream README states the source release does
  **not** cover the game data — "the game data remains subject to the original copyright"
  — so `delver.jar` content is not under the source license at all **[uncertain]**
  (external README).
- Port note: this repo takes ideas and data shapes, never code. Donor data is reference
  material under gitignored `local/` (see `docs/research/delver-data-inventory.md`);
  runtime content is authored fresh under `content/`.

## Engine architecture

### Package roles

All rows **[donor]**, paths relative to `core/src/main/java/`:

| Package (directory) | Role | Key classes |
|---|---|---|
| `com/interrupt/dungeoneer/` | app shell, tick loop, input, art/audio glue | `GameApplication` (libGDX `Game`, owns screens), `GameManager` (tick/render wrapper), `GameInput`, `Art`, `Audio` |
| `com/interrupt/dungeoneer/game/` | run state, level, progression, mods | `Game` (run state, tick, save/load, level change), `Level` (tile map + entities + lighting), `GameData`, `Progression`, `ModManager`, `SavedLevelContainer`, `TravelInfo` |
| `com/interrupt/dungeoneer/game/gamemode/delver/` | all Delver-specific run rules | `DelverGameMode` (level layout, stairs, traps, death/win, escape effects) |
| `com/interrupt/dungeoneer/game/pathfinding/` | monster navigation | `PathfindingInterface`, `NodeGraphPathfinding` (default), `DoomStylePathfinding` (steering) |
| `com/interrupt/dungeoneer/tiles/` | tile model | `Tile`, `TileData`, `TileMaterials`, `ExitTile` |
| `com/interrupt/dungeoneer/generator/` | procedural dungeon | `DungeonGenerator`, `RoomGenerator`, `GenInfo`, `GenTheme`, `SectionDefinition`, `TexturePainter`, `rooms/`, `halls/`, `stairs/` |
| `com/interrupt/dungeoneer/gfx/` | renderer | `GlRenderer`, `Tesselator`, `WorldChunk`, `TextureAtlas`, `drawables/`, `animation/` |
| `com/interrupt/dungeoneer/entities/` | world objects | `Entity` → `Actor` → `Player` / `Monster`; `Item`, `Door`, `Stairs`, `Sprite`, `Model`, `triggers/`, `areas/`, `spells/`, `projectiles/`, `items/` |
| `com/interrupt/dungeoneer/statuseffects/` | timed effects | `StatusEffect` + `Burning`, `Poison`, `Slow`, `Paralyze`, `Invisibility`, `Shield`, … |
| `com/interrupt/dungeoneer/rpg/` | stats | `Stats`, `Class`, `ClassManager`, `Skill` (stubs) |
| `com/interrupt/dungeoneer/screens/` | app screens | `SplashScreen`, `MainMenuScreen`, `GameScreen`, `LevelChangeScreen`, `LoadingScreen`, `GameOverScreen`, `WinScreen`, `StatsScreen` |
| `com/interrupt/dungeoneer/overlays/` | modal UI stack | `OverlayManager`, `PauseOverlay`, `MapOverlay`, `MessageOverlay`, `LevelUpOverlay`, `SelectSaveSlotOverlay`, `ShopOverlay` |
| `com/interrupt/dungeoneer/ui/` | HUD widgets | `Hud`, `Hotbar`, `InventoryItemButton`, `EquipLoc`, `CharacterScreen`, `Tooltip`, `UiSkin` |
| `com/interrupt/dungeoneer/input/` | semantic input | `Actions`, `GamepadManager`, `GamepadDefinition` |
| `com/interrupt/dungeoneer/serializers/` | persistence | `KryoSerializer`, `v2/LevelSerializer`, `v1/LevelSerializer`, `TileSerializer` |
| `com/interrupt/managers/` | data registries | `ItemManager`, `MonsterManager`, `EntityManager`, `TileManager`, `HUDManager`, `ShaderManager`, `StringManager`, `MessageManager` |
| `com/interrupt/helpers/` | support | `LootListHelper`, `Message`, `PlayerHistory`, `ShopItem`, `Upgrade` |
| `com/interrupt/api/steam/`, `com/interrupt/dungeoneer/scripting/` | platform + mod scripting | `SteamApi`, `ScriptLoader` — out of scope for the port |

### Tick model

Delver is real-time with continuous physics over a grid; there is no turn clock. The timing
unit is a fixed **tick = 1/60 s**: `GameScreen.render` resets `delta = 1 / 60f`, clamps it
at `0.083f`, then calls `gameManager.tick(delta * 60f)` — the whole game counts in ticks
**[donor]** `com/interrupt/dungeoneer/screens/GameScreen.java:64-73`. `GameManager.tick`
coalesces sub-tick deltas (`time_since_last_tick < 0.3333333f` drops the frame) and owns
Esc/pause handling **[donor]** `com/interrupt/dungeoneer/GameManager.java:75-84`. All timers
are tick counts: `messageTimer = 60 * seconds` **[donor]** `com/interrupt/dungeoneer/game/Game.java:818-826`;
monsters swing every `attackTime = 60` ticks plus `rand(10)` **[donor]**
`com/interrupt/dungeoneer/entities/Monster.java:83`; `StatusEffect.timer` defaults to 1000
ticks **[donor]** `com/interrupt/dungeoneer/statuseffects/StatusEffect.java:18`.

`Game.tick` ordering **[donor]** `com/interrupt/dungeoneer/game/Game.java:480-525`:
message/flash timers → death check → `gameMode.tickGame` (input-driven actions) →
`level.tick(timeModifiedDelta)` (entities) → `player.tick(delta * actorTimeScale)` →
`input.tick()` → `Audio.tick` → `pathfindingManager.tick` → scene2d `ui.act` →
hotbar/backpack `tickUI` → `hud.tick`. Two time modifiers exist: global `gameTimeScale` and
per-actor `Actor.actorTimeScale` **[donor]** `com/interrupt/dungeoneer/entities/Actor.java`.

### Screens and overlay stack

`GameApplication` (libGDX `Game`) caches the core screens and switches between them via
static helpers; the full screen set lives in `screens/` (table above) **[donor]**
`com/interrupt/dungeoneer/GameApplication.java`, `com/interrupt/dungeoneer/screens/`.
`OverlayManager` layers a pausable modal stack on any screen (pause, fullscreen map, notes,
level-up pick, save-slot select, shops, options) **[donor]**
`com/interrupt/dungeoneer/overlays/OverlayManager.java`,
`com/interrupt/dungeoneer/overlays/`. Escape is centralized: close overlay → close
inventory/interact menu → push `PauseOverlay` **[donor]**
`com/interrupt/dungeoneer/GameManager.java`.

### Run flow

1. Title `SplashScreen` (any key, driven by `data/splash.dat`) → `MainMenuScreen` →
   `SelectSaveSlotOverlay` → `LoadingScreen` background-loads `Game(saveLoc)` **[donor]**
   `com/interrupt/dungeoneer/screens/`.
2. Run start: `Game.Start` loads `Progression`, builds the player from the `data/player.dat`
   template; `DelverGameMode.onGameStart` may substitute the one-time tutorial level
   **[donor]** `com/interrupt/dungeoneer/game/Game.java`,
   `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java`.
3. Descent: explore a generated floor, find the down stairs; `Game.changeLevel` /
   `doLevelChange` saves and places the player at `level.up` facing `exitRotation`;
   `levelNum` indexes the section-built level layout **[donor]**
   `com/interrupt/dungeoneer/game/Game.java`.
4. Orb escape: the objective is a `QuestItem`; `isHoldingOrb` switches on screen flashes,
   earthquake sounds and debris **[donor]** `com/interrupt/dungeoneer/entities/Player.java:121`,
   `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java:321-400`; ambient
   spawns escalate (`monsterSpawnTime` 600→60, `monsterCount` 3→15) **[donor]**
   `com/interrupt/dungeoneer/game/Level.java:2773-2782`.
5. Win: stairs-up at `levelNum == 1` without a `QuestItem` in inventory → "cannot leave"
   message; with one → level exit → `gameData.endingLevel` → `WinScreen`, `Progression`
   persisted **[donor]** `com/interrupt/dungeoneer/game/Game.java` (changeLevel),
   `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (onWin).
6. Death: `hp <= 0` → `player.die()` → `OnGameOver` → `deleteRunSave` → `GameOverScreen`
   **[donor]** `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java:81-100`.
7. Meta loop: `Progression` fields `gold` (start 40), `lowestFloor`, `experienceGained`,
   `messagesFound`, `wins`, `deaths`, `sawTutorial`, `inventoryUpgrades`, `hotbarUpgrades`,
   `uniqueTilesSeen`, `uniqueItemsSpawned`, `dungeonAreasSeen`, `modsUsed` survive the run;
   `wins` feeds New Game+ as `+ getNumberOfWins() * 2` to monster stats **[donor]**
   `com/interrupt/dungeoneer/game/Progression.java:9-40`, `com/interrupt/dungeoneer/entities/Monster.java:402`.

## Depth model

A run's floors come from **sections**. `SectionDefinition` carries `difficultyLevel`,
`sortOrder`, `name`, `floors`, `transitionLevel` (the floor after the last regular one),
weighted `levelTemplates`, and `levelTemplateDistribution`; `getDungeonLevel(floor) =
difficultyLevel + floor`, and `buildLevels()` orders templates by floor with the transition
level last **[donor]** `com/interrupt/dungeoneer/generator/SectionDefinition.java:9-100`.
Section shape example **[data]** `assets/generator/Test/section.dat` in
`/home/agent/research/delver-engine`:

```json
{ "difficultyLevel": 1, "sortOrder": 1, "name": "Test", "floors": 2,
  "levelTemplates": [ { "class": "com.interrupt.dungeoneer.game.Level",
      "theme": "TEST", "fogStart": 3.0, "fogEnd": 30.0,
      "generated": false, "levelFileName": "levels/test-level.bin" } ] }
```

Layout assembly: `DelverGameMode.getGameLevelLayout` prefers a predefined `data/dungeons.dat`
if any mod provides one; otherwise it scans every mod's `generator/*/section.dat`, sorts
sections by `sortOrder`, and concatenates their `buildLevels()` output **[donor]**
`com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (getGameLevelLayout).
Each floor template is a `Level` definition carrying theme, fog, music, spawn rates and
`generated` flag **[data]** `generator/Dungeon/section.dat` in `delver.jar`. Depth drives
difficulty: `Monster.Init` computes `levelDifficulty = (int)(dungeonLevel * 1.5f)`, adds
30% of any player-level overage, and New Game+ adds `getNumberOfWins() * 2` **[donor]**
`com/interrupt/dungeoneer/entities/Monster.java:349, 402`.

Stairs and exits are marker-driven. `GenInfo.Markers` is the spawn language (`playerStart`,
`exitLocation`, `stairDown`, `stairUp`, `monster`, `loot`, `key`, `boss`, `torch`, `decor`,
`door`, `secret`) **[donor]** `com/interrupt/dungeoneer/generator/GenInfo.java`.
`placeStairsDown` picks one `exitLocation` candidate for the down stairs when
`makeStairsDown` is set; every leftover candidate receives the level's `objectivePrefab` or
guaranteed good loot (armor, weapon, wand, potion or ranged weapon at
`player.level + rand(2)`) **[donor]**
`com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (placeStairsDown).

## Gameplay systems inventory

- **Movement and collision** — continuous velocity (`xa/ya/za`) with per-axis AABB
  collision, step-up, bounce, gravity, floor friction and water/lava timers; the tile grid
  answers `isFree`/`collidesWorldOrEntities` queries **[donor]**
  `com/interrupt/dungeoneer/entities/Entity.java`, `com/interrupt/dungeoneer/game/Level.java`.
  Player collision half-extents `(0.2, 0.2, 0.65)`, `stepHeight = 0.35`, `jumpHeight = 0.05`
  **[donor]** `com/interrupt/dungeoneer/entities/Player.java:118, 235`.
- **Combat** — universal roll: 15% dodge, else `damage = roll(1..attack) − armorClass`
  floored at 1, armor degrades on a hit **[donor]**
  `com/interrupt/dungeoneer/entities/Actor.java:146-163` (damageRoll). Player melee is a
  real-time charge attack: hold up to `attackChargeTime = 40` ticks, charge ratio scales
  weapon damage, `getAttackSpeed() = attackSpeed + DEX * 0.016` **[donor]**
  `com/interrupt/dungeoneer/entities/Player.java:101, 2087-2089`. Weapon roll:
  `baseDamage * attackPower + rand + elemental`, min 1 **[donor]**
  `com/interrupt/dungeoneer/entities/items/Weapon.java:93`. `DamageType`: PHYSICAL, MAGIC,
  FIRE, ICE, LIGHTNING, POISON, HEALING, PARALYZE, VAMPIRE **[donor]**
  `com/interrupt/dungeoneer/entities/items/Weapon.java:22`.
- **Monster AI** — flag states plus explicit timers, no state-machine class: `alerted`,
  `fleeing`, `keepDistance`, `chasetarget`, `hostile`, `AmbushMode` **[donor]**
  `com/interrupt/dungeoneer/entities/Monster.java`. Cull beyond 17 tiles in x or y; LOS via
  `canSeeIncludingDoors(..., 17)` **[donor]** `com/interrupt/dungeoneer/entities/Monster.java:456, 527`.
  Light-based stealth: notice radius is `playerdist < (player.visiblityMod * 15) + 3` — a
  dark player is noticed close, a lit one far **[donor]**
  `com/interrupt/dungeoneer/entities/Monster.java:557`, `com/interrupt/dungeoneer/entities/Player.java:224`.
  Repath every 50 ticks; give up when the path is lost and the player unseen **[donor]**
  `com/interrupt/dungeoneer/entities/Monster.java:575-586`. Pain roll `painChance (0.75) +
  damage fraction`; flee at `hp <= maxHp * fleeThreshold (0.25)` **[donor]**
  `com/interrupt/dungeoneer/entities/Monster.java:194-198, 435-439`. Pathfinding sits
  behind `PathfindingInterface`; default is `NodeGraphPathfinding` (swappable via
  `gameData.pathfindingMode`) **[donor]** `com/interrupt/dungeoneer/game/Game.java:123, 141`.
  `DoomStylePathfinding` is steering, not a BFS flood: try the direct move, else sweep
  ±270° in 15° steps with 110° turn acceptance, walkability tested with `StepHeight = 0.35`
  **[donor]** `com/interrupt/dungeoneer/game/pathfinding/DoomStylePathfinding.java:13-16, 131-155`
  (an earlier survey's "BFS" shorthand for this class is inaccurate).
- **Items and inventory** — flat slot array: `inventorySize = 23`, `hotbarSize = 5`
  defaults, both upgradable; keys bypass slots; first weapon auto-equips **[donor]**
  `com/interrupt/dungeoneer/entities/Player.java:137-138, 263-286`. The HUD lays out
  `quickSlots = Hotbar(6, 1, 0)` + `backpack = Hotbar(6, 3, 6)` over that array **[donor]**
  `com/interrupt/managers/HUDManager.java:6-7`. Equipment is an `equipLoc`-string-keyed map
  ("ARMOR"/"OFFHAND"/"RING"/"AMULET") **[donor]**
  `com/interrupt/dungeoneer/ui/EquipLoc.java`, `com/interrupt/dungeoneer/entities/Item.java`.
  Loot: level buckets with `GetLeveledLoot` window `[max(level*0.5, 1), level]`, 20% suffix
  + 20% prefix enchant rolls, unidentified chances, uniques once per run via
  `Progression.uniqueItemsSpawned` **[donor]** `com/interrupt/managers/ItemManager.java:121-135`,
  `com/interrupt/helpers/LootListHelper.java`, `com/interrupt/dungeoneer/game/Progression.java:23`.
- **Spells, wands, status effects** — `Spell` fields: `mpCost`, `baseDamage`, `randDamage`,
  `damageType`, `min/maxDistanceToTarget`, `applyStatusEffect`, `castVfx` **[donor]**
  `com/interrupt/dungeoneer/entities/spells/Spell.java`; `cast` spends mana (monsters),
  `zap` is free (wands/scrolls); a wand is `spell + charges + magicStatBoostMod` **[donor]**
  `com/interrupt/dungeoneer/entities/items/Wand.java`. `StatusEffect` fields: `timer`
  (1000), `speedMod`, `damageMod`, `magicDamageMod`, `shader`; same-class effects refresh
  rather than stack **[donor]**
  `com/interrupt/dungeoneer/statuseffects/StatusEffect.java:18-25`,
  `com/interrupt/dungeoneer/entities/Actor.java` (addStatusEffect).
- **Progression** — XP per kill `3 + monster.level` **[donor]**
  `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java:122-123`; level
  threshold `(level * 4) * (level * 2)` **[donor]**
  `com/interrupt/dungeoneer/entities/Actor.java:141-143`. Level-up: full heal plus one pick
  of three shuffled stats **[donor]**
  `com/interrupt/dungeoneer/overlays/LevelUpOverlay.java:204`; `maxHp = (int)(END * (END /
  3f)) + 4`, then `+= (level - 1) * 0.5` **[donor]**
  `com/interrupt/dungeoneer/overlays/LevelUpOverlay.java:178-180`.
- **Saves** — run dir `save/<slot>/` holds `player.dat` (JSON) and `levels/<n>.bin` (Kryo
  level snapshots); `Progression` lives outside the run dir at `<optionsDir>/game_<slot>.dat`,
  so it survives run deletion **[donor]** `com/interrupt/dungeoneer/game/Game.java:873-914, 1268-1293`.
  Save boundaries: every level change, pause, and run end, after `preSaveCleanup` strips
  transient state; a `saveVersion` field triggers migration **[donor]**
  `com/interrupt/dungeoneer/game/Game.java:1045-1074`. Permadeath: `deleteRunSave` removes
  the run dir on death and win — only `Progression` persists **[donor]**
  `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java:81-100`.
- **Audio** — filenames may be comma-separated variant pools, one picked at random
  (`filename.split(",")`) **[donor]** `com/interrupt/dungeoneer/Audio.java:109`; 3D
  positioned sounds and per-tile ambient cross-fade **[donor]**
  `com/interrupt/dungeoneer/Audio.java`, `com/interrupt/dungeoneer/entities/AmbientSound.java`.
  Music is shuffled track lists (comma-split at `Audio.java:307`) with volume driven by
  `hp / maxHp` — music fades near death **[donor]**
  `com/interrupt/dungeoneer/entities/Player.java:755-757`.
- **Input semantics** — one input owner (`GameInput`, libGDX `InputProcessor`) with per-tick
  `keyEvents`, mouse edge detection and pointer lock **[donor]**
  `com/interrupt/dungeoneer/GameInput.java`. Semantic `Actions` enum with defaults:
  `USE=E, ATTACK=SPACE, DROP=Q, INVENTORY=I, ITEM_NEXT=], ITEM_PREVIOUS=[, MAP=M, PAUSE=ESC,
  FORWARD/STRAFE=WASD, TURN/LOOK=arrows, JUMP` **[donor]**
  `com/interrupt/dungeoneer/input/Actions.java:36-44`. Number keys map to hotbar slots via
  `doHotbarAction(player, n)` **[donor]**
  `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java:227-230`.

## UI/HUD inventory

The HUD is drawn by `GlRenderer.drawUI` with a 2D batch and bitmap font; interactive slots
are scene2d widgets **[donor]** `com/interrupt/dungeoneer/gfx/GlRenderer.java:604`.

- **Health** — bottom-left bar scaled `hp / getMaxHp()` behind `ui/healthbar.png`; the
  `"hp/max"` text turns red at or below 20% (`hp > getMaxHp() / 5 ? WHITE : RED`); bar fill
  is green while poisoned, red otherwise **[donor]**
  `com/interrupt/dungeoneer/gfx/GlRenderer.java:1603-1623`.
- **Minimap** — 20×20-tile window around the player (`player.x - 10 … + 20`) cut from the
  map texture, which is painted at 4×4 px per tile **[donor]**
  `com/interrupt/dungeoneer/gfx/GlRenderer.java:621, 628-629`.
- **Fullscreen map** — `MapOverlay`, bound to `MAP` **[donor]**
  `com/interrupt/dungeoneer/overlays/MapOverlay.java`, `com/interrupt/dungeoneer/input/Actions.java:42`.
- **Inventory on the HUD** — quick slots + backpack always visible (hidden only via
  `Options.hideUI`), drag & drop to equip, tooltips near the cursor **[donor]**
  `com/interrupt/managers/HUDManager.java`, `com/interrupt/dungeoneer/ui/Hud.java`,
  `com/interrupt/dungeoneer/ui/InventoryItemButton.java`.
- **Message channels** — (1) transient centered message with countdown timer **[donor]**
  `com/interrupt/dungeoneer/game/Game.java:68, 818-826`; (2) per-tick `useMessage` prompt
  ("Press E to…") **[donor]** `com/interrupt/dungeoneer/game/Game.java:71`; (3) full lore
  notes in `MessageOverlay` fed by message files **[donor]**
  `com/interrupt/dungeoneer/overlays/MessageOverlay.java`,
  `com/interrupt/managers/MessageManager.java`; message-file shape **[data]**
  `data/messages/credits.dat` in `delver.jar`.
- **Crosshair** — a `"+"` glyph at 35% alpha, drawn center-screen **[donor]**
  `com/interrupt/dungeoneer/gfx/GlRenderer.java:160, 770-776`.

## Out of scope for the port

- **DelvEdit** level editor **[donor]** `/home/agent/research/delver-engine/editor/`,
  `DelvEdit.jar` — the port authors content in `content/`, not a donor editor.
- **Steam/Workshop integration** **[donor]** `com/interrupt/api/steam/`.
- **Mods loader and overlay merge** **[donor]**
  `com/interrupt/dungeoneer/game/ModManager.java` — content layout stays mod-shaped, the
  loader is not ported.
- **Overworld and travel** (`OverworldLevel`, `OverworldChunk`, `TravelInfo` warps,
  `game/terrain/`) — the linear depth run is the core loop **[donor]**
  `com/interrupt/dungeoneer/game/`.
- **Ink dialogue** (blade-ink) **[donor]**
  `com/interrupt/dungeoneer/entities/triggers/TriggeredDialogue.java`,
  `com/interrupt/dungeoneer/overlays/DialogueOverlay.java`.
- **Kryo binary format itself** **[donor]**
  `com/interrupt/dungeoneer/serializers/KryoSerializer.java` — keep only the idea of a level
  snapshot at save boundaries, via rusty-engine persistence primitives.
- **Mobile HUD and gamepad polish** **[donor]**
  `com/interrupt/dungeoneer/ui/MobileHud.java`,
  `com/interrupt/dungeoneer/input/GamepadManager.java` — keep the semantic `Actions`
  abstraction, skip the platform surfaces.
- **Mod Java scripting** (disabled upstream, delverengine issue #267) and the vestigial
  class/skill system **[donor]** `com/interrupt/dungeoneer/scripting/`,
  `com/interrupt/dungeoneer/rpg/ClassManager.java`, `com/interrupt/dungeoneer/rpg/Skill.java`.