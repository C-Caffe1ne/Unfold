import AppKit
import SwiftUI
import XCTest

@testable import Unfold

@MainActor
final class PixelEditorHelpTests: XCTestCase {
    func test_aTooltipLeadsWithTheShortcutAndTheName() {
        let help = PixelHelp.text("Undo", key: "⌘Z", "Steps back one change.")
        XCTAssertEqual(help, "⌘Z · Undo\nSteps back one change.")
    }

    func test_aControlWithoutAShortcutLeadsWithItsNameAlone() {
        let help = PixelHelp.text("Canvas Size", "Resizes every frame.")
        XCTAssertEqual(help, "Canvas Size\nResizes every frame.")
    }

    /// A tool added without a tooltip would otherwise reach the tool rail with
    /// nothing but its own name in the hover.
    func test_everyToolCarriesAShortcutLineAndADescription() {
        for tool in PixelTool.allCases {
            let lines = tool.help.split(separator: "\n", omittingEmptySubsequences: false)
            XCTAssertEqual(lines.count, 2, "\(tool.rawValue) tooltip is not two lines")
            XCTAssertTrue(lines[0].hasSuffix(tool.title), "\(tool.rawValue) heading omits its name")
            XCTAssertGreaterThan(lines[1].count, 20, "\(tool.rawValue) description is too thin")
            XCTAssertTrue(lines[1].hasSuffix("."), "\(tool.rawValue) description is not a sentence")
            if let key = tool.shortcutLabel {
                XCTAssertTrue(lines[0].hasPrefix(key + " · "), "\(tool.rawValue) heading omits \(key)")
            } else {
                XCTAssertEqual(String(lines[0]), tool.title)
            }
        }
    }

    /// The tooltips name the keys the canvas actually listens for. A tooltip
    /// promising a key nothing handles is worse than no tooltip.
    func test_everyToolShortcutInATooltipReachesThatTool() {
        for tool in PixelTool.allCases {
            guard let label = tool.shortcutLabel else { continue }
            let shift = label.hasPrefix("⇧")
            let key = String(label.drop(while: { $0 == "⇧" }))
            XCTAssertEqual(PixelTool.shortcut(key, shift: shift), tool,
                "\(label) does not select \(tool.rawValue)")
        }
    }

    func test_theTooltipPanelSplitsTheHeadingFromTheDescription() {
        let parts = PixelHelp.split(PixelHelp.text("Undo", key: "⌘Z", "Steps back one change."))
        XCTAssertEqual(parts.heading, "⌘Z · Undo")
        XCTAssertEqual(parts.detail, "Steps back one change.")
    }

    /// A description that wrapped onto a second line would otherwise lose
    /// everything after the first break.
    func test_onlyTheFirstBreakSeparatesTheHeading() {
        let parts = PixelHelp.split("Heading\nFirst sentence.\nSecond sentence.")
        XCTAssertEqual(parts.heading, "Heading")
        XCTAssertEqual(parts.detail, "First sentence.\nSecond sentence.")
    }

    func test_textWithoutABreakIsAllHeading() {
        let parts = PixelHelp.split("Heading only")
        XCTAssertEqual(parts.heading, "Heading only")
        XCTAssertEqual(parts.detail, "")
    }

    func test_theTooltipWaitsHalfASecond() {
        XCTAssertEqual(PixelTooltipPanel.delay, 0.5, accuracy: 0.0001)
    }

    /// `.help` alone showed nothing over the tool rail, so the tooltips now
    /// ride on a tracking view of the editor's own. If that overlay ever stops
    /// reaching AppKit, every tooltip in the editor goes quiet again.
    func test_theEditorLayoutCarriesTooltipTrackersThatLetClicksThrough() throws {
        let model = PixelEditorModel(document: PixelDocument(width: 16, height: 16))
        let host = NSHostingView(
            rootView: PixelEditorView(
                model: model, saveToLibrary: {}, openDocument: {}, save: {}, saveAs: {}))
        host.frame = NSRect(x: 0, y: 0, width: 1120, height: 780)
        let window = NSWindow(
            contentRect: host.frame, styleMask: [.titled], backing: .buffered, defer: true)
        window.contentView = host
        host.layoutSubtreeIfNeeded()
        host.displayIfNeeded()

        func trackers(_ view: NSView) -> [PixelTooltipTrackingView] {
            let here = (view as? PixelTooltipTrackingView).map { [$0] } ?? []
            return here + view.subviews.flatMap(trackers)
        }
        let found = trackers(host)
        XCTAssertGreaterThanOrEqual(found.count, PixelTool.allCases.count,
            "the tool rail alone should contribute one tracker per tool")
        for tracker in found {
            tracker.updateTrackingAreas()
            XCTAssertEqual(tracker.trackingAreas.count, 1, "a tracker without a tracking area is deaf")
            XCTAssertNil(tracker.hitTest(NSPoint(x: 1, y: 1)),
                "a tracker that swallows clicks would make its control dead")
        }
    }
}
