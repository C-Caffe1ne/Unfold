import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Converts an animated image file (GIF today — see the type-check in
/// `load(url:)`) into the same format-agnostic `AnimationClip`
/// `AnimationClipLoader` produces from a sprite sheet. `SpriteAnimator`
/// never needs to know which of the two produced its clip.
///
/// This is an independent decode path only — nothing in `Character`,
/// `CharacterManifest`, or `CharacterAssetLoader` calls into this yet.
/// Wiring an actual `.gif` character asset to it is future work.
enum AnimatedImageClipLoader {

    enum LoadError: Error, Equatable, CustomStringConvertible {
        case fileTooLarge(bytes: Int)
        case unsupportedFormat
        case unreadableSource
        case noFrames
        case tooManyFrames(count: Int)
        case canvasTooLarge(width: Int, height: Int)
        case inconsistentCanvas(frameIndex: Int)
        case failedToDecodeFrame(index: Int)

        var description: String {
            switch self {
            case .fileTooLarge(let bytes):
                return "source file is \(bytes) bytes, over the \(AnimatedImageValidationPolicy.maxFileSizeBytes)-byte limit"
            case .unsupportedFormat:
                return "source is not a GIF"
            case .unreadableSource:
                return "source could not be read as an image at all"
            case .noFrames:
                return "source defines no frames"
            case .tooManyFrames(let count):
                return "source has \(count) frames, over the \(AnimatedImageValidationPolicy.maxFrameCount)-frame limit"
            case .canvasTooLarge(let width, let height):
                return "source canvas is \(width)x\(height), over the \(AnimatedImageValidationPolicy.maxWidth)x\(AnimatedImageValidationPolicy.maxHeight) limit"
            case .inconsistentCanvas(let frameIndex):
                return "frame \(frameIndex) has a different canvas size than frame 0"
            case .failedToDecodeFrame(let index):
                return "frame \(index) could not be decoded"
            }
        }
    }

    // MARK: - Entry point

    static func load(url: URL) throws -> AnimationClip {
        let attributes = try? FileManager.default.attributesOfItem(atPath: url.path)
        let fileSize = (attributes?[.size] as? Int) ?? 0
        try validateFileSize(bytes: fileSize)

        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              let type = CGImageSourceGetType(source)
        else {
            throw LoadError.unreadableSource
        }
        guard (type as String) == UTType.gif.identifier else {
            throw LoadError.unsupportedFormat
        }

        let frameCount = CGImageSourceGetCount(source)
        guard frameCount > 0 else { throw LoadError.noFrames }
        guard frameCount <= AnimatedImageValidationPolicy.maxFrameCount else {
            throw LoadError.tooManyFrames(count: frameCount)
        }

        let loop = resolveLoop(gifLoopCount: readLoopCount(source: source))

        var canvasSize: (width: Int, height: Int)?
        var frames: [AnimationFrame] = []
        frames.reserveCapacity(frameCount)

        for index in 0..<frameCount {
            guard let image = CGImageSourceCreateImageAtIndex(source, index, nil) else {
                throw LoadError.failedToDecodeFrame(index: index)
            }

            if let canvasSize {
                guard image.width == canvasSize.width, image.height == canvasSize.height else {
                    throw LoadError.inconsistentCanvas(frameIndex: index)
                }
            } else {
                guard image.width <= AnimatedImageValidationPolicy.maxWidth,
                      image.height <= AnimatedImageValidationPolicy.maxHeight
                else {
                    throw LoadError.canvasTooLarge(width: image.width, height: image.height)
                }
                canvasSize = (image.width, image.height)
            }

            let (unclamped, clamped) = readDelayTimes(source: source, index: index)
            let duration = resolveFrameDuration(unclampedDelayTime: unclamped, delayTime: clamped)
            frames.append(AnimationFrame(image: image, duration: duration))
        }

        return AnimationClip(frames: frames, loop: loop)
    }

    // MARK: - ImageIO metadata extraction

    private static func readDelayTimes(source: CGImageSource, index: Int) -> (unclamped: Double?, clamped: Double?) {
        guard
            let properties = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
            let gif = properties[kCGImagePropertyGIFDictionary] as? [CFString: Any]
        else {
            return (nil, nil)
        }
        return (
            gif[kCGImagePropertyGIFUnclampedDelayTime] as? Double,
            gif[kCGImagePropertyGIFDelayTime] as? Double
        )
    }

    private static func readLoopCount(source: CGImageSource) -> Int? {
        guard
            let properties = CGImageSourceCopyProperties(source, nil) as? [CFString: Any],
            let gif = properties[kCGImagePropertyGIFDictionary] as? [CFString: Any]
        else {
            return nil
        }
        return gif[kCGImagePropertyGIFLoopCount] as? Int
    }

    // MARK: - Pure policy (unit-testable without any real file/ImageIO call)

    /// `unclampedDelayTime` wins over `delayTime` when both are present and
    /// valid (GIF convention: the "unclamped" value is the animation's
    /// actual authored delay; the clamped one is what some old browsers
    /// rounded slow/fast delays to). Whichever raw value is selected is
    /// then validated: missing, non-finite, or `<= 0` falls back to
    /// `AnimatedImageValidationPolicy.fallbackFrameDuration`; a short but
    /// positive value is clamped up to `minFrameDuration` rather than
    /// forced all the way to the fallback, so a real fast animation still
    /// plays fast.
    static func resolveFrameDuration(unclampedDelayTime: Double?, delayTime: Double?) -> TimeInterval {
        guard let raw = unclampedDelayTime ?? delayTime, raw.isFinite, raw > 0 else {
            return AnimatedImageValidationPolicy.fallbackFrameDuration
        }
        return max(raw, AnimatedImageValidationPolicy.minFrameDuration)
    }

    /// `AnimationClip.loop` is a `Bool`, so a GIF's repeat *count* can't be
    /// represented exactly. Policy: `0` (the GIF/ImageIO convention for
    /// "repeat forever") maps to `true`; a missing loop extension — the
    /// common shape for a GIF authored to play once — maps to `false`;
    /// and an explicit finite count (which this model can't express as
    /// "play exactly N times") is simplified to `false` rather than
    /// silently looping forever, since holding the last frame after one
    /// play is the less surprising failure mode of the two.
    static func resolveLoop(gifLoopCount: Int?) -> Bool {
        gifLoopCount == 0
    }

    static func validateFileSize(bytes: Int) throws {
        guard bytes <= AnimatedImageValidationPolicy.maxFileSizeBytes else {
            throw LoadError.fileTooLarge(bytes: bytes)
        }
    }
}
