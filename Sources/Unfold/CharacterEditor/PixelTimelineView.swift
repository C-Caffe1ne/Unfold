import AppKit
import SwiftUI

@MainActor
struct PixelTimelineView: View {
    @ObservedObject var model: PixelEditorModel
    @State private var drag: (token: String, kind: String, index: Int, document: PixelDocument)?

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("LAYERS / FRAMES").font(.caption.weight(.semibold))
                Spacer()
                Button("Blank") { model.addFrame(duplicate: false) }.disabled(model.document.frameCount >= Constants.editorFrameCountRange.upperBound)
                Button("Duplicate") { model.addFrame(duplicate: true) }.disabled(model.document.frameCount >= Constants.editorFrameCountRange.upperBound)
                Button("Delete Frame", action: model.deleteFrame).disabled(model.document.frameCount <= Constants.editorFrameCountRange.lowerBound)
            }
            ScrollView([.horizontal, .vertical]) {
                VStack(alignment: .leading, spacing: 4) {
                    HStack(spacing: 4) {
                        Text("Double-click layer to rename").font(.caption2).frame(width: 180)
                        ForEach(0..<model.document.frameCount, id: \.self) { frame in
                            Text("\(frame + 1)" + (model.document.settings(at: frame).isVisible ? "" : " ◌"))
                                .font(.caption.monospacedDigit()).frame(width: 58, height: 24)
                                .background(model.selectedFrame == frame ? Color.orange.opacity(0.3) : Color.secondary.opacity(0.1))
                                .onTapGesture { model.selectFrame(frame) }
                                .onDrag { beginDrag("frame", index: frame) }
                                .dropDestination(for: String.self) { values, _ in drop(values, kind: "frame", at: frame) }
                        }
                    }
                    ForEach(Array(model.document.layers.indices.reversed()), id: \.self) { layer in
                        HStack(spacing: 4) {
                            PixelLayerHeader(model: model, index: layer)
                                .frame(width: 180, height: 58)
                                .onDrag { beginDrag("layer", index: layer) }
                                .dropDestination(for: String.self) { values, _ in drop(values, kind: "layer", at: layer) }
                            ForEach(0..<model.document.frameCount, id: \.self) { frame in
                                Button {
                                    model.selectLayer(layer)
                                    model.selectFrame(frame)
                                } label: {
                                    ZStack {
                                        Color(nsColor: .controlBackgroundColor)
                                        if let image = PixelDocumentCodec.image(model.document.layers[layer].frames[frame].pixels,
                                            width: model.document.width, height: model.document.height) {
                                            Image(decorative: image, scale: 1).resizable().interpolation(.none).scaledToFit().padding(3)
                                        }
                                    }.frame(width: 58, height: 58)
                                        .border(model.selectedLayer == layer && model.selectedFrame == frame ? Color.orange : Color.secondary.opacity(0.3), width: 2)
                                }.buttonStyle(.plain).accessibilityLabel("\(model.document.layers[layer].name), frame \(frame + 1)")
                            }
                        }
                    }
                }
            }
        }.padding(10)
    }

    private func beginDrag(_ kind: String, index: Int) -> NSItemProvider {
        let token = UUID().uuidString
        drag = (token, kind, index, model.document)
        return NSItemProvider(object: token as NSString)
    }

    private func drop(_ values: [String], kind: String, at index: Int) -> Bool {
        guard let source = drag, values == [source.token], source.kind == kind,
              source.document == model.document, source.index != index else { return false }
        drag = nil
        if kind == "frame" { model.moveFrame(from: source.index, to: index) }
        else { model.moveLayer(from: source.index, to: index) }
        return true
    }
}

@MainActor
private struct PixelLayerHeader: View {
    @ObservedObject var model: PixelEditorModel
    let index: Int
    @State private var editing = false
    @State private var name = ""
    @FocusState private var focused: Bool

    var body: some View {
        HStack(spacing: 4) {
            Button { model.toggleLayerVisibility(index) } label: {
                Image(systemName: model.document.layers[index].isVisible ? "eye" : "eye.slash")
            }.help("Toggle layer visibility (⇧X)")
            Button { model.toggleLayerLock(index) } label: {
                Image(systemName: model.document.layers[index].isLocked ? "lock.fill" : "lock.open")
            }.help("Lock pixels")
            if editing {
                TextField("Layer name", text: $name).focused($focused)
                    .onSubmit { finish() }
                    .onExitCommand { editing = false; focused = false }
                    .onChange(of: focused) { if !$0 { finish() } }
            } else {
                Text(model.document.layers[index].name).lineLimit(1).frame(maxWidth: .infinity, alignment: .leading)
                    .onTapGesture(count: 2) {
                        model.selectLayer(index)
                        name = model.document.layers[index].name
                        editing = true
                        focused = true
                    }
                    .onTapGesture { model.selectLayer(index) }
            }
        }.buttonStyle(.plain).padding(5)
            .background(model.selectedLayer == index ? Color.orange.opacity(0.2) : Color.secondary.opacity(0.08))
    }

    private func finish() {
        guard editing else { return }
        editing = false
        model.renameLayer(index, name: name)
    }
}

@MainActor
struct PixelPlaybackControls: View {
    @ObservedObject var model: PixelEditorModel
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Picker("Playback", selection: Binding(get: { model.document.playbackMode }, set: { value in model.change { $0.playbackMode = value } })) {
                ForEach(PixelPlaybackMode.allCases) { Text($0.title).tag($0) }
            }
            if model.document.playbackMode == .range {
                Stepper("First: \(model.document.playbackStart + 1)", value: Binding(get: { model.document.playbackStart + 1 }, set: { value in
                    model.change { $0.playbackStart = value - 1; $0.clampPlaybackRange() }
                }), in: 1...model.document.frameCount)
                Stepper("Last: \((model.document.playbackEnd ?? (model.document.frameCount - 1)) + 1)", value: Binding(get: { (model.document.playbackEnd ?? (model.document.frameCount - 1)) + 1 }, set: { value in
                    model.change { $0.playbackEnd = value - 1; $0.clampPlaybackRange() }
                }), in: (model.document.playbackStart + 1)...model.document.frameCount)
            }
            Text("Frame \(model.selectedFrame + 1)").font(.caption.weight(.semibold))
            Toggle("Include in playback", isOn: Binding(get: { model.document.settings(at: model.selectedFrame).isVisible }, set: { _ in model.toggleFrameVisibility(model.selectedFrame) }))
            Toggle("Use FPS duration", isOn: Binding(get: { model.document.settings(at: model.selectedFrame).durationMS == nil }, set: { inherit in
                model.setFrameDuration(inherit ? nil : Int((model.document.duration(at: model.selectedFrame) * 1000).rounded()))
            }))
            if model.document.settings(at: model.selectedFrame).durationMS != nil {
                HStack {
                    Text("Duration (ms)")
                    TextField("Milliseconds", value: Binding(get: { model.document.settings(at: model.selectedFrame).durationMS ?? 100 }, set: { model.setFrameDuration($0) }), format: .number)
                        .frame(width: 64)
                }
                Text("10–60,000 ms").font(.caption2).foregroundStyle(.secondary)
            }
        }.font(.caption)
    }
}
