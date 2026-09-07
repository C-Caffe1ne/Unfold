# Unfold

A tiny free macOS menu bar app that reminds you to stretch after long stretches
of Mac use, with a character (starting with Cat) that shows up to remind you.

## Status

Step 4 — V1 sprite sheet spec finalized. Animations are frame-index based
(not row-based), so a clip can span multiple rows. The bundled "Mochi" cat
ships an 8-column, 384×384px placeholder sheet with a real idle loop (8
frames) and a stretch clip (12 frames spanning two rows) — both verified
playing end to end in the packaged app.

## Requirements

The character editor uses a native SwiftUI/AppKit pixel canvas, with Piskel
source compatibility, layers, animation frames, Undo/Redo, and PNG/Piskel file
import/export. See [native pixel editor](docs/native-pixel-editor.md) for features,
format limits, and macOS verification status.

- macOS 13+
- Swift 5.9+ toolchain (Xcode or Command Line Tools)

## Build & run

```sh
# Quick dev run (no bundle → notifications degrade to log lines)
swift run

# Proper menu bar agent with a real bundle (notifications work)
swift build -c release
./Scripts/make-app-bundle.sh release
open "build/Spine Keepet.app"
```

To stop the bundled app: use **Quit Spine Keepet** from its menu.

## Project layout

```
Sources/Unfold/
  App/            UnfoldMain (entry point), AppDelegate (composition root)
  MenuBar/        StatusItemController — NSStatusItem + NSMenu, the only UI
  Timer/          StretchTimer — Date-based countdown, pause/reset, idle-aware
  Session/        ActivityMonitor — idle detection (protocol + system impl)
  Settings/       SettingsStore (UserDefaults), StretchInterval, SwiftUI view
  Notifications/  NotificationManager — UNUserNotificationCenter wrapper
  Stretch/        StretchEvent, StretchCoordinator — fans a stretch event out
                  to notifications + the overlay
  Character/      Character model (id/name/spriteSheet/animations/source),
                  manifest + package loader, repositories, manager, and the
                  view that resolves a character's animation into a sprite
  Sprite/         Generic sprite-sheet engine — SpriteSheetDefinition,
                  SpriteAnimationDefinition, SpriteSheetImage (frame
                  cropping), SpriteAnimator (playback), SpriteAnimationView.
                  Knows nothing about characters, the timer, or the overlay.
  Overlay/        OverlayController, OverlayWindowController (NSPanel),
                  StretchOverlayView — the centered reminder card
  Resources/
    Characters/<id>/character.json + spritesheet.png — built-in character
    packages, copied into the app's resource bundle at build time
  Support/        Constants, Strings, TimeFormatting
Scripts/
  make-app-bundle.sh — wraps the binary + resource bundle into "Spine Keepet.app"
  generate-placeholder-spritesheet.swift — regenerates the test cat sheet
```

## Design choices

- **UI vs. logic are separate.** `StatusItemController` reads state from
  `StretchTimer` / `SettingsStore` and forwards actions back; it holds no timer
  logic.
- **The countdown is derived from a target `Date`,** not a per-second counter, so
  it stays accurate when the 1 s display tick is delayed in the background.
- **Idle time doesn't count.** While `ActivityMonitor` reports the user as idle,
  the target date is pushed forward. Step 1 uses a simple system-wide idle query;
  the timer only depends on the `ActivityMonitoring` protocol, so per-app
  tracking can be swapped in later.
- **Settings persist** through `UserDefaults` on every change.
- **Launch at login** is stubbed (`SettingsStore.launchAtLogin`) but not yet
  wired to `SMAppService`.
- **Timer knows nothing about characters.** It only ever produces a
  `StretchEvent`. `StretchCoordinator` is the seam that turns that event into
  "notify + show the current character in the overlay":
  `Timer → StretchEvent → StretchCoordinator → CharacterManager → Character
  → CharacterAnimationView → SpriteAnimator → SpriteAnimationView → Overlay`.
- **Character sourcing is abstracted.** `CharacterRepository` is a protocol;
  `CompositeCharacterRepository` combines `BuiltInCharacterRepository` (loads
  bundled packages) and `ImportedCharacterRepository` (a stub returning `[]`
  until `.unfoldcharacter` import is built) into one flat list.
  `CharacterManager` and the UI never know which repository a character came
  from.
- **Animation is sprite-sheet based**, not Lottie. A character has one
  `spriteSheet` (`SpriteSheetDefinition`: file + columns + rows + the
  intended frameWidth/frameHeight, V1 default 384×384px) and a set of named
  `SpriteAnimationDefinition`s (**`frames: [Int]`** + fps + loop) — an
  animation is a list of frame indices, not a row, so a 12-frame clip on an
  8-column sheet naturally spans two rows without any special-casing.
  `Sprite/` is a self-contained engine — `SpriteSheetImage` decodes the
  sheet once with `ImageIO` (`CGImageSource`, not `NSImage`, to avoid
  display-scale-dependent rasterization) and crops each frame's `CGImage`
  from that same in-memory image (`column = index % columns`,
  `row = index / columns`, never a hardcoded cell size), `SpriteAnimator`
  drives frame progression against real time and exposes it as `@Published`,
  and `SpriteAnimationView` renders it. None of the three know what a "Cat"
  or a "Timer" is.
- **Character display size** is `Constants.characterDisplaySize` (192pt,
  recommended range 160–220pt); the overlay panel is
  `Constants.overlaySize` (380×380pt, within the 360–440pt target width).
  384px frames stay crisp at that size on Retina displays.
- **Overlay dismiss is manual-only in V1** — `Constants.overlayAutoDismissDelay`
  is `nil` by default; `OverlayController` only schedules a timed dismiss
  when it's set. This is the extension point for "auto-dismiss a few
  seconds after the animation ends" later, without restructuring anything.
- **`AnimationKey`** (`Character/CharacterAnimation.swift`) is a
  `RawRepresentable` string wrapper, not a closed `enum` — built-in kinds
  (`.idle`, `.stretch`, `.yawn`, `.celebration`, `.sleep`) are static
  constants, but an imported character package can define its own key
  without changing this type.
- **`CharacterManifest` + `CharacterPackageLoader`** decode and validate
  `character.json` (id, spriteSheet grid, per-animation frame ranges) into a
  `Character`. The same loader path is used for bundled and (later) imported
  packages — `loadBuiltIn(id:)` today, `loadImported(packageDirectory:)`
  ready for the future import flow.
- **`CharacterAssetLoader`** is the only place that turns a manifest's
  package-relative file name into a real `URL` — it rejects absolute paths
  and `..` traversal before touching disk, so a character package can only
  ever reference files inside itself.
