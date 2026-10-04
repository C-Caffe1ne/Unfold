#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
APP="${2:-$ROOT/artifacts/Unfold.app}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo "Use osx-arm64 or osx-x64" >&2; exit 1;; esac
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :UnfoldReleaseVersion' "$APP/Contents/Info.plist")"
case "$VERSION" in *-beta|*-beta.[0-9]*) ;; *) echo "Expected a beta bundle" >&2; exit 1;; esac
EXPECTED="$(grep -m1 -oE '<Version>[^<]+</Version>' "$ROOT/src/Unfold.Desktop/Unfold.Desktop.csproj" | sed -E 's#</?Version>##g')"
if [ "$VERSION" != "$EXPECTED" ]; then echo "Bundle version does not match project" >&2; exit 1; fi
case "$(file -b "$APP/Contents/MacOS/Unfold")" in
  *arm64*) [ "$RID" = osx-arm64 ] || exit 1;;
  *x86_64*) [ "$RID" = osx-x64 ] || exit 1;;
  *) echo "Unexpected app host architecture" >&2; exit 1;;
esac
codesign --verify --deep --strict "$APP"
STAGING="$(mktemp -d "$ROOT/artifacts/dmg-stage.XXXXXX")"
trap 'rm -rf "$STAGING"' EXIT
ditto "$APP" "$STAGING/Unfold.app"
ln -s /Applications "$STAGING/Applications"
cat > "$STAGING/설치 안내.txt" <<'TEXT'
Unfold 설치

Unfold.app을 Applications 폴더로 드래그하세요.
Applications 폴더에서 Unfold를 실행한 뒤 이 디스크를 추출하세요.
업데이트 전에는 메뉴 막대의 Unfold 종료로 앱을 완전히 종료하세요.
설정·휴식 기록·커스텀 펫 데이터는 앱과 별도로 보관됩니다.
TEXT
IMAGE="$ROOT/artifacts/Unfold-v$VERSION-$RID.dmg"
TEMP_IMAGE="$ROOT/artifacts/Unfold-v$VERSION-$RID.tmp.dmg"
rm -f "$TEMP_IMAGE"
hdiutil create -volname "Unfold Beta ${VERSION%%-*}" -srcfolder "$STAGING" \
  -fs HFS+ -format UDZO -imagekey zlib-level=9 -ov "$TEMP_IMAGE"
if [ -n "${UNFOLD_CODESIGN_IDENTITY:-}" ]; then
  codesign --force --timestamp --sign "$UNFOLD_CODESIGN_IDENTITY" "$TEMP_IMAGE"
fi
hdiutil verify "$TEMP_IMAGE"
if [ -n "${UNFOLD_NOTARY_PROFILE:-}" ]; then
  # Ticket on the app survives copying into the image; the image itself also
  # receives a ticket before the final artifact is replaced.
  xcrun stapler validate "$APP"
  bash "$ROOT/Scripts/notarize-macos.sh" "$TEMP_IMAGE" "$UNFOLD_NOTARY_PROFILE"
fi
mv "$TEMP_IMAGE" "$IMAGE"
echo "$IMAGE"
