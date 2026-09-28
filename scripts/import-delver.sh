#!/usr/bin/env bash
# Thin wrapper for the two-step Delver import policy:
#   extract   — one-shot donor pull into gitignored local/ (extract + report +
#               normalize); delegates to scripts/extract-delver-reference.sh
#   normalize — re-normalize local/extracted into local/normalized
#   report    — inventory report of local/extracted
#
# Usage: ./scripts/import-delver.sh extract|normalize|report
# Requires the .NET 10 SDK available as `dotnet` on PATH.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL="$REPO_ROOT/local"
TOOL_PROJECT="$REPO_ROOT/src/Delver.Import.Tool/Delver.Import.Tool.csproj"

case "${1:-}" in
  extract)
    exec "$REPO_ROOT/scripts/extract-delver-reference.sh"
    ;;
  normalize)
    exec dotnet run --project "$TOOL_PROJECT" -c Release -- normalize --in "$LOCAL/extracted" --out "$LOCAL"
    ;;
  report)
    exec dotnet run --project "$TOOL_PROJECT" -c Release -- report --in "$LOCAL/extracted"
    ;;
  *)
    echo "usage: $0 extract|normalize|report" >&2
    exit 2
    ;;
esac
