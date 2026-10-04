#!/usr/bin/env bash
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo "Use osx-arm64 or osx-x64" >&2; exit 1;; esac
if [ -n "${UNFOLD_NOTARY_PROFILE:-}" ] && { [ -z "${UNFOLD_CODESIGN_IDENTITY:-}" ] || [ "${UNFOLD_CODESIGN_IDENTITY:-}" = "-" ]; }; then
  echo "Notarization requires UNFOLD_CODESIGN_IDENTITY" >&2; exit 1
fi
dotnet run --project "$ROOT/tools/Unfold.MediaSetup" -- "$RID" "$ROOT"
CSPROJ="$ROOT/src/Unfold.Desktop/Unfold.Desktop.csproj"
# The csproj <Version> is the single source of truth so the bundle and the
# published archive name can never drift out of sync with each other.
VERSION="$(grep -m1 -oE '<Version>[^<]+</Version>' "$CSPROJ" | sed -E 's#</?Version>##g')"
if [ -z "$VERSION" ]; then echo "Could not read <Version> from $CSPROJ" >&2; exit 1; fi
BUNDLE_VERSION="${VERSION%%-*}"
IFS=. read -r VERSION_MAJOR VERSION_MINOR VERSION_PATCH <<< "$BUNDLE_VERSION"
BETA_REVISION=0
case "$VERSION" in *-beta.*) BETA_REVISION="${VERSION##*-beta.}" ;; esac
if ! [[ "$BETA_REVISION" =~ ^[0-9]+$ ]] || [ "$BETA_REVISION" -gt 99 ]; then
  echo "Expected a beta revision between 0 and 99" >&2; exit 1
fi
BUILD_NUMBER=$(((10#$VERSION_MAJOR * 1000000 + 10#$VERSION_MINOR * 1000 + 10#$VERSION_PATCH) * 100 + 10#$BETA_REVISION))
DISPLAY_NAME="Unfold"
case "$VERSION" in *-beta|*-beta.[0-9]*) DISPLAY_NAME="Unfold Beta v$BUNDLE_VERSION" ;; esac
OUT="$ROOT/artifacts/$RID"
# A deleted or renamed pet must not survive in the next published bundle.
if [ -L "$OUT" ]; then echo "Refusing linked publish directory: $OUT" >&2; exit 1; fi
rm -rf "$OUT"
dotnet publish "$CSPROJ" -c Release -r "$RID" \
  --self-contained true -p:PublishReadyToRun=true -o "$OUT"
APP="$ROOT/artifacts/Unfold.app"
# A stale bundle from an earlier run would leave unsigned Mach-O files behind.
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/." "$APP/Contents/MacOS/"
cp "$ROOT/THIRD-PARTY-NOTICES.md" "$APP/Contents/Resources/"
cp "$ROOT/docs/cross-platform.md" "$APP/Contents/Resources/README.md"
cp "$ROOT/Art/Brand/unfold-lilac-v1/desktop/unfold.icns" "$APP/Contents/Resources/Unfold.icns"
if [ -f "$ROOT/docs/releases/v$VERSION.md" ]; then
  cp "$ROOT/docs/releases/v$VERSION.md" "$APP/Contents/Resources/RELEASE-NOTES.md"
fi
chmod +x "$APP/Contents/MacOS/Unfold"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Unfold</string>
<key>CFBundleDisplayName</key><string>$DISPLAY_NAME</string>
<key>CFBundleIdentifier</key><string>app.unfold.desktop</string>
<key>CFBundleExecutable</key><string>Unfold</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$BUNDLE_VERSION</string>
<key>CFBundleVersion</key><string>$BUILD_NUMBER</string>
<key>CFBundleIconFile</key><string>Unfold.icns</string>
<key>UnfoldReleaseVersion</key><string>$VERSION</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
# The updater-enabled bundle includes Velopack's metadata and native helper.
# Tool version is pinned in .config/dotnet-tools.json.
dotnet tool restore
python3 "$ROOT/Scripts/package-updates.py" --runtime "$RID" --payload "$APP"
tar -czf "$ROOT/artifacts/Unfold-v$VERSION-$RID.tar.gz" -C "$ROOT/artifacts" Unfold.app
# notarytool takes .zip, .dmg or .pkg; ditto is the only zip that keeps the
# signature and symlinks intact.
rm -f "$ROOT/artifacts/Unfold-v$VERSION-$RID.zip"
ditto -c -k --keepParent "$APP" "$ROOT/artifacts/Unfold-v$VERSION-$RID.zip"
bash "$ROOT/Scripts/package-macos-dmg.sh" "$RID" "$APP"
echo "$APP"
