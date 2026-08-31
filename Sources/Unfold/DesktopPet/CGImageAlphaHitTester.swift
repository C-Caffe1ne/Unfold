import CoreGraphics

/// Alpha-hit-testing primitive behind Desktop Pet Phase 4's click-through:
/// given a `CGImage` displayed aspect-fit inside some bounds (exactly how
/// `SpriteAnimationView` renders it — `Image(decorative:scale:1,
/// orientation:.up).resizable().aspectRatio(contentMode: .fit)`), decides
/// whether a point falls on a "visible enough" pixel vs. transparent
/// letterbox/pillarbox padding or a truly transparent part of the character.
///
/// Pure image/geometry math — no `NSWindow`/`NSEvent`/interaction state
/// anywhere in it (that lives in `DesktopPetHitTestPolicy`, the one real
/// caller). The only mutable state is a single-slot decoded-alpha cache, so
/// repeated queries against the same still-displayed frame (as happens on
/// every tick of the hit-test poll while the animation isn't advancing)
/// don't re-decode the whole image every time.
///
/// `point`/`bounds` share one coordinate space: origin top-left, y
/// increasing downward — the same space `SpriteAnimationView` renders in.
/// A `CGImage`'s own row 0 is its *top* row (see
/// `GIFFixtureBuilder.pixel(of:x:y:)`'s doc comment for why, verified
/// empirically against a real decode), and `Image(orientation: .up)` draws
/// that row at the top of the rendered rect — so no Y-flip is needed
/// anywhere in this mapping.
final class CGImageAlphaHitTester {

    private var cachedImage: CGImage?
    private var cachedAlpha: [UInt8] = []
    private var cachedWidth = 0
    private var cachedHeight = 0

    func containsVisiblePixel(
        image: CGImage,
        at point: CGPoint,
        renderedIn bounds: CGRect,
        alphaThreshold: UInt8 = Constants.petHitTestAlphaThreshold,
        paddingPoints: CGFloat = Constants.petHitTestPaddingPoints
    ) -> Bool {
        let imageSize = CGSize(width: image.width, height: image.height)
        guard let renderRect = Self.aspectFitRect(imageSize: imageSize, in: bounds) else { return false }

        // By construction (`aspectFitRect` scales both axes uniformly),
        // width/height give the same scale up to floating-point rounding —
        // either works as "display points per source pixel."
        let scale = renderRect.width / imageSize.width
        guard scale > 0, scale.isFinite else { return false }

        let imagePoint = CGPoint(
            x: (point.x - renderRect.minX) / scale,
            y: (point.y - renderRect.minY) / scale
        )
        let paddingPixels = paddingPoints > 0 ? Int((paddingPoints / scale).rounded(.up)) : 0

        ensureAlphaCached(for: image)
        guard cachedWidth > 0, cachedHeight > 0 else { return false }

        let centerX = Int(imagePoint.x.rounded(.down))
        let centerY = Int(imagePoint.y.rounded(.down))
        for dy in -paddingPixels...paddingPixels {
            let y = centerY + dy
            guard y >= 0, y < cachedHeight else { continue }
            for dx in -paddingPixels...paddingPixels {
                let x = centerX + dx
                guard x >= 0, x < cachedWidth else { continue }
                if alpha(atX: x, y: y) >= alphaThreshold { return true }
            }
        }
        return false
    }

    private func alpha(atX x: Int, y: Int) -> UInt8 {
        cachedAlpha[(y * cachedWidth + x) * 4 + 3]
    }

    /// Decodes `image`'s alpha channel into `cachedAlpha`, unless it's
    /// already cached (compared by reference — `CGImage` bridges to a
    /// Swift class, so `===` is a real identity check, not a deep pixel
    /// comparison). A distinct `CGImage` instance per animation frame
    /// (`AnimationClip`'s frames are decoded once at load time and reused
    /// as playback loops) means this only re-decodes when the visible frame
    /// actually changes — cheap even at the hit-test poll's ~30Hz, since
    /// idle animations advance far slower than that.
    private func ensureAlphaCached(for image: CGImage) {
        if let cachedImage, cachedImage === image { return }

        let width = image.width
        let height = image.height
        var buffer = [UInt8](repeating: 0, count: max(width * height * 4, 0))
        if width > 0, height > 0 {
            let colorSpace = CGColorSpaceCreateDeviceRGB()
            // Same technique `GIFFixtureBuilder.pixel(of:x:y:)` uses to read
            // a decoded image back deterministically: draw into a same-size
            // premultiplied-last RGBA context at (0,0), which places the
            // image's row 0 at the top of the buffer.
            if let context = CGContext(
                data: &buffer,
                width: width,
                height: height,
                bitsPerComponent: 8,
                bytesPerRow: width * 4,
                space: colorSpace,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            ) {
                context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            }
        }

        cachedImage = image
        cachedAlpha = buffer
        cachedWidth = width
        cachedHeight = height
    }

    /// The rect `imageSize` renders into within `bounds` under
    /// aspect-fit — centered, scaled uniformly so the whole image is
    /// visible with no cropping, letterboxed/pillarboxed on the other axis.
    /// `nil` for degenerate (zero or negative) sizes.
    static func aspectFitRect(imageSize: CGSize, in bounds: CGRect) -> CGRect? {
        guard imageSize.width > 0, imageSize.height > 0, bounds.width > 0, bounds.height > 0 else { return nil }
        let scale = min(bounds.width / imageSize.width, bounds.height / imageSize.height)
        let fittedSize = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
        let origin = CGPoint(
            x: bounds.minX + (bounds.width - fittedSize.width) / 2,
            y: bounds.minY + (bounds.height - fittedSize.height) / 2
        )
        return CGRect(origin: origin, size: fittedSize)
    }
}
