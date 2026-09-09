import Foundation

enum PixelDrawing {
    static func line(from a: PixelPoint, to b: PixelPoint) -> [PixelPoint] {
        var x = a.x
        var y = a.y
        var result = [PixelPoint]()
        let dx = abs(b.x - x)
        let dy = -abs(b.y - y)
        let sx = x < b.x ? 1 : -1
        let sy = y < b.y ? 1 : -1
        var error = dx + dy
        while true {
            result.append(.init(x: x, y: y))
            if x == b.x && y == b.y { break }
            let twice = 2 * error
            if twice >= dy {
                error += dy
                x += sx
            }
            if twice <= dx {
                error += dx
                y += sy
            }
        }
        return result
    }
    static func interpolate(_ a: UInt32, _ b: UInt32, fraction: Double) -> UInt32 {
        let t = max(0, min(1, fraction))
        return [24, 16, 8, 0].reduce(UInt32(0)) { result, shift in
            let v = Double((a >> shift) & 255) * (1 - t) + Double((b >> shift) & 255) * t
            return result | UInt32(v.rounded()) << shift
        }
    }
    static func brightness(_ color: UInt32, darken: Bool) -> UInt32 {
        guard color & 255 != 0 else { return color }
        return [24, 16, 8].reduce(color & 255) { result, shift in
            let value = Int((color >> shift) & 255) + (darken ? -26 : 26)
            return result | UInt32(max(0, min(255, value))) << shift
        }
    }
    static func draw(
        document: inout PixelDocument, tool: PixelTool, from: PixelPoint, to: PixelPoint,
        color: UInt32, background: UInt32, brush: Int, layer: Int, frame: Int,
        selection: Set<Int>?, symmetryX: Bool, symmetryY: Bool, darken: Bool
    ) {
        guard document.layers.indices.contains(layer), (0..<document.frameCount).contains(frame),
            !document.layers[layer].isLocked
        else { return }
        let original = document.layers[layer].frames[frame].pixels
        var pairs = [(from, to)]
        if symmetryX {
            pairs += pairs.map {
                (
                    .init(x: document.width - 1 - $0.0.x, y: $0.0.y),
                    .init(x: document.width - 1 - $0.1.x, y: $0.1.y)
                )
            }
        }
        if symmetryY {
            pairs += pairs.map {
                (
                    .init(x: $0.0.x, y: document.height - 1 - $0.0.y),
                    .init(x: $0.1.x, y: document.height - 1 - $0.1.y)
                )
            }
        }
        if tool == .fill {
            for (_, point) in pairs {
                let mask = PixelSelection.wand(
                    at: point, pixels: original, width: document.width, height: document.height,
                    allowed: selection)
                for i in mask { document.layers[layer].frames[frame].pixels[i] = color }
            }
            return
        }
        if tool == .gradient {
            let dx = Double(to.x - from.x)
            let dy = Double(to.y - from.y)
            let denominator = dx * dx + dy * dy
            for i in original.indices where selection?.contains(i) ?? true {
                let t =
                    denominator == 0
                    ? 0
                    : (Double(i % document.width - from.x) * dx + Double(
                        i / document.width - from.y) * dy)
                        / denominator
                document.layers[layer].frames[frame].pixels[i] = interpolate(
                    color, background, fraction: t)
            }
            return
        }
        if tool == .brightness || tool == .spray {
            var touched = Set<Int>()
            let radius = max(1, min(8, brush))
            for (a, b) in pairs {
                for point in line(from: a, to: b) {
                    for y in (point.y - radius + 1)...(point.y + radius - 1) {
                        for x in (point.x - radius + 1)...(point.x + radius - 1) {
                            guard document.contains(.init(x: x, y: y)) else { continue }
                            let i = y * document.width + x
                            if tool == .brightness
                                || (Int.random(in: 0..<3) == 0
                                    && (x - point.x) * (x - point.x) + (y - point.y) * (y - point.y)
                                        < radius * radius)
                            {
                                touched.insert(i)
                            }
                        }
                    }
                }
            }
            for i in touched where selection?.contains(i) ?? true {
                document.layers[layer].frames[frame].pixels[i] =
                    tool == .brightness ? brightness(original[i], darken: darken) : color
            }
            return
        }
        for (a, b) in pairs {
            document.draw(
                tool: tool, from: a, to: b, color: color, brush: brush, layer: layer, frame: frame)
        }
        if let selection {
            for i in original.indices where !selection.contains(i) {
                document.layers[layer].frames[frame].pixels[i] = original[i]
            }
        }
    }
}
