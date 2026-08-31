import XCTest
@testable import Unfold

/// `DesktopPetGeometry` is pure `CGRect`/`CGPoint` math — no `NSScreen`/
/// `NSWindow` anywhere in it — so multi-monitor layouts (including
/// negative-origin secondary displays) are directly testable with
/// synthetic frames, without real display hardware.
final class DesktopPetGeometryTests: XCTestCase {

    // MARK: - bottomRightOrigin (Phase 1, relocated here unchanged)

    func test_bottomRightOrigin_appliesMarginFromRightAndBottomEdges() {
        let origin = DesktopPetGeometry.bottomRightOrigin(
            visibleFrame: CGRect(x: 0, y: 0, width: 1440, height: 900),
            windowSize: CGSize(width: 192, height: 192),
            margin: 24
        )
        XCTAssertEqual(origin.x, 1440 - 192 - 24)
        XCTAssertEqual(origin.y, 0 + 24)
    }

    func test_bottomRightOrigin_respectsVisibleFrameOrigin_notJustSize() {
        let origin = DesktopPetGeometry.bottomRightOrigin(
            visibleFrame: CGRect(x: 100, y: 25, width: 1440, height: 850),
            windowSize: CGSize(width: 192, height: 192),
            margin: 24
        )
        XCTAssertEqual(origin.x, 100 + 1440 - 192 - 24)
        XCTAssertEqual(origin.y, 25 + 24)
    }

    // MARK: - draggedOrigin (grab offset preservation, §11/§6)

    func test_draggedOrigin_translatesWindowByExactlyTheCursorDelta() {
        let origin = DesktopPetGeometry.draggedOrigin(
            startWindowOrigin: CGPoint(x: 500, y: 300),
            startGlobalLocation: CGPoint(x: 700, y: 400),
            currentGlobalLocation: CGPoint(x: 750, y: 380)
        )
        XCTAssertEqual(origin, CGPoint(x: 550, y: 280))
    }

    func test_draggedOrigin_doesNotSnapToCursor_regardlessOfWhereWithinTheWindowWasGrabbed() {
        // Two different "grab points" within the same window (near its left
        // edge vs. near its right edge) that both then move by the same
        // cursor delta must produce the same window-origin delta — proving
        // the grabbed point stays fixed under the cursor instead of the
        // window recentering on it.
        let cursorDelta = CGPoint(x: 30, y: -15)

        let leftEdgeGrab = DesktopPetGeometry.draggedOrigin(
            startWindowOrigin: CGPoint(x: 0, y: 0),
            startGlobalLocation: CGPoint(x: 10, y: 10), // grabbed near the window's left edge
            currentGlobalLocation: CGPoint(x: 10 + cursorDelta.x, y: 10 + cursorDelta.y)
        )
        let rightEdgeGrab = DesktopPetGeometry.draggedOrigin(
            startWindowOrigin: CGPoint(x: 0, y: 0),
            startGlobalLocation: CGPoint(x: 180, y: 10), // grabbed near the window's right edge
            currentGlobalLocation: CGPoint(x: 180 + cursorDelta.x, y: 10 + cursorDelta.y)
        )

        XCTAssertEqual(leftEdgeGrab, rightEdgeGrab, "the window-origin delta must depend only on cursor movement, not on where within the window it was grabbed")
        XCTAssertEqual(leftEdgeGrab, CGPoint(x: cursorDelta.x, y: cursorDelta.y))
    }

    func test_draggedOrigin_zeroMovement_returnsStartOrigin() {
        let origin = DesktopPetGeometry.draggedOrigin(
            startWindowOrigin: CGPoint(x: 42, y: 17),
            startGlobalLocation: CGPoint(x: 100, y: 100),
            currentGlobalLocation: CGPoint(x: 100, y: 100)
        )
        XCTAssertEqual(origin, CGPoint(x: 42, y: 17))
    }

    // MARK: - clamp (§10/§12)

    func test_clamp_leavesFrameUnchanged_whenAlreadyFullyInside() {
        let frame = CGRect(x: 100, y: 100, width: 192, height: 192)
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        XCTAssertEqual(DesktopPetGeometry.clamp(frame, to: visibleFrame), frame)
    }

    func test_clamp_pullsFrameBackFromTheRightAndTopEdges() {
        let frame = CGRect(x: 1400, y: 850, width: 192, height: 192) // hangs off right+top
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let clamped = DesktopPetGeometry.clamp(frame, to: visibleFrame)

        XCTAssertEqual(clamped.origin, CGPoint(x: 1440 - 192, y: 900 - 192))
        XCTAssertTrue(visibleFrame.contains(clamped))
    }

    func test_clamp_pullsFrameBackFromTheLeftAndBottomEdges() {
        let frame = CGRect(x: -50, y: -50, width: 192, height: 192)
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let clamped = DesktopPetGeometry.clamp(frame, to: visibleFrame)

        XCTAssertEqual(clamped.origin, CGPoint(x: 0, y: 0))
    }

    func test_clamp_withNegativeOriginVisibleFrame_asOnASecondaryDisplayToTheLeft() {
        // A secondary display positioned to the left of/above the main one
        // in System Settings' arrangement has negative screen coordinates.
        let frame = CGRect(x: -3000, y: 50, width: 192, height: 192)
        let visibleFrame = CGRect(x: -2560, y: 0, width: 2560, height: 1440)
        let clamped = DesktopPetGeometry.clamp(frame, to: visibleFrame)

        XCTAssertTrue(visibleFrame.contains(clamped))
        XCTAssertEqual(clamped.origin.x, -2560)
    }

    func test_clamp_whenFrameLargerThanVisibleFrame_alignsToOrigin_withoutInvertedRange() {
        let frame = CGRect(x: 500, y: 500, width: 2000, height: 2000)
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let clamped = DesktopPetGeometry.clamp(frame, to: visibleFrame)

        XCTAssertEqual(clamped.origin, CGPoint(x: 0, y: 0))
    }

    // MARK: - bestScreenIndex (§11/§13)

    func test_bestScreenIndex_picksTheScreenWithMostOverlap() {
        let windowFrame = CGRect(x: 1400, y: 100, width: 192, height: 192) // mostly on screen 1
        let screens = [
            CGRect(x: 0, y: 0, width: 1440, height: 900),
            CGRect(x: 1440, y: 0, width: 1920, height: 1080),
        ]
        XCTAssertEqual(DesktopPetGeometry.bestScreenIndex(forWindowFrame: windowFrame, among: screens), 1)
    }

    func test_bestScreenIndex_withNegativeOriginSecondaryDisplay() {
        let windowFrame = CGRect(x: -2400, y: 100, width: 192, height: 192)
        let screens = [
            CGRect(x: 0, y: 0, width: 1440, height: 900), // primary
            CGRect(x: -2560, y: 0, width: 2560, height: 1440), // secondary, to the left
        ]
        XCTAssertEqual(DesktopPetGeometry.bestScreenIndex(forWindowFrame: windowFrame, among: screens), 1)
    }

    func test_bestScreenIndex_fallsBackToNearestScreen_whenWindowOverlapsNothing() {
        // Far to the right of both screens — screen 1 (further right) is
        // unambiguously closer than screen 0.
        let windowFrame = CGRect(x: 999_999, y: 0, width: 192, height: 192)
        let screens = [
            CGRect(x: 0, y: 0, width: 1440, height: 900),
            CGRect(x: 1440, y: 0, width: 1920, height: 1080),
        ]
        XCTAssertEqual(DesktopPetGeometry.bestScreenIndex(forWindowFrame: windowFrame, among: screens), 1)
    }

    func test_bestScreenIndex_fallsBackToNearestScreen_theOtherDirection() {
        let windowFrame = CGRect(x: -999_999, y: 0, width: 192, height: 192)
        let screens = [
            CGRect(x: 0, y: 0, width: 1440, height: 900),
            CGRect(x: 1440, y: 0, width: 1920, height: 1080),
        ]
        XCTAssertEqual(DesktopPetGeometry.bestScreenIndex(forWindowFrame: windowFrame, among: screens), 0)
    }

    func test_bestScreenIndex_returnsNil_whenNoScreensAtAll() {
        XCTAssertNil(DesktopPetGeometry.bestScreenIndex(forWindowFrame: .zero, among: []))
    }

    // MARK: - Normalization round-trip (§13/§14)

    func test_normalizedPosition_bottomRight_isCloseToOneOne() {
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let windowSize = CGSize(width: 192, height: 192)
        let origin = DesktopPetGeometry.bottomRightOrigin(visibleFrame: visibleFrame, windowSize: windowSize, margin: 0)

        let normalized = DesktopPetGeometry.normalizedPosition(forOrigin: origin, windowSize: windowSize, visibleFrame: visibleFrame)

        XCTAssertEqual(normalized.x, 1, accuracy: 0.0001)
        XCTAssertEqual(normalized.y, 0, accuracy: 0.0001)
    }

    func test_normalizedPosition_andOrigin_roundTrip() {
        let visibleFrame = CGRect(x: -2560, y: 0, width: 2560, height: 1440)
        let windowSize = CGSize(width: 192, height: 192)
        let originalOrigin = CGPoint(x: -1200, y: 600)

        let normalized = DesktopPetGeometry.normalizedPosition(forOrigin: originalOrigin, windowSize: windowSize, visibleFrame: visibleFrame)
        let restored = DesktopPetGeometry.origin(fromNormalized: normalized, windowSize: windowSize, visibleFrame: visibleFrame)

        XCTAssertEqual(restored.x, originalOrigin.x, accuracy: 0.01)
        XCTAssertEqual(restored.y, originalOrigin.y, accuracy: 0.01)
    }

    func test_normalizedPosition_clampsToZeroToOne_evenForAnOutOfBoundsOrigin() {
        let visibleFrame = CGRect(x: 0, y: 0, width: 1440, height: 900)
        let windowSize = CGSize(width: 192, height: 192)

        let normalized = DesktopPetGeometry.normalizedPosition(forOrigin: CGPoint(x: -500, y: 5000), windowSize: windowSize, visibleFrame: visibleFrame)

        XCTAssertEqual(normalized.x, 0)
        XCTAssertEqual(normalized.y, 1)
    }

    func test_normalizedPosition_whenWindowLargerThanVisibleFrame_doesNotDivideByNegative() {
        let visibleFrame = CGRect(x: 0, y: 0, width: 100, height: 100)
        let windowSize = CGSize(width: 192, height: 192)

        let normalized = DesktopPetGeometry.normalizedPosition(forOrigin: CGPoint(x: 10, y: 10), windowSize: windowSize, visibleFrame: visibleFrame)

        XCTAssertEqual(normalized.x, 0)
        XCTAssertEqual(normalized.y, 0)
    }
}
