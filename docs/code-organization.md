# Code organization

## 1. Projects

`DelveRpg.Kit` (run domain, no packages) → `DelveRpg.Rulesets.Delver`
(Delver meaning, references Kit) → `DelveRpg.Host` (product entry, references
both plus `Rusty.Engine`). `Delver.Import` → `Delver.Import.Tool` is a
separate offline tree with no package references at all. Tests mirror each
tree; `DelveRpg.Architecture.Tests` enforces this graph mechanically.

## 2. Namespaces

File-scoped namespaces mirror folders (`DelveRpg.Kit.Session`,
`DelveRpg.Rulesets.Delver.Content`, `Delver.Import.RelaxedJson`). Folders are
product domains, not layer names.

## 3. The Kit's seams

The Kit speaks to the outside world through three plain types: `IRulesCatalog`
(definitions and loot), `IRandomSource` (draws), and `GameTuning` (adjustable
values). The Host adapts Engine services behind them; tests use scripted
doubles. No Engine type appears in the Kit, and no donor vocabulary appears
in its sources (enforced in `ArchitectureLawTests`).

## 4. One mutable owner per domain

`RunSession` owns the run (floor, player, monsters, ground items, timers).
`DungeonLevel` owns the grid. `InventoryStore` owns slots. `PlayerState`
owns the character. Everything else reads those owners; nothing shadows their
state.

## 5. The tick

One admitted Engine step = one `RunSession.Tick(RunInput)`. The tick order is
fixed and readable: timers → movement → combat → interaction → hotbar →
monster turns → effects → escape arc → remembered map → death check →
experience. Donor tick ordering (`game/Game.java:480`) is the reason the
order looks like it does.

## 6. Rules formulas

Verified donor formulas live beside their citation in `CombatResolver`,
`LevelProgression`, and `DelverRuleset` (`dungeonLevel = difficultyLevel +
floor`). They change only with a docs/gameplay-design.md decision.

## 7. Content

`content/delve/` holds one bundle manifest, content-pack descriptors, pack
payloads, and a tuning profile. `DelverComposition.Load` resolves and
validates all-or-nothing. Ids are namespaced (`delve.monster.*`,
`delve.item.*`); the Kit sees only opaque ids.

## 8. Input

Physical mappings and intents are declared in `DelveRpg.Host.csproj` (the
staged manifest). `DelveInputRouter` is the only place Engine input events
become meaning; `Clear` facts release held state. The DOM companion claims
the same declared intent names.

## 9. Presentation

`DelveSceneRenderer` + `LevelMesh` translate run state into Engine appearance
facts; `DelveHudProjection` + `UiDocumentEncoder` translate `HudFacts` into
the one UI projection. Neither evaluates rules.

## 10. Persistence

`DelveSaveStore` wraps the Engine's managed state store. `RunSnapshot` is the
save boundary shape (plain serializable data); `MetaProgression` survives
runs. Save shape changes are product decisions, not migrations.

## 11. Import tree

`Delver.Import` owns donor format knowledge (relaxed JSON, jar layout,
normalized reference output). Runtime code never references it; its output
goes to gitignored `local/` or `content/delve/imports/`.

## 12. Generated output

`obj/` (SDK composition), `src/ui/generated/` (tsc), `.runtime/` (installed
pair), `node_modules/`, `local/` — all ignored, never edited, never committed.

## What would force a re-plan

- The Engine drops or changes an API this repo builds on (appearance facts,
  persistence store, UI projection, content snapshot) — the pinned pair moves
  only with a green `verify.sh`.
- A donor-fidelity review shows a recorded "approximate" verdict quietly
  became a different game (e.g. the escape arc stops mattering).
- The Kit needs a second clock or a second state authority to answer a
  requirement — that is an Engine gap discussion first, never a second loop.
- Content packs can no longer express a needed ruleset distinction without
  code — schema work, then rules, in that order.
