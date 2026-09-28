# Content

Product-authored data. Content is data: changing valid content does not
require a product rebuild (`rusty dev` restages on change), and content never
carries code. Nothing here is donor data — donor rips live in gitignored
`local/`, and normalized donor reference packs are regenerated into the
ignored `content/delve/imports/`.

| Path | Shape |
| --- | --- |
| `delve/bundles/delve-run.json` | The bundle manifest: ruleset, content packs, tuning profile. "A bundle's selection is what loads." |
| `delve/content-packs/<id>.json` | Pack descriptors: id, ruleset, payload path under `delve/` |
| `delve/packs/*.json` | Pack payloads: `monsters`, `items`, `sections`, `loot` tables |
| `delve/tuning/<id>.json` | Tuning profiles; every field maps onto `GameTuning` |

The typed schemas live in `DelveRpg.Rulesets.Delver/Content/`; composition
resolution and all-or-nothing validation live in `DelverComposition`. A pack
with missing tables, repeated ids, or loot entries that name no item fails
the load with the problems named — a bad pack fails at load, not mid-run.

Ids are namespaced: `delve.monster.*`, `delve.item.*`. Monster and item level
windows (`minDungeonLevel`/`maxDungeonLevel`, `minItemLevel`/`maxItemLevel`)
decide floor eligibility; the `loot` table adds weighted draws inside those
windows. Section definitions carry the donor depth model: `difficultyLevel`,
`floors`, `levelTemplates`, `transitionLevel`.
