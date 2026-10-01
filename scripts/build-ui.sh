#!/usr/bin/env bash
# Compiles the DOM companion (src/ui) against the Engine pair's own UI
# declarations. The Host project runs this as its RustyEngineProductUiBuildCommand
# and passes $(RustyEngineProductUiTypes), the pair's `@rusty-engine/product-ui`
# and `@rusty-engine/live-debug` declarations. `tsc -p` cannot take an extra
# input file, so a throwaway config extends src/ui/tsconfig.json with it: the
# compiler options keep one home.
set -euo pipefail

types=${1:?usage: scripts/build-ui.sh <path to rusty-engine-product-ui.d.ts>}
[[ -f "$types" ]] || { echo "Engine UI declarations not found: $types (run rusty install)." >&2; exit 1; }

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
config_dir=$(mktemp -d)
trap 'rm -rf "$config_dir"' EXIT
cat > "$config_dir/tsconfig.json" <<JSON
{
  "extends": "$repo_root/src/ui/tsconfig.json",
  "files": ["$repo_root/src/ui/main.ts", "$types"]
}
JSON
npm --prefix "$repo_root" exec -- tsc -p "$config_dir/tsconfig.json"
