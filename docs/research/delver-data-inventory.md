# Donor data inventory: delver.jar and delver-engine

Companion to `docs/research/delver-survey.md`. Provenance tags are the same: **[donor]**
(documented at a named donor source path), **[data]** (observed in the named donor data
file), **[uncertain]** (not directly verified). Donor roots:

- `/home/agent/research/delver-engine` — source + engine-stub assets; paths prefixed
  `assets/` live here.
- `/home/agent/research/delver-game/delver.jar` — the compiled commercial content set
  (2021-10-22 build); unprefixed data paths are jar paths.

Counts below were produced with `unzip -l /home/agent/research/delver-game/delver.jar`
piped through `awk`/`sort`/`uniq -c`, and `find`/`wc -l` against
`/home/agent/research/delver-engine`.

## What is where

| Location | What it is | Verified size |
|---|---|---|
| `assets/` in delver-engine | engine stub content: empty tables (`items.dat` is 407 B, `monsters.dat` 120 B), greybox theme `assets/generator/Test/`, one test level, shaders, meshes, UI skin | 189 manifest lines in `assets/assets.txt`; 190 files on disk including the manifest **[data]** `assets/assets.txt` |
| `delver.jar` | the commercial content set: audio, levels, textures, meshes, fonts, shaders, `.dat` tables, plus vendored libraries and natives | extension counts: 3,169 `.class`, 415 `.mp3`, 212 `.bin`, 142 `.obj`, 109 `.png`, 78 `.dat`, 40 `.frag`, 37 `.vert`, 10 `.fnt`, 9 `.json`, 8 `.atlas` **[data]** `delver.jar` |
| `jsonschema/current/` in delver-engine | JSON Schemas (draft-06) for every data class | 103 files = 88 `*.schema.json` (87 class schemas + `Interfaces.schema.json`) + 15 `filetypes/*.dat` **[donor]** `jsonschema/current/` (`find … -type f \| wc -l` = 103) |

`DelvEdit.jar` (same directory) carries the identical data set plus editor bytecode
(5,344 entries, 3,903 `.class`) — data entries are the same categories as `delver.jar`
**[uncertain]** (from `/tmp/dungeon-survey/delver-content-format-survey.md`, not
re-listed here).

## .dat content files

All `.dat` files are relaxed JSON (libGDX `Json`, unquoted keys tolerated), read through
`com/interrupt/utils/JsonUtil.java` **[donor]**. The `"class"` field is a Java
class-name discriminator; the port drops discriminators and interprets shapes in typed C#.

| File (delver.jar) | Shape | Reader **[donor]** |
|---|---|---|
| `data/game.dat` | GameData manifest: `gameMode` class, `entityDataFiles`/`monsterDataFiles`/`itemDataFiles`, `playerDataFile`, `hudDataFile`, `playerJumpEnabled`, `tutorialLevel`, `endingLevel`, `overrideBaseGame` | `com/interrupt/dungeoneer/game/GameData.java`, loaded via `com/interrupt/dungeoneer/game/Game.java` |
| `data/entities.dat` | `{entities: {category: {name: EntityLike}}}` prefabs | `com/interrupt/managers/EntityManager.java` |
| `data/items.dat` | ItemManager tables: `melee/armor/ranged/unique/wands/potions/food/scrolls/decorations/junk` + enchantment maps | `com/interrupt/managers/ItemManager.java` |
| `data/monsters.dat` | `{monsters: {category: [Monster]}}` | `com/interrupt/managers/MonsterManager.java` |
| `data/player.dat` | serialized Player template | `com/interrupt/dungeoneer/entities/Player.java` |
| `data/hud.dat` | `{quickSlots, backpack}` Hotbar defs | `com/interrupt/managers/HUDManager.java` |
| `data/tiles.dat` | `{tileData: {atlas: {index: TileData}}}` | `com/interrupt/managers/TileManager.java` |
| `data/strings.dat` | `{key: LocalizedString}` — dotted code keys plus item/enchantment names, plural forms | `com/interrupt/managers/StringManager.java` |
| `data/animations.dat` | `{animations: {name: LerpedAnimation}}` | merged in `com/interrupt/dungeoneer/game/ModManager.java` |
| `data/spritesheets.dat` | array of `TextureAtlas` (entity sheets) | `com/interrupt/dungeoneer/gfx/GlRenderer.java` |
| `data/walltextures.dat` | array of `TextureAtlas` (tile sheets) | `com/interrupt/dungeoneer/gfx/GlRenderer.java` |
| `data/shaders.dat` | array of `ShaderData` (`name`, `vertex`, `fragment`, `attributes`) | `com/interrupt/managers/ShaderManager.java`, merged in `game/ModManager.java` |
| `data/splash.dat` | `SplashScreenInfo` (backgrounds, logo, music) | `com/interrupt/dungeoneer/screens/SplashScreen.java` |
| `data/dungeons-testing.dat` | array of `Level` — predefined layout; note the code looks for `data/dungeons.dat`, which no shipped jar provides, so live runs use the section scan | `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (getGameLevelLayout) |
| `data/quests.dat` | plain narrative text, not JSON | no reader found in `core/` **[uncertain]** |
| `data/room-builders/{cave,dungeon,sewer,temple}_rooms.dat` | room-builder configs (`secretDoors`, `defaultMaterial`, `prefabs`, `lakes`) | `com/interrupt/dungeoneer/generator/RoomGenerator.java` |
| `data/messages/*.dat` (28 files) | `{repeats: false, messages: [["line1","line2"]]}` note/dialogue text | `com/interrupt/managers/MessageManager.java` |
| `generator/*/info.dat` (6) | `GenTheme` (chunk size, complexity, `texturePainters`, `genInfos` spawn rules) | `com/interrupt/dungeoneer/generator/DungeonGenerator.java` |
| `generator/*/section.dat` (5) | `SectionDefinition` (depth plan, inline `Level` templates) | `com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (getGameLevelLayout) |
| `levels/test/*.dat` (10) | legacy JSON levels (`SavedLevelContainer`), e.g. `beerhall.dat`, `slimeking.dat` | `com/interrupt/dungeoneer/game/SavedLevelContainer.java`, fallback in `com/interrupt/dungeoneer/game/Game.java` load path |

Engine-stub equivalents of the `data/` set live at `assets/data/` in delver-engine (13
top-level `.dat` files plus `ink-test.ink`/`ink-test.json`) **[data]** `assets/data/`.

## Generator themes (delver.jar `generator/`)

188 files across 7 theme folders: 167 `.bin` room templates + 21 `.dat` (11 `info.dat`/
`section.dat` + 10 JSON room templates) **[data]** `delver.jar`.

| Theme | `info.dat` | `section.dat` | room templates | template subdirs (file counts) |
|---|---|---|---|---|
| Dungeon | yes | yes | 99 `.bin` | Beginnings 8, Corners 15, Ends 12, Halls 23, Intersections 17, Starts 13, TriIntersections 11 |
| Sewer | yes | yes | 36 `.bin` | Corners 6, Ends 6, Halls 6, Intersections 7, Starts 4, TriIntersections 7 |
| Cave | yes | yes | 14 `.bin` + 7 `.dat` | Corners 4+2, Ends 3, Halls 2+1, Intersections 1+1, Starts 2+2, TriIntersections 2+1 |
| Undead | yes | yes | 18 `.bin` + 3 `.dat` | Corners 1+2, Ends 4, Halls 2+1, Intersections 3, Starts 6, TriIntersections 2 |
| Cold | yes | — | — | theme only (`textures/Cold/`) |
| Outdoor | yes | — | — | theme only (`textures/` outdoors sheets) |
| StartCamp | — | yes | — | section only |

**[data]** all cells: `unzip -l … delver.jar` listings under `generator/`. Only themes with
a `section.dat` join `getGameLevelLayout`'s section scan **[donor]**
`com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java`; Cold and Outdoor reach
a run through other sections' `levelTemplates` theme strings — `Dungeon/section.dat`
references `DUNGEON` + `CAVE`, `Cave/section.dat` `CAVE` + `COLD`, `Sewer/section.dat`
`SEWER`, `Undead/section.dat` `UNDEAD`, `StartCamp/section.dat` `OUTDOOR` **[data]**
`generator/*/section.dat` in `delver.jar`. `generator/Test/` (`info.dat` + `section.dat`)
is a delver-engine stub, not a jar theme **[data]** `assets/generator/Test/`.

## Levels and room templates (.bin)

212 Kryo `.bin` files = 167 generator room templates + 45 under `levels/` (24 root levels
such as `start.bin`, `tutorial.bin`, `bosslevel*.bin`, `*-screen-splash.bin`,
`sewer-transition.bin`, `shop-interstitial.bin`; `levels/Cold/coldarea.bin`; 20 in
`levels/test/`) **[data]** `delver.jar`. Hand-authored floors and save snapshots share one
format: Kryo binary written by
`com/interrupt/dungeoneer/serializers/v2/LevelSerializer.java`; per-floor load order is
`<n>.bin` → `<n>.lvl` → `<n>.dat` (legacy JSON) **[donor]**
`com/interrupt/dungeoneer/game/Game.java` (load path, ~lines 959-1035). Level templates
point at authored floors via `Level.levelFileName` **[data]**
`assets/generator/Test/section.dat`.

## PNG atlas layout convention

Texture "atlases" are fixed-grid sheets with no packer step and no packing metadata; the
column count is declared per atlas in `data/spritesheets.dat` / `data/walltextures.dat`,
and tiles/items reference frames by integer index (`tex`, `heldTex`, `inventoryTex`,
`particleTex`) plus a named atlas **[donor]**
`com/interrupt/dungeoneer/gfx/TextureAtlas.java` (shape: `columns`, `filename`, `name`,
`scale`, `y_offset`, `shader`, `filter`).

- `data/walltextures.dat` — 8 tile atlases, all `columns: 4`: `t1` (`textures.png`), `t2`
  (`textures1.png`), `room` (`textures/Room/textures.png`), `dungeon`, `cave`, `cold`,
  `sewer`, `temple` (`textures/<Theme>/textures.png`) **[data]** `data/walltextures.dat`
  in `delver.jar`.
- `data/spritesheets.dat` — entity/item sheets: `entity` (`entities.png`, 16 cols),
  `texture`/`sprite`/`fog_sprites` (4 cols), `item`/`armor`/`melee`/`wands`/`unique`
  (8 cols, `scale: 0.5`, `y_offset: 0.25`), `particle`/`particle_large` (8), `font`
  (16 cols), theme entity sheets `entities_dungeon/sewer/cave/undead/boss/surprise` and
  `npcs` (16 cols), `critters` (4), plus per-theme sprite sheets
  **[data]** `data/spritesheets.dat` in `delver.jar`.
- Root sheets (35 `.png`) plus `textures/<Theme>/` sets (each `textures.png`,
  `sprites.png`, `meshes.png`, `doors.png`) and `ui/`, `splash/` art make up the 109
  `.png` total **[data]** `delver.jar`. One stray non-atlas file sits in the audio tree:
  `audio/particles.png` **[data]** `delver.jar`.

## Audio naming

All audio is `.mp3` (javazoom decoder; `tools/convert-audio.sh` batch-converts ogg → mp3)
**[donor]** `/home/agent/research/delver-engine/tools/convert-audio.sh`. 415 files:
`mobs/` 206, `footsteps/` 25, `music/` 23, `magic/` 23, `drops/` 15, `ui/` 14, `break/` 8,
`rustle/` 7, `inventory/` 3, `ambient/` 3, `potions/` 2, `trap/` 1, plus 85 root-level sfx
(`attack*.mp3`, `hit*.mp3`, `door_stone_*.mp3`, `pu_gold.mp3`, …) **[data]** `delver.jar`.

Variant convention: numbered suffixed pools — `hit.mp3`, `hit_02.mp3`, `hit_03.mp3`,
`hit_04.mp3`; `wep_swing_heavy_01.mp3`…`_04` — and consumers reference whole pools as
comma-separated lists (`"break/earthquake1.mp3,break/earthquake2.mp3"`), one picked at
play time **[donor]** `com/interrupt/dungeoneer/Audio.java:109`
(`filename.split(",")`); example pool in use **[donor]**
`com/interrupt/dungeoneer/game/gamemode/delver/DelverGameMode.java` (tickEscapeEffects).

## Fonts, shaders, meshes

- **Fonts** — 10 `.fnt`: `ui/pixel.fnt` (BMFont game font, loaded by
  `com/interrupt/dungeoneer/ui/UiSkin.java`) **[donor]**; 8 `Roboto*.fnt` under
  `ui/editor/HoloSkin/` (DelvEdit skin) and `com/badlogic/gdx/utils/arial-15.fnt`
  (libGDX internal) **[data]** `delver.jar`. The in-game bitmap font texture is
  `font.png` (16-column glyph sheet) **[data]** `data/spritesheets.dat`.
- **Shaders** — 77 GLSL files (40 `.frag` + 37 `.vert`, including `shaders/android/`
  variants), referenced by name from `data/shaders.dat` / entity `shader` fields **[data]**
  `delver.jar`; program registry **[donor]**
  `com/interrupt/managers/ShaderManager.java`.
- **Meshes** — 142 `.obj`: 72 root props (`chair.obj`, `door_*`, `statue1.obj`, …),
  per-theme sets (`Dungeon/` 42, `Cold/` 10, `Temple/` 8, `Sewer/` 7), and `shadows/`
  imposters (`blob.obj`, `rectangle.obj`, `sphere.obj`) **[data]** `delver.jar`; referenced
  by `Model.meshFile` and `DrawableMesh` **[donor]**
  `com/interrupt/dungeoneer/entities/Model.java`,
  `com/interrupt/dungeoneer/gfx/drawables/DrawableMesh.java`.

## The JSON schema system

103 files under `jsonschema/current/` (verified count above): 87 class schemas mirroring
the Java package tree (`dungeoneer/entities/Monster.schema.json` ↔
`com.interrupt.dungeoneer.entities.Monster`), `Interfaces.schema.json`, and 15
`filetypes/*.dat` stubs **[donor]** `jsonschema/current/`.

- **Filename binding** — each `filetypes/*.dat` is a tiny schema mapping a well-known
  runtime filename to its class schema: `game.dat`→`GameData`, `items.dat`→`ItemManager`,
  `monsters.dat`→`MonsterManager`, `player.dat`→`Player`, `hud.dat`→`HUDManager`,
  `entities.dat`→`{entities: {cat: {name: EntityLike}}}`,
  `tiles.dat`→`{tileData: {atlas: {idx: TileData}}}`,
  `strings.dat`→`{key: LocalizedString}`, `animations.dat`→`LerpedAnimation`,
  `spritesheets.dat`/`walltextures.dat`→`[TextureAtlas]`, `shaders.dat`→`[ShaderData]`,
  `splash.dat`→`SplashScreenInfo`, `dungeons.dat`→`[Level]`,
  `section.dat`→`SectionDefinition`, `info.dat`→`GenTheme` **[donor]**
  `jsonschema/current/filetypes/*.dat`.
- **Flattened inheritance** — subclass schemas inline every inherited property tagged with
  a vendor `"baseClass": "<declaring class>"` marker (e.g. `Monster`'s 114 properties
  include `baseClass: "Actor"` rows); every property carries description + default, so the
  schemas double as authoring docs **[donor]**
  `jsonschema/current/dungeoneer/entities/Monster.schema.json`,
  `jsonschema/current/dungeoneer/entities/Entity.schema.json`.
- **Polymorphic unions** — `Interfaces.schema.json` defines `oneOf` sets (`EntityLike`,
  `ItemLike` 13, `SpellLike` 12, `TriggerLike`, `AreaLike` 7, …) used wherever arbitrary
  entities are placed **[donor]** `jsonschema/current/Interfaces.schema.json`.
- **Known drift** — `MonsterManager.schema.json` is titled "ItemManager";
  `spells/Identifiy.schema.json` and `items/Elixer.schema.json` are misspelled; the
  shipped `data/game.dat`'s `gameMode` field has no schema counterpart **[donor]**
  `jsonschema/current/`, **[data]** `data/game.dat` in `delver.jar`.

Port note: `content/` in this repo adapts these shapes into its own typed definitions —
the schema files themselves, Java class discriminators, and the `filetypes` binding layer
are not reused. What transfers is the *shape vocabulary* (Level template, GenTheme,
GenInfo spawn rule, TileData material, TextureAtlas grid, Item/Monster fields) and the
file-convention-over-registry loading model.

## Extraction policy

- Direct rips of donor content stay in a **gitignored `local/`** tree and are never
  committed. Extraction is done by `scripts/extract-delver-reference.sh` (that path, once
  it exists in this repo) against
  `/home/agent/research/delver-game/delver.jar`; nothing under `/home/agent/research/` is
  modified.
- This is not only hygiene: the upstream source release explicitly excludes game data —
  "the game data remains subject to the original copyright"
  **[uncertain]** (`Interrupt/delverengine` v1.1.0 `README.md`, external) — so jar content
  is reference material only.
- Runtime code never loads donor files. It consumes this repo's own normalized, adapted
  content under `content/`, interpreted in typed C# through Engine content services
  (per `AGENTS.md`).
- Worth extracting for reference: `data/*.dat`, `data/messages/`, `data/room-builders/`,
  `generator/**/*.{dat,bin}`, `levels/**` (JSON `.dat` levels are human-readable),
  root/`textures/`/`ui/`/`splash/` PNGs (palette and grid-layout reference), `ui/pixel.fnt`
  glyph layout, and the `audio/` naming scheme. Skip `.class`, natives,
  `META-INF/` **[data]** `delver.jar`.
