# DOM companion

`src/ui/main.ts` exports `mountProductUi(root, context)` and the pure
`renderHud(doc, root, snapshot)` used by DOM tests. The companion is the HUD
of a Delver-style descent: phase line, health, messages, the use prompt, the
flat hotbar, stats, the escape indicator, the explored map window, and the
level-up chooser. Staging the Host compiles the TypeScript to the ignored
`src/ui/generated/` through the SDK's UI build (only when a UI input changed)
and stages that output.

The projection contract is `delve.ui.snapshot.v1` on the `delve.hud` stream
(declared in both `DelveRpg.Host.csproj` and `DelveHudProjection`). A snapshot
carries: `phase` (`title`, `run`, `levelup`, `dead`, `won`) and `runPhase`
(the Kit's `Playing`/`LevelUp`/`Dead`/`Won`), run facts (`runIndex`,
`section`, `dungeonLevel`), vitals (`hp`, `maxHp`, `playerLevel`,
`experience`, `experienceToNext`), economy (`gold`, `keys`), `messages`,
`usePrompt`, `hotbar` (slots with `index`, `itemId`, `name`, `kind`, `count`,
`wielded`), `inventoryOpen`, `mapOpen`, `stats`, `levelUp` (`offers`,
`cursor`), `holdingOrb`, `escapePressure`, and `minimap` (`size`, `cells`,
`playerX`, `playerY`, `heading`). The `cells` grid is row-major around the
player: blank unseen, `#` explored wall, `.` explored floor, `+` door, `~`
water, `<`/`>` stairs, `@` the player's tile.

The inventory and map views (the donor's modal inventory and fullscreen map)
are pending UI slices; `inventoryOpen` and `mapOpen` are already projected so
those slices need no contract change. Keyboard hotbar intents cover the six
base slots (`hotbar.1`–`hotbar.6`); meta-upgraded slots beyond six have no
key path yet, and upgrades are not spendable in this slice
(docs/gameplay-design.md).

Controls claim only declared digital intents — `menu.confirm`,
`menu.cancel`, `menu.up`, `menu.down` — the same names the staged product
manifest declares, so a control cannot send something nothing handles. Run
state lives in C#; input delivery, projection transport, the canvas, and
rendering belong to Engine.

Keep only browser assets in `src/ui/`. The host admits every staged file by
its content type; documentation belongs under `docs/`. Keep this lane to DOM
presentation, accessibility, and semantic actions. Dispose event listeners
and subscriptions when the host unmounts the UI.
