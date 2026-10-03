#!/usr/bin/env bash
# Submit a signed app or DMG, require acceptance, and attach its offline ticket.
# Credentials stay in the keychain; never pass an account password to this file.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="${1:?Usage: notarize-macos.sh /path/to/Unfold.app-or.dmg keychain-profile}"
PROFILE="${2:-${UNFOLD_NOTARY_PROFILE:-}}"
if [ -z "$PROFILE" ]; then echo "A notarytool keychain profile is required" >&2; exit 1; fi
if [ ! -e "$TARGET" ] || [ -L "$TARGET" ]; then echo "Missing or linked target: $TARGET" >&2; exit 1; fi
codesign --verify --deep --strict "$TARGET"
codesign --display --verbose=4 "$TARGET" 2>&1 | grep 'Authority=Developer ID Application:' > /dev/null
LOGS="${UNFOLD_NOTARY_LOG_DIR:-$ROOT/artifacts/notarization}/$(basename "$TARGET")"
mkdir -p "$LOGS"
# Keep each attempt, including rejected submissions and their full Apple logs.
ATTEMPT="$(mktemp -d "$LOGS/attempt.XXXXXX")"
case "$TARGET" in
  *.app)
    UPLOAD="$ATTEMPT/submission.zip"
    ditto -c -k --keepParent "$TARGET" "$UPLOAD"
    ;;
  *.dmg) UPLOAD="$TARGET" ;;
  *) echo "Only .app and .dmg targets are supported" >&2; exit 1;;
esac
shasum -a 256 "$UPLOAD" > "$ATTEMPT/submission.sha256"
xcrun notarytool submit "$UPLOAD" --keychain-profile "$PROFILE" \
  --output-format json > "$ATTEMPT/submit.json"
ID="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["id"])' "$ATTEMPT/submit.json")"
echo "Submitted $(basename "$TARGET"): $ID"
# wait returns a nonzero status for a rejection. Fetch Apple's diagnostic log
# before reporting that failure, and never staple or publish a rejected file.
WAIT_EXIT=0
xcrun notarytool wait "$ID" --keychain-profile "$PROFILE" \
  --output-format json > "$ATTEMPT/result.json" || WAIT_EXIT=$?
xcrun notarytool log "$ID" --keychain-profile "$PROFILE" "$ATTEMPT/apple-log.json"
STATUS="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("status", "Unknown"))' "$ATTEMPT/result.json")"
if [ "$WAIT_EXIT" -ne 0 ] || [ "$STATUS" != Accepted ]; then
  echo "Notarization $STATUS: $ID; inspect $ATTEMPT/apple-log.json" >&2
  exit 1
fi
xcrun stapler staple "$TARGET"
xcrun stapler validate "$TARGET"
codesign --verify --deep --strict "$TARGET"
case "$TARGET" in
  *.app)
    # Apple recommends this more accurate app check on macOS 14 and later.
    # Older hosts retain the legacy Gatekeeper assessment.
    if command -v syspolicy_check >/dev/null 2>&1; then
      syspolicy_check distribution "$TARGET"
    else
      spctl --assess --type execute --verbose=2 "$TARGET"
    fi
    ;;
  *.dmg) spctl --assess --type open --context context:primary-signature --verbose=2 "$TARGET" ;;
esac
echo "Accepted and stapled $(basename "$TARGET"): $ID"
