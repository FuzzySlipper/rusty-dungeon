# Rusty Dungeon

A first-person roguelike descent in the shape of *Delver*: real-time movement
through a generated tile dungeon, monsters that chase what they see, items as
the whole RPG layer, and one objective — grab the orb at the bottom and climb
out alive. C# owns the run; the Rusty Engine owns the host, the update path,
input delivery, rendering, and the browser shell.

The game adapts concepts and data shapes from the Delver donor
(`com.interrupt.dungeoneer`); it is a similar game, not a port of donor code.
See [docs/gameplay-design.md](docs/gameplay-design.md) for every fidelity
decision, [docs/research/](docs/research/) for the donor surveys, and
[docs/playtesting.md](docs/playtesting.md) for driving the running game from
tools.

## Ownership

> The Engine guarantees. The Kit shapes. The ruleset decides. The bundle
> assembles. The Host launches.

| Layer | Owns |
| --- | --- |
| `Rusty.Engine` (pinned package) | Update admission, input delivery, rendering/resources, content snapshot, persistence primitives, UI transport, host |
| `DelveRpg.Kit` | Ruleset-neutral run domain: tile world, generation, actors, AI, combat, inventory, progression, session |
| `DelveRpg.Rulesets.Delver` | Delver-derived meanings: content schemas, monster/item/section/loot tables, run-plan expansion |
| `DelveRpg.Host` | Product composition, semantic input interpretation, scene presentation, HUD projection, save boundaries |
| `src/ui/` | DOM companion: HUD presentation, accessibility, semantic menu intents |
| `content/` | Product-authored bundles, packs, and tuning (never code, never donor rips) |
| `Delver.Import(.Tool)` | Offline donor-format knowledge: relaxed-JSON `.dat`, jar inventory, normalization — outside the runtime graph |
| `local/` (gitignored) | Operator-supplied donor extractions and probes; direct rips live here and nowhere else |

## Run shape

One run walks a plan of themed **sections** (donor `SectionDefinition`): each
section contributes ordered floors at `section difficulty + floor` difficulty
and an optional transition floor. Floors are generated rooms-and-corridors
with door seams, stairs, monsters, and loot; the map you have seen stays
explored on the HUD minimap. Combat is real-time: hold to charge a swing,
release in range, resolve dodge → roll → armor like the donor. Death or
victory deletes the run save (permadeath); only meta progression survives.

Recorded divergences from the donor (never to be "corrected" silently):

- Monster navigation is a bounded grid BFS; the donor's node-graph paths and
  steering sweeps are simplified away.
- Monster detection is the donor's light-based stealth, sampled on the tile
  grid from wall torches; partial shadow and held light sources are not
  modeled.
- Projectiles fly over the Kit's tile grid with simplified collision (no
  knockback, splash or deflection); see docs/gameplay-design.md §Combat.
- Ascended floors regenerate from the run seed instead of being restored from
  snapshots; only the current floor is snapshotted at save boundaries.
- Ripped art and audio are never committed. With the operator's donor files
  staged (gitignored), tiles, ceilings, monsters, items and the held weapon
  use donor sheet cells named by authored manifests; without them the game
  runs on authored placeholder colors and plates.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `src/DelveRpg.Kit/` | Run domain: `World/`, `Actors/`, `Ai/`, `Combat/`, `Effects/`, `Inventory/`, `Progression/`, `Session/` |
| `src/DelveRpg.Rulesets.Delver/` | Content schemas and the Delver ruleset |
| `src/DelveRpg.Host/` | `DelveProduct` entry, input router, scene renderer, HUD projection, saves |
| `src/Delver.Import/`, `src/Delver.Import.Tool/` | Offline donor decoding and normalization (`delverimport`) |
| `src/ui/` | DOM companion (TypeScript, compiled to ignored `generated/`) |
| `content/delve/` | Bundle, content pack, and tuning JSON |
| `docs/` | Architecture, UI contract, gameplay design, donor research, review lanes |
| `tests/` | Kit, ruleset, import, architecture, and UI suites |
| `scripts/` | Engine install, build/stage, run, donor extraction, verify |
| `Directory.Build.props` | The one Engine SDK/runtime pair pin |

## Develop and verify

The supported runtime pair targets Linux x64. Install the .NET 10 SDK, Node.js
(with npm), `curl` and `tar`. NativeAOT also needs Clang and zlib development
headers. Get the Engine's `rusty` command once with
`curl -fsSL https://raw.githubusercontent.com/FuzzySlipper/rusty-engine/main/scripts/install-rusty.sh | bash`.

```bash
rusty install                   # install the pinned Engine pair into the shared cache
./scripts/verify.sh             # all suites, CoreCLR staging
rusty dev --project src/DelveRpg.Host/DelveRpg.Host.csproj --port 8787
```

Open the URL printed by the host. WASD walks, the mouse looks (pointer lock),
hold the primary button to charge a swing, `E` uses doors and stairs, `1`-`6`
are hotbar slots, `I` inventory, `M` map, `Enter`/`W`/`S` drive menus. `rusty
dev` rebuilds and restarts the product when C# under the Host, Kit, or ruleset
changes; a UI or content edit is recompiled/restaged and reloaded without a C#
build.

`Directory.Build.props` pins the immutable SDK/runtime pair; `rusty update`
adopts the newest published pair deliberately, rewrites the pin, and lists the
release notes to read. `./scripts/verify.sh --aot` additionally publishes
NativeAOT as a fidelity check. Normal product builds consume the installed package; an adjacent Engine
checkout is never a build dependency.

To pull donor reference material for research (into gitignored `local/`):

```bash
./scripts/extract-delver-reference.sh   # needs the operator's delver-game/delver-engine
./scripts/import-delver.sh report
```

## Evidence posture

A check that only compiles is not verification, and a demonstration is not
completion. `./scripts/verify.sh` names what it covers: the pinned pair install, the
Kit/ruleset/import/architecture/UI suites, and product staging. Visible
interaction claims need a live host run; build/staging and host launch are
reported separately.
