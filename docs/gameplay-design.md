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
chain mail = Chainmail 7, iron helmet = Iron cap 2). Item conditions/degradation
([donor] `entities/items/Item.java` `ItemCondition`) are **deferred**.

**Swing cadence — faithful in timing.** A released swing plays out
before its blow lands.
- **Quick or full.** Below half charge it is the quick swing; from half up,
  the full one ([donor] `entities/Player.java:1578-1581`).
- **Timing.** It plays at `speed × 0.25 + (DEX − 4) × 0.015` (:1567), where
  `speed` is the donor weapon speed. The blow lands at
  `actionTime / playback × 0.5` ticks, and the next attack may start after
  `0.75` of the swing's `length / playback` ([donor]
  `entities/items/Sword.java:56-69`).
- **Swing styles.** Each weapon names a style in the pack's `swingStyles`,
  whose lengths and action times are the donor clips' ([data]
  `data/animations.dat` `daggerAttack` 7.5/5, `daggerAttackStrong` 11.25/6.5,
  `swordAttack` 12.5/10, `swordAttackStrong` 15.75/7, `maceAttack` 17.5/12,
  `maceStrongAttack` 18.75/9). The dagger, sword and mace take the dagger,
  sword and mace styles; [data] `items.dat` gives Steel shortsword the
  sword clips.
- **Example.** The rusty dagger's quick swing lasts 30 ticks at DEX 4 and
  lands at tick 10. The next attack can follow at tick 22.
- **Bows and wands.** A bow fires at release and sets no wait. A wand fires
  at release and waits on its 2.5-length clip at the donor's default 0.5
  speed (15 ticks); an auto-fire wand waits its fire interval.
- **Charging.** Walking while charging slows by up to
  `0.5 × (1.2 − DEX × 0.06)` (:879-884).
- **No unarmed attack.** An empty hand neither charges nor swings, as in the
  donor (:1597).

**Approximate:** a weapon without a swing style strikes on release and
waits the flat `attackCooldownTicks`; no shipped weapon lacks one.

**Ranged weapons — approximate.** Bows, wands and casting monsters fire
projectiles that the Kit flies over its own tile grid; the Engine only draws
them. A bow fires on release like the melee swing. It spends one arrow from
the first ammo stack in the pack ("You have no arrows." without one), rolls
the same weapon damage as a melee hit at the charged power, and launches at
`attackPower × range / 8` tiles per tick along the look direction. The arrow
then falls under the donor's gravity of 0.0035 per tick²
([donor] `entities/items/Bow.java:45-98` `doAttack`,
`entities/projectiles/Projectile.java:105-106`). A full draw of the hunting
bow (range 7, [data] `data/items.dat` Hunter's Bow) flies straight across a
room, while a tap drops at the player's feet. An arrow that hits a monster
stays in it and drops where it dies ([donor] `projectiles/Missile.java`
`addArrowLootToMonster`). An arrow that hits a wall, floor or ceiling breaks
one time in ten ([donor] `Missile.java` `breakChance` 0.1) and otherwise
lies on the last open tile as a pickup that rejoins the stack. Arrows come
in sixes (Arrows `count` 6 in `items.dat`).

A wand spends one of its charges per bolt; when it has none, it fizzles
instead of firing. A bolt rolls `power + roll(0..randDamage + max(0, MAG − 4))`
whatever the charge ([donor] `entities/items/Wand.java:82-125`,
`spells/Spell.java:115-122`). Bolts fly straight at the wand's speed, and the
wand's accuracy scatters them by up to `(1 − accuracy) × 45°`
([donor] `spells/MagicMissile.java:49-61`). The wand of sparks is the donor's
Lesser missile wand: magic 1+1, 30 charges, speed 0.45, accuracy 0.92, and
auto-fire, so holding the attack zaps once per 15 ticks
([donor] `entities/Player.java:1215, 1243-1249`, Wand `handAnimateTimer =
autoFireTime × 3`). A wand without auto-fire fires on release.

A monster with a `ranged` attack casts a straight bolt at the player while
alerted, in sight and inside its distance window. It waits 40–59 ticks after
being alerted, then casts once per `cooldownTicks` plus 0–29 ticks of jitter
([donor] `entities/Monster.java:555-565, 820-850`). A projectile hit skips
dodge and armor on both sides, because the donor's projectile calls
`hit`/`takeDamage` directly ([donor] `projectiles/Projectile.java:188-213`,
`entities/Player.java:2066-2070`).

**Approximate:**
- Flight is swept in steps of 0.1 tile against walls and closed doors, the
  floor (height 0) and the ceiling (height 1), instead of the donor's
  collision boxes.
- Bodies are cylinders: monsters have radius 0.3 and the player radius 0.25,
  both 0.8 tall.
- Knockback, splash damage, hit decals, trails and Zelda-style deflection are
  left out.
- An arrow flies as its item icon rather than a direction-facing sprite.
- Wand charges do not grow with magic (the donor's `magicStatBoostMod`).
- Monsters have no mana, so the EYE's `mpCost` is not modelled.
- In-flight projectiles and the arrows a monster carries are not saved.

**Damage types — approximate.** Weapons and monster bolts carry a damage
type: physical, magic, fire, ice, lightning, poison or paralyze (the donor's
`DamageType`, less healing and vampire). An elemental hit leaves the donor's
status effect ([donor] `statuseffects/StatusEffect.java:46-70`
`getStatusEffect`):
- fire sets the target burning half the time;
- ice slows;
- poison poisons;
- paralyze paralyzes;
- magic and lightning leave nothing.

The donor's magic resistance (`getMagicResistModBoost`) is not modelled. Bolts
are tinted by their type's donor colour and carry a point light of that
colour ([donor] `game/Colors.java`, `items/Weapon.java`
`getEnchantmentColor`); physical arrows carry none.

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
monster and take the ZOMBIE and EYE stats that match their art. The stone
golem is the EYE in behaviour too. It does not chase
([data] `monsters.dat` EYE `chasetarget: false`); it holds where it was
alerted and casts its magic missile: 3+3 magic damage at speed 0.1, every 100
ticks. Our spider and wraith content had been flagged ranged, but their donor
monsters (SPIDER, GHOST) are melee, so they are melee again.
**Approximate:** the donor's ±1 random level jitter above difficulty 4 is
left out (a regenerated floor's monsters come back at the same level), and movement speed and detect range
stay authored (the donor's `speed` is an acceleration in a different
movement model).

**Telegraphed monster attacks — faithful in timing.** Monster blows are no
longer instant.
- **Starting.** An alerted monster starts an attack once the player is within
  its 0.3 body plus `max(reach 0.6, attackStartDistance)` and in sight
  ([donor] `entities/Monster.java:753-762`). Its next attack waits
  `attackTime` (60) plus 0–9 ticks of jitter ([donor] `Monster.java`
  `attack()`, :1093-1132).
- **Winding up.** The monster stands still and plays its attack cells, as
  the donor holds it with `postAttackMoveWaitTimer` (:735-750).
- **Landing.** The blow comes when the animation reaches the donor's
  `DamageAction` frame: `attackWindupTicks` is that frame's time into the
  animation ([data] `monsters.dat` attack animations, speed being the whole
  sequence's ticks). The values are rat 47, slime 28, bat 15, kobold 35,
  spider 18, skeleton 18, ogre 27 and golem 19; the wraith, the donor GHOST,
  has no attack animation and strikes at once.
- **Dodging.** The blow lands only if the player is still within reach plus
  body (0.9) and in sight, as the donor's `tryDamageHit` re-checks
  (:1224-1259). Otherwise it "falls short", so stepping back dodges.
- **Lunging and flinching.** The rat (WORM) and bat start from further off
  (`attackStartDistance` 1.75 and 1.5) and lunge mid-wind-up (the donor's
  `ImpulseAction`, 0.09 and 0.07 tiles per tick at ticks 28 and 5). A
  flinch cancels the blow being wound up.
- **Separation.** With the blow's reach at 0.9 and the separation at 0.75, a
  monster holding at separation can hit, and one step back takes the
  player out of reach.

**Approximate:**
- The WORM's after-attack `StunAnimationAction` (`attackDelay` 100) is left
  out.
- The lunge ignores the impulse's vertical part.
- A lunge cannot carry a monster into the player's separation.

**Hit feedback — approximate.** A blow that lands does more than change
numbers.
- **Knockback.** It replaces the monster's velocity with the push times
  `min(knockback × 1.1, 0.6)` and stuns it for `ceil(5 × that)` ticks
  ([donor] `entities/Monster.java:939-952`). A melee push is the facing times
  the donor weapon's default 0.5 reach, the donor's `facing × usedist`
  ([donor] `items/Sword.java:95-100`); a projectile pushes with its own
  velocity. The knockback is the weapon's (dagger 0.1, short sword 0.2, mace
  0.4, bow 0.2, wand 0.01 — [data] `items.dat`) times the attack power.
  Knocked-back monsters slide under the donor's floor friction, keeping 80%
  per tick ([donor] `entities/Entity.java:465-493`), and stop at walls.
- **Pain.** A pain roll of `painChance + damage / maxHp × 0.5`, always on a
  killing blow, flinches the monster through its hurt animation. Meanwhile
  it cannot move, and a blow it was winding up is spoiled
  ([donor] `Monster.java:417-436`, :729-732). `painChance` and the flinch
  length are the donor monsters' (`painChance`, hurt animation speed, in
  [data] `monsters.dat`); the wraith, the donor GHOST, has no hurt animation
  and never flinches.
- **Death.** At zero hit points a monster staggers through its hurt cells
  for the donor's 22-tick `deathDelay` (:213, :464-468), unhittable and
  inactive. Then it falls: experience, loot and any arrows it caught drop,
  and a corpse plays its death cells once and stays on the floor, like the
  donor's `Corpse` ([donor] `entities/Corpse.java`). Corpses are not saved
  and vanish when the floor is left.
- **The player.** A monster's landed melee blow sets the player's velocity
  away from it at the monster's `attackKnockback` (the donor DamageAction's
  0.05; the kobold, the donor GOBLIN, hits at 0.15 —
  [donor] `Monster.java:1245-1250`). Any damage flashes the view red for 20
  ticks, fading linearly ([donor] `game/Game.java:849-854`,
  `gfx/GlRenderer.java:584-586`, `DelverGameMode.java:186-191`). The flash
  peaks at 0.6 red instead of the donor's full red and fades in five steps.
  It is an unlit, alpha-blended sprite of the authored white texture
  `content/delve/art/white.png`, tinted red and filling the view in the
  Engine's viewmodel layer. The natural quad, a retained mesh with a blended
  material, does not work on the pinned Engine: its renderer creates static
  mesh instances in the scene layer whatever layer the fact names
  (`render-wgpu` `apply.rs` `CreateStaticMeshInstance`), so the quad draws
  at the world origin. Filed upstream as rusty-engine task 9093.

**Approximate:**
- Knockback ignores monster weight and the stat knockback bonus.
- A monster's body fits by its centre tile ±0.3, not a collision box.
- A corpse inherits no velocity.

## Monster AI

**Noticing the player — approximate (light-based stealth).** A monster
notices the player when they are in sight within 17 tiles (a closed door
blocks sight) and closer than `3 + 15 × visibility` tiles: 3 in the dark,
18 fully lit ([donor] `entities/Monster.java:527, 554-566`). Visibility is
the light where the player stands, squared and capped at 1. Attacking sets
it to 1 for that tick ([donor] `entities/Player.java:852-854, 1371, 1620`).
A monster the player has hurt notices them on sight at any range, the
donor's `alertValue` (:946).
- **Light.** It comes from the floor's wall torches with the donor's
  falloff, `min(1, 2 × (1 − distance / 3.2))`, and a wall in between is full
  shadow ([donor] `game/Level.java:1734-1815`, `entities/Light.java:201-233`).
  The level ambient is black, as the donor's default is.
- **The player's own torch.** It does not count: the donor's player light is
  not a held item, and only held items' lights add to visibility.
- **Torches are a rules fact.** `TorchPlacement` and `LightLevel` live in
  the Kit, and the Host draws the session's torches.
- **Tuning.** The 3 and 15 are `noticeDarkTiles` and `noticeLightTiles` in
  the tuning profile.

**Approximate:**
- Light is sampled on the 2D tile grid with tile line of sight; the donor
  uses partial shadow and 3D light heights.
- Held light sources (a torch item) do not exist yet.
- The per-archetype `detectRange` this replaces is gone.

**Idle wandering — faithful in shape.** An idle monster wanders the donor's
way ([donor] `Monster.java:612-700`):
- it walks to a random open neighbouring tile, not straight back the way it
  came unless that is the only way;
- it moves at 60% of its chase speed (`getSpeed`, :881-895);
- it picks anew on arrival or every 100 ticks;
- on one roll in five it has a 3-in-20 chance to rest for 220 ticks.

`ambushes: true` keeps a monster still until it notices the player, the
donor's `AmbushMode.WaitToSee` (:38-52). No donor data uses it, and none of
ours does either. The donor has no sleep state, so there is none here.

**Chasing — approximate.** A chase follows a bounded grid BFS path, opens
closed doors in it, and flees below 25% HP ([donor] `entities/Monster.java`
flee threshold). It cuts corners: the monster heads for the furthest next
path tile its 0.3 half-width body has a clear straight run to. So it runs
diagonals and rounds corners instead of stepping from tile centre to tile
centre. The donor steers node to node over its node-graph "smell" flood
([donor] `game/pathfinding/NodeGraphPathfinding.java`). The chase gives up
once the player is out of sight beyond 17 tiles.

**Bodies do not overlap** ([donor] `entities/Monster.java`
`checkEntityCollision`):
- A chasing monster holds once it is within `actorSeparationTiles` (0.75)
  of the player and fights from there.
- The player cannot walk closer to a monster than that; stepping away is
  always allowed.
- A monster will not step into another monster's separation. It shoves the
  one in its way aside with a fifth of its own speed, as the donor's
  encroaching monster pushes ([donor] `Monster.java:1266-1297`).

**Approximate:** the donor's stuck detection and random path adjustment
(`tryPathAdjust`, :502-519, 912-925) are left out; the shove and the
cut-corner run cover the common jams.

Two donor flags shape movement once a monster is alerted:
- `chasesTarget: false` holds it where it stands (the donor's
  `chasetarget`);
- `keepsDistance` backs it away from a player closer than three tiles
  ([donor] `entities/Monster.java:549-552` `keepDistance`).

A ranged monster fights from wherever those leave it.

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
wand and food; ours is the rusty dagger, leather armor, a potion of healing,
the wand of sparks and bread. There is no pants slot, and potions and wands
are not drawn at random per run. Loot rolls use weighted tables over a
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
([donor] `statuseffects/StatusEffect.java`). Elemental hits apply them with
the donor effects' default timings. Burning lasts 600 ticks and poison 1000,
and each strikes for 1 every 160 ticks. Poison never takes the last hit point
([donor] `BurningEffect.java`, `PoisonEffect.java` `canKill`). Slow lasts 500
ticks at half speed, and paralysis lasts 500. **Approximate** — five kinds
instead of the donor's full set; no duration-scaled stacking rules beyond
refresh-with-max-magnitude.

## Doors, keys, traps, pots and triggers

**Locked doors and keys — approximate (a product vault).** A locked door
takes one key, opens and stays open, and says "The door is locked" without
a key ([donor] `entities/Door.java:67-71, 212-236` `isLocked`/`takesKey`;
keys are a plain count, [donor] `entities/Player.java:99, 1814-1816`). The
donor's generated floors never lock a door: its generator has no lock or key
markers. So the vault is this product's.
- On `vaultChance` of floors (0.6), one door is locked: one whose closing
  leaves the stairs reachable and cuts off a side room of at least six
  tiles.
- The room gets two extra finds.
- The key lies on the open side, at least six steps from the start and
  outside the entrance room. Every floor stays completable without opening
  it.
- Monsters cannot path through a locked door.

**Traps — the donor's intended rule.** Each open floor tile rolls
`trapChance` (1.2%) and stays more than 6 tiles (Chebyshev) from the start
and the stairs ([donor] `gamemode/delver/DelverGameMode.java:505-588`
`generateTraps`). That donor function never places a trap, because its
eligibility array is never set; this applies the rule it was written to
apply. Half the traps are spikes and half are pressure plates, the Dungeon
section's `["PressureTrap", "ProximitySpikes"]` ([data]
`generator/Dungeon/section.dat`).
- **Spikes** follow the donor's ProximitySpikes ([donor]
  `entities/Spikes.java`; [data] `entities.dat` ProximitySpikes). A body
  moving onto the tile springs them. They rise over 10 ticks and strike every
  body on the tile once for 2 at full extension. They stay up 20 ticks, sink,
  and rearm after 80. They are drawn as an authored bed of steel pyramids
  rising out of the floor (the donor's `spikes.obj` mesh is not imported)
  and hidden when flush.
- **Pressure plates** set off a random trap where they lie, the donor's
  PressureTrap → TriggeredTrap `random` ([donor]
  `entities/triggers/TriggeredTrap.java:35-46`): a fire burst for
  `6 + dungeon level / 2`, a poison burst for 2, or a teleport elsewhere on
  the floor. Any body presses a plate, which sinks while pressed.
- **Wall bolts.** `wallBoltRoomChance` (0.1) of rooms hide a tripwire. When
  the player crosses it, a wall 3–7 tiles away in line shoots a plain magic
  missile (2 + roll(0..2) magic, speed 0.17), then rests 200 ticks — the
  donor's Magic Missile Trap prefab ([data] `entities.dat:6146`, a
  PLAYER_TOUCHED trigger feeding an EntitySpawner). Room builders place it
  far more rarely in the donor.

**Triggers — approximate.** A touch trigger fires an id, and every trap
effect with that id answers. This is the donor's chain, `level.trigger(id)`
([donor] `entities/triggers/Trigger.java:222-226`, `game/Level.java:3073-3086`),
kept to the two chains generated floors use: plate → trap and tripwire →
wall bolt. A trigger fires once per step onto it and then waits out its
reset. Hand-authored chains (buttons, doors that trigger, messages) are left
out.

**Pots — faithful in rule.** The donor's three dungeon pots stand against
room walls on `potChance` (3%) of the tiles there ([data] `entities.dat`
Pot_0/Pot_1/Pot_Exploding, `dungeon_rooms.dat` pot wall prefab; sprites
from `textures/Dungeon/sprites.png` cells 4–6). They are solid. Any damage
counts: a swing, a projectile or a burst ([donor] `entities/Breakable.java:152`).
- The sturdy pot takes 2 points; the fragile and exploding ones take 1.
- A broken plain pot holds a surprise half the time and no loot ([data]
  `surpriseSpawnChance` 0.5, `lootSpawnChance` 0). The donor draws the
  surprise from its theme's bombs and monster spawners ([data]
  `generator/Dungeon/info.dat` surprises). Here it is, at even odds, a
  monster for the floor or a bomb that goes off after 40 ticks (6 fire
  within 1.5 tiles).
- The exploding pot bursts with no damage and shoves what stands within 3
  tiles, as its Explosion does (`impulseDistance` 3).

**Approximate:**
- Trap and burst reach is a radius of 1 tile.
- Spikes give no knockback.
- Crates and barrels, which are meshes in the donor, wait on decorations
  (task 9079).
- Spike timers are not saved; the traps, triggers and pots are.

Wand bolts are in (see Combat). The other spells — scrolls, beams, splash,
teleport ([donor] `entities/spells/*`) — are **deferred**.

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
effect timers, attack charge and cooldowns, velocities, the pursuit timer,
projectiles in flight and arrows lodged in monsters —
like the donor's `preSaveCleanup` ([donor] `game/Game.java` `save`); hit
points clamp to [0, maxHp] on restore. A wand's charges are kept. The donor's save migration story
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

**Lighting.** The dungeon is lit by point lights, like the donor's light
maps fall to near-dark away from torches. The Engine's neutral world and
viewmodel rigs are disabled in `DelveRpg.Host.csproj`; what remains is the
product's own: a faint cool ambient (0.06), the player's torch, wall
torches, and a hand light on the held weapon.
- The player carries a warm point light at eye height, the donor's player
  light ([donor] `entities/Player.java:173-176` `torchColor` (1, 0.8, 0.4),
  `torchRange` 3, `updatePlayerLight`): range 3.5, intensity 8.
- Wall torches are the donor's `Torch` ([data] `data/entities.dat` "Torch",
  [donor] `entities/Torch.java`): a fullbright flame sprite looping
  `sprites.png` cells 32–39 over 30 ticks and a point light of colour
  (1, 0.8, 0.2), range 4, intensity 8. The donor places them from room
  template markers ([donor] `generator/GenInfo.java` `Markers.torch`,
  `RoomGenerator.java:299-302`); our generated rooms have none, so
  the Kit's `TorchPlacement` hangs them on walls from the tile grid — floor tiles
  against a wall in a hashed order, at least 6 tiles apart, at most 48 —
  the same grid always getting the same torches.
- The held weapon is lit by a warm light parented to it, so it rides in the
  camera-local viewmodel layer (the donor tints the held item by the light
  where the player stands). The Engine requires a light's parent in the
  published scene, so the hand light is made after the weapon's first
  publish and released before a publish without it.
**Approximate:** intensities and ranges are tuned by eye for the Engine's
`1/distance` falloff with a range window; no flicker, no flame particles, no
baked light maps. Verified in play that the product's settings hold: with
every product light at zero the world renders black apart from the fullbright
torch sprites, and disabling the viewmodel rig blacks the weapon until the
hand light lights it.

**Sprites.** Monsters and ground items are Y-locked Engine billboard sprites
over donor sheet cells; the held weapon is a sprite in the Engine's
camera-local viewmodel layer. `content/delve/art/sprites.json` maps each
content `sprite` id to a sheet and cell; sheets are fixed 32px grids indexed
`column + row * columns` ([donor] `gfx/TextureAtlas.java:195-213`), and
monster walk/attack ranges and their timing (speed = whole sequence in ticks,
[donor] `gfx/animation/SpriteAnimation.java:79-102`) follow
[data] `data/monsters.dat`. Attack cells play right after a blow, the walk
cycle while hunting, the resting cell while idle. Projectiles are billboards
too. An arrow is the arrow item cell. A bolt is the donor's white particle
cells, looped and drawn fullbright: `particles.png` 52–55 for the wand and
88–95 for the EYE ([data] `items.dat`, `monsters.dat`
`magicMissileProjectile`). It is tinted by damage type at creation and drags
a coloured point light (intensity 4, range 2.5) that is created, moved and
disposed with it.

**Held weapon — approximate.** The held weapon is posed from
`content/delve/art/held.json`, with these clips per swing style:
- a charge clip, followed by the charge fraction;
- a quick and a full swing, played over the Kit's swing length. A swing
  starts from the pose it was released from, as the donor blends a new clip
  from the previous pose.

Keyframes interpolate linearly, the donor `LerpedAnimation` default
([donor] `gfx/animation/lerp3d/LerpedAnimation.java`, `Player.java:1374-1405`).
The weapon follows the donor's head bob,
`sin(tick × 0.319) × min(speed, 0.15) × 0.3`, at 0.55, and so does the camera
([donor] `Player.java:582`, `gfx/GlRenderer.java:452, 2034`).

**Deliberate divergence:** the keyframes are authored for this product.
They are not the donor's `animations.dat` clips, which are its game data.
They take the donor's kind of motion (the dagger stabs, then slashes; the
sword sweeps; the mace chops down) and peak where each swing's blow lands.
They move the sprite in the camera-local viewmodel layer by right/up/forward
offsets, a roll and a pitch, rather than the donor's decal rotations.

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
donor's alpha-trimmed regions; the giant rat,
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
