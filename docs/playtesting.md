# Playtesting

Two lanes reach the running product without a person at the keyboard.

## Engine harness lane (direct)

Run `rusty dev --project src/DelveRpg.Host/DelveRpg.Host.csproj --live-debug --headless`.
`--headless` matters: without a watching page the title screen never
consumes input. Then drive `/__rusty/product/runtime/...` directly:
- `debug/execute` with `engine.time.mode manual` and
  `engine.time.advance <ms>`;
- `control/claim` then `input` batches in the `gameplay.default` context;
- `frames/capture?format=png` for captures;
- `outputs/fresh` for the HUD projection.

Run saves live in `.runtime/persistence` and are throwaway test state;
delete them for a fresh run.

## crew-services playtest service

The product registers the Engine's `PlaytestDebugModule` through
`DelveProduct.RegisterDebugCommands`, so the service's assist operations
work. `src/DelveRpg.Host/Playtest/DelvePlaytest.cs` supplies:
- `observe`: run phase, the player's position, facing, health, weapon,
  charge, cooldown, visibility and notice radius, nearby monsters with
  state, distance, relative bearing, wind-up and flinch, nearby ground
  items, and recent messages;
- `act`, with these ids: `forward`, `back`, `left`, `right`, `use`,
  `attack` (held for the wielded weapon's full charge), `quick-attack`,
  `confirm`, and `hotbar-1`–`hotbar-6`, each with its current availability
  and reason;
- `look`: turns the view through the run's own look rules without
  advancing time.

`delve.grid` (a plain debug command) prints the true floor within 16 tiles
of the player: walls, doors, `L` locked doors, stairs, `^` spikes, `_`
plates, `o` pots, `m` monsters, `i` items and `@` the player. The first line
gives the top-left tile. Use it to route, since the HUD map shows only what
was explored. Observations also list nearby spikes, plates, pots and locked
doors, plus the keys carried; hidden tripwires are left out.

The profiles in `~/.config/crew-playtest/games.json` are
`rusty-dungeon-hosted` (browser, with the DOM HUD) and `rusty-dungeon-engine`
(world frames only). Each session starts its own host from this repository's
`.den-serve.json`. Delete `.runtime/persistence` first for a fresh run.

```sh
playtest start rusty-dungeon-engine
playtest assist SESSION --json '{"op":"time","mode":"action-driven"}'
playtest assist SESSION --json '{"op":"act","id":"confirm"}'
playtest assist SESSION --json '{"op":"observe"}'
playtest assist SESSION --json '{"op":"act","id":"attack","capture":true}'
playtest stop SESSION
```

An attack's swing begins on release and its blow lands partway through, so
advance a little after an `attack` action to see the hit. A Windows (DX12)
target exists on the service; parity checks there belong to presentation
task 9082.
