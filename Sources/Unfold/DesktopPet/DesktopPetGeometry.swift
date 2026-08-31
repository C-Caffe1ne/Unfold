import CoreGraphics

/// Pure, AppKit-independent window-placement geometry for the Desktop Pet
/// — no `NSScreen`/`NSWindow` anywhere in this file, so every case
/// (including multi-monitor layouts with negative-origin secondary
/// displays) is directly unit-testable with synthetic `CGRect`s, without
/// real display hardware. `DesktopPetWindowController` is the only real
/// caller — it maps real `NSScreen`s to their `visibleFrame`s before
/// calling in, and maps a returned index back to a real `NSScreen`
/// afterward.
enum DesktopPetGeometry {

    /// Bottom-right corner of `visibleFrame` (Dock/menu bar already
    /// excluded), inset by `margin` on both edges. Phase 1's initial
    /// placement, and the fallback whenever no valid saved position exists.
    static func bottomRightOrigin(visibleFrame: CGRect, windowSize: CGSize, margin: CGFloat) -> CGPoint {
        CGPoint(
            x: visibleFrame.maxX - windowSize.width - margin,
            y: visibleFrame.minY + margin
        )
    }

    /// Translates `startWindowOrigin` by exactly how far the cursor has
    /// moved (`currentGlobalLocation - startGlobalLocation`) — never
    /// recenters or snaps the window to the cursor, so wherever within the
    /// pet the user grabbed stays under the cursor for the whole drag.
    static func draggedOrigin(
        startWindowOrigin: CGPoint,
        startGlobalLocation: CGPoint,
        currentGlobalLocation: CGPoint
    ) -> CGPoint {
        CGPoint(
            x: startWindowOrigin.x + (currentGlobalLocation.x - startGlobalLocation.x),
            y: startWindowOrigin.y + (currentGlobalLocation.y - startGlobalLocation.y)
        )
    }

    /// Clamps `frame` so it lies entirely within `visibleFrame`. If `frame`
    /// is larger than `visibleFrame` on an axis, it's aligned to
    /// `visibleFrame`'s origin on that axis rather than producing an
    /// inverted range.
    static func clamp(_ frame: CGRect, to visibleFrame: CGRect) -> CGRect {
        let maxX = max(visibleFrame.minX, visibleFrame.maxX - frame.width)
        let maxY = max(visibleFrame.minY, visibleFrame.maxY - frame.height)
        let x = min(max(frame.origin.x, visibleFrame.minX), maxX)
        let y = min(max(frame.origin.y, visibleFrame.minY), maxY)
        return CGRect(origin: CGPoint(x: x, y: y), size: frame.size)
    }

    /// Index into `visibleFrames` of the screen `windowFrame` overlaps the
    /// most, by area. If `windowFrame` overlaps none of them at all (e.g.
    /// a position saved before a display was unplugged, or a drag flung
    /// far past every screen's edge), falls back to whichever screen's
    /// center is closest to `windowFrame`'s — so the pet reappears near
    /// wherever it drifted off from, rather than jumping to an arbitrary
    /// array position. `nil` only if `visibleFrames` is empty.
    static func bestScreenIndex(forWindowFrame windowFrame: CGRect, among visibleFrames: [CGRect]) -> Int? {
        guard !visibleFrames.isEmpty else { return nil }

        var bestOverlapIndex: Int?
        var bestOverlapArea: CGFloat = 0
        for (index, frame) in visibleFrames.enumerated() {
            let intersection = frame.intersection(windowFrame)
            let area = intersection.isNull ? 0 : intersection.width * intersection.height
            if area > bestOverlapArea {
                bestOverlapArea = area
                bestOverlapIndex = index
            }
        }
        if let bestOverlapIndex { return bestOverlapIndex }

        let windowCenter = CGPoint(x: windowFrame.midX, y: windowFrame.midY)
        var bestIndex = 0
        var bestDistanceSquared = CGFloat.greatestFiniteMagnitude
        for (index, frame) in visibleFrames.enumerated() {
            let dx = frame.midX - windowCenter.x
            let dy = frame.midY - windowCenter.y
            let distanceSquared = dx * dx + dy * dy
            if distanceSquared < bestDistanceSquared {
                bestDistanceSquared = distanceSquared
                bestIndex = index
            }
        }
        return bestIndex
    }

    /// Window origin as a 0...1 fraction of the *usable* movement range
    /// within `visibleFrame` — i.e. accounting for `windowSize`, not just
    /// `visibleFrame`'s own size, so a normalized value of (1, 1) always
    /// means "flush against the far edge," never "window hanging half off
    /// the screen." `0` on an axis where the window doesn't fit at all
    /// (`visibleFrame` smaller than `windowSize` there).
    static func normalizedPosition(forOrigin origin: CGPoint, windowSize: CGSize, visibleFrame: CGRect) -> CGPoint {
        let usableWidth = visibleFrame.width - windowSize.width
        let usableHeight = visibleFrame.height - windowSize.height
        let x = usableWidth > 0 ? clamp01((origin.x - visibleFrame.minX) / usableWidth) : 0
        let y = usableHeight > 0 ? clamp01((origin.y - visibleFrame.minY) / usableHeight) : 0
        return CGPoint(x: x, y: y)
    }

    /// Inverse of `normalizedPosition` — re-projects a stored 0...1
    /// fraction back into a concrete origin within `visibleFrame`, against
    /// whatever `windowSize`/`visibleFrame` are *now* (which may differ
    /// from when the position was saved, e.g. after a resolution change).
    static func origin(fromNormalized normalized: CGPoint, windowSize: CGSize, visibleFrame: CGRect) -> CGPoint {
        let usableWidth = max(visibleFrame.width - windowSize.width, 0)
        let usableHeight = max(visibleFrame.height - windowSize.height, 0)
        return CGPoint(
            x: visibleFrame.minX + normalized.x * usableWidth,
            y: visibleFrame.minY + normalized.y * usableHeight
        )
    }

    private static func clamp01(_ value: CGFloat) -> CGFloat {
        min(max(value, 0), 1)
    }
}
