#!/usr/bin/env bash
# AppKit sizes the title bar and its buttons for the SDK the main executable declares.
# The stock .NET apphost declares an older SDK, so macOS 26 draws the previous 28 pt layout.
# Avalonia's native library already targets the macOS 26 SDK; declare the same SDK on the
# apphost and keep its minimum OS. Packaging signs the bundle again afterwards.
set -euo pipefail
HOST="${1:?Usage: declare-macos-appkit-sdk.sh /path/to/apphost}"
SDK=26.0
if ! xcrun --find vtool >/dev/null 2>&1; then
  echo "error: vtool is required to set the macOS window layout SDK. Install the Xcode command line tools." >&2
  exit 1
fi
BUILD="$(xcrun vtool -show-build "$HOST")"
MINOS="$(awk '$1 == "minos" { print $2; exit }' <<< "$BUILD")"
CURRENT="$(awk '$1 == "sdk" { print $2; exit }' <<< "$BUILD")"
if [ -z "$MINOS" ] || [ -z "$CURRENT" ]; then echo "error: $HOST has no macOS build version" >&2; exit 1; fi
if [ "${CURRENT%%.*}" -ge "${SDK%%.*}" ]; then exit 0; fi
xcrun vtool -set-build-version macos "$MINOS" "$SDK" -replace -output "$HOST.sdk" "$HOST" 2>/dev/null
mv -f "$HOST.sdk" "$HOST"
codesign --force --sign - "$HOST" 2>/dev/null
