# Rusty Dungeon agent guidance

Rusty Dungeon is a first-person roguelike descent in the shape of *Delver*,
built on the Rusty Engine SDK. C# owns the run; the Engine owns the host, the
one update path, input delivery, rendering, and the browser shell.

> The product decides. The Engine guarantees. The Kit shapes. The ruleset
> decides. The bundle assembles. The Host launches.

## Start here

Read [README.md](README.md) for commands and repository layout,
[docs/architecture.md](docs/architecture.md) for current owners, and
[docs/gameplay-design.md](docs/gameplay-design.md) before changing gameplay —
it records the fidelity verdicts and divergences from the donor, and recorded
divergences are decisions, not defects to "fix" later. Before changing the
Engine boundary, read the Engine's `AGENTS.md`, `docs/architecture.md`, and
`docs/csharp-sdk.md` in an explicitly identified source checkout (locally,
`../rusty-engine`). Source is reference material; ordinary builds consume the
installed package pinned in `Directory.Build.props`. Verify capabilities
against the pinned SDK rather than assuming the source checkout and the
package are identical.

The user request and owning task define scope and acceptance. Report failed
reads; do not invent task state. Continue independently authorized work and
pause only decisions that need unavailable authority.

## Fidelity: similar, not a remake

This repository adapts concepts and data shapes from the Delver donor
(`com.interrupt.dungeoneer` source at the operator's `~/research/delver-engine`,
binary at `~/research/delver-game`). We take ideas and shapes; we do not
translate donor code into C#, and we do not copy its files. Format knowledge
lives only in `Delver.Import`; runtime code consumes this repo's own
normalized, authored content. Approximate is the normal verdict; every
system's verdict is written down in docs/gameplay-design.md.

Direct rips from the donor binaries stay in the gitignored `local/` directory
and are never committed — the donor's game data is not covered by its source
license. Donor claims in prose carry a path (`[donor] class/file`, `[data]
file-in-jar`, or `[uncertain]`); a claim without a path is unverified.

## Ownership and source

- `src/DelveRpg.Kit/` owns the ruleset-neutral run domain: the tile world and
  generation, actors, monster AI, combat resolution, status effects,
  inventory, progression, and the one mutable `RunSession`. It references no
  packages and speaks no donor vocabulary; its seams (`IRulesCatalog`,
  `IRandomSource`, `GameTuning`) are plain interfaces.
- `src/DelveRpg.Rulesets.Delver/` owns Delver-derived meaning: content
  schemas, the monster/item/section/loot tables, the run plan (sections →
  ordered floors), and the composition loader. It decides what ids mean.
- `src/DelveRpg.Host/` owns product composition and policy: the Engine
  product entry, semantic input interpretation, scene presentation, the HUD
  projection, and save boundaries. Game rules do not appear here.
- `src/ui/` is a DOM companion. It renders the last admitted
  `delve.ui.snapshot.v1` projection and claims declared intents. Gameplay
  state, game rendering, the canvas, transport, and scheduling stay with their
  C#/Engine owners.
- `content/` holds product-authored data — bundles, packs, tuning. Content is
  data: it never carries code and never carries donor rips. Typed C# reads it
  through the Engine content snapshot.
- `src/Delver.Import{,.Tool}/` is offline tooling over the operator's own
  donor files. It is deliberately outside the runtime graph: no Engine
  package, no Kit reference, and no runtime project may reference it.
- The SDK generates composition and interop under ignored `obj/` output.
  Product code stays safe C#: no handwritten ABI/PInvoke, exports, raw native
  access, downstream Rust, or checked-in composition projects.

There is one Engine-admitted update path. Use its time/input facts; do not add
another loop, clock, scheduler, renderer, or state authority downstream. One
admitted step is one Kit tick (the fixed step is 60 Hz; donor timers are
already expressed in 1/60 s ticks).

## Product style

Prefer ordinary readable C#, explicit composition, direct methods, and one
clear mutable owner per domain (`RunSession`, `DungeonLevel`, `InventoryStore`,
`PlayerState`). Keep operations thin: read, decide, apply, publish. Use typed
boundaries where they help; do not introduce a framework, reflection
discovery, generic bus, or service locator for hypothetical needs.

Use nullable types, file-scoped namespaces, and `internal`/`sealed` defaults
where the public product contract does not require otherwise. Put adjustable
gameplay values in `GameTuning` and its content profile (`content/delve/
tuning/`), not in call sites. Formulas the donor verified stay as written and
cited (see `LevelProgression`, `CombatResolver`); a deliberate change of a
formula is a gameplay-design decision with a doc entry.

Trust first-party runtime state and Engine-admitted data. Preserve concrete
eligibility rules, current-data errors, and resource lifetime/disposal. Do not
add repeated hashing, compatibility layers, whole-state rollback, or
validation ceremony without a task-owned failure it prevents. Content
composition validates all-or-nothing at load and names what is missing.

## Engine dependencies and gaps

`Directory.Build.props` owns the exact SDK/runtime pin. Install it with
`rusty install`; deliberately advance it with `rusty update`, read the release
notes it lists, then run `./scripts/verify.sh`. Keep exact versions in executable
configuration and evidence, not duplicated in prose. Normal development uses
the matched runtime pack through `rusty dev`. NativeAOT is an explicit
fidelity/release check (`./scripts/verify.sh --aot`). Never make an adjacent
Engine checkout a build dependency or modify it as part of downstream work.

If a required mechanism is missing, verify the safe API, name the blocked
behavior and upstream owner, and file/link one narrow Engine request when that
is authorized. Distinguish a missing mechanism or binding from a helper or
documentation gap. Stop that dependent slice; continue independent work. Do
not conceal the gap with a local substitute, fake success, or proof-only path.

## Review and evidence

Use [docs/agent-review/](docs/agent-review/README.md). Every change gets an
Engine-reuse and an existing-product-reuse check; trivial changes may record
that no mechanism is affected. Ruleset or import behavior that claims donor
precedent also gets the [donor-fidelity](docs/agent-review/lanes/donor-fidelity.md)
lane. Assign bounded independent lanes when review agents are requested or the
task's review workflow calls for them. Keep the same reviewer for fix rounds
and reconcile source-backed findings against the original task. Review is not
an extra user-approval gate.

`./scripts/verify.sh` runs the explicit suite list (import, Kit, ruleset,
architecture, node UI) and stages the CoreCLR product. Distinguish
build/staging, host launch, and visible interaction claims. Repeat passed
checks only after material changes or an unresolved failure. A check that only
compiles is not verification.

Preserve unrelated edits. Keep generated output and installed artifacts
ignored. Do not reset, force-push, or change adjacent repositories. Report
what changed, relevant checks, and concrete limitations. Commit/push when
requested or authorized by the active task; a review packet does not authorize
publishing.

## Git

AGENTS.md is tracked despite the ignore rule (`git add -f AGENTS.md`); it is
the working contract for this repository. Commit directly to the branch with
sentence subjects that name the behavior change; include the task id when a
task owns the work. A task without a commit hash is not done.
