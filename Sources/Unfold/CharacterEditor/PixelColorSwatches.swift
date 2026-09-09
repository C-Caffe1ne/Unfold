import AppKit
import SwiftUI

/// The foreground and background colours as two overlapping squares with a
/// swap arrow tucked into the corner — the arrangement pixel editors have
/// used for decades, and the one place in the window either colour is set.
@MainActor
struct PixelColorSwatches: View {
    @ObservedObject var model: PixelEditorModel

    private static let square: CGFloat = 32
    /// Wide enough that the swap arrow gets a corner of its own: the squares
    /// still overlap, but neither one reaches into the arrow's 20pt box.
    private static let offset: CGFloat = 20
    private static let corner: CGFloat = 20

    var body: some View {
        ZStack(alignment: .topLeading) {
            PixelColorWell(packed: $model.backgroundColor)
                .frame(width: Self.square, height: Self.square)
                .offset(x: Self.offset, y: Self.offset)
                .pixelTooltip(PixelHelp.text("Background",
                    "The far end of the gradient ramp. No other tool uses it."))
            PixelColorWell(packed: $model.color)
                .frame(width: Self.square, height: Self.square)
                .pixelTooltip(PixelHelp.text("Foreground",
                    "The color every painting tool draws with, and the start of the gradient ramp."))
            Button(action: swap) {
                Image(systemName: "arrow.triangle.2.circlepath")
                    .font(.system(size: 14, weight: .semibold))
                    .frame(width: Self.corner, height: Self.corner)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .offset(y: Self.square)
            .pixelTooltip(PixelHelp.text("Swap Colors",
                "Exchanges the foreground and background colors."))
            .accessibilityLabel("Swap foreground and background colors")
        }
        .frame(width: Self.square + Self.offset, height: Self.square + Self.offset, alignment: .topLeading)
    }

    private func swap() {
        let foreground = model.color
        model.color = model.backgroundColor
        model.backgroundColor = foreground
    }
}

/// A colour well that draws itself: a checkerboard, the colour over it, and a
/// border that thickens while the well is the one the system colour panel is
/// editing. Clicking still opens that panel, which is all `NSColorWell` is
/// wanted for here.
private struct PixelColorWell: NSViewRepresentable {
    @Binding var packed: UInt32

    func makeNSView(context: Context) -> PixelSwatchWell {
        let well = PixelSwatchWell()
        if #available(macOS 14.0, *) { well.supportsAlpha = true }
        // The canvas reads keys directly, so a well that took focus would
        // swallow the tool shortcuts typed right after picking a colour.
        well.refusesFirstResponder = true
        well.target = context.coordinator
        well.action = #selector(Coordinator.wellChanged(_:))
        return well
    }

    func updateNSView(_ well: PixelSwatchWell, context: Context) {
        context.coordinator.packed = $packed
        // Writing the same colour back would fire the action again and race
        // the binding, so only a real difference is pushed down.
        guard Coordinator.pack(well.color) != packed else { return }
        well.color = Coordinator.unpack(packed)
    }

    func makeCoordinator() -> Coordinator { Coordinator(packed: $packed) }

    final class Coordinator: NSObject {
        var packed: Binding<UInt32>
        init(packed: Binding<UInt32>) { self.packed = packed }

        @objc func wellChanged(_ sender: NSColorWell) {
            let value = Self.pack(sender.color)
            if packed.wrappedValue != value { packed.wrappedValue = value }
        }

        static func pack(_ color: NSColor) -> UInt32 {
            guard let rgb = color.usingColorSpace(.sRGB) else { return 0 }
            func byte(_ value: CGFloat) -> UInt32 { UInt32((min(max(value, 0), 1) * 255).rounded()) }
            return byte(rgb.redComponent) << 24 | byte(rgb.greenComponent) << 16
                | byte(rgb.blueComponent) << 8 | byte(rgb.alphaComponent)
        }

        static func unpack(_ value: UInt32) -> NSColor {
            NSColor(srgbRed: CGFloat((value >> 24) & 255) / 255,
                    green: CGFloat((value >> 16) & 255) / 255,
                    blue: CGFloat((value >> 8) & 255) / 255,
                    alpha: CGFloat(value & 255) / 255)
        }
    }
}

/// Drawn rather than styled: none of the stock well styles show what is
/// behind a partly transparent colour, and the foreground swatch is regularly
/// exactly that.
final class PixelSwatchWell: NSColorWell {
    static let checkSide: CGFloat = 6

    override var isOpaque: Bool { false }

    /// A stock well is wider than it is tall and says so, which stretched the
    /// SwiftUI frame around it into a rectangle. Nothing here needs an
    /// intrinsic size: the layout above hands down an exact square.
    override var intrinsicContentSize: NSSize {
        NSSize(width: NSView.noIntrinsicMetric, height: NSView.noIntrinsicMetric)
    }

    /// Before macOS 14 a well has no `supportsAlpha` of its own; the shared
    /// panel carries the setting, so it is armed as the panel is summoned.
    override func mouseDown(with event: NSEvent) {
        NSColorPanel.shared.showsAlpha = true
        super.mouseDown(with: event)
    }

    override func draw(_ dirtyRect: NSRect) {
        let rect = bounds.insetBy(dx: 1, dy: 1)
        drawCheckerboard(in: rect)
        color.setFill()
        rect.fill(using: .sourceOver)
        (isActive ? NSColor.controlAccentColor : NSColor.separatorColor).setStroke()
        let border = NSBezierPath(rect: rect.insetBy(dx: 0.5, dy: 0.5))
        border.lineWidth = isActive ? 2 : 1
        border.stroke()
    }

    private func drawCheckerboard(in rect: NSRect) {
        NSColor.white.setFill()
        rect.fill()
        NSColor(white: 0.78, alpha: 1).setFill()
        var row = 0
        var y = rect.minY
        while y < rect.maxY {
            var column = 0
            var x = rect.minX
            while x < rect.maxX {
                if (row + column).isMultiple(of: 2) {
                    NSRect(x: x, y: y, width: Self.checkSide, height: Self.checkSide)
                        .intersection(rect).fill()
                }
                x += Self.checkSide
                column += 1
            }
            y += Self.checkSide
            row += 1
        }
    }
}
