import Foundation

/// Straight-alpha RGBA, in top-left row-major order. No renderer-owned buffers.
struct PixelFrame: Equatable {
    var pixels: [UInt32]

    init(width: Int, height: Int) {
        pixels = Array(repeating: 0, count: width * height)
    }
}

struct PixelLayer: Equatable {
    var name: String
    var opacity: Double = 1
    var frames: [PixelFrame]
}

struct PixelPoint: Equatable {
    let x: Int
    let y: Int
}

enum PixelTool: String, CaseIterable, Identifiable {
    case pencil, eraser, fill, eyedropper, line, rectangle, ellipse
    var id: String { rawValue }
    var title: String { rawValue.capitalized }
    var symbol: String {
        switch self {
        case .pencil: return "pencil.tip"
        case .eraser: return "eraser"
        case .fill: return "drop.fill"
        case .eyedropper: return "eyedropper"
        case .line: return "line.diagonal"
        case .rectangle: return "rectangle"
        case .ellipse: return "circle"
        }
    }
}

struct PixelDocument: Equatable {
    static let maximumLayers = 16
    var width: Int
    var height: Int
    var fps: Double = 12
    var name = "Unfold Character"
    var description = ""
    /// Bottom layer first, matching Piskel's serialized layer order.
    var layers: [PixelLayer]
    var frameCount: Int { layers[0].frames.count }
    var byteCount: Int { width * height * frameCount * layers.count * 4 }

    init(width: Int = Constants.editorDefaultCanvasSide, height: Int = Constants.editorDefaultCanvasSide) {
        self.width = min(max(width, 1), Constants.editorCanvasSideRange.upperBound)
        self.height = min(max(height, 1), Constants.editorCanvasSideRange.upperBound)
        layers = [PixelLayer(name: "Layer 1", frames: [PixelFrame(width: self.width, height: self.height)])]
    }

    func contains(_ point: PixelPoint) -> Bool {
        (0..<width).contains(point.x) && (0..<height).contains(point.y)
    }

    func compositedFrame(at index: Int) -> PixelFrame {
        var result = PixelFrame(width: width, height: height)
        guard (0..<frameCount).contains(index) else { return result }
        for layer in layers where layer.opacity > 0 {
            for i in result.pixels.indices {
                let source = layer.frames[index].pixels[i]
                if source & 255 == 0 { continue }
                result.pixels[i] = source & 255 == 255 && layer.opacity == 1
                    ? source : Self.composite(source, over: result.pixels[i], opacity: layer.opacity)
            }
        }
        return result
    }

    static func composite(_ source: UInt32, over destination: UInt32, opacity: Double) -> UInt32 {
        let sa = Double(source & 255) / 255 * opacity
        let da = Double(destination & 255) / 255
        let alpha = sa + da * (1 - sa)
        guard alpha > 0 else { return 0 }
        var result = UInt32((alpha * 255).rounded())
        for shift in [24, 16, 8] {
            let s = Double((source >> shift) & 255)
            let d = Double((destination >> shift) & 255)
            let channel = UInt32(((s * sa + d * da * (1 - sa)) / alpha).rounded())
            result |= min(channel, 255) << shift
        }
        return result
    }

    mutating func draw(tool: PixelTool, from start: PixelPoint, to end: PixelPoint,
                       color: UInt32, brush: Int, layer: Int, frame: Int) {
        guard layers.indices.contains(layer), (0..<frameCount).contains(frame) else { return }
        if tool == .fill {
            floodFill(at: end, color: color, layer: layer, frame: frame)
            return
        }
        guard tool != .eyedropper else { return }
        let ink: UInt32 = tool == .eraser ? 0 : color
        let size = min(max(brush, 1), 8)
        func stamp(_ point: PixelPoint, into pixels: inout [UInt32]) {
            let offset = (size - 1) / 2
            for y in (point.y - offset)..<(point.y - offset + size) {
                for x in (point.x - offset)..<(point.x - offset + size) where (0..<width).contains(x) && (0..<height).contains(y) {
                    pixels[y * width + x] = ink
                }
            }
        }
        var pixels = layers[layer].frames[frame].pixels
        let left = min(start.x, end.x), right = max(start.x, end.x)
        let top = min(start.y, end.y), bottom = max(start.y, end.y)
        if tool == .rectangle {
            for x in left...right {
                stamp(PixelPoint(x: x, y: top), into: &pixels)
                stamp(PixelPoint(x: x, y: bottom), into: &pixels)
            }
            for y in top...bottom {
                stamp(PixelPoint(x: left, y: y), into: &pixels)
                stamp(PixelPoint(x: right, y: y), into: &pixels)
            }
        } else if tool == .ellipse && left != right && top != bottom {
            let rx = Double(right - left) / 2, ry = Double(bottom - top) / 2
            let cx = Double(left) + rx, cy = Double(top) + ry
            // Sample both axes to keep narrow ellipses connected.
            for x in left...right {
                let dy = ry * sqrt(max(0, 1 - pow((Double(x) - cx) / rx, 2)))
                stamp(PixelPoint(x: x, y: Int((cy - dy).rounded())), into: &pixels)
                stamp(PixelPoint(x: x, y: Int((cy + dy).rounded())), into: &pixels)
            }
            for y in top...bottom {
                let dx = rx * sqrt(max(0, 1 - pow((Double(y) - cy) / ry, 2)))
                stamp(PixelPoint(x: Int((cx - dx).rounded()), y: y), into: &pixels)
                stamp(PixelPoint(x: Int((cx + dx).rounded()), y: y), into: &pixels)
            }
        } else {
            // Bresenham bridges sparse mouse events without gaps.
            var x = start.x, y = start.y
            let dx = abs(end.x - x), dy = -abs(end.y - y)
            let sx = x < end.x ? 1 : -1, sy = y < end.y ? 1 : -1
            var error = dx + dy
            while true {
                stamp(PixelPoint(x: x, y: y), into: &pixels)
                if x == end.x && y == end.y { break }
                let twice = 2 * error
                if twice >= dy { error += dy; x += sx }
                if twice <= dx { error += dx; y += sy }
            }
        }
        layers[layer].frames[frame].pixels = pixels
    }

    private mutating func floodFill(at point: PixelPoint, color: UInt32, layer: Int, frame: Int) {
        guard contains(point) else { return }
        var pixels = layers[layer].frames[frame].pixels
        let origin = point.y * width + point.x
        let old = pixels[origin]
        guard old != color else { return }
        var queue = [origin]
        pixels[origin] = color
        var cursor = 0
        while cursor < queue.count {
            let index = queue[cursor]
            cursor += 1
            let x = index % width, y = index / width
            for p in [PixelPoint(x: x - 1, y: y), PixelPoint(x: x + 1, y: y),
                      PixelPoint(x: x, y: y - 1), PixelPoint(x: x, y: y + 1)] where contains(p) {
                let next = p.y * width + p.x
                if pixels[next] == old {
                    pixels[next] = color
                    queue.append(next)
                }
            }
        }
        layers[layer].frames[frame].pixels = pixels
    }

    mutating func resize(width newWidth: Int, height newHeight: Int) {
        guard Constants.editorCanvasSideRange.contains(newWidth), Constants.editorCanvasSideRange.contains(newHeight) else { return }
        for l in layers.indices {
            for f in 0..<frameCount {
                var resized = PixelFrame(width: newWidth, height: newHeight)
                for y in 0..<min(height, newHeight) {
                    for x in 0..<min(width, newWidth) {
                        resized.pixels[y * newWidth + x] = layers[l].frames[f].pixels[y * width + x]
                    }
                }
                layers[l].frames[f] = resized
            }
        }
        width = newWidth
        height = newHeight
    }

    mutating func insertFrame(after index: Int, duplicate: Bool) {
        guard frameCount < Constants.editorFrameCountRange.upperBound, (0..<frameCount).contains(index) else { return }
        for l in layers.indices {
            let newFrame = duplicate ? layers[l].frames[index] : PixelFrame(width: width, height: height)
            layers[l].frames.insert(newFrame, at: index + 1)
        }
    }

    mutating func removeFrame(at index: Int) {
        guard frameCount > 1, (0..<frameCount).contains(index) else { return }
        for l in layers.indices { layers[l].frames.remove(at: index) }
    }

    mutating func moveFrame(from: Int, to: Int) {
        guard (0..<frameCount).contains(from), (0..<frameCount).contains(to), from != to else { return }
        for l in layers.indices {
            let frame = layers[l].frames.remove(at: from)
            layers[l].frames.insert(frame, at: to)
        }
    }

    mutating func flip(layer: Int, frame: Int, horizontal: Bool) {
        let old = layers[layer].frames[frame].pixels
        for y in 0..<height {
            for x in 0..<width {
                let sx = horizontal ? width - 1 - x : x
                let sy = horizontal ? y : height - 1 - y
                layers[layer].frames[frame].pixels[y * width + x] = old[sy * width + sx]
            }
        }
    }
}
