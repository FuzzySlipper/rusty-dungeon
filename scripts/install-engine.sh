#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
props="$repo_root/Directory.Build.props"
engine_repo=FuzzySlipper/rusty-engine
update=false
case "${1:-}" in
  '') ;;
  --update) update=true ;;
  -h|--help) echo "usage: $0 [--update] (install the pin, or install and pin the newest published pair)"; exit 0 ;;
  *) echo "usage: $0 [--update]" >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo 'Too many arguments.' >&2; exit 2; }
version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' "$props")
revision=$(sed -n 's|.*<RustyEnginePairSourceRevision>\([^<]*\)</RustyEnginePairSourceRevision>.*|\1|p' "$props")
if [[ "$update" == true ]]; then
  tag=$(gh release list -R "$engine_repo" --limit 100 --json tagName,publishedAt,isDraft \
    --jq '[.[] | select(.isDraft == false and (.tagName | startswith("csharp-sdk-v")))] | sort_by(.publishedAt) | last | .tagName')
  [[ -n "$tag" && "$tag" != null ]] || { echo 'No published Engine pair found.' >&2; exit 1; }
  version=${tag#csharp-sdk-v}
fi
[[ -n "$version" && -n "$revision" ]] || { echo 'Missing Engine pin in Directory.Build.props.' >&2; exit 1; }
pair="$repo_root/.runtime/pairs/$version"
mkdir -p "$repo_root/.runtime/pairs" "$repo_root/.runtime/sdk-feed"
temporary=$(mktemp -d "$repo_root/.runtime/pairs/.install.XXXXXX")
trap 'rm -rf -- "$temporary"' EXIT
if [[ ! -d "$pair" ]]; then
  archive="rusty-engine-csharp-pair-$version-linux-x64.tar.gz"
  gh release download "csharp-sdk-v$version" -R "$engine_repo" --dir "$temporary" \
    --pattern "$archive" --pattern "$archive.sha256"
  (cd "$temporary" && sha256sum --check "$archive.sha256")
  tar -xzf "$temporary/$archive" -C "$temporary"
  extracted="$temporary/${archive%.tar.gz}"
else
  extracted="$pair"
fi
# Use the Engine's verifier for the complete immutable package/runtime artifact.
"$extracted/verify-pair.sh" --directory "$extracted"
manifest="$extracted/pair-manifest.json"
if [[ "$update" == true ]]; then
  revision=$(jq -r '.sourceRevision' "$manifest")
fi
jq -e --arg version "$version" --arg revision "$revision" \
  '.package.id == "Rusty.Engine" and .package.version == $version and
   .sourceRevision == $revision and .runtime.sourceRevision == $revision' \
  "$manifest" >/dev/null || { echo 'Engine pair does not match the requested pin.' >&2; exit 1; }
if [[ "$extracted" != "$pair" ]]; then
  mv -- "$extracted" "$pair"
fi
package="Rusty.Engine.$version.nupkg"
feed="$repo_root/.runtime/sdk-feed/$package"
if [[ -f "$feed" ]]; then
  cmp "$pair/sdk-feed/$package" "$feed" || { echo 'Installed SDK differs from the immutable pair.' >&2; exit 1; }
else
  cp "$pair/sdk-feed/$package" "$temporary/$package"
  mv "$temporary/$package" "$feed"
fi
if [[ "$update" == true ]]; then
  sed -E \
    -e "s|(<RustyEnginePackageVersion>)[^<]*(</RustyEnginePackageVersion>)|\1$version\2|" \
    -e "s|(<RustyEnginePairSourceRevision>)[^<]*(</RustyEnginePairSourceRevision>)|\1$revision\2|" \
    "$props" > "$temporary/Directory.Build.props"
  mv "$temporary/Directory.Build.props" "$props"
fi
echo "Installed Rusty Engine $version ($revision)."
