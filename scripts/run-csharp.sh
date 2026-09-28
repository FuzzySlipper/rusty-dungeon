#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
# The CoreCLR product loader resolves hostfxr through DOTNET_ROOT and the dev
# supervisor launches the compiler through RUSTY_DEV_DOTNET; derive both from
# the dotnet we can find. Serve brokers run with a minimal PATH, so fall back
# to the standard per-user SDK location before giving up.
dotnet_bin=$(command -v dotnet 2>/dev/null || true)
if [[ -z "$dotnet_bin" && -x "$HOME/.dotnet/dotnet" ]]; then
  dotnet_bin="$HOME/.dotnet/dotnet"
fi
if [[ -n "$dotnet_bin" ]]; then
  # `rusty dev` spawns plain `dotnet` from PATH for builds and msbuild probes.
  dotnet_dir=$(cd "$(dirname "$dotnet_bin")" && pwd)
  case ":$PATH:" in
    *":$dotnet_dir:"*) ;;
    *) export PATH="$dotnet_dir:$PATH" ;;
  esac
  if [[ -z "${DOTNET_ROOT:-}" ]]; then
    DOTNET_ROOT="$dotnet_dir"
    export DOTNET_ROOT
  fi
  if [[ -z "${RUSTY_DEV_DOTNET:-}" ]]; then
    RUSTY_DEV_DOTNET="$dotnet_bin"
    export RUSTY_DEV_DOTNET
  fi
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
