#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' "$repo_root/Directory.Build.props")
runtime="$repo_root/.runtime/pairs/$version/runtime-pack"
if [[ ! -x "$runtime/bin/rusty" ]]; then
  echo 'Install the pinned Engine pair first: ./scripts/install-engine.sh' >&2
  exit 1
fi
exec "$runtime/bin/rusty" dev \
  --project "$repo_root/src/RustyTemplate.Game/RustyTemplate.Game.csproj" \
  --runtime "$runtime" "$@"
