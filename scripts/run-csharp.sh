#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
# The CoreCLR product loader resolves hostfxr through DOTNET_ROOT; derive it
# from the dotnet on PATH when the environment does not provide one.
if [[ -z "${DOTNET_ROOT:-}" ]] && command -v dotnet >/dev/null; then
  DOTNET_ROOT=$(cd "$(dirname "$(command -v dotnet)")" && pwd)
  export DOTNET_ROOT
fi
version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' "$repo_root/Directory.Build.props")
runtime="$repo_root/.runtime/pairs/$version/runtime-pack"
if [[ ! -x "$runtime/bin/rusty" ]]; then
  echo 'Install the pinned Engine pair first: ./scripts/install-engine.sh' >&2
  exit 1
fi
exec "$runtime/bin/rusty" dev \
  --project "$repo_root/src/DelveRpg.Host/DelveRpg.Host.csproj" \
  --runtime "$runtime" "$@"
