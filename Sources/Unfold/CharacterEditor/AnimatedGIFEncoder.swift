import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Writes a document out as an animated GIF, and reports up front what that
/// write would cost.
///
/// GIF is the only export format the editor offers that cannot carry a
/// document faithfully: it has one-bit transparency and a palette of a few
/// hundred entries, where the canvas has eight-bit alpha and no colour limit
/// at all. Rather than discover that after the file is on disk, `lossiness`
/// answers the question beforehand, so a caller can warn and let the user
/// decide. Everything the two functions assume about the encoder was
/// measured against ImageIO on macOS, not inferred from the GIF spec —
/// ImageIO makes choices the format does not require, and the comments below
/// record which ones matter.
enum AnimatedGIFEncoder {

    /// Everything about a document that GIF cannot carry. Callers warn with
    /// this before writing.
    struct Lossiness: Equatable {
        /// GIF transparency is a single palette index: a pixel is either
        /// fully there or not there at all. Every partial alpha in the
        /// document is therefore forced to one end or the other, and which
        /// end is measured — ImageIO rounds at 25% (alpha 64 of 255). At or
        /// above that a pixel is written fully opaque, keeping its colour, so
        /// a translucent layer comes back hard-edged. Below it the pixel does
        /// not survive at all: it reads back as nothing.
        ///
        /// That second outcome is the one worth warning about. A hard edge is
        /// visible the moment the file is opened, but a soft glow, a drop
        /// shadow, or the faint fringe of an anti-aliased outline simply
        /// disappears, and the export looks merely a little cleaner than it
        /// should rather than obviously broken.
        var partialAlpha = false
        /// The document holds more distinct colours than one shared palette
        /// can hold, so the encoder will quantise some of them away.
        var colourReduction = false
        var isLossless: Bool { !partialAlpha && !colourReduction }
    }

    /// Largest number of distinct opaque colours that survives a write.
    ///
    /// Two short of the 256 entries a GIF palette nominally holds, and the
    /// gap is measured rather than reasoned: with well-separated colours in a
    /// single frame, ImageIO changed no pixels at 250 colours, 8 pixels at
    /// 255, and 16 at 256 — and a 400-colour frame came back holding exactly
    /// 254 distinct colours. Transparency does not spend one of them; the
    /// same 250 colours beside a transparent region still round-tripped
    /// untouched.
    static let paletteCapacity = 254

    static func encode(_ document: PixelDocument) throws -> Data {
        let sequence = document.playbackSequence
        guard !sequence.isEmpty else { throw PixelDocumentCodec.Failure.invalid("Show at least one frame in the playback range before exporting GIF.") }
        let data = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(
            data, UTType.gif.identifier as CFString, sequence.count, nil) else {
            throw PixelDocumentCodec.Failure.invalid("Could not create a GIF.")
        }
        if document.playbackMode != .once {
            CGImageDestinationSetProperties(destination, [
                kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0] as [CFString: Any]
            ] as [CFString: Any] as CFDictionary)
        }

        // GIF stores a delay in hundredths of a second, so the document's
        // frame rate is rounded on the way out: 12 fps writes 1/12 s and
        // reads back as 0.08 s, a touch over 12.5 fps. Nothing here can avoid
        // that — the format has no finer unit — so both keys are written with
        // the exact value and the container does the rounding. The unclamped
        // key is set alongside the clamped one because some readers honour
        // only one of the two, and because the clamped key silently floors
        // very short delays to 0.1 s in several of them.
        for index in sequence {
            let delay = document.duration(at: index)
            let frameProperties = [kCGImagePropertyGIFDictionary: [
                kCGImagePropertyGIFUnclampedDelayTime: delay,
                kCGImagePropertyGIFDelayTime: delay
            ] as [CFString: Any]] as [CFString: Any] as CFDictionary
            let frame = document.compositedFrame(at: index)
            guard let image = PixelDocumentCodec.image(frame.pixels, width: document.width, height: document.height) else {
                throw PixelDocumentCodec.Failure.invalid("Could not render frame \(index + 1) of the animation.")
            }
            CGImageDestinationAddImage(destination, image, frameProperties)
        }
        guard CGImageDestinationFinalize(destination) else {
            throw PixelDocumentCodec.Failure.invalid("Could not encode the GIF.")
        }
        return data as Data
    }

    /// What a `encode` of this document would lose, in one pass over the
    /// composited frames.
    ///
    /// Composited, not stored, and that distinction is the whole reason this
    /// cannot be a cheap scan of the layer buffers: a layer at 50% opacity
    /// turns fully opaque paint into partial alpha, and only the composite
    /// ever shows it.
    static func lossiness(of document: PixelDocument) -> Lossiness {
        var result = Lossiness()
        // ImageIO writes ONE palette shared by every frame, not a local
        // palette per frame — measured: four frames of 200 disjoint colours
        // each lost colour in all four, not none. So the count has to run
        // across the whole document; a per-frame check would call a document
        // lossless right up to the point where it is not.
        var colours = Set<UInt32>()
        for index in Set(document.playbackSequence) {
            for pixel in document.compositedFrame(at: index).pixels {
                let alpha = pixel & 255
                // A hole is not a colour and spends no palette slot.
                guard alpha > 0 else { continue }
                if alpha < 255 { result.partialAlpha = true }
                if !result.colourReduction {
                    // Keyed on RGB alone, because alpha is not what the
                    // palette stores. A pixel at or above the 25% cut-off is
                    // written at full opacity, so it shares a slot with the
                    // opaque pixel of its own hue rather than adding one —
                    // which keying on RGBA would get wrong. Below the cut-off
                    // the pixel vanishes and takes up no slot at all, so this
                    // over-counts those; harmless, because any document
                    // holding one has already set `partialAlpha` and will be
                    // warned about regardless. The error only ever costs a
                    // caller an extra warning, and can never call a document
                    // lossless that is not.
                    colours.insert(pixel >> 8)
                    if colours.count > paletteCapacity {
                        result.colourReduction = true
                        // The answer is already decided; keeping the rest of
                        // the document's colours would only cost memory.
                        colours = []
                    }
                }
                // Nothing later in the scan can change either flag back.
                if result.partialAlpha && result.colourReduction { return result }
            }
        }
        return result
    }
}
