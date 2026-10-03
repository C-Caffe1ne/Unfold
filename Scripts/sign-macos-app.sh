#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="${1:?Usage: sign-macos-app.sh /path/to/Unfold.app}"
: "${UNFOLD_CODESIGN_IDENTITY:?Set a Developer ID Application identity, or - for local ad-hoc validation}"
SIGNING_OPTIONS=(--force)
if [ "$UNFOLD_CODESIGN_IDENTITY" != "-" ]; then SIGNING_OPTIONS+=(--options runtime --timestamp); fi
if [ ! -d "$APP/Contents/MacOS" ] || [ -L "$APP" ]; then
  echo "Expected an app bundle, not a symbolic link: $APP" >&2; exit 1
fi
# Keep non-native .NET assemblies/configuration in Resources, with relative
# links where the host expects them. Apple treats regular files in MacOS as
# nested code. Its recommended symlink layout avoids generic signatures stored
# in extended attributes, which ordinary file transfers can lose (TN2206).
MANAGED="$APP/Contents/Resources/Managed"
mkdir -p "$MANAGED"
while IFS= read -r -d '' data; do
  case "$(file -b "$data")" in Mach-O*) continue;; esac
  name="$(basename "$data")"
  if [ -e "$MANAGED/$name" ]; then echo "Conflicting managed resource: $name" >&2; exit 1; fi
  if codesign --display "$data" >/dev/null 2>&1; then
    codesign --remove-signature "$data"
  fi
  mv "$data" "$MANAGED/$name"
  ln -s "../Resources/Managed/$name" "$data"
done < <(find "$APP/Contents/MacOS" -maxdepth 1 -type f -print0)
# Data-only directories also belong in Resources. Keep the public runtime paths
# as links so existing asset consumers continue to work.
for name in Assets Licenses; do
  data="$APP/Contents/MacOS/$name"
  if [ -d "$data" ] && [ ! -L "$data" ]; then
    mv "$data" "$APP/Contents/Resources/$name"
    ln -s "../Resources/$name" "$data"
  fi
done
# The .NET host resolves the entry assembly's symlink, so its base directory
# becomes Managed. Keep bundled native libraries and asset/tool paths available
# there as well; otherwise it can incorrectly fall back to a system .NET host.
while IFS= read -r -d '' payload; do
  name="$(basename "$payload")"
  if [ ! -e "$MANAGED/$name" ] && [ ! -L "$MANAGED/$name" ]; then
    ln -s "../../MacOS/$name" "$MANAGED/$name"
  fi
done < <(find "$APP/Contents/MacOS" -mindepth 1 -maxdepth 1 ! -type l -print0)
# Include data directories that were linked into Resources above.
for name in Assets Licenses; do
  if [ -e "$APP/Contents/MacOS/$name" ] && [ ! -e "$MANAGED/$name" ]; then
    ln -s "../$name" "$MANAGED/$name"
  fi
done
# Earlier ad-hoc --deep builds also signed images and other data through xattrs.
# Remove those generic signatures before sealing the new bundle. They must be
# resource hashes, not nested requirements for an old ad-hoc identity.
while IFS= read -r -d '' data; do
  case "$(file -b "$data")" in Mach-O*) continue;; esac
  if codesign --display "$data" >/dev/null 2>&1; then
    codesign --remove-signature "$data"
  fi
done < <(find "$APP/Contents" -type f ! -path '*/_CodeSignature/*' ! -name Info.plist -print0)
# Mixed tool directories contain both Mach-O executables and license text.
# Place the latter in Resources with links alongside their corresponding tool.
while IFS= read -r -d '' data; do
  case "$(file -b "$data")" in Mach-O*) continue;; esac
  relative="${data#"$APP/Contents/MacOS/"}"
  destination="$APP/Contents/Resources/NativeSupport/$relative"
  mkdir -p "$(dirname "$destination")"
  mv "$data" "$destination"
  link="$(python3 -c 'import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "$destination" "$(dirname "$data")")"
  ln -s "$link" "$data"
done < <(find "$APP/Contents/MacOS" -type f -print0)
# Sign native files inside out. Only the main host needs allow-jit.
while IFS= read -r -d '' binary; do
  if [ "$binary" = "$APP/Contents/MacOS/Unfold" ]; then continue; fi
  case "$(file -b "$binary")" in Mach-O*) ;; *) continue;; esac
  codesign "${SIGNING_OPTIONS[@]}" --sign "$UNFOLD_CODESIGN_IDENTITY" "$binary"
  codesign --verify --strict "$binary"
done < <(find "$APP/Contents/MacOS" -type f -print0)
codesign "${SIGNING_OPTIONS[@]}" \
  --entitlements "$ROOT/Packaging/Unfold.Desktop.entitlements" \
  --sign "$UNFOLD_CODESIGN_IDENTITY" "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"
if [ "$UNFOLD_CODESIGN_IDENTITY" != "-" ]; then
  codesign --display --verbose=4 "$APP" 2>&1 | grep 'Authority=Developer ID Application:' > /dev/null
fi
