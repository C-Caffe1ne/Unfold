import AppKit
import SwiftUI

@MainActor
struct PixelTimelineView: View {
    @ObservedObject var model: PixelEditorModel
    @State private var drag: Drag?

    /// What a drag picked up. The token guards against a drop from somewhere
    /// else in the app, and the document snapshot against a drop onto a
    /// timeline that changed while the drag was in flight.
    private struct Drag {
        enum Kind { case frame, layer, cel }
        let token: String
        let kind: Kind
        /// Only meaningful for `.cel`.
        let layer: Int
        let index: Int
        let document: PixelDocument
    }

    private static let namePanelWidth: CGFloat = 186
    private static let cellSide: CGFloat = 58
    private static let headerHeight: CGFloat = 24

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 0) {
                Text("LAYERS").font(.caption.weight(.semibold))
                    .frame(width: Self.namePanelWidth, alignment: .leading)
                Text("FRAMES").font(.caption.weight(.semibold))
                Spacer()
                Button("Blank") { model.addFrame(duplicate: false) }.disabled(model.document.frameCount >= Constants.editorFrameCountRange.upperBound)
                    .pixelTooltip(PixelHelp.text("Blank Frame", "Inserts an empty frame after the selected one, in every layer at once."))
                Button("Duplicate") { model.addFrame(duplicate: true) }.disabled(model.document.frameCount >= Constants.editorFrameCountRange.upperBound)
                    .pixelTooltip(PixelHelp.text("Duplicate Frame", "Inserts a copy of the selected frame after it, in every layer at once."))
                Button("Delete Frame", action: model.deleteFrame).disabled(model.document.frameCount <= Constants.editorFrameCountRange.lowerBound)
                    .pixelTooltip(PixelHelp.text("Delete Frame", "Removes the selected frame from every layer. Undo restores it."))
                Button { model.moveFrame(by: -1) } label: { Image(systemName: "chevron.left") }
                    .pixelTooltip(PixelHelp.text("Move Frame Earlier", "Swaps the selected frame with the one before it, every layer following."))
                    .disabled(model.selectedFrame == 0)
                Button { model.moveFrame(by: 1) } label: { Image(systemName: "chevron.right") }
                    .pixelTooltip(PixelHelp.text("Move Frame Later", "Swaps the selected frame with the one after it, every layer following."))
                    .disabled(model.selectedFrame == model.document.frameCount - 1)
            }
            // One vertical scroll around both panels keeps their rows level;
            // only the frame side scrolls sideways, so the names stay put.
            ScrollView(.vertical) {
                HStack(alignment: .top, spacing: 0) {
                    layerPanel
                    Divider()
                    ScrollView(.horizontal) {
                        frameGrid.padding(.leading, 6)
                    }
                }
            }
        }.padding(10)
    }

    private var layerPanel: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 4) {
                Text("Double-click to rename").font(.caption2).foregroundStyle(.secondary)
                    .pixelTooltip(PixelHelp.text("Layers", "Topmost row draws in front. Drag a name to restack, double-click it to rename."))
                Spacer()
                Button(action: model.addLayer) { Image(systemName: "plus") }
                    .pixelTooltip(PixelHelp.text("Add Layer", "Adds an empty layer on top of the stack, with one cel per frame."))
                    .disabled(model.document.layers.count >= PixelDocument.maximumLayers)
                Button(action: model.deleteLayer) { Image(systemName: "trash") }
                    .pixelTooltip(PixelHelp.text("Delete Layer", "Removes the selected layer and all of its cels. Undo restores them."))
                    .disabled(model.document.layers.count <= 1)
            }
            .frame(width: Self.namePanelWidth, height: Self.headerHeight)
            ForEach(Array(model.document.layers.indices.reversed()), id: \.self) { layer in
                PixelLayerHeader(model: model, index: layer)
                    .frame(height: Self.cellSide)
                    .onDrag { beginDrag(.layer, index: layer) }
                    .dropDestination(for: String.self) { values, _ in drop(values, kind: .layer, at: layer) }
            }
        }.frame(width: Self.namePanelWidth, alignment: .leading)
    }

    private var frameGrid: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 4) {
                ForEach(0..<model.document.frameCount, id: \.self) { frame in
                    Text("\(frame + 1)" + (model.document.settings(at: frame).isVisible ? "" : " ◌"))
                        .font(.caption.monospacedDigit())
                        .frame(width: Self.cellSide, height: Self.headerHeight)
                        .background(model.selectedFrame == frame ? Color.orange.opacity(0.3) : Color.secondary.opacity(0.1))
                        .pixelTooltip(frameHelp(frame))
                        .onTapGesture { model.selectFrame(frame) }
                        .onDrag { beginDrag(.frame, index: frame) }
                        .dropDestination(for: String.self) { values, _ in drop(values, kind: .frame, at: frame) }
                }
            }
            ForEach(Array(model.document.layers.indices.reversed()), id: \.self) { layer in
                HStack(spacing: 4) {
                    ForEach(0..<model.document.frameCount, id: \.self) { frame in
                        cell(layer: layer, frame: frame)
                    }
                }
            }
        }
    }

    private func cell(layer: Int, frame: Int) -> some View {
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
            }.frame(width: Self.cellSide, height: Self.cellSide)
                .border(model.selectedLayer == layer && model.selectedFrame == frame ? Color.orange : Color.secondary.opacity(0.3), width: 2)
        }.buttonStyle(.plain)
            .accessibilityLabel("\(model.document.layers[layer].name), frame \(frame + 1)")
            .pixelTooltip(PixelHelp.text("\(model.document.layers[layer].name), frame \(frame + 1)",
                "Click to edit this cel; drag it to move it within its own layer alone."))
            .onDrag { beginDrag(.cel, layer: layer, index: frame) }
            .dropDestination(for: String.self) { values, _ in drop(values, kind: .cel, layer: layer, at: frame) }
    }

    /// The hollow circle marks a frame the playback skips, so the tooltip says
    /// what it means rather than leaving the reader to guess at the symbol.
    private func frameHelp(_ frame: Int) -> String {
        let excluded = model.document.settings(at: frame).isVisible
            ? "" : " ◌ marks it as left out of playback."
        return PixelHelp.text("Frame \(frame + 1)",
            "Click to select it; drag it to move the whole frame, every layer at once." + excluded)
    }

    private func beginDrag(_ kind: Drag.Kind, layer: Int = 0, index: Int) -> NSItemProvider {
        let token = UUID().uuidString
        drag = Drag(token: token, kind: kind, layer: layer, index: index, document: model.document)
        return NSItemProvider(object: token as NSString)
    }

    private func drop(_ values: [String], kind: Drag.Kind, layer: Int = 0, at index: Int) -> Bool {
        guard let source = drag, values == [source.token], source.kind == kind,
              source.document == model.document, source.index != index else { return false }
        // A cel belongs to one layer; dropping it on another would have to
        // overwrite whatever is already there, so that drop is refused.
        if kind == .cel && source.layer != layer { return false }
        drag = nil
        switch kind {
        case .frame: model.moveFrame(from: source.index, to: index)
        case .layer: model.moveLayer(from: source.index, to: index)
        case .cel: model.moveCel(layer: layer, from: source.index, to: index)
        }
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
    /// The field's rectangle in the window, used to tell a click on the field
    /// apart from a click anywhere else.
    @State private var fieldFrame: CGRect = .zero
    @State private var outsideClicks: Any?

    var body: some View {
        HStack(spacing: 4) {
            Button { model.toggleLayerVisibility(index) } label: {
                Image(systemName: model.document.layers[index].isVisible ? "eye" : "eye.slash")
            }.pixelTooltip(PixelHelp.text(model.document.layers[index].isVisible ? "Hide Layer" : "Show Layer",
                "Leaves the layer out of the canvas and the export. Its pixels are untouched and stay editable."))
            Button { model.toggleLayerLock(index) } label: {
                Image(systemName: model.document.layers[index].isLocked ? "lock.fill" : "lock.open")
            }.pixelTooltip(PixelHelp.text(model.document.layers[index].isLocked ? "Unlock Layer" : "Lock Layer",
                "A locked layer refuses drawing and cel moves. Selection tools still work on it."))
            if editing {
                TextField("Layer name", text: $name).focused($focused)
                    .pixelTooltip(PixelHelp.text("Layer name", key: "Return",
                        "Commits the new name. Escape, or a click anywhere else, discards it."))
                    .onSubmit { finish() }
                    .onExitCommand { cancel() }
                    // Clicking away is not a confirmation, so it discards the
                    // edit. Only Return commits a new name.
                    .onChange(of: focused) { if !$0 { editing = false } }
                    .background(GeometryReader { proxy in
                        Color.clear
                            .onAppear { fieldFrame = proxy.frame(in: .global) }
                            .onChange(of: proxy.frame(in: .global)) { fieldFrame = $0 }
                    })
            } else {
                Text(model.document.layers[index].name).lineLimit(1).frame(maxWidth: .infinity, alignment: .leading)
                    .pixelTooltip(PixelHelp.text(model.document.layers[index].name,
                        "Click to select the layer, double-click to rename it, drag it to restack it."))
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
            .onChange(of: editing) { $0 ? watchForOutsideClicks() : stopWatching() }
            .onDisappear(perform: stopWatching)
    }

    private func finish() {
        guard editing else { return }
        editing = false
        focused = false
        model.renameLayer(index, name: name)
    }

    private func cancel() {
        editing = false
        focused = false
    }

    /// Clicking a plain SwiftUI control on macOS does not take first
    /// responder, so the field kept focus and the rename stayed open however
    /// far away the user clicked. Watch the window's own mouse-downs instead;
    /// the event is passed along untouched, so the click still lands on
    /// whatever it hit.
    private func watchForOutsideClicks() {
        stopWatching()
        outsideClicks = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { event in
            let inWindow = event.locationInWindow
            let content = event.window?.contentView
            MainActor.assumeIsolated {
                guard let content else { return }
                let local = content.convert(inWindow, from: nil)
                // SwiftUI's global space measures down from the top-left; an
                // unflipped content view measures up from the bottom.
                let point = content.isFlipped
                    ? local : CGPoint(x: local.x, y: content.bounds.height - local.y)
                if !fieldFrame.contains(point) { cancel() }
            }
            return event
        }
    }

    private func stopWatching() {
        if let outsideClicks { NSEvent.removeMonitor(outsideClicks) }
        outsideClicks = nil
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
            .pixelTooltip(PixelHelp.text("Playback", "Play once, loop, bounce back and forth, or loop a chosen range of frames."))
            if model.document.playbackMode == .range {
                Stepper("First: \(model.document.playbackStart + 1)", value: Binding(get: { model.document.playbackStart + 1 }, set: { value in
                    model.change { $0.playbackStart = value - 1; $0.clampPlaybackRange() }
                }), in: 1...model.document.frameCount)
                    .pixelTooltip(PixelHelp.text("Range Start", "First frame of the looped range. It cannot pass the last."))
                Stepper("Last: \((model.document.playbackEnd ?? (model.document.frameCount - 1)) + 1)", value: Binding(get: { (model.document.playbackEnd ?? (model.document.frameCount - 1)) + 1 }, set: { value in
                    model.change { $0.playbackEnd = value - 1; $0.clampPlaybackRange() }
                }), in: (model.document.playbackStart + 1)...model.document.frameCount)
                    .pixelTooltip(PixelHelp.text("Range End", "Last frame of the looped range, included in it."))
            }
            Text("Frame \(model.selectedFrame + 1)").font(.caption.weight(.semibold))
            Toggle("Include in playback", isOn: Binding(get: { model.document.settings(at: model.selectedFrame).isVisible }, set: { _ in model.toggleFrameVisibility(model.selectedFrame) }))
                .pixelTooltip(PixelHelp.text("Include in playback", "Turning it off skips the frame during playback. The frame and its pixels stay editable."))
            Toggle("Use FPS duration", isOn: Binding(get: { model.document.settings(at: model.selectedFrame).durationMS == nil }, set: { inherit in
                model.setFrameDuration(inherit ? nil : Int((model.document.duration(at: model.selectedFrame) * 1000).rounded()))
            }))
                .pixelTooltip(PixelHelp.text("Use FPS duration", "On, the frame follows the document frame rate. Off, it holds for its own duration."))
            if model.document.settings(at: model.selectedFrame).durationMS != nil {
                HStack {
                    Text("Duration (ms)")
                    TextField("Milliseconds", value: Binding(get: { model.document.settings(at: model.selectedFrame).durationMS ?? 100 }, set: { model.setFrameDuration($0) }), format: .number)
                        .frame(width: 64)
                        .pixelTooltip(PixelHelp.text("Frame Duration", "How long this one frame holds, from 10 ms to 60 s. Anything outside that is refused."))
                }
                Text("10–60,000 ms").font(.caption2).foregroundStyle(.secondary)
            }
        }.font(.caption)
    }
}
