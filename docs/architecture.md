# Product architecture

> The Engine guarantees. The Kit shapes. The ruleset decides. The bundle
> assembles. The Host launches.

```text
Run domain and rules (C#, DelveRpg.Kit + Rulesets.Delver)
  -> Rusty.Engine safe SDK (DelveRpg.Host adapters)
  -> SDK-generated composition and ABI
  -> packaged Rust host, input, UI transport, renderer, browser shell
  -> DOM companion (src/ui)
```

## Owners

| Path or service | Responsibility |
| --- | --- |
| `DelveRpg.Kit/World/` | Tile grid (`DungeonLevel`), grid line of sight, remembered map (`FogMap`), room-and-corridor generation |
| `DelveRpg.Kit/Actors/`, `Effects/` | Bodies, player/monster state, timed status effects |
| `DelveRpg.Kit/Ai/` | Monster state machine and bounded grid pathfinding |
| `DelveRpg.Kit/Combat/` | Charge attacks, dodge → roll → armor resolution, kill experience |
| `DelveRpg.Kit/Inventory/`, `Progression/` | Flat slot inventory, equipment, experience curve, meta progression |
| `DelveRpg.Kit/Session/` | `RunSession`: the one mutable run owner — tick, actions, escape arc, HUD facts, capture/restore |
| `DelveRpg.Rulesets.Delver/` | Content schemas and composition loader, catalog tables, run-plan expansion (`DelverRuleset`) |
| `DelveRpg.Host/DelveProduct.cs` | Lifecycle, run policy (title, permadeath, meta), update admission |
| `DelveRpg.Host/Input/` | Engine input events → semantic `RunInput` |
| `DelveRpg.Host/Presentation/` | Level mesh, actor and held-weapon sprites, camera — Engine graphics only |
| `DelveRpg.Host/Hud/` | `HudFacts` → `delve.ui.snapshot.v1` projection |
| `DelveRpg.Host/Save/` | Run snapshot and meta stores over Engine persistence |
| `src/ui/main.ts` | DOM HUD: renders the projection, claims declared intents |
| `content/delve/` | Bundle selection, content pack, tuning profile |
| `Delver.Import{,.Tool}` | Offline donor formats; never in the runtime graph |
| Engine SDK/runtime | Generated interop, admitted updates/input, rendering/resources, content snapshot, persistence, UI transport, host, browser shell |

## Lifecycle and data flow

The installed runtime loads SDK-generated CoreCLR composition. Its bind checks
the SDK/runtime ABI identity and constructs the product with
`ProductCreateContext`. The product constructor resolves the Delver
composition from the Engine content snapshot (`DelverComposition.Load`),
builds the ruleset, opens its UI stream, and prepares the save stores and
scene renderer.

The Engine admits `ProductUpdate` callbacks on the declared 60 Hz fixed step.
`DelveInputRouter` folds the update's copied input events (semantic intents
from keys, pointer, and the DOM companion) into one `RunInput` per admitted
step; every `Clear` fact releases derived held state. `RunSession.Tick` applies
it — movement, charge combat, use/hotbar actions, monster turns, effects,
escape-arc spawns, remembered-map updates — and produces facts. The renderer
publishes camera and appearance facts through the Engine snapshot path; the
HUD projection is one typed `UiValue` on the `delve.hud` stream.

Save boundaries are run policy in the Host: every floor change, every pause,
and shutdown capture a `RunSnapshot` through Engine persistence primitives.
Death and victory delete the run save and update meta progression
(permadeath); a present run save resumes on the next start.

## Build and host

`Directory.Build.props` pins one immutable SDK/runtime pair. The Engine `rusty`
command installs it into its shared cache and supplies its SDK package source
to restores. The product's package reference supplies the public services and
build targets; generated bindings are ignored output, never edited sources.

`rusty build --project src/DelveRpg.Host/DelveRpg.Host.csproj` compiles and
stages CoreCLR through `StageRustyEngineCoreClrProduct`. Staging compiles the
TypeScript companion into `src/ui/generated` through the SDK's UI build
(`RustyEngineProductUiBuildCommand`, which runs `scripts/build-ui.sh` against
the pair's `RustyEngineProductUiTypes`), and only when a UI input changed; a plain
`dotnet build` does not stage or compile the UI. `rusty dev` runs the pinned
pair's runtime and owns staging, watching (the SDK default plus every
referenced project), worker replacement, and serving.
`rusty build --aot` (`VerifyRustyEngineAot`) publishes NativeAOT for explicit
fidelity checks. The
product supplies only its C#, DOM UI, and content; browser assets and transport
come from the runtime pack.

Before adding a mechanism, check both the installed safe SDK and the owners
above. Product meaning stays downstream. A missing Engine capability is an
upstream request, not another local host, transport, scheduler, or renderer.

## Known upstream gaps

- **Arrow keys in the physical keyboard vocabulary.** The product runtime's
  mapping parser (`parse_keyboard_control`, `csharp-product-runtime`) and the
  `KeyboardControl` enum accept letters, `digit-0`–`digit-9`,
  space/enter/escape, and the shift/control/alt pairs — no arrow keys.
  Products therefore double letter keys for semantic intents (W/S serve both
  movement and menu up/down here). *Request-ready wording:* extend
  `KeyboardControl` with `ArrowUp`/`ArrowDown`/`ArrowLeft`/`ArrowRight`,
  accept `arrow-up`/`arrow-down`/`arrow-left`/`arrow-right` in
  `parse_keyboard_control`, and translate the corresponding `KeyboardEvent`
  codes in the browser shell. Owner: the Engine's input support (shared by
  the product runtime and the playtest harness). Until then the workaround
  is double-mapped letter keys.
