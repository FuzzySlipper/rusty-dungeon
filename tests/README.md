# Tests

Each suite mirrors a source tree; `scripts/verify.sh` runs them all with an
explicit list.

| Directory | Suite | Answers |
| --- | --- | --- |
| `Delver.Import.Tests` | `dotnet test` | Donor format decoding: relaxed JSON, jar classification/extraction, normalization determinism. Synthetic fixtures only; the real-archive test early-returns a pass without the operator's jar. |
| `DelveRpg.Kit.Tests` | `dotnet test` | Run-domain behavior: generation determinism and connectivity, line of sight and remembered map, combat boundaries, inventory rules, the donor formulas, session ticks, capture/restore. |
| `DelveRpg.Rulesets.Delver.Tests` | `dotnet test` | Delver meaning: composition loading and validation, run-plan expansion (`difficultyLevel + floor`), catalog windows, loot bounds, the shipped content's internal consistency. |
| `DelveRpg.Architecture.Tests` | `dotnet test` | The boundary rules of AGENTS.md in code: the dependency graph, the Kit vocabulary law, one product entry, import isolation, the intent/contract pairing between manifest and DOM. |
| `DelveRpg.Host.Tests` | `dotnet test` | Host seams that need no Engine context: the keyed-random bound translation and the input router's held/one-shot/Clear semantics. Lifecycle policy (save boundaries, permadeath) stays review-covered: it lives in `DelveProduct`, which the suite cannot construct without an Engine context. |
| `DelveRpg.Ui.Tests` | `node --test` | The DOM companion contract: rendering from `delve.ui.snapshot.v1`, declared intent claims, disposal. |

Conventions: xunit with explicit fixture doubles (`ScriptedRandom`,
`ScriptedCatalog`), no test depends on donor files, and review-lane probe
files (`Temp*Tests.cs`, `[Tt]mp*.cs`) stay ignored and disposable. A check
that only compiles is not verification; a suite that asserts nothing about
behavior is not a test.
