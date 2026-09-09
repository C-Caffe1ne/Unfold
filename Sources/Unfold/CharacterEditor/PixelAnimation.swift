import Foundation

struct PixelFrameSettings: Codable, Equatable {
    /// nil inherits the document FPS. A custom hold lasts 10 ms ... 60 s.
    var durationMS: Int? = nil
    var isVisible = true
    static let durationRange = 10...60_000
}

enum PixelPlaybackMode: String, Codable, CaseIterable, Identifiable {
    case once, loop, pingPong, range
    var id: String { rawValue }
    var title: String {
        switch self {
        case .once: return "Once"
        case .loop: return "Loop"
        case .pingPong: return "Ping-pong"
        case .range: return "Range loop"
        }
    }
}

extension PixelDocument {
    static let defaultPalette: [UInt32] = [0x171923FF, 0xFFFFFFFF, 0x9195A3FF, 0xD74949FF,
        0xF4B860FF, 0xF3E7A2FF, 0x72B883FF, 0x4B8CBFFF, 0x8A6CBFFF, 0xE89FB6FF, 0x805E49FF, 0]

    func settings(at index: Int) -> PixelFrameSettings {
        frameSettings.indices.contains(index) ? frameSettings[index] : PixelFrameSettings()
    }

    mutating func normalizeFrameSettings() {
        frameSettings = (0..<frameCount).map { settings(at: $0) }
    }

    mutating func clampPlaybackRange() {
        playbackStart = min(max(0, playbackStart), frameCount - 1)
        if let end = playbackEnd { playbackEnd = min(max(playbackStart, end), frameCount - 1) }
    }

    func duration(at index: Int) -> TimeInterval {
        if let ms = settings(at: index).durationMS {
            return Double(min(max(ms, PixelFrameSettings.durationRange.lowerBound), PixelFrameSettings.durationRange.upperBound)) / 1000
        }
        return 1 / (fps.isFinite && fps > 0 ? fps : 12)
    }

    var visibleFrames: [Int] { (0..<frameCount).filter { settings(at: $0).isVisible } }

    /// Sequence is shared by preview, GIF export and library playback.
    var playbackSequence: [Int] {
        let first = min(max(0, playbackStart), frameCount - 1)
        let last = min(max(first, playbackEnd ?? frameCount - 1), frameCount - 1)
        let indices = visibleFrames.filter { playbackMode != .range || (first...last).contains($0) }
        if playbackMode == .pingPong && indices.count > 2 {
            return indices + indices.dropFirst().dropLast().reversed()
        }
        return indices
    }

    func playbackSample(elapsed: TimeInterval) -> (frame: Int?, finished: Bool) {
        let sequence = playbackSequence
        guard let last = sequence.last else { return (nil, true) }
        let total = sequence.reduce(0.0) { $0 + duration(at: $1) }
        let time = elapsed.isFinite ? max(0, elapsed) : 0
        if playbackMode == .once && time >= total { return (last, true) }
        var remaining = playbackMode == .once ? time : time.truncatingRemainder(dividingBy: total)
        for index in sequence {
            let hold = duration(at: index)
            if remaining < hold { return (index, false) }
            remaining -= hold
        }
        return (last, false)
    }
}
