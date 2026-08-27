#!/bin/bash
#
# Wraps the compiled binary in a minimal Unfold.app bundle so that
# UserNotifications works and macOS treats it as a proper menu bar agent
# (LSUIElement = no Dock icon).
#
# Usage:
#   swift build -c release
#   ./Scripts/make-app-bundle.sh            # uses release
#   ./Scripts/make-app-bundle.sh debug      # uses debug build
#
set -euo pipefail

CONFIG="${1:-release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BIN="$ROOT/.build/$CONFIG/Unfold"
RESOURCE_BUNDLE="$ROOT/.build/$CONFIG/Unfold_Unfold.bundle"
APP="$ROOT/build/Unfold.app"

if [ ! -f "$BIN" ]; then
    echo "error: binary not found at $BIN" >&2
    echo "       run: swift build -c $CONFIG" >&2
    exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/Unfold"

# Character packages (sprite sheets, character.json, ...) live in the SwiftPM
# resource bundle. Bundle.module looks for it next to the executable *and*
# under Bundle.main.resourceURL, so Contents/Resources is the right place
# inside a real .app.
if [ -d "$RESOURCE_BUNDLE" ]; then
    cp -R "$RESOURCE_BUNDLE" "$APP/Contents/Resources/"
else
    echo "warning: resource bundle not found at $RESOURCE_BUNDLE (characters will fail to load)" >&2
fi

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>Unfold</string>
    <key>CFBundleDisplayName</key><string>Unfold</string>
    <key>CFBundleIdentifier</key><string>com.unfold.app</string>
    <key>CFBundleExecutable</key><string>Unfold</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>0.1.0</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>LSUIElement</key><true/>
</dict>
</plist>
PLIST

# Ad-hoc signature; enough for local runs and for notification delivery.
codesign --force --sign - "$APP" >/dev/null 2>&1 || true

echo "built $APP"
echo "run:  open \"$APP\"   (or)   \"$APP/Contents/MacOS/Unfold\""
