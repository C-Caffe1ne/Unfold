import XCTest
@testable import Unfold

/// Covers what's deterministic and cheap to assert directly: panel
/// properties right after `init` (real `NSPanel`/`NSWindow` instances, but
/// never actually ordered on screen — reading properties off an unshown
/// window is not brittle the way asserting real screen placement or Space
/// behavior would be), and the mouse-event/drag/persistence wiring driven
/// through real `NSEvent`s with `currentGlobalMouseLocation` injected for
/// determinism. Pure placement math itself lives in
/// `DesktopPetGeometryTests`. Genuinely screen-dependent behaviors (actual
/// on-screen position, Space/full-screen behavior, click-through, focus
/// preservation) are verified in the real Debug app instead, per the Phase
/// spec.
@MainActor
final class DesktopPetWindowControllerTests: XCTestCase {

    private var cleanups: [() -> Void] = []

    override func tearDownWithError() throws {
        cleanups.forEach { $0() }
        cleanups = []
    }

    private func makeIsolatedPositionStore() -> DesktopPetPositionStore {
        let (defaults, cleanup) = IsolatedUserDefaults.make()
        cleanups.append(cleanup)
        return DesktopPetPositionStore(defaults: defaults)
    }

    // MARK: - Panel configuration

    private func makeController() -> DesktopPetWindowController {
        let character = BuiltInCharacters.emergencyFallback
        return DesktopPetWindowController(character: character, positionStore: makeIsolatedPositionStore())
    }

    func test_init_createsWindow() {
        let controller = makeController()
        XCTAssertNotNil(controller.window)
    }

    func test_window_isTransparentAndBorderless() {
        let window = makeController().window!
        XCTAssertFalse(window.isOpaque)
        XCTAssertEqual(window.backgroundColor, .clear)
        XCTAssertFalse(window.hasShadow)
        XCTAssertTrue(window.styleMask.contains(.borderless))
        XCTAssertFalse(window.styleMask.contains(.titled))
        XCTAssertFalse(window.styleMask.contains(.resizable))
        XCTAssertFalse(window.styleMask.contains(.closable))
        XCTAssertFalse(window.styleMask.contains(.miniaturizable))
    }

    func test_window_floatsAboveNormalWindows_butBelowSystemUI() {
        let window = makeController().window!
        XCTAssertEqual(window.level, .floating)
        XCTAssertLessThan(window.level, .statusBar)
        XCTAssertLessThan(window.level, .popUpMenu)
    }

    func test_window_joinsAllSpacesAndCoexistsWithFullScreenApps() {
        let window = makeController().window!
        XCTAssertTrue(window.collectionBehavior.contains(.canJoinAllSpaces))
        XCTAssertTrue(window.collectionBehavior.contains(.fullScreenAuxiliary))
    }

    func test_window_receivesMouseEvents() {
        // Phase 2: interaction requires real mouseDown/mouseUp delivery —
        // this intentionally flips the Phase 1 expectation (`true`).
        let window = makeController().window!
        XCTAssertFalse(window.ignoresMouseEvents)
    }

    func test_window_isNotMovableByBackgroundDrag() {
        // The pet moves by explicit mouseDragged handling (Phase 3), not
        // AppKit's own background-drag — that must stay off, or dragging
        // anywhere in the transparent canvas (not just the visible pixels)
        // would double-move the window.
        let window = makeController().window!
        XCTAssertFalse(window.isMovableByWindowBackground)
    }

    func test_window_sizeMatchesCharacterDisplaySize() {
        let window = makeController().window!
        XCTAssertEqual(window.frame.size.width, Constants.characterDisplaySize)
        XCTAssertEqual(window.frame.size.height, Constants.characterDisplaySize)
    }

    func test_window_doesNotActivateAppOrBecomeKeyOrMain() {
        // The pet must never steal focus from whatever app the user is
        // using — true across Phase 1/2/3 alike.
        let window = makeController().window!
        XCTAssertFalse(window.canBecomeKey)
        XCTAssertFalse(window.canBecomeMain)
    }

    // MARK: - Mouse event wiring

    /// Interaction tests need a character with a real idle animation —
    /// unlike `makeController()`'s `emergencyFallback` (used by the
    /// window-property tests above, which don't care about animation
    /// content), `DesktopPetAnimationController` returns `nil` for a
    /// character with no idle at all.
    private func makeInteractiveController() -> DesktopPetWindowController {
        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        return DesktopPetWindowController(character: character, positionStore: makeIsolatedPositionStore())
    }

    private func mouseEvent(_ type: NSEvent.EventType, at location: NSPoint, windowNumber: Int) -> NSEvent {
        NSEvent.mouseEvent(
            with: type,
            location: location,
            modifierFlags: [],
            timestamp: 0,
            windowNumber: windowNumber,
            context: nil,
            eventNumber: 0,
            clickCount: 1,
            pressure: type == .leftMouseUp ? 0 : 1
        )!
    }

    func test_mouseDown_isForwardedToTheAnimationController() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        XCTAssertEqual(controller.interactionState, .pointerDown)
    }

    func test_mouseUp_withoutPriorMouseDown_doesNotCrash() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel

        window.mouseUp(with: mouseEvent(.leftMouseUp, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber)) // must not crash

        XCTAssertEqual(controller.interactionState, .idle)
    }

    // MARK: - Drag: window movement

    func test_draggingPastThreshold_movesTheWindow_byExactlyTheCursorDelta() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel
        // Starts at the Phase 1 bottom-right default (only `desktopPetScreenMargin`
        // pt from the right/bottom edges) — drag up and to the *left*, into
        // the screen where there's room, so the assertion below isn't
        // confounded by `DesktopPetGeometry.clamp` pulling the window back.
        let startOrigin = window.frame.origin

        var mouseLocation = CGPoint(x: 500, y: 500)
        controller.currentGlobalMouseLocation = { mouseLocation }

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        mouseLocation = CGPoint(x: 460, y: 530) // (-40, +30), past the 5pt drag threshold
        window.mouseDragged(with: mouseEvent(.leftMouseDragged, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        XCTAssertEqual(window.frame.origin.x, startOrigin.x - 40, accuracy: 0.01)
        XCTAssertEqual(window.frame.origin.y, startOrigin.y + 30, accuracy: 0.01)
    }

    func test_draggingBelowThreshold_doesNotMoveTheWindow() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel
        let startOrigin = window.frame.origin

        var mouseLocation = CGPoint(x: 500, y: 500)
        controller.currentGlobalMouseLocation = { mouseLocation }

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        mouseLocation = CGPoint(x: 502, y: 500) // 2pt, under the 5pt threshold
        window.mouseDragged(with: mouseEvent(.leftMouseDragged, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        XCTAssertEqual(window.frame.origin, startOrigin)
    }

    func test_draggedWindow_staysWithinTheScreensVisibleFrame() throws {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel

        guard !NSScreen.screens.isEmpty else {
            throw XCTSkip("no screen available in this environment")
        }

        var mouseLocation = CGPoint(x: 500, y: 500)
        controller.currentGlobalMouseLocation = { mouseLocation }
        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        // Drag far past any real screen's edge.
        mouseLocation = CGPoint(x: 500 + 100_000, y: 500 + 100_000)
        window.mouseDragged(with: mouseEvent(.leftMouseDragged, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        // Must land within *some* real screen's visibleFrame — not
        // necessarily `NSScreen.main` specifically (with 2+ screens, the
        // nearest-screen fallback in `DesktopPetGeometry.bestScreenIndex`
        // may reasonably pick a different one).
        let landedOnARealScreen = NSScreen.screens.contains { $0.visibleFrame.contains(window.frame) }
        XCTAssertTrue(landedOnARealScreen, "an extreme drag must still clamp into a real visibleFrame, never leave the pet stranded off-screen")
    }

    // MARK: - Drag: click/long-press regression (through the real event path)

    func test_quickClickWithoutDragging_stillProducesClick() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel
        controller.currentGlobalMouseLocation = { CGPoint(x: 500, y: 500) } // never moves

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        window.mouseUp(with: mouseEvent(.leftMouseUp, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        XCTAssertEqual(controller.interactionState, .click)
    }

    // MARK: - Position persistence

    func test_mouseUp_afterADrag_savesAPosition() {
        let store = makeIsolatedPositionStore()
        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        let controller = DesktopPetWindowController(character: character, positionStore: store)
        let window = controller.window as! NSPanel

        var mouseLocation = CGPoint(x: 500, y: 500)
        controller.currentGlobalMouseLocation = { mouseLocation }
        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        mouseLocation = CGPoint(x: 560, y: 500) // past threshold
        window.mouseDragged(with: mouseEvent(.leftMouseDragged, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        window.mouseUp(with: mouseEvent(.leftMouseUp, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))

        XCTAssertNotNil(store.load(), "a drag that actually happened must persist a position on mouseUp")
    }

    func test_mouseUp_withoutADrag_doesNotSaveAPosition() {
        let store = makeIsolatedPositionStore()
        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        let controller = DesktopPetWindowController(character: character, positionStore: store)
        let window = controller.window as! NSPanel
        controller.currentGlobalMouseLocation = { CGPoint(x: 500, y: 500) }

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        window.mouseUp(with: mouseEvent(.leftMouseUp, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber)) // plain click, no drag

        XCTAssertNil(store.load(), "a plain click must not write a position — nothing moved")
    }

    // MARK: - Startup restoration

    func test_init_restoresFromASavedPosition_onTheMatchingScreen() throws {
        guard let screen = NSScreen.main, let displayID = screen.displayID else {
            throw XCTSkip("no screen available in this environment")
        }
        let store = makeIsolatedPositionStore()
        store.save(DesktopPetStoredPosition(screenIdentifier: displayID, normalizedX: 0, normalizedY: 0))

        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        let controller = DesktopPetWindowController(character: character, positionStore: store)
        let window = controller.window!

        let expectedOrigin = DesktopPetGeometry.origin(
            fromNormalized: CGPoint(x: 0, y: 0),
            windowSize: window.frame.size,
            visibleFrame: screen.visibleFrame
        )
        XCTAssertEqual(window.frame.origin.x, expectedOrigin.x, accuracy: 0.01)
        XCTAssertEqual(window.frame.origin.y, expectedOrigin.y, accuracy: 0.01)
    }

    func test_init_fallsBackToBottomRight_whenNoPositionSaved() {
        let store = makeIsolatedPositionStore() // fresh, nothing saved
        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        let controller = DesktopPetWindowController(character: character, positionStore: store)
        let window = controller.window!

        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        let expected = DesktopPetGeometry.bottomRightOrigin(
            visibleFrame: screen.visibleFrame,
            windowSize: window.frame.size,
            margin: Constants.desktopPetScreenMargin
        )
        XCTAssertEqual(window.frame.origin.x, expected.x, accuracy: 0.01)
        XCTAssertEqual(window.frame.origin.y, expected.y, accuracy: 0.01)
    }

    func test_init_fallsBackToBottomRight_whenSavedScreenNoLongerExists() {
        let store = makeIsolatedPositionStore()
        // A display identifier that (barring wild coincidence) doesn't
        // match any screen actually connected to this machine.
        store.save(DesktopPetStoredPosition(screenIdentifier: 999_999_999, normalizedX: 0.5, normalizedY: 0.5))

        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        let controller = DesktopPetWindowController(character: character, positionStore: store)
        let window = controller.window!

        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }
        let expected = DesktopPetGeometry.bottomRightOrigin(
            visibleFrame: screen.visibleFrame,
            windowSize: window.frame.size,
            margin: Constants.desktopPetScreenMargin
        )
        XCTAssertEqual(window.frame.origin.x, expected.x, accuracy: 0.01)
        XCTAssertEqual(window.frame.origin.y, expected.y, accuracy: 0.01)
    }

    func test_init_doesNotCrash_withCorruptSavedValues() {
        let (defaults, cleanup) = IsolatedUserDefaults.make()
        cleanups.append(cleanup)
        defaults.set("garbage", forKey: "desktopPetScreenIdentifier")
        defaults.set(5.0, forKey: "desktopPetNormalizedX") // out of 0...1
        defaults.set(0.5, forKey: "desktopPetNormalizedY")
        let store = DesktopPetPositionStore(defaults: defaults)

        let character = CharacterPackageLoader.loadBuiltIn(id: "default-cat")!
        _ = DesktopPetWindowController(character: character, positionStore: store) // must not crash
    }

    // MARK: - Alpha hit-testing / dynamic click-through (Phase 4)

    /// `refreshMouseEventAcceptance()` is `DesktopPetHitTestPolicy`'s real
    /// call site — normally driven by a `Timer` polling at ~30Hz (see the
    /// controller's doc comment on why a poll is needed at all: once
    /// `ignoresMouseEvents = true`, the window itself stops receiving
    /// `mouseMoved`). Exposed non-`private` specifically so these tests can
    /// drive one tick deterministically without spinning the real run loop
    /// or depending on the real system cursor position — same reasoning
    /// `currentGlobalMouseLocation` is injectable for.
    func test_refreshMouseEventAcceptance_whenCursorFarOutsideWindow_ignoresMouseEvents() {
        let controller = makeInteractiveController()
        let window = controller.window!
        controller.currentGlobalMouseLocation = { CGPoint(x: 100_000, y: 100_000) }

        controller.refreshMouseEventAcceptance()

        XCTAssertTrue(window.ignoresMouseEvents)
    }

    func test_refreshMouseEventAcceptance_duringPointerDown_alwaysAccepts_evenWithCursorOutsideWindow() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        XCTAssertEqual(controller.interactionState, .pointerDown)

        // A fast drag can carry the cursor beyond the window's own frame
        // between polls — ownership must not drop mid-hold (§12).
        controller.currentGlobalMouseLocation = { CGPoint(x: 100_000, y: 100_000) }
        controller.refreshMouseEventAcceptance()

        XCTAssertFalse(window.ignoresMouseEvents)
    }

    func test_refreshMouseEventAcceptance_afterMouseUp_returnsToAlphaBasedPolicy() {
        let controller = makeInteractiveController()
        let window = controller.window as! NSPanel

        window.mouseDown(with: mouseEvent(.leftMouseDown, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        window.mouseUp(with: mouseEvent(.leftMouseUp, at: NSPoint(x: 96, y: 96), windowNumber: window.windowNumber))
        XCTAssertEqual(controller.interactionState, .click)

        controller.currentGlobalMouseLocation = { CGPoint(x: 100_000, y: 100_000) }
        controller.refreshMouseEventAcceptance()

        XCTAssertTrue(window.ignoresMouseEvents, "once the mouse button is released, hit-testing must resume immediately — it must not wait for the reaction animation to finish")
    }

    func test_refreshMouseEventAcceptance_withoutAnimationController_alwaysAccepts() {
        // The `emergencyFallback`/SF-Symbol path: no decoded frame exists to
        // alpha-test against, so the whole canvas stays the hit area,
        // exactly like Phase 1-3.
        let controller = makeController()
        let window = controller.window!
        controller.currentGlobalMouseLocation = { window.frame.origin } // inside the window

        controller.refreshMouseEventAcceptance()

        XCTAssertFalse(window.ignoresMouseEvents)
    }
}
