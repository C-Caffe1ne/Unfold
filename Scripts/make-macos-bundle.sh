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
# A stale bundle from an earlier run would leave unsigned Mach-O files behind.
rm -rf "$APP"
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
# Ad hoc by default, so a local build needs no certificate. Export
# UNFOLD_CODESIGN_IDENTITY="Developer ID Application: ..." to produce a release
# bundle that notarization can accept.
if [ -n "${UNFOLD_CODESIGN_IDENTITY:-}" ]; then
  ENTITLEMENTS="$ROOT/Packaging/Unfold.Desktop.entitlements"
  # Apple does not support --deep for Developer ID: sign the nested Mach-O files
  # first, then the bundle. Entitlements belong to the main executable only.
  while IFS= read -r -d '' binary; do
    if [ "$binary" = "$APP/Contents/MacOS/Unfold" ]; then continue; fi
    case "$(file -b "$binary")" in Mach-O*) ;; *) continue;; esac
    codesign --force --options runtime --timestamp --sign "$UNFOLD_CODESIGN_IDENTITY" "$binary"
  done < <(find "$APP/Contents/MacOS" -type f -print0)
  codesign --force --options runtime --timestamp --entitlements "$ENTITLEMENTS" \
    --sign "$UNFOLD_CODESIGN_IDENTITY" "$APP"
  codesign --verify --strict --verbose=2 "$APP"
else
  codesign --force --deep --sign - "$APP"
fi
tar -czf "$ROOT/artifacts/Unfold-$RID.tar.gz" -C "$ROOT/artifacts" Unfold.app
# notarytool takes .zip, .dmg or .pkg; ditto is the only zip that keeps the
# signature and symlinks intact.
rm -f "$ROOT/artifacts/Unfold-$RID.zip"
ditto -c -k --keepParent "$APP" "$ROOT/artifacts/Unfold-$RID.zip"
echo "$APP"
