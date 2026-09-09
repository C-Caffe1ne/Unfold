import Foundation

enum PixelTool: String, CaseIterable, Identifiable {
    case pencil, eraser, fill, eyedropper, line, rectangle, ellipse
    case rectangleSelection, lassoSelection, magicWand, move, hand, brightness, spray, gradient
    var id: String { rawValue }
    var title: String {
        switch self {
        case .rectangleSelection: return "Rectangle Selection"
        case .lassoSelection: return "Lasso Selection"
        case .magicWand: return "Magic Wand"
        default: return rawValue.capitalized
        }
    }
    var symbol: String {
        switch self {
        case .pencil: return "pencil.tip"
        case .eraser: return "eraser"
        case .fill: return "drop.fill"
        case .eyedropper: return "eyedropper"
        case .line: return "line.diagonal"
        case .rectangle: return "rectangle"
        case .ellipse: return "circle"
        case .rectangleSelection: return "rectangle.dashed"
        case .lassoSelection: return "lasso"
        case .magicWand: return "wand.and.stars"
        case .move: return "arrow.up.and.down.and.arrow.left.and.right"
        case .hand: return "hand.draw"
        case .brightness: return "sun.max"
        case .spray: return "aqi.medium"
        case .gradient: return "rectangle.lefthalf.filled"
        }
    }
    var shortcutLabel: String? {
        switch self {
        case .pencil: return "B"
        case .eraser: return "E"
        case .fill: return "G"
        case .eyedropper: return "I"
        case .line: return "L"
        case .rectangle: return "U"
        case .ellipse: return "⇧U"
        case .rectangleSelection: return "M"
        case .lassoSelection: return "Q"
        case .magicWand: return "W"
        case .move: return "V"
        case .hand: return "H"
        case .brightness: return nil
        case .spray: return "⇧B"
        case .gradient: return "⇧G"
        }
    }
    static func shortcut(_ key: String, shift: Bool = false) -> PixelTool? {
        switch key.lowercased() {
        case "b": return shift ? .spray : .pencil
        case "e": return .eraser
        case "g": return shift ? .gradient : .fill
        case "i": return .eyedropper
        case "l": return .line
        case "q": return .lassoSelection
        case "m": return .rectangleSelection
        case "w": return .magicWand
        case "v": return .move
        case "h": return .hand
        case "u": return shift ? .ellipse : .rectangle
        default: return nil
        }
    }
}
