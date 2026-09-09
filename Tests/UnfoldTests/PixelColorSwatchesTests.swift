import AppKit
import XCTest
@testable import Unfold

/// The foreground/background pair draws its own swatches rather than taking a
/// stock `NSColorWell` style, so these render one through AppKit's real
/// display path and read the pixels back. A well that quietly drew itself the
/// system way instead would fail here rather than in front of the user.
@MainActor
final class PixelColorSwatchesTests: XCTestCase {
    private static let side = 32

    /// Renders into a context tagged sRGB. `bitmapImageRepForCachingDisplay`
    /// hands back the display's own space, and reading P3 pixels as sRGB
    /// drifts every channel — a colour-management artefact of the test rather
    /// than anything the swatch did.
    private func render(_ color: NSColor) -> CGContext {
        let side = Self.side
        let context = CGContext(data: nil, width: side, height: side, bitsPerComponent: 8,
                                bytesPerRow: side * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        let well = PixelSwatchWell(frame: NSRect(x: 0, y: 0, width: CGFloat(side), height: CGFloat(side)))
        well.color = color
        let graphics = NSGraphicsContext(cgContext: context, flipped: false)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = graphics
        well.displayIgnoringOpacity(well.bounds, in: graphics)
        NSGraphicsContext.restoreGraphicsState()
        return context
    }

    private func pixel(_ context: CGContext, x: Int, y: Int) -> (r: Int, g: Int, b: Int, a: Int) {
        let bytes = context.data!.assumingMemoryBound(to: UInt8.self)
        let offset = y * context.bytesPerRow + x * 4
        return (Int(bytes[offset]), Int(bytes[offset + 1]), Int(bytes[offset + 2]), Int(bytes[offset + 3]))
    }

    private func center(_ context: CGContext) -> (r: Int, g: Int, b: Int, a: Int) {
        pixel(context, x: Self.side / 2, y: Self.side / 2)
    }

    func test_theWellPaintsTheColorItWasGiven() {
        let drawn = center(render(NSColor(srgbRed: 1, green: 0, blue: 0, alpha: 1)))
        XCTAssertEqual(drawn.r, 255)
        XCTAssertEqual(drawn.g, 0)
        XCTAssertEqual(drawn.b, 0)
        XCTAssertEqual(drawn.a, 255)
    }

    func test_aFullyTransparentColorShowsTheCheckerboardBehindIt() {
        let drawn = center(render(NSColor(srgbRed: 0, green: 0, blue: 0, alpha: 0)))
        XCTAssertEqual(drawn.a, 255, "the checkerboard should be opaque, not a hole through the swatch")
        XCTAssertGreaterThan(drawn.r, 128,
            "a transparent color must leave the light checkerboard visible, not paint black")
    }

    /// Half-alpha over white and over grey lands on two different values, so a
    /// single sample cannot tell them apart; scanning two checkerboard squares
    /// can. A well that ignored alpha would paint every pixel identically.
    func test_aPartlyTransparentColorBlendsOverTheCheckerboardRatherThanHidingIt() {
        let context = render(NSColor(srgbRed: 0, green: 0, blue: 1, alpha: 0.5))
        let row = Self.side / 2
        let span = 2 * Int(PixelSwatchWell.checkSide)
        var reds = Set<Int>()
        for step in 0..<span where Self.side / 4 + step < Self.side {
            reds.insert(pixel(context, x: Self.side / 4 + step, y: row).r)
        }
        XCTAssertGreaterThan(reds.count, 1,
            "the checkerboard should still vary under a half-transparent color")
    }
}
