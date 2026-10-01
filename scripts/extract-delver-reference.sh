#!/usr/bin/env bash
# Extract Delver donor reference material for offline study and adaptation.
#
# Usage:
#   ./scripts/extract-delver-reference.sh [--jar <delver.jar>] [--engine <delver-engine dir>] [--out <local dir>]
#
# Defaults:
#   --jar    /home/agent/research/delver-game/delver.jar
#   --engine /home/agent/research/delver-engine
#   --out    <repo>/local
#
# Requires the .NET 10 SDK available as `dotnet` on PATH
# (e.g. export PATH="$HOME/.dotnet:$PATH"). The delverimport tool is built
# on demand from src/Delver.Import.Tool when it is missing or stale.
#
# Runs the reference pull (extract + report + normalize) into <out>:
#   <out>/extracted/   donor entries worth referencing (dat, bin, png, obj,
#                      json, atlas skins, fnt), inner paths preserved
#   <out>/jsonschema/  jsonschema/current/** from the engine checkout
#   <out>/normalized/  strict-JSON reference tables + normalized documents
#
# Direct binary rips land only in the gitignored local/ directory; this
# script never writes outside <out>.
set -euo pipefail

JAR=/home/agent/research/delver-game/delver.jar
ENGINE=/home/agent/research/delver-engine
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$REPO_ROOT/local"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --jar|--engine|--out)
      [[ $# -ge 2 ]] || { echo "error: missing value for $1" >&2; exit 2; }
      case "$1" in
        --jar) JAR="$2" ;;
        --engine) ENGINE="$2" ;;
        --out) OUT="$2" ;;
      esac
      shift 2
      ;;
    -h|--help)
      sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "error: unknown option $1" >&2
      exit 2
      ;;
  esac
done

[[ -f "$JAR" ]] || { echo "error: jar not found: $JAR" >&2; exit 1; }

TOOL_PROJECT="$REPO_ROOT/src/Delver.Import.Tool/Delver.Import.Tool.csproj"
TOOL_DLL="$REPO_ROOT/src/Delver.Import.Tool/bin/Release/net10.0/delverimport.dll"

needs_build=0
if [[ ! -f "$TOOL_DLL" ]]; then
  needs_build=1
else
  while IFS= read -r source; do
    if [[ "$source" -nt "$TOOL_DLL" ]]; then
      needs_build=1
      break
    fi
  done < <(find "$REPO_ROOT/src/Delver.Import" "$REPO_ROOT/src/Delver.Import.Tool" \
    \( -name '*.cs' -o -name '*.csproj' \))
fi

if [[ "$needs_build" -eq 1 ]]; then
  echo "Building delverimport..."
  dotnet build "$TOOL_PROJECT" -c Release
fi

run_tool() {
  dotnet "$TOOL_DLL" "$@"
}

extract_args=(extract --jar "$JAR" --out "$OUT")
if [[ -d "$ENGINE/jsonschema/current" ]]; then
  extract_args+=(--engine "$ENGINE")
else
  echo "warning: no jsonschema/current under '$ENGINE'; skipping schema copy" >&2
fi

run_tool "${extract_args[@]}"
run_tool report --in "$OUT/extracted"
run_tool normalize --in "$OUT/extracted" --out "$OUT"

echo
echo "Delver reference material written to:"
echo "  $OUT/extracted    donor entries (dat, bin, png, obj, json, atlas, fnt)"
echo "  $OUT/jsonschema   engine content schemas"
echo "  $OUT/normalized   normalized strict-JSON reference tables"

# Stage the tile atlases and sprite sheets the authored art manifests name
# into the gitignored content import area: Engine textures open from product
# content only, and donor pixels must never be committed.
ART_STAGE="$REPO_ROOT/content/delve/imports/art"
mkdir -p "$ART_STAGE"
staged=0
for atlas in $(python3 - "$REPO_ROOT/content/delve/art/tiles.json" "$REPO_ROOT/content/delve/art/sprites.json" <<'PYEOF'
import json, sys
for path in sys.argv[1:]:
    manifest = json.load(open(path))
    for name in manifest.get("atlases", {}):
        print(name)
PYEOF
); do
  if [[ -f "$OUT/extracted/$atlas" ]]; then
    # The Engine admits RGB/RGBA PNGs; donor sheets saved palette-mode
    # (armor.png) are expanded to RGBA, keeping their transparency.
    python3 - "$OUT/extracted/$atlas" "$ART_STAGE/$atlas" <<'PYEOF'
import shutil, sys
try:
    from PIL import Image
except ImportError:
    Image = None
source, target = sys.argv[1], sys.argv[2]
if Image is None:
    shutil.copyfile(source, target)
    sys.exit(0)
with Image.open(source) as image:
    if image.mode in ("RGB", "RGBA"):
        shutil.copyfile(source, target)
    else:
        image.convert("RGBA").save(target)
PYEOF
    staged=$((staged + 1))
  fi
done
echo "  content/delve/imports/art   $staged staged atlas file(s) for the level and sprite art"

# Lit sprites read a normal sheet per sprite sheet, derived from the staged
# colour sheet (also donor-derived, so it stays in the gitignored stage).
if python3 -c 'import numpy, PIL' 2>/dev/null; then
  python3 "$REPO_ROOT/scripts/derive-sprite-normals.py" "$REPO_ROOT/content/delve/art/sprites.json" "$ART_STAGE"
else
  echo "warning: numpy/Pillow missing; sprites will draw unlit (no derived normal sheets)" >&2
fi
echo "Note: donor rips must never be committed; local/ and content/delve/imports/ are gitignored."
