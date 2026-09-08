#!/bin/bash
#
# Wraps the compiled binary in a minimal "Spine Keepet.app" bundle so that
# UserNotifications works and macOS treats it as a proper menu bar agent
# (LSUIElement = no Dock icon). The internal executable name and resource
# bundle name stay "Unfold" — only the product-facing .app wrapper is
# renamed, mirroring PRODUCT_NAME vs. EXECUTABLE_NAME in Unfold.xcodeproj.
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
APP="$ROOT/build/Spine Keepet.app"

if [ ! -f "$BIN" ]; then
    echo "error: binary not found at $BIN" >&2
    echo "       run: swift build -c $CONFIG" >&2
    exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/Unfold"

# Character packages (sprite sheets, character.json, ...) live in the SwiftPM
# resource bundle. SwiftPM's generated `Bundle.module` accessor looks for it
# at `Bundle.main.bundleURL` + the bundle name — for a macOS .app, that's
# the bundle's TOP level (sibling to Contents/), not Contents/Resources.
# (Verified directly: Contents/Resources placement only ever worked before
# because the accessor's fallback silently hit a *hardcoded dev-machine
# absolute .build/ path instead — which broke the moment the app ran
# sandboxed or from a different machine/directory. See the release-
# readiness report for how this was found.)
if [ -d "$RESOURCE_BUNDLE" ]; then
    cp -R "$RESOURCE_BUNDLE" "$APP/"
else
    echo "warning: resource bundle not found at $RESOURCE_BUNDLE (characters will fail to load)" >&2
fi

cp "$ROOT/Packaging/Info.plist" "$APP/Contents/Info.plist"

# Packaging/Info.plist's CFBundleIdentifier may be the literal
# "$(PRODUCT_BUNDLE_IDENTIFIER)" build-setting macro (Xcode writes this
# form automatically once you edit the identifier in its Signing UI) — the
# Xcode pipeline resolves that macro itself while processing Info.plist,
# but this script just does a plain `cp`, so the macro would otherwise end
# up in the shell-built app's Info.plist verbatim. Pull the real value
# straight from Unfold.xcodeproj/project.pbxproj (the actual single source
# of truth) so both pipelines end up with the same identifier without
# duplicating it here.
#
# The macro appears in more than CFBundleIdentifier: the exported UTI for
# the .unf document type is derived from it too, and it is nested inside
# arrays where `plutil -replace` cannot reach by key path. Substituting
# the literal text everywhere in the file covers every occurrence,
# present and future. An unresolved macro here is not cosmetic — Launch
# Services will happily register a UTI literally named
# "$(product_bundle_identifier).unf".
PBXPROJ="$ROOT/Unfold.xcodeproj/project.pbxproj"
if [ -f "$PBXPROJ" ]; then
    BUNDLE_ID="$(grep -m1 'PRODUCT_BUNDLE_IDENTIFIER' "$PBXPROJ" | sed -E 's/.*= *([^;]+);.*/\1/' | tr -d ' ')"
    if [ -n "$BUNDLE_ID" ]; then
        sed -i '' "s/\\\$(PRODUCT_BUNDLE_IDENTIFIER)/$BUNDLE_ID/g" "$APP/Contents/Info.plist"
        plutil -lint "$APP/Contents/Info.plist" >/dev/null
    else
        echo "warning: could not read PRODUCT_BUNDLE_IDENTIFIER from $PBXPROJ — Info.plist may contain an unresolved \$(PRODUCT_BUNDLE_IDENTIFIER) placeholder" >&2
    fi
fi
if grep -q 'PRODUCT_BUNDLE_IDENTIFIER' "$APP/Contents/Info.plist"; then
    echo "warning: Info.plist still contains an unresolved \$(PRODUCT_BUNDLE_IDENTIFIER); the .unf document type will not register correctly" >&2
fi

# Ad-hoc signature with the real App Sandbox entitlement applied, so local
# runs actually exercise the sandboxed code paths (UserDefaults, bundle
# resource loading, CGEventSource, notifications, SMAppService) rather than
# only ever being tested unsandboxed. This is still not a substitute for
# signing with a real Developer ID / App Store distribution certificate —
# see the release-readiness report for what still needs a real signing
# identity to verify.
codesign --force --sign - --entitlements "$ROOT/Packaging/Unfold.entitlements" "$APP" >/dev/null 2>&1 || true

echo "built $APP"
echo "run:  open \"$APP\"   (or)   \"$APP/Contents/MacOS/Unfold\""

