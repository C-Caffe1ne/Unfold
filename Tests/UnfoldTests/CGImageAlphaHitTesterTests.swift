import XCTest
@testable import Unfold

/// `CGImageAlphaHitTester` is the alpha-hit-testing primitive behind
/// Desktop Pet Phase 4's click-through: given a `CGImage` displayed
/// aspect-fit inside some bounds (exactly how `SpriteAnimationView` renders
/// it — `Image(decorative:scale:1,orientation:.up).resizable()
/// .aspectRatio(contentMode: .fit)`), decide whether a point is on a
/// visible-enough pixel. Pure image/geometry math (plus a small per-instance
/// decode cache) — no `NSWindow`/`NSEvent`/interaction state anywhere in it,
/// so every case is directly testable with synthetic `CGImage`s built via
/// `GIFFixtureBuilder.makeImage`, matching its documented row-major/row-0-
/// is-top pixel convention (the same convention `Image(orientation: .up)`
/// renders in — see the tester's own doc comment on why no Y-flip is
/// needed between window/view coordinates and image pixel coordinates).
final class CGImageAlphaHitTesterTests: XCTestCase {

    private func makeTester() -> CGImageAlphaHitTester { CGImageAlphaHitTester() }

    // MARK: - Basic opaque/transparent (square image, square bounds, 1:1 scale)

    func test_opaquePixel_isVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .white }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5, y: 5),
            renderedIn: CGRect(x: 0, y: 0, width: 10, height: 10),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertTrue(hit)
    }

    func test_fullyTransparentPixel_isNotVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .clear }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5, y: 5),
            renderedIn: CGRect(x: 0, y: 0, width: 10, height: 10),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertFalse(hit)
    }

    // MARK: - Threshold boundary

    func test_alphaBelowThreshold_isNotVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in
            GIFFixtureBuilder.RGBA(r: 255, g: 255, b: 255, a: 25)
        }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5, y: 5),
            renderedIn: CGRect(x: 0, y: 0, width: 10, height: 10),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertFalse(hit)
    }

    func test_alphaAtExactlyThreshold_isVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in
            GIFFixtureBuilder.RGBA(r: 255, g: 255, b: 255, a: 26)
        }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5, y: 5),
            renderedIn: CGRect(x: 0, y: 0, width: 10, height: 10),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertTrue(hit)
    }

    // MARK: - Out of bounds

    func test_pointFarOutsideSourceBounds_isNotVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .white }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5000, y: 5000),
            renderedIn: CGRect(x: 0, y: 0, width: 10, height: 10),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertFalse(hit)
    }

    func test_pointInLetterboxArea_outsideAspectFitRenderedRect_isNotVisible() {
        // A wide (20x10) image aspect-fit into square (20x20) bounds letterboxes
        // to a 20x10 rect vertically centered (y: 5...15). An opaque image with
        // a point at y=2 (top letterbox bar) must miss even though it's within
        // the outer bounds rect.
        let image = GIFFixtureBuilder.makeImage(width: 20, height: 10) { _, _ in .white }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 10, y: 2),
            renderedIn: CGRect(x: 0, y: 0, width: 20, height: 20),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertFalse(hit)
    }

    // MARK: - Scale mapping

    func test_sourceToDisplayScaleMapping_pointNearRenderedEdge_mapsToCorrectSourcePixel() {
        // 10x10 source fit into 20x20 bounds (2x scale). The right half of the
        // source is opaque, left half transparent. A point 3/4 of the way
        // across the rendered rect (x=15 of 20) must land in the source's
        // right (opaque) half.
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { x, _ in
            x >= 5 ? .white : .clear
        }
        let tester = makeTester()

        let hitOnRight = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 15, y: 10),
            renderedIn: CGRect(x: 0, y: 0, width: 20, height: 20),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        let hitOnLeft = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 5, y: 10),
            renderedIn: CGRect(x: 0, y: 0, width: 20, height: 20),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertTrue(hitOnRight)
        XCTAssertFalse(hitOnLeft)
    }

    func test_384SourceInto192Display_mapping() {
        // Mirrors the real default-cat spritesheet frame size (384x384) vs.
        // the real Desktop Pet window size (192x192) — 0.5x scale.
        let image = GIFFixtureBuilder.makeImage(width: 384, height: 384) { x, _ in
            x < 192 ? .clear : .white // left half transparent, right half opaque
        }
        let tester = makeTester()
        let bounds = CGRect(x: 0, y: 0, width: 192, height: 192)

        XCTAssertFalse(tester.containsVisiblePixel(image: image, at: CGPoint(x: 48, y: 96), renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0))
        XCTAssertTrue(tester.containsVisiblePixel(image: image, at: CGPoint(x: 144, y: 96), renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0))
    }

    // MARK: - Y-axis orientation

    func test_yAxis_topOfImageMapsToTopOfBounds_notBottom() {
        // Opaque top half (rows 0..<5, "top" per GIFFixtureBuilder's row-0-is-
        // top convention), transparent bottom half. A point near the top of
        // `bounds` (small y, per the top-left-origin/y-down convention
        // SwiftUI and this tester both use) must hit; near the bottom must not.
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, y in
            y < 5 ? .white : .clear
        }
        let tester = makeTester()
        let bounds = CGRect(x: 0, y: 0, width: 10, height: 10)

        XCTAssertTrue(tester.containsVisiblePixel(image: image, at: CGPoint(x: 5, y: 1), renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0), "near the top of bounds must read the image's top (opaque) rows")
        XCTAssertFalse(tester.containsVisiblePixel(image: image, at: CGPoint(x: 5, y: 9), renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0), "near the bottom of bounds must read the image's bottom (transparent) rows")
    }

    // MARK: - Rectangular aspect-fit

    func test_rectangularImage_aspectFit_visiblePixelWithinRenderedRect_isHit() {
        let image = GIFFixtureBuilder.makeImage(width: 20, height: 10) { _, _ in .white }
        let tester = makeTester()

        // Letterboxed into 20x20 bounds -> rendered rect is y: 5...15.
        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 10, y: 10),
            renderedIn: CGRect(x: 0, y: 0, width: 20, height: 20),
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertTrue(hit)
    }

    // MARK: - Hit padding

    func test_hitPadding_transparentPixelNearVisibleNeighbor_isHit() {
        // A single opaque pixel at the source's center; everything else
        // transparent. With padding, a cursor point a couple of *display*
        // points away (but still within the padding radius) must still hit.
        let image = GIFFixtureBuilder.makeImage(width: 20, height: 20) { x, y in
            (x == 10 && y == 10) ? .white : .clear
        }
        let tester = makeTester()
        let bounds = CGRect(x: 0, y: 0, width: 20, height: 20) // 1:1 scale

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 12, y: 10), // 2 display points away from the opaque pixel
            renderedIn: bounds,
            alphaThreshold: 26,
            paddingPoints: 2
        )
        XCTAssertTrue(hit)
    }

    func test_hitPadding_pointBeyondPaddingRadius_isNotHit() {
        let image = GIFFixtureBuilder.makeImage(width: 20, height: 20) { x, y in
            (x == 10 && y == 10) ? .white : .clear
        }
        let tester = makeTester()
        let bounds = CGRect(x: 0, y: 0, width: 20, height: 20) // 1:1 scale

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint(x: 17, y: 10), // 7 display points away — beyond a 2pt padding
            renderedIn: bounds,
            alphaThreshold: 26,
            paddingPoints: 2
        )
        XCTAssertFalse(hit)
    }

    // MARK: - Degenerate inputs

    func test_zeroSizedBounds_isNotVisible() {
        let image = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .white }
        let tester = makeTester()

        let hit = tester.containsVisiblePixel(
            image: image,
            at: CGPoint.zero,
            renderedIn: CGRect.zero,
            alphaThreshold: 26,
            paddingPoints: 0
        )
        XCTAssertFalse(hit)
    }

    // MARK: - Cache correctness across repeated/alternating images

    func test_repeatedQueriesAgainstTheSameImage_produceConsistentResults() {
        let opaque = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .white }
        let transparent = GIFFixtureBuilder.makeImage(width: 10, height: 10) { _, _ in .clear }
        let tester = makeTester()
        let bounds = CGRect(x: 0, y: 0, width: 10, height: 10)
        let point = CGPoint(x: 5, y: 5)

        XCTAssertTrue(tester.containsVisiblePixel(image: opaque, at: point, renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0))
        // Switching to a different image (invalidating any single-slot cache)
        // and back must not leave a stale cached result behind.
        XCTAssertFalse(tester.containsVisiblePixel(image: transparent, at: point, renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0))
        XCTAssertTrue(tester.containsVisiblePixel(image: opaque, at: point, renderedIn: bounds, alphaThreshold: 26, paddingPoints: 0))
    }
}
