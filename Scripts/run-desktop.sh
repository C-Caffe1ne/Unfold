#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export AVALONIA_TELEMETRY_OPTOUT=1
# A separate profile prevents a running installed app from receiving activation.
# Set UNFOLD_DATA_DIR explicitly to reuse a development profile across runs.
if [[ -z "${UNFOLD_DATA_DIR:-}" ]]; then
  export UNFOLD_DATA_DIR="$(mktemp -d "${TMPDIR:-/tmp}/Unfold-dev.XXXXXX")"
fi
printf 'Unfold source: %s\nDevelopment data: %s\n' "$ROOT" "$UNFOLD_DATA_DIR"
exec dotnet run --project "$ROOT/src/Unfold.Desktop/Unfold.Desktop.csproj" -c Release -- "$@"
