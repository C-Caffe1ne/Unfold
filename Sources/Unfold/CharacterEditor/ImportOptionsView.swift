import SwiftUI

/// Asks how an image should become a document. Shown only when the answer is
/// not obvious: an oversized image, or one shaped like a sprite sheet.
@MainActor
struct ImportOptionsView: View {
    let imageWidth: Int
    let imageHeight: Int
    let preview: CGImage?
    let confirm: (ImportOptions) -> Void
    let cancel: () -> Void

    @State private var choice: Choice
    @State private var frameWidth: Int
    @State private var frameHeight: Int
    @State private var cropX = 0
    @State private var cropY = 0
    @State private var cropSide: Int

    private enum Choice: String, CaseIterable, Identifiable {
        case split = "Split into frames"
        case crop = "Crop"
        case scale = "Scale to fit"
        var id: String { rawValue }
    }

    init(imageWidth: Int, imageHeight: Int, preview: CGImage?,
         suggestion: ImportOptions,
         confirm: @escaping (ImportOptions) -> Void, cancel: @escaping () -> Void) {
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
        self.preview = preview
        self.confirm = confirm
        self.cancel = cancel
        let cap = Constants.editorCanvasSideRange.upperBound
        let floor = Constants.editorCanvasSideRange.lowerBound
        switch suggestion {
        case .split(let width, let height):
            _choice = State(initialValue: .split)
            _frameWidth = State(initialValue: width)
            _frameHeight = State(initialValue: height)
            _cropSide = State(initialValue: max(floor, min(cap, min(imageWidth, imageHeight))))
        case .crop(let rect):
            _choice = State(initialValue: .crop)
            _frameWidth = State(initialValue: max(floor, min(cap, imageWidth)))
            _frameHeight = State(initialValue: max(floor, min(cap, imageHeight)))
            _cropSide = State(initialValue: Int(rect.width))
            _cropX = State(initialValue: Int(rect.origin.x))
            _cropY = State(initialValue: Int(rect.origin.y))
        case .single, .scaleToFit:
            _choice = State(initialValue: .scale)
            _frameWidth = State(initialValue: max(floor, min(cap, imageWidth)))
            _frameHeight = State(initialValue: max(floor, min(cap, imageHeight)))
            _cropSide = State(initialValue: max(floor, min(cap, min(imageWidth, imageHeight))))
        }
    }

    private var isOversized: Bool {
        let cap = Constants.editorCanvasSideRange.upperBound
        return imageWidth > cap || imageHeight > cap
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Import Image").font(.headline)
            Text("\(imageWidth) × \(imageHeight) px")
                .font(.callout).foregroundStyle(.secondary)
            if isOversized {
                Label("Larger than \(Constants.editorCanvasSideRange.upperBound) px per side. Crop or scale it to fit.",
                      systemImage: "exclamationmark.triangle.fill")
                    .font(.callout).foregroundStyle(.orange)
            }
            if let preview {
                Image(decorative: preview, scale: 1)
                    .resizable().interpolation(.none).scaledToFit()
                    .frame(maxWidth: 260, maxHeight: 160)
                    .background(Color(nsColor: .underPageBackgroundColor))
            }
            Picker("", selection: $choice) {
                ForEach(Choice.allCases) { Text($0.rawValue).tag($0) }
            }.pickerStyle(.segmented).labelsHidden()

            switch choice {
            case .split:
                Stepper("Frame width: \(frameWidth) px", value: $frameWidth,
                        in: Constants.editorCanvasSideRange)
                Stepper("Frame height: \(frameHeight) px", value: $frameHeight,
                        in: Constants.editorCanvasSideRange)
                Text(splitSummary).font(.caption).foregroundStyle(.secondary)
            case .crop:
                Stepper("Size: \(cropSide) px", value: $cropSide, in: cropSideRange)
                Stepper("Left: \(cropX) px", value: $cropX, in: 0...max(0, imageWidth - cropSide))
                Stepper("Top: \(cropY) px", value: $cropY, in: 0...max(0, imageHeight - cropSide))
            case .scale:
                Text("Scaled with nearest-neighbour sampling, so pixels stay hard-edged.")
                    .font(.caption).foregroundStyle(.secondary)
            }

            HStack {
                Spacer()
                Button("Cancel", action: cancel).keyboardShortcut(.cancelAction)
                Button("Open") { confirm(selected) }.keyboardShortcut(.defaultAction)
            }
        }.padding(24).frame(width: 380)
    }

    /// A crop cannot be larger than the image it comes from, nor than the
    /// canvas it becomes.
    private var cropSideRange: ClosedRange<Int> {
        let upper = max(Constants.editorCanvasSideRange.lowerBound,
                        min(Constants.editorCanvasSideRange.upperBound, min(imageWidth, imageHeight)))
        return Constants.editorCanvasSideRange.lowerBound...upper
    }

    private var splitSummary: String {
        guard frameWidth > 0, frameHeight > 0,
              imageWidth % frameWidth == 0, imageHeight % frameHeight == 0 else {
            return "That frame size does not divide the image evenly."
        }
        let count = (imageWidth / frameWidth) * (imageHeight / frameHeight)
        return "\(count) frame\(count == 1 ? "" : "s")"
    }

    private var selected: ImportOptions {
        switch choice {
        case .split: return .split(frameWidth: frameWidth, frameHeight: frameHeight)
        case .crop: return .crop(CGRect(x: cropX, y: cropY, width: cropSide, height: cropSide))
        case .scale: return .scaleToFit
        }
    }
}
