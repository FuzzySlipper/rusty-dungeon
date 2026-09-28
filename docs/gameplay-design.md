# Gameplay design

The product decides what a Delver-style run *is* here; this document records
every system's fidelity verdict against the donor so later work reconciles
against decisions instead of re-litigating them. A divergence recorded here is
a decision, not a defect. Donor claims carry paths; the research docs
([research/delver-survey.md](research/delver-survey.md),
[research/delver-data-inventory.md](research/delver-data-inventory.md)) hold
the full evidence.

Verdict vocabulary: **faithful** (same rule, same effect), **approximate**
(same role, simpler mechanism), **deliberate divergence** (a recorded
difference), **deferred** (slice not built yet), **skipped** (will not be
built).

## Timing

One admitted Engine step is one game tick at 60 Hz — the donor's tick is also
1/60 s and all its timers are tick counts
([donor] `screens/GameScreen.render`, `game/Game.java:480`). All Kit timers
(`attackChargeTicks`, effect durations, spawn cadences) are tick counts.
**Faithful.**

## Movement

Real-time continuous movement over a tile grid with friction and wall sliding.
The donor's velocity/friction/step-up physics
([donor] `entities/Player.java`, `collision/Collidor.java`) are simplified to
accelerate–friction–clamp with per-axis grid collision (body radius 0.25
tiles). Charging a swing slows the walk
([donor] `Player.java` `walkMod`). Look pitch clamps at ±80°.
**Approximate** (no slopes, jump, or water currents; `Water` tiles are floor
with a different color).

## Level model and generation

The donor tesselates a heightbox tile grid and grows prefab chunks through a
chunk graph ([donor] `generator/DungeonGenerator.java`, `tiles/Tile.java`).
This port owns a flat tile-kind grid (`Wall`, `Floor`, `DoorClosed`,
`DoorOpen`, `StairsDown`, `StairsUp`, `Water`) and generates rooms +
L-corridors with door seams, stairs from far exit candidates, and bonus loot
at leftover candidates ([donor] `gamemode/delver/DelverGameMode.java`
`placeStairsDown`). Generation is validated: disconnected or unusable floors
regenerate, and an impossible config fails loudly.
**Approximate** — the guarantees (connected floor, exits far from start,
seams become doors) are kept; the chunk-prefab system is not.

## Depth: sections and floors

**Faithful.** A run is ordered *sections*; each contributes `floors` templates
plus an optional transition floor, and a floor's difficulty is
`section.difficultyLevel + floor`
([donor] `generator/SectionDefinition.java:100`). Our `content/delve/packs/
delve-core.json` sections (Sewers → Temple → Caverns → Crypt → Frozen Deep)
mirror the donor's theme names as authored homage, not donor data. Monster
and loot eligibility windows are per dungeon level
([donor] `managers/ItemManager`, `LootListHelper`).

## Remembered map and visibility

Grid line of sight (Bresenham with wall/door blocking) feeds a sticky
per-tile explored flag in a window around the player; the HUD shows a map
window of the explored grid. The donor marches a parametric ray
([donor] `game/Level.java` `canSee`) and paints an incrementally updated map
texture ([donor] `gfx/GlRenderer.java` `makeMapTextureForLevel`,
`overlays/MapOverlay.java`). **Approximate** (no shadowcasting, no texture
painting — the DOM renders the cell grid).

## Combat

**Faithful core.** Physical attacks: flat dodge chance (0.15), then
`roll(1..attack) − armor class`, minimum 1
([donor] `entities/Actor.java` `damageRoll`). Attack magnitude folds weapon
power and the attack stat into one roll; armor class is defense stat + worn
gear. Player melee is a real-time charge attack: hold to wind up
(`attackChargeTicks`, 40 like the donor's `attackChargeTime`), the blow lands
on completion in a forward reach slice
([donor] `entities/Player.java` `doAttack`). **Deliberate divergence:** wands
and bows currently swing like melee weapons; ranged bolts and elemental
damage are a later slice. Item conditions/degradation
([donor] `entities/items/Item.java` `ItemCondition`) are **deferred**.

## Monster AI

Idle until the player is detected (range + line of sight), then chase along a
bounded grid BFS path; flee below 25% HP
([donor] `entities/Monster.java` flee threshold). **Deliberate divergence:**
the donor's node-graph paths and steering sweeps
([donor] `game/pathfinding/NodeGraphPathfinding.java`,
`DoomStylePathfinding.java`) are replaced by grid BFS; the donor's light-based
stealth (detection radius from light level,
[donor] `entities/Player.java` `updatePlayerLight`) is replaced by a fixed
per-archetype `detectRange`. Sleep/ambush flags are **deferred**.

## Items and inventory

The flat slot array on the HUD — hotbar first, then backpack — with named
equipment slots is **faithful in shape** ([donor] `ui/Hotbar.java`,
`ui/EquipLoc.java`, `managers/HUDManager.java`); slot counts grow with meta
upgrades ([donor] `game/Progression.java`). Walk-over pickup, number-key
use/wield, stackable gold/keys/potions. Loot rolls use weighted tables over a
per-item level window ([donor] `helpers/LootListHelper.java` level buckets).
Prefix/suffix enchantments ([donor] `entities/items/ItemModification.java`)
are **deferred** (the `enchantChance` tuning knob exists); uniques, bags, and
shops are **skipped/deferred**.

## Progression

**Faithful formulas.** Kill experience `3 + monsterLevel`
([donor] `entities/Actor.java` `addExperience`); cumulative level threshold
`(level*4)*(level*2)`; level-up heals to full and offers one of three shuffled
stats ([donor] `overlays/LevelUpOverlay.java:204`);
`maxHp = (int)(END*(END/3f)) + 4` then `+= (level-1)*0.5f`
([donor] `overlays/LevelUpOverlay.java:178-180`, kept with its float
semantics). Meta progression (gold, wins/deaths, hotbar/backpack upgrades)
survives runs like the donor's `Progression`. **Deferred:** spending gold on
upgrades between runs (the counters exist; the shop does not).

## Status effects

Timed effects that refresh rather than stack: poison/burn tick damage,
slow/haste speed multipliers, paralysis blocks action
([donor] `statuseffects/StatusEffect.java`). **Approximate** — five kinds
instead of the donor's full set; no duration-scaled stacking rules beyond
refresh-with-max-magnitude.

## Spells, doors, keys, traps

**Deferred.** Doors open without keys in this slice; keys are collectible but
locked doors ([donor] `entities/Door.java` `isLocked/takesKey`) and the spike
traps, breakables, and trigger chains ([donor] `entities/triggers/`) are not
placed yet. Wand bolts and scrolls ([donor] `entities/spells/*`) wait on the
ranged slice.

## The objective and the escape arc

Grab the objective on the deepest floor, then climb back to the surface;
leaving without it is refused ([donor] `game/Game.java` `changeLevel` win
gate). While it is held, pursuit spawns tighten their cadence and group size
([donor] `game/Level.java:2773-2782`). **Faithful in shape**; the escalation
curve is smoothed (cadence 600→60 ticks, groups 3→15).

## Saves and permadeath

Run state is snapshotted at every floor change, on pause, and on shutdown —
the donor's save boundaries ([donor] `game/Game.java` `save`) — as player
facts plus the current floor grid, monsters, and items. Death and victory
delete the run save; only meta progression survives
([donor] `gamemode/delver/DelverGameMode.java` `deleteRunSave`).
**Deliberate divergence:** ascended floors regenerate from the run seed
instead of loading floor snapshots (only the current floor is stored); the
donor's save migration story (`saveVersion`) is not needed at this shape.

## Presentation and audio

The first-person view is retained Engine geometry: tile floor quads and wall
boxes, actor plates, one perspective camera with distance fog handled by the
host background ([donor] `gfx/GlRenderer.java` distance fog is a shader
mix). **Deliberate divergence:** authored placeholder colors stand in for the
donor's texture atlases and light-mapped billboards; donor art and audio are
never committed (see AGENTS.md fidelity rules). Audio, music, and screen
flashes ([donor] `Audio.java`, `Game.flash`) are **deferred**.

## Content and mods

Content is typed JSON authored here (`content/delve/`), with the donor's
data-shape ideas: a classless typed schema, level windows, weighted loot,
section definitions ([donor] `assets/data/*.dat`, `jsonschema/current`).
The donor's mod overlay system ([donor] `game/ModManager.java`) is
**skipped** — the content layout is deliberately moddable later (one bundle
selects packs and tuning), but no overlay loader will be built in this repo's
foundation.
