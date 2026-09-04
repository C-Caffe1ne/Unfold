# Desktop Pet Interaction Design

Status: approved design

## Goal

Make the existing macOS desktop pet feel more companion-like at stretch-reminder time without importing VPet code, assets, formats, or Windows dependencies.

## Product boundary

- The app remains a macOS-native Swift/AppKit/SwiftUI application.
- Only a real StretchTimer event may start the pet's stretch animation and speech bubble.
- No manual stretch, random behaviour, random speech, AI-generated speech, items, needs, jobs, Steam, plugins, or VPet-format support is included.
- Windows/Linux hosts may later reuse documented asset and event contracts, not the macOS window implementation.

## Interaction contract

- A right-click on visible pet pixels offers Hide Pet and Open Settings.
- The menu bar offers Show Pet only while the pet is hidden.
- Hidden state is process-local; relaunch always shows the pet.
- A speech bubble is a separate transparent, click-through panel above the pet.
- Pet left-click dismisses an open bubble without playing a click reaction.
- Bubble position follows pet drags and remains on the visible screen.
- Hiding the pet immediately dismisses the bubble; showing does not restore old copy.

## Event contract

```text
StretchTimer event
  -> StretchCoordinator
     -> existing notification and overlay
     -> DesktopPetPresenting.presentStretch(character)
        -> play actual stretch once
        -> show resolved stretch speech
        -> return to idle when non-looping playback finishes
```

The desktop-pet branch must never reset, pause, or reschedule StretchTimer. It must display stretch and speech even when Reduce Motion is active, per the explicit product choice.

## Character and speech contract

- Keep character.json as the animation manifest. Do not adopt VPet WPF/LPS configuration.
- Animation keys remain extensible strings; Mochi uses idle and stretch for this work.
- A package may optionally provide speech.<language>.json beside character.json.
- Missing, malformed, unreadable, empty, or event-missing speech files fall back to app-provided Korean stretch copy.
- Future locale files use the same sibling-file naming, for example speech.en.json.

## Technical boundaries

- Retain SpriteAnimator, current animation loaders, alpha hit testing, drag geometry, and existing character validation.
- Use narrow notification, overlay, and desktop-pet presentation protocols to connect StretchCoordinator to its three effects without AppKit coupling.
- Keep bubble geometry pure and keep AppKit panels in a dedicated controller.
- Add no dependency, entitlement, global event tap, analytics, account, StoreKit, or network service.

## Validation contract

- Automated tests cover speech fallback, event fan-out, stretch-to-idle playback, visibility lifecycle, and bubble geometry.
- Existing animation, alpha-hit, drag, persistence, timer, overlay, and character tests remain green.
- Verify SwiftPM and Xcode Debug/Release builds.
- Manually verify actual event fan-out, click-through, menu actions, hide/show, drag/multiple screens, and the selected Reduce Motion behavior.
