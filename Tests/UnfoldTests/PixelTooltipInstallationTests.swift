import AppKit
import SwiftUI
import XCTest
@testable import Unfold

/// `.pixelTooltip` draws the editor's own panel. `.help` makes SwiftUI draw
/// one of its own from the same string, so asking for both put two tooltips
/// on screen for a single hover — the system's below the pointer and the
/// editor's beside the control.
///
/// SwiftUI renders `.help` itself rather than setting `NSView.toolTip`: a
/// hosted `Button("x").help("y")` contains no view carrying a tooltip, only a
/// `HelpView` wrapper in the view type. That wrapper is therefore what these
/// tests look for.
@MainActor
final class PixelTooltipInstallationTests: XCTestCase {
    private func wrapsInHelpView<V: View>(_ view: V) -> Bool {
        String(reflecting: type(of: view)).contains("HelpView")
    }

    func test_theEditorsTooltipDoesNotAlsoAskSwiftUIForOne() {
        let view = Button("Rectangle") {}
            .pixelTooltip(PixelHelp.text("Rectangle", key: "U", "Draws a rectangle outline."))
        XCTAssertFalse(wrapsInHelpView(view),
            "SwiftUI would draw a second tooltip from the same string")
    }

    /// The comparison that gives the test above its teeth: `.help` really is
    /// what introduces the wrapper.
    func test_helpIsWhatIntroducesTheWrapper() {
        XCTAssertTrue(wrapsInHelpView(Button("Rectangle") {}.help("U · Rectangle")),
            "if this stops holding, .help changed and pixelTooltip could use it again")
    }

    /// Dropping `.help` must not drop what VoiceOver reads, so the text is
    /// still attached — as a hint, which draws nothing.
    func test_theTooltipTextIsStillOfferedToVoiceOver() {
        let text = PixelHelp.text("Rectangle", key: "U", "Draws a rectangle outline.")
        let view = Button("Rectangle") {}.pixelTooltip(text)
        XCTAssertTrue(String(reflecting: type(of: view)).contains("AccessibilityAttachmentModifier"),
            "the tooltip text should still reach assistive technology")
    }
}
