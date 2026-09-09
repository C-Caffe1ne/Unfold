import Foundation

enum PixelSelection {
    static func rectangle(from a: PixelPoint, to b: PixelPoint, width: Int, height: Int) -> Set<Int>
    {
        let x0 = max(0, min(a.x, b.x))
        let x1 = min(width - 1, max(a.x, b.x))
        let y0 = max(0, min(a.y, b.y))
        let y1 = min(height - 1, max(a.y, b.y))
        guard x0 <= x1, y0 <= y1 else { return [] }
        return Set((y0...y1).flatMap { y in (x0...x1).map { y * width + $0 } })
    }
    static func wand(
        at point: PixelPoint, pixels: [UInt32], width: Int, height: Int, allowed: Set<Int>? = nil
    ) -> Set<Int> {
        guard (0..<width).contains(point.x), (0..<height).contains(point.y) else { return [] }
        let origin = point.y * width + point.x
        let target = pixels[origin]
        guard allowed?.contains(origin) ?? true else { return [] }
        var result: Set<Int> = [origin]
        var queue = [origin]
        var cursor = 0
        while cursor < queue.count {
            let i = queue[cursor]
            cursor += 1
            let x = i % width
            let y = i / width
            for p in [
                PixelPoint(x: x - 1, y: y), PixelPoint(x: x + 1, y: y), PixelPoint(x: x, y: y - 1),
                PixelPoint(x: x, y: y + 1),
            ] where (0..<width).contains(p.x) && (0..<height).contains(p.y) {
                let j = p.y * width + p.x
                if allowed?.contains(j) ?? true, pixels[j] == target, result.insert(j).inserted {
                    queue.append(j)
                }
            }
        }
        return result
    }
    static func lasso(_ points: [PixelPoint], width: Int, height: Int) -> Set<Int> {
        guard let first = points.first else { return [] }
        guard points.count >= 3 else {
            return rectangle(from: first, to: points.last!, width: width, height: height)
        }
        var mask = Set<Int>()
        // Scanline intersections avoid testing every polygon edge for every canvas pixel.
        for y in 0..<height {
            var crossings = [Double]()
            for i in points.indices {
                let a = points[i]
                let b = points[(i + 1) % points.count]
                if (a.y > y) != (b.y > y) {
                    crossings.append(
                        Double(b.x - a.x) * Double(y - a.y) / Double(b.y - a.y) + Double(a.x))
                }
            }
            crossings.sort()
            for i in stride(from: 0, to: crossings.count - crossings.count % 2, by: 2) {
                let left = max(0, Int(ceil(crossings[i])))
                let right = min(width - 1, Int(floor(crossings[i + 1])))
                if left <= right { for x in left...right { mask.insert(y * width + x) } }
            }
        }
        for i in points.indices {
            for p in PixelDrawing.line(from: points[i], to: points[(i + 1) % points.count])
            where (0..<width).contains(p.x) && (0..<height).contains(p.y) {
                mask.insert(p.y * width + p.x)
            }
        }
        return mask
    }
    static func moved(_ selection: Set<Int>, dx: Int, dy: Int, width: Int, height: Int) -> Set<Int>
    {
        Set(
            selection.compactMap { i in
                let x = i % width + dx
                let y = i / width + dy
                return (0..<width).contains(x) && (0..<height).contains(y) ? y * width + x : nil
            })
    }
}
