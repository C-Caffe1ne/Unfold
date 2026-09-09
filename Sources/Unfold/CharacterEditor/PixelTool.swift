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
    /// The Latin letter a key press stands for, whatever the character it
    /// produced.
    ///
    /// `charactersIgnoringModifiers` ignores modifiers, not input methods.
    /// With 2-set Korean armed the `e` key reports "ㄷ", so every tool
    /// shortcut stopped matching and the editor looked as though it had no
    /// keyboard at all. The character is still tried first, so a Latin layout
    /// that moves its letters keeps the shortcuts it has; the physical key is
    /// only the fallback.
    static func shortcutLetter(characters: String?, keyCode: UInt16) -> String? {
        if let lowered = characters?.lowercased(), lowered.count == 1,
            let scalar = lowered.unicodeScalars.first,
            (Unicode.Scalar("a").value...Unicode.Scalar("z").value).contains(scalar.value) {
            return lowered
        }
        return ansiLetters[keyCode]
    }

    /// The ANSI virtual key codes for the letters the editor binds. Korean,
    /// Japanese and Pinyin input methods all sit on this physical layout, so
    /// the code identifies the key even when the character cannot.
    private static let ansiLetters: [UInt16: String] = [
        0: "a", 2: "d", 4: "h", 5: "g", 6: "z", 9: "v", 11: "b",
        12: "q", 13: "w", 14: "e", 32: "u", 34: "i", 37: "l", 46: "m",
    ]

    /// Resolves a key press to its tool, seeing through an input method.
    static func shortcut(characters: String?, keyCode: UInt16, shift: Bool = false) -> PixelTool? {
        guard let letter = shortcutLetter(characters: characters, keyCode: keyCode) else { return nil }
        return shortcut(letter, shift: shift)
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
