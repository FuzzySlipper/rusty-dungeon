# Rusty Template

A minimal Rusty Engine product: C# owns a counter, a DOM button sends an
`increment` intent, and Engine transports the counter projection to the UI.
The packaged Engine owns the host, input, update loop, and browser shell.

## Setup

The supported runtime pair targets Linux x64. Install the .NET 10 SDK, GitHub
CLI (`gh`, authenticated for release access), `jq`, `tar`, `unzip`, and standard shell
utilities. NativeAOT also needs the platform compiler/linker prerequisites
(Clang and zlib development headers on Linux).

```bash
./scripts/install-engine.sh
./scripts/build-csharp.sh
./scripts/run-csharp.sh --port 8787
```

Open the URL printed by the host. The Increment button changes the counter.
The runner delegates to the installed `rusty dev`: CoreCLR loads the product,
and changes to declared C#, UI, or content inputs rebuild and restart it.
Runtime options such as `--bind-host`, `--live-debug`, and `--debugger` pass
through to `rusty dev`.

The installer downloads and verifies the immutable SDK/runtime pair pinned in
`Directory.Build.props`. NuGet resolves the SDK from `.runtime/sdk-feed`; the
runner selects the same version under `.runtime/pairs/`. No Engine source
checkout is required. Installed artifacts are local, ignored output.

To adopt the newest published pair deliberately:

```bash
./scripts/install-engine.sh --update
./scripts/build-csharp.sh
```

The pin changes only after successful installation. Include
`Directory.Build.props` in the resulting source change. For an explicit
NativeAOT fidelity/release check:

```bash
./scripts/build-csharp.sh --aot
```

## Repository shape

| Path | Responsibility |
| --- | --- |
| `src/RustyTemplate.Game/` | Ordinary safe C# product, counter state, and product metadata |
| `src/ui/main.js` | DOM presentation and semantic input |
| `content/` | Product-authored content root |
| `Directory.Build.props` | Matched Engine SDK/runtime pin |
| `scripts/` | Install, build/stage, and development commands |
| `docs/architecture.md` | Current ownership and data flow |
| `docs/ui.md` | DOM companion contract |
| `docs/agent-review/` | Reusable review workflow and lane packets |

The SDK generates CoreCLR/NativeAOT composition beneath `obj/`. The runtime
pack supplies the browser shell. Product metadata, input intents, content/UI
roots, and projection identity live in the ordinary `.csproj`.

## Start a product from this template

1. Rename the C# directory/project, namespace, and entry type together. Update
   the project path in the build/run scripts.
2. Set the product ID/title and UI projection stream/contract in the project
   file. Keep the C# stream/contract constants aligned. Define semantic intents
   there and keep their C#/DOM callers aligned.
3. Replace the counter domain and DOM UI with the product's behavior. Add
   authored data under `content/` and load it through Engine services. Keep
   documentation outside `src/ui/`; every file there is staged as a web asset.
4. Customize `AGENTS.md` and the architecture owner map for the actual product.
   Add a Den project or donor contract only if the new project uses one.
5. Keep the generic review lanes, adding concrete owner pointers and relevant
   task-specific questions as described in [the review guide](docs/agent-review/README.md).

Read [AGENTS.md](AGENTS.md) before extending the product. Keep instructions
about current behavior and ownership; exact dependency identities belong in
configuration, and task status belongs in the task system.
