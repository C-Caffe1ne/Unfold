import XCTest
@testable import Unfold

/// Covers what's deterministic and cheap to assert directly: the bottom-
/// right position math (pure, no window/screen involved) and the panel's
/// configured properties right after `init` (real `NSPanel`/`NSWindow`
/// instances, but never actually ordered on screen — reading properties
/// off an unshown window is not brittle the way asserting real screen
/// placement or Space behavior would be). Those latter, genuinely
/// screen-dependent behaviors (actual on-screen position, Space/full-screen
/// behavior, click-through) are verified in the real Debug app instead, per
/// the Phase spec.
@MainActor
final class DesktopPetWindowControllerTests: XCTestCase {

    // MARK: - Bottom-right position math (pure)

    func test_bottomRightOrigin_appliesMarginFromRightAndBottomEdges() {
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let windowSize = CGSize(width: 192, height: 192)

        let origin = DesktopPetWindowController.bottomRightOrigin(
            visibleFrame: visibleFrame,
            windowSize: windowSize,
            margin: 24
        )

        XCTAssertEqual(origin.x, 1440 - 192 - 24)
        XCTAssertEqual(origin.y, 0 + 24)
    }

    func test_bottomRightOrigin_respectsVisibleFrameOrigin_notJustSize() {
        // A visibleFrame that doesn't start at (0,0) — e.g. a secondary
        // display, or a main screen with the Dock along the left edge
        // shifting minX. The result must be relative to visibleFrame's own
        // origin, not assume it's zero.
        let visibleFrame = CGRect(x: 100, y: 25, width: 1440, height: 850)
        let windowSize = CGSize(width: 192, height: 192)

        let origin = DesktopPetWindowController.bottomRightOrigin(
            visibleFrame: visibleFrame,
            windowSize: windowSize,
            margin: 24
        )

        XCTAssertEqual(origin.x, 100 + 1440 - 192 - 24)
        XCTAssertEqual(origin.y, 25 + 24)
    }

    func test_bottomRightOrigin_resultingFrameStaysWithinVisibleFrame() {
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let windowSize = CGSize(width: 192, height: 192)

        let origin = DesktopPetWindowController.bottomRightOrigin(
            visibleFrame: visibleFrame,
            windowSize: windowSize,
            margin: 24
        )
        let resultingFrame = CGRect(origin: origin, size: windowSize)

        XCTAssertTrue(visibleFrame.contains(resultingFrame))
    }

    // MARK: - Panel configuration

    private func makeController() -> DesktopPetWindowController {
        let character = BuiltInCharacters.emergencyFallback
        return DesktopPetWindowController(character: character)
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

    func test_window_ignoresMouseEvents() {
        let window = makeController().window!
        XCTAssertTrue(window.ignoresMouseEvents)
    }

    func test_window_isNotMovableByBackgroundDrag() {
        let window = makeController().window!
        XCTAssertFalse(window.isMovableByWindowBackground)
    }

    func test_window_sizeMatchesCharacterDisplaySize() {
        let window = makeController().window!
        XCTAssertEqual(window.frame.size.width, Constants.characterDisplaySize)
        XCTAssertEqual(window.frame.size.height, Constants.characterDisplaySize)
    }

    func test_window_doesNotActivateAppOrBecomeKeyOrMain() {
        // A Desktop Pet with no interaction yet must never steal focus from
        // whatever app the user is actually using.
        let window = makeController().window!
        XCTAssertFalse(window.canBecomeKey)
        XCTAssertFalse(window.canBecomeMain)
    }
}
