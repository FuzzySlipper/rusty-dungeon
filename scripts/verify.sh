#!/usr/bin/env bash
# Focused verification for this repository: pair identity, all test suites, and
# CoreCLR staging. The project and suite lists are deliberate — a discovery
# based loop silently stops covering a project when one disappears.
#
# Requires dotnet on PATH (e.g. export PATH="$HOME/.dotnet:$PATH") and node.
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
aot=false
case "${1:-}" in
  '') ;;
  --aot) aot=true ;;
  -h|--help) echo "usage: $0 [--aot] (pair identity, tests, staging; optionally NativeAOT)"; exit 0 ;;
  *) echo "usage: $0 [--aot]" >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo 'Too many arguments.' >&2; exit 2; }

version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' "$repo_root/Directory.Build.props")
revision=$(sed -n 's|.*<RustyEnginePairSourceRevision>\([^<]*\)</RustyEnginePairSourceRevision>.*|\1|p' "$repo_root/Directory.Build.props")
pair="$repo_root/.runtime/pairs/$version"
[[ -d "$pair" ]] || { echo "Install the pinned Engine pair first: ./scripts/install-engine.sh" >&2; exit 1; }
"$pair/verify-pair.sh" --directory "$pair"
jq -e --arg version "$version" --arg revision "$revision" \
  '.package.id == "Rusty.Engine" and .package.version == $version and
   .sourceRevision == $revision and .runtime.sourceRevision == $revision' \
  "$pair/pair-manifest.json" >/dev/null \
  || { echo "Installed Engine pair does not match Directory.Build.props." >&2; exit 1; }
echo "Engine pair identity ok: $version"

projects=(
  tests/Delver.Import.Tests/Delver.Import.Tests.csproj
  tests/DelveRpg.Kit.Tests/DelveRpg.Kit.Tests.csproj
  tests/DelveRpg.Rulesets.Delver.Tests/DelveRpg.Rulesets.Delver.Tests.csproj
  tests/DelveRpg.Architecture.Tests/DelveRpg.Architecture.Tests.csproj
)
for project in "${projects[@]}"; do
  echo "== dotnet test $project"
  dotnet test "$repo_root/$project" --configuration Release --nologo
done

echo "== node --test tests/DelveRpg.Ui.Tests"
npm --prefix "$repo_root" exec -- tsc -p "$repo_root/src/ui"
node --test "$repo_root"/tests/DelveRpg.Ui.Tests/*.test.mjs

echo "== build and stage the product"
if [[ "$aot" == true ]]; then
  "$repo_root/scripts/build-csharp.sh" --aot
else
  "$repo_root/scripts/build-csharp.sh"
fi

echo "verify: all checks green."
