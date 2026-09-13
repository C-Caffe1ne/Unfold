#!/bin/bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet_cli="${UNFOLD_DOTNET:-dotnet}"
if ! command -v "$dotnet_cli" >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  dotnet_cli="$HOME/.dotnet/dotnet"
fi
exec "$dotnet_cli" "$repo_root/src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll" --validate-characters "${1:-$repo_root/Assets/Characters}"
