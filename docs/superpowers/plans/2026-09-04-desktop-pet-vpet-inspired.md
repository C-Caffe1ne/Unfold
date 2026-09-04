# Desktop Pet VPet-Inspired Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add timer-driven stretch playback, optional Korean character speech, an anchored click-through speech bubble, and non-persistent desktop-pet controls without changing Stretch Reminder scheduling.

**Architecture:** Preserve the existing rendering and timer engines. StretchCoordinator depends on narrow notification, overlay, and desktop-pet presentation protocols; DesktopPetWindowController owns AppKit presentation; a pure helper computes bubble frames. Character-package speech is optional, with app copy as the safe fallback.

**Tech Stack:** Swift 5.9, SwiftPM, XCTest, AppKit, SwiftUI, Foundation, existing Xcode app target.

**Spec:** docs/superpowers/specs/2026-09-04-desktop-pet-vpet-inspired-design.md

## Global Constraints

- Target macOS 13 or later and preserve SwiftPM/Xcode dual builds.
- Add no dependency, entitlement, VPet asset/code/format, global event tap, analytics, account, StoreKit, or network service.
- Only StretchTimer events can play pet stretch or open speech.
- Desktop-pet presentation must not reset, pause, or reschedule StretchTimer.
- Bubble is click-through; pet click only dismisses it and does not play a click reaction.
- Stretch and speech intentionally ignore Reduce Motion for this user-selected feature.
- Keep existing Stretch Reminder behavior and the current character rendering chain.

---

### Task 1: Optional character speech and safe fallback

**Files:**
- Create: Sources/Unfold/DesktopPet/DesktopPetSpeech.swift
- Create: Sources/Unfold/Resources/Characters/default-cat/speech.ko.json
- Modify: Sources/Unfold/Support/Strings.swift
- Test: Tests/UnfoldTests/DesktopPetSpeechTests.swift

**Interfaces:**
- `DesktopPetSpeechDocument` decodes `events: [String: [String]]`.
- `DesktopPetSpeechResolver.stretchMessage(for:localeIdentifier:randomIndex:)` returns one character stretch message or app Korean fallback.
- The resolver uses `CharacterAssetLoader.resolveFileURL` with a safe `speech.<language>.json` file name.

- [ ] Write tests that prove valid bundled Korean copy is selected and missing/malformed/empty content returns `Strings.PetSpeech.defaultKoreanStretchMessages[0]`.
- [ ] Run `swift test --filter DesktopPetSpeechTests`; expect compile failure before implementation.
- [ ] Implement first-locale-component lookup (`ko-KR` to `speech.ko.json`), JSON decode, non-empty stretch selection, and fallback.
- [ ] Add `speech.ko.json` with a non-empty `stretch` array.
- [ ] Re-run `swift test --filter DesktopPetSpeechTests`; expect PASS.
- [ ] Commit: `feat: add desktop pet speech fallback`.

### Task 2: Bubble geometry and independent click-through bubble panel

**Files:**
- Create: Sources/Unfold/DesktopPet/DesktopPetSpeechBubbleGeometry.swift
- Create: Sources/Unfold/DesktopPet/DesktopPetSpeechBubbleWindowController.swift
- Test: Tests/UnfoldTests/DesktopPetSpeechBubbleGeometryTests.swift

**Interfaces:**
- `DesktopPetSpeechBubbleGeometry.frame(petFrame:bubbleSize:visibleFrame:gap:)` centers above the pet and clamps fully to visibleFrame.
- `DesktopPetSpeechBubbleWindowController` exposes `show(message:petFrame:visibleFrame:)`, `move(petFrame:visibleFrame:)`, and `dismiss()`.

- [ ] Write tests for centered-above placement and top/left/right edge clamping.
- [ ] Run `swift test --filter DesktopPetSpeechBubbleGeometryTests`; expect compile failure before implementation.
- [ ] Implement the pure frame function and a borderless, nonactivating, floating, clear, shadowless, permanently click-through AppKit panel hosting the bubble view.
- [ ] Re-run `swift test --filter DesktopPetSpeechBubbleGeometryTests`; expect PASS.
- [ ] Commit: `feat: add anchored desktop pet speech bubble`.

### Task 3: Pet stretch presentation, dismissal, drag tracking, and visibility

**Files:**
- Modify: Sources/Unfold/DesktopPet/DesktopPetAnimationController.swift
- Modify: Sources/Unfold/DesktopPet/DesktopPetWindowController.swift
- Modify: Tests/UnfoldTests/DesktopPetAnimationControllerTests.swift
- Modify: Tests/UnfoldTests/DesktopPetWindowControllerTests.swift

**Interfaces:**
- `DesktopPetAnimationController.playStretch() -> Bool` starts the displayed character's exact non-looping stretch despite Reduce Motion and returns to idle on finish; it returns `false` without changing idle when the displayed character has no exact stretch animation.
- `DesktopPetWindowController.presentStretch(for:)`, `setVisible(_:)`, and test-visible `isSpeechBubbleVisible`.

- [ ] Add tests for stretch-to-idle transition and hide-dismiss-show-without-restore lifecycle.
- [ ] Run the affected test classes; expect compile failure for the new APIs.
- [ ] Store the launch-time displayed Character in DesktopPetWindowController and implement bubble ownership. `presentStretch(for:)` plays and speaks only when the stored character has an exact stretch clip; it does not silently turn a missing stretch into an endless idle reaction. A mouse-down first dismisses an open bubble and does not forward an interaction reaction. Drag and display-reconfiguration reposition the bubble. Hiding orders out both panels; showing orders in only the pet.
- [ ] Re-run focused animation/window tests; expect PASS.
- [ ] Commit: `feat: present timer-driven desktop pet stretch`.

### Task 4: Timer fan-out and app-control menus

**Files:**
- Modify: Sources/Unfold/Stretch/StretchCoordinator.swift
- Modify: Sources/Unfold/App/AppDelegate.swift
- Modify: Sources/Unfold/MenuBar/StatusItemController.swift
- Modify: Sources/Unfold/DesktopPet/DesktopPetWindowController.swift
- Create: Tests/UnfoldTests/StretchCoordinatorDesktopPetTests.swift
- Create: Tests/UnfoldTests/StatusItemControllerDesktopPetTests.swift

**Interfaces:**
- `protocol StretchNotifying: AnyObject` provides `notifyStretchDue()`; NotificationManager conforms.
- `@MainActor protocol StretchOverlayPresenting: AnyObject` provides `presentStretchReminder(for: Character)`; OverlayController conforms.
- `@MainActor protocol DesktopPetPresenting: AnyObject` provides `presentStretch(for: Character)` and `setVisible(_: Bool)`; DesktopPetWindowController conforms.
- StretchCoordinator injects all three protocols and calls each once for every handled StretchEvent.
- StatusItemController receives `onSetDesktopPetVisible: (Bool) -> Void` and exposes Show Pet only when hidden.

- [ ] Write a fake-service test proving notification, overlay, and desktop pet each receive one event.
- [ ] Run `swift test --filter StretchCoordinatorDesktopPetTests`; expect compile failure before protocol injection.
- [ ] Make NotificationManager and OverlayController conform to their new narrow protocols, inject the existing desktop pet from AppDelegate, add right-click Hide Pet/Open Settings, and add menu-bar Show Pet while hidden. Never call StretchTimer.reset from these actions.
- [ ] Run focused coordinator/menu tests; expect PASS.
- [ ] Commit: `feat: connect desktop pet to stretch reminders`.

### Task 5: Regression, build, and manual QA

**Files:**
- Modify: docs/superpowers/plans/2026-09-04-desktop-pet-vpet-inspired.md

- [ ] Run `swift test && swift build && swift build -c release`; expect every XCTest suite and both builds to pass.
- [ ] Run `xcodebuild -project Unfold.xcodeproj -scheme 'Spine Keepet' -configuration Debug build` and the matching Release command; expect BUILD SUCCEEDED for both.
- [ ] Use the Debug Trigger Stretch Reminder menu to check notification/overlay/pet/bubble fan-out; bubble click-through and pet-click dismissal; bubble drag tracking; right-click settings/hide; menu-bar show; relaunch-visible default; alpha hit testing; and selected Reduce Motion behavior.
- [ ] Record automated, manual, and blocked results separately. Record multi-monitor verification as manual-only if no second display exists.
- [ ] Commit recorded validation only after automated checks pass: `docs: record desktop pet interaction verification`.
