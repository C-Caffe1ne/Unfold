import AppKit
import XCTest
@testable import Unfold

/// The tooltip used to be placed from `NSEvent.mouseLocation` and clamped
/// against whichever screen the pointer was judged to be on — so it could land
/// nowhere near the control it described, and on a second display it could be
/// pulled onto the main one. It is anchored to the control now, and this is
/// the arithmetic that does it.
final class PixelTooltipPlacementTests: XCTestCase {
    /// One 1440×900 screen with its origin at zero, menu bar excluded.
    private let screen = NSRect(x: 0, y: 0, width: 1440, height: 875)
    private let size = NSSize(width: 200, height: 60)

    private func place(_ control: NSRect, screens: [NSRect]? = nil) -> NSRect {
        PixelTooltipPanel.frame(size: size, near: control, screens: screens ?? [screen])
    }

    func test_theTooltipSitsJustBelowTheControlAndAlignedToItsLeftEdge() {
        let control = NSRect(x: 300, y: 500, width: 38, height: 32)
        let frame = place(control)
        XCTAssertEqual(frame.maxY, control.minY - PixelTooltipPanel.gap)
        XCTAssertEqual(frame.minX, control.minX)
        XCTAssertEqual(frame.size, size)
    }

    func test_itFlipsAboveTheControlWhenThereIsNoRoomBelow() {
        let control = NSRect(x: 300, y: 20, width: 38, height: 32)
        let frame = place(control)
        XCTAssertEqual(frame.minY, control.maxY + PixelTooltipPanel.gap,
            "with the control near the bottom edge the tooltip belongs above it")
    }

    func test_itStaysOnScreenHorizontally() {
        let atRightEdge = place(NSRect(x: 1400, y: 500, width: 38, height: 32))
        XCTAssertLessThanOrEqual(atRightEdge.maxX, screen.maxX)
        XCTAssertGreaterThanOrEqual(atRightEdge.minX, screen.minX)

        let atLeftEdge = place(NSRect(x: -10, y: 500, width: 38, height: 32))
        XCTAssertGreaterThanOrEqual(atLeftEdge.minX, screen.minX)
    }

    /// The editor is regularly opened on a second display. The tooltip has to
    /// be clamped against the screen the control is on, never the main one.
    func test_aControlOnASecondDisplayKeepsItsTooltipOnThatDisplay() {
        let second = NSRect(x: 1440, y: 0, width: 1920, height: 1080)
        let control = NSRect(x: 2200, y: 600, width: 38, height: 32)
        let frame = place(control, screens: [screen, second])
        XCTAssertTrue(second.contains(NSPoint(x: frame.midX, y: frame.midY)),
            "the tooltip should stay on the display holding the control")
        XCTAssertEqual(frame.minX, control.minX)
    }

    /// A control dragged past every known screen still has to produce a frame
    /// rather than nothing at all.
    func test_aControlOnNoKnownScreenIsStillPlacedBesideItself() {
        let control = NSRect(x: 9000, y: 9000, width: 38, height: 32)
        let frame = place(control, screens: [screen])
        XCTAssertEqual(frame.minX, control.minX)
        XCTAssertEqual(frame.maxY, control.minY - PixelTooltipPanel.gap)
    }
}
