import Foundation

enum PixelPalette {
    static let maximumColors = 256
    enum ImportError: LocalizedError {
        case invalid
        var errorDescription: String? {
            "Invalid GIMP palette. Use a GIMP Palette header and up to 256 RGB colors (0–255)."
        }
    }
    static func parseGPL(_ text: String) throws -> [UInt32] {
        guard text.utf8.count <= 1_048_576 else { throw ImportError.invalid }
        let lines = text.components(separatedBy: .newlines)
        guard lines.first == "GIMP Palette" else { throw ImportError.invalid }
        var colors = [UInt32]()
        var started = false
        for raw in lines.dropFirst() {
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.isEmpty || line.hasPrefix("#") { continue }
            if !started && line.hasPrefix("Name:") { continue }
            if !started && line.hasPrefix("Columns:") {
                guard let columns = Int(line.dropFirst(8).trimmingCharacters(in: .whitespaces)),
                    (0...256).contains(columns)
                else { throw ImportError.invalid }
                continue
            }
            started = true
            let parts = line.split(whereSeparator: { $0.isWhitespace })
            guard parts.count >= 3, let r = UInt32(parts[0]), let g = UInt32(parts[1]),
                let b = UInt32(parts[2]), r <= 255, g <= 255, b <= 255, colors.count < maximumColors
            else { throw ImportError.invalid }
            colors.append(r << 24 | g << 16 | b << 8 | 255)
        }
        guard !colors.isEmpty else { throw ImportError.invalid }
        return colors
    }
}
