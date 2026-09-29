#!/usr/bin/env bash
# Focused verification for this repository: pinned pair install, all test suites, and
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
  -h|--help) echo "usage: $0 [--aot] (pinned pair install, tests, staging; optionally NativeAOT)"; exit 0 ;;
  *) echo "usage: $0 [--aot]" >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo 'Too many arguments.' >&2; exit 2; }

# Installs the pinned pair when it is missing (a no-op offline once installed).
(cd "$repo_root" && rusty install)

projects=(
  tests/Delver.Import.Tests/Delver.Import.Tests.csproj
  tests/DelveRpg.Kit.Tests/DelveRpg.Kit.Tests.csproj
  tests/DelveRpg.Rulesets.Delver.Tests/DelveRpg.Rulesets.Delver.Tests.csproj
  tests/DelveRpg.Architecture.Tests/DelveRpg.Architecture.Tests.csproj
  tests/DelveRpg.Host.Tests/DelveRpg.Host.Tests.csproj
)
for project in "${projects[@]}"; do
  echo "== dotnet test $project"
  dotnet test "$repo_root/$project" --configuration Release --nologo
done

echo "== node --test tests/DelveRpg.Ui.Tests"
npm --prefix "$repo_root" exec -- tsc -p "$repo_root/src/ui"
node --test "$repo_root"/tests/DelveRpg.Ui.Tests/*.test.mjs

echo "== build and stage the product"
host_project="$repo_root/src/DelveRpg.Host/DelveRpg.Host.csproj"
dotnet msbuild "$host_project" -restore -t:StageRustyEngineCoreClrProduct -p:Configuration=Release
if [[ "$aot" == true ]]; then
  dotnet msbuild "$host_project" -restore -t:VerifyRustyEngineAot -p:Configuration=Release
fi

echo "verify: all checks green."
