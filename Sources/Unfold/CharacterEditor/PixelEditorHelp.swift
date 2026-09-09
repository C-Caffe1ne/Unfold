import Foundation

/// Tooltip text for the editor's controls, kept in one place so a control and
/// the key that reaches it cannot drift apart.
///
/// Every tooltip opens with the shortcut and the control's name, then says on
/// the next line what the control does. A control with no key opens with its
/// name alone rather than an empty gap.
enum PixelHelp {
    static func text(_ title: String, key: String? = nil, _ detail: String) -> String {
        let heading = key.map { "\($0) · \(title)" } ?? title
        return heading + "\n" + detail
    }

    /// Splits a tooltip back into its two parts so the panel can weight the
    /// heading. Only the first break separates them; a detail that runs to two
    /// lines stays whole.
    static func split(_ text: String) -> (heading: String, detail: String) {
        guard let br = text.firstIndex(of: "\n") else { return (text, "") }
        return (String(text[text.startIndex..<br]), String(text[text.index(after: br)...]))
    }
}

extension PixelTool {
    /// One sentence on what the tool does, and where it is not obvious, on what
    /// it refuses to do.
    var detail: String {
        switch self {
        case .pencil:
            return "Paints freehand with the drawing color."
        case .eraser:
            return "Clears pixels back to transparent."
        case .fill:
            return "Floods the touching pixels that match the one clicked. A selection edge stops it."
        case .eyedropper:
            return "Takes the color under the pointer from the composited frame."
        case .line:
            return "Draws a straight line from where the drag starts to where it ends."
        case .rectangle:
            return "Draws a rectangle outline between the dragged corners."
        case .ellipse:
            return "Draws an ellipse outline inside the dragged rectangle."
        case .rectangleSelection:
            return "Selects a rectangle. Painting is then confined to it."
        case .lassoSelection:
            return "Selects the area inside a freehand outline, boundary included."
        case .magicWand:
            return "Selects the touching pixels that exactly match the one clicked, in this layer's cel alone."
        case .move:
            return "Drags the selected pixels, leaving transparency behind them."
        case .hand:
            return "Drags the canvas. Holding Space does the same without changing tool."
        case .brightness:
            return "Lightens the pixels under the brush; Darken reverses it. Transparent pixels stay untouched."
        case .spray:
            return "Scatters stamps in a circle the width of the brush along the drag."
        case .gradient:
            return "Ramps once from the foreground color to the background color across the drag."
        }
    }

    var help: String { PixelHelp.text(title, key: shortcutLabel, detail) }
}
