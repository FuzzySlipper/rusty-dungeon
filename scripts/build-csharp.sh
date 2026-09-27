#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
project="$repo_root/src/RustyTemplate.Game/RustyTemplate.Game.csproj"
target=StageRustyEngineCoreClrProduct
case "${1:-}" in
  '') ;;
  --aot) target=VerifyRustyEngineAot ;;
  -h|--help) echo "usage: $0 [--aot] (build and stage CoreCLR; optionally publish NativeAOT)"; exit 0 ;;
  *) echo "usage: $0 [--aot]" >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo 'Too many arguments.' >&2; exit 2; }
dotnet build "$project" --configuration Release
dotnet msbuild "$project" -t:"$target" -p:Configuration=Release
