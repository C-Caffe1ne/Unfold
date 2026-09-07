#!/usr/bin/env bash
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo "Use osx-arm64 or osx-x64" >&2; exit 1;; esac
OUT="$ROOT/artifacts/$RID"
dotnet publish "$ROOT/src/Unfold.Desktop/Unfold.Desktop.csproj" -c Release -r "$RID" \
  --self-contained true -p:PublishReadyToRun=true -o "$OUT"
APP="$ROOT/artifacts/Unfold.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/." "$APP/Contents/MacOS/"
cp "$ROOT/THIRD-PARTY-NOTICES.md" "$APP/Contents/Resources/"
chmod +x "$APP/Contents/MacOS/Unfold"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Unfold</string>
<key>CFBundleDisplayName</key><string>Unfold</string>
<key>CFBundleIdentifier</key><string>app.unfold.desktop</string>
<key>CFBundleExecutable</key><string>Unfold</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.2.0</string>
<key>CFBundleVersion</key><string>2</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
codesign --force --deep --sign - "$APP"
tar -czf "$ROOT/artifacts/Unfold-$RID.tar.gz" -C "$ROOT/artifacts" Unfold.app
echo "$APP"
