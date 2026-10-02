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

Real-time continuous movement over a tile grid with friction and wall sliding,
**camera-relative like an FPS**: forward follows the look yaw and strafe is
its perpendicular (the donor's walk ([donor] `entities/Player.java`); the
same facing basis the use-reach slice uses). The donor's velocity/friction/
step-up physics ([donor] `entities/Player.java`, `collision/Collidor.java`)
are simplified to accelerate–friction–clamp with per-axis grid collision
(body radius 0.25 tiles). Charging a swing slows the walk
([donor] `Player.java` `walkMod`). Look pitch clamps at ±80°.
**Approximate** (no slopes, jump, or step-up heights; `Water` tiles are floor
with a different color).

## Level model and generation

The donor tesselates a heightbox tile grid and grows prefab chunks through a
chunk graph ([donor] `generator/DungeonGenerator.java`, `tiles/Tile.java`).
This port owns a flat tile-kind grid (`Wall`, `Floor`, `DoorClosed`,
`DoorOpen`, `StairsDown`, `StairsUp`, `Water`) and generates rooms +
L-corridors with door seams, stairs from far exit candidates, and bonus loot
at leftover candidates ([donor] `gamemode/delver/DelverGameMode.java`
`placeStairsDown`). Closed doors are *navigable* seams — connectivity and
monster paths run through them because any actor can open them — while
sight and player movement stay blocked until the door is opened. The run
starts on safe ground: no hostile spawns land in the entrance room or its
doorway seams, and every other monster spawn sits at least six navigation
tiles away. Generation is validated: disconnected or unusable floors
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

**Faithful.** The donor's two attack shapes. A monster's blow is
`Actor.damageRoll`: flat dodge chance (0.15), then `roll(1..attack) − armor
class`, minimum 1 ([donor] `entities/Actor.java:146-168`); the player's
armor class is worn gear plus a point per defense above 4
([donor] `entities/Player.java:2179-2208` `GetArmorClass`). The player's
melee is a real-time charge: holding winds the charge up to the weapon's
full charge and holds it there, and releasing swings at
`attackPower = charge / full` ([donor] `entities/Player.java:1221-1258,
1588-1592`) into a forward reach slice. A hit deals
`(int)(baseDamage × attackPower) + roll(0..randDamage + max(0, ATK − 4))`,
minimum 1, straight onto the monster with no dodge or armor
([donor] `entities/items/Weapon.java:93-104`, `entities/Monster.java:939-957`).
Weapon `power` is the donor `baseDamage`, `randDamage` its random part, and
`chargeTicks` the donor's 40-tick `attackChargeTime` divided by the weapon's
`speed` (the donor's charge speed); weapon and armor values are the donor
items' ([data] `data/items.dat`: rusty dagger = Iron dagger 2+1, short sword =
Steel shortsword 5+3, mace = Iron mace 2+2, hunting bow = Hunter's Bow 3+6 at
speed 1.125, wand of sparks = Lesser missile wand 1+1; leather armor 3,
chain mail = Chainmail 7, iron helmet = Iron cap 2). **Approximate:** an
unarmed swing hits for 1 (the donor has no unarmed swing), and the cooldown
after a swing is a flat `attackCooldownTicks` rather than the donor's
animation-length wait. **Deliberate divergence:** wands
and bows currently swing like melee weapons; ranged bolts and elemental
damage are a later slice. Item conditions/degradation
([donor] `entities/items/Item.java` `ItemCondition`) are **deferred**.

**Monster stats — faithful.** Each monster carries its donor counterpart's
base hit points and `atk` ([data] `data/monsters.dat`), armor class 0 (no
donor monster sets `ac`), no weapon power, and the donor's 60-tick
`attackTime` ([donor] `entities/Monster.java:83`). A monster spawns at
`max(floor(dungeonLevel*1.5), baseLevel)`, bumped by 30% of any lead the
player has over that difficulty ([donor] `Monster.java:346-366` `Init`), and
each level past the first adds 2 hit points and widens its attack roll by 2
([donor] `entities/Actor.java:122-139` `initLevel` raises `atk` by the level
and `Monster.java:1240` rolls `atk + level`); `MonsterScaling` keeps that as
written. Our stand-ins map to donor monsters: giant rat = WORM, slime =
SLIME, cave bat = the CAVE BAT, kobold = GOBLIN, giant spider = SPIDER,
skeleton = SKELETON, wraith = GHOST; the ogre and stone golem have no donor
monster and take the ZOMBIE and EYE stats that match their art.
**Approximate:** the donor's ±1 random level jitter above difficulty 4 and
its 0–9 tick attack-timer jitter are left out (a regenerated floor's
monsters come back at the same level), and movement speed and detect range
stay authored (the donor's `speed` is an acceleration in a different
movement model).

## Monster AI

Idle until the player is detected (range + line of sight — a closed door
blocks detection), then chase along a bounded grid BFS path, opening closed
doors in the path; flee below 25% HP
([donor] `entities/Monster.java` flee threshold). **Deliberate divergence:**
the donor's node-graph paths and steering sweeps
([donor] `game/pathfinding/NodeGraphPathfinding.java`,
`DoomStylePathfinding.java`) are replaced by grid BFS; the donor's light-based
stealth (detection radius from light level,
[donor] `entities/Player.java` `updatePlayerLight`) is replaced by a fixed
per-archetype `detectRange`. Bodies do not overlap, like the donor's entity
collision ([donor] `entities/Monster.java` `checkEntityCollision`): a chasing
monster holds once it is within `actorSeparationTiles` (0.75) of the player
and fights from there, and the player cannot walk closer to a monster than
that (stepping away is always allowed). **Approximate** — monsters may still
overlap each other. Sleep/ambush flags are **deferred**.

## Items and inventory

The flat slot array on the HUD — hotbar first, then backpack — with named
equipment slots is **faithful in shape** ([donor] `ui/Hotbar.java`,
`ui/EquipLoc.java`, `managers/HUDManager.java`); slot counts grow with meta
upgrades ([donor] `game/Progression.java`). Walk-over pickup, number-key
use/wield, stackable gold/keys/potions.

A fresh character starts with the pack's `startingKit`, in slot order, with
the first weapon wielded and the first armor worn — the donor's
`startingInventory` ([data] `data/player.dat`, [donor]
`entities/Player.java:297-334` `makeStartingInventory`). **Approximate**: the
donor gives an iron dagger, leather armor, leather pants, and a random potion,
wand and food; ours is the rusty dagger, leather armor, a potion of healing
and bread. There is no pants slot, the wand waits on the ranged slice, and
potions are not shuffled per run. Loot rolls use weighted tables over a
per-item level window ([donor] `helpers/LootListHelper.java` level buckets).
Prefix/suffix enchantments ([donor] `entities/items/ItemModification.java`)
are **deferred** (the `enchantChance` tuning knob exists); uniques, bags, and
shops are **skipped/deferred**.

## Progression

**Faithful formulas.** Kill experience `3 + level`, where the donor's
`level` field is the spawn level minus one ([donor]
`gamemode/delver/DelverGameMode.java:123`, `entities/Actor.java` `initLevel`); cumulative level threshold
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
facts plus the current floor grid, monsters, items, and a pending level-up
offer. Death and victory delete the run save; only meta progression survives
([donor] `gamemode/delver/DelverGameMode.java` `deleteRunSave`). A save whose
run index no longer fits the rebuilt plan, or that records a finished run, is
discarded on resume. **Deliberate divergence:** ascended floors regenerate
from the run seed instead of loading floor snapshots (only the current floor
is stored), and the save boundary strips transient combat state — status
effect timers, attack charge and cooldowns, velocities, the pursuit timer —
like the donor's `preSaveCleanup` ([donor] `game/Game.java` `save`); hit
points clamp to [0, maxHp] on restore. The donor's save migration story
(`saveVersion`) is not needed at this shape.

## Randomness

Two deterministic sources, one division. Floor generation and run-plan
template draws use the Kit's seeded `SplitMixRandom` keyed by `(run seed,
run index)`, because an ascended floor must replay identically no matter how
many live draws happened elsewhere. Everything that happens *during* a run —
combat rolls, loot, level-up offers, escape spawns — draws through the
injected `IRandomSource`, which the Host answers with the Engine's keyed
random service (`IRandomService.DrawKeyed`, keys counter-based per run).
The Kit's `IRandomSource.Next` is half-open; the Host translates to the
Engine's inclusive bounds in `KeyedRandomSource` alone. **Deliberate
divergence:** draw counters are not saved, so a resumed run restarts the
keyed draw sequence rather than continuing the unloaded run's — the same
transient amnesty as §Saves; runs remain fair and deterministic within one
continuous session.

## Presentation and audio

**Ceilings.** Every open tile gets a ceiling quad at wall height, as the
donor tesselates one per tile ([donor] `gfx/Tesselator.java:430-472`),
textured with the donor's default ceiling cell (t1 cell 1). One retained
ambient light keeps the downward faces readable under the Engine's default
rig, standing in for the donor's level ambient. **Deliberate divergence:** no
per-theme ceiling painters (the Sewer theme's cells 10/11 live in an atlas
this slice does not stage) and no sky tiles.

**Torch.** The player carries a warm point light at eye height, the
donor's player light ([donor] `entities/Player.java:173-176` `torchColor`
(1, 0.8, 0.4), `torchRange` 3, `updatePlayerLight`). **Approximate:** range 4
at intensity 1.0 under the Engine's falloff, tuned by eye; no flicker, and
the Engine's default rig still lights the level from above (the donor bakes
light maps instead).

**Sprites.** Monsters and ground items are Y-locked Engine billboard sprites
over donor sheet cells; the held weapon is a sprite in the Engine's
camera-local viewmodel layer. `content/delve/art/sprites.json` maps each
content `sprite` id to a sheet and cell; sheets are fixed 32px grids indexed
`column + row * columns` ([donor] `gfx/TextureAtlas.java:195-213`), and
monster walk/attack ranges and their timing (speed = whole sequence in ticks,
[donor] `gfx/animation/SpriteAnimation.java:79-102`) follow
[data] `data/monsters.dat`. Attack cells play right after a blow, the walk
cycle while hunting, the resting cell while idle.

Sprites are lit by the scene's lights through a normal sheet per sprite sheet
(`lighting.mode` `authored-normal`, strength 1.5). The normal sheets are
derived offline by `scripts/derive-sprite-normals.py` — a dome over each
sprite's silhouette plus a little luminance relief — and staged beside the
donor sheets; without them sprites draw unlit. Compared in play under the
torch: `derived-gradient` (the shader bumps the colour's red channel) embosses
the pixel grid and dark outlines into grooves; `synthetic` is visually flat
on atlas sprites, because the Engine's dome spans the whole atlas UV rather
than the cell; authored normals give the soft rounded volume the donor's
light-mapped billboards suggest. **Deliberate divergences:** lit normals
rather than the donor's light-map tint, and whole cells rather than the
donor's alpha-trimmed regions; the held weapon follows a simple charge-rise and
cooldown-sweep pose instead of the donor's keyframed `daggerCharge` /
`daggerAttack` animations ([data] `data/animations.dat`); the giant rat,
ogre and stone golem have no donor monster and borrow the worm, zombie and
eye art. Palette-mode sheets (armor.png) are expanded to RGBA when staged,
since the Engine admits RGB/RGBA PNGs.


The first-person view is retained Engine geometry: tile floor and ceiling quads and wall
boxes, actor sprites, one perspective camera with distance fog handled by the
host background ([donor] `gfx/GlRenderer.java` distance fog is a shader
mix). Level tiles carry the donor's tile atlases ([donor] `data/tiles.dat`,
`data/walltextures.dat` fixed-grid sheets): the authored manifest
`content/delve/art/tiles.json` maps tile roles (floor, wall, water, door, ceiling) to
atlas cells, the extract script stages the named sheets into the gitignored
`content/delve/imports/art/` (Engine textures open from product content
only), and theme tints multiply the texture. With no staged art the level
keeps its authored placeholder colors — the product runs identically without
donor pixels, which are never committed (see AGENTS.md fidelity rules).
**Deliberate divergence:** one cell per role rather than the donor's
per-theme `texturePainters` ([donor] `generator/*/info.dat`) and light-mapped
billboards; stairs stay vertex-colored, and actors without staged sheets fall
back to placeholder plates. Audio, music, and
screen flashes ([donor] `Audio.java`, `Game.flash`) are **deferred**.

## Content and mods

Content is typed JSON authored here (`content/delve/`), with the donor's
data-shape ideas: a classless typed schema, level windows, weighted loot,
section definitions ([donor] `assets/data/*.dat`, `jsonschema/current`).
The donor's mod overlay system ([donor] `game/ModManager.java`) is
**skipped** — the content layout is deliberately moddable later (one bundle
selects packs and tuning), but no overlay loader will be built in this repo's
foundation.
