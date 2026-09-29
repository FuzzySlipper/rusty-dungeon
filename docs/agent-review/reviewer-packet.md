# Reviewer packet

Give each reviewer this file plus the selected lane(s), all read-only unless
the task explicitly assigns a fix.

- **Repository.** `/home/agent/dev/rusty-dungeon`, working tree or the exact
  commit/diff under review. Owned scope: the changed paths and their callers.
- **What this repo is.** A first-person roguelike descent in the shape of
  *Delver*, adapting the donor `com.interrupt.dungeoneer` concepts to the
  Rusty Engine SDK. Requirements come from the user's request and AGENTS.md;
  gameplay decisions are recorded in docs/gameplay-design.md and are not
  re-litigated in review.
- **Contracts.** AGENTS.md ownership rules; docs/architecture.md owner map;
  docs/ui.md projection contract (`delve.ui.snapshot.v1`); content schemas in
  `DelveRpg.Rulesets.Delver/Content/`; input manifest in
  `DelveRpg.Host.csproj`.
- **Engine identity.** The pinned SDK/runtime pair in
  `Directory.Build.props` (installed by `rusty install`; `rusty status` shows where).
  Boundary claims must be checked against the pinned package, not an adjacent
  Engine checkout.
- **Checks already run.** `./scripts/verify.sh` (pair identity, import/Kit/
  ruleset/architecture suites, node UI suite, CoreCLR staging). Report what
  was run, its result, and known limitations — build/staging is not host
  launch, and host launch is not visible interaction.

Finding shape (see README.md): `no findings` / `findings` / `unable to
verify`; each finding has a stable ID, the requirement or boundary it
concerns, an exact file/line or a reproducing command with observed result,
the concrete consequence, and the minimum property for closure. Separate a
verified defect from an upstream gap, an open question, or an optional
improvement. Fixes return to the same reviewer with finding IDs.
