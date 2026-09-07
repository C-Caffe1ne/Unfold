import Combine
import Foundation

@MainActor
final class PixelEditorModel: ObservableObject {
    @Published private(set) var document: PixelDocument
    @Published var selectedFrame = 0
    @Published var selectedLayer = 0
    @Published var tool: PixelTool = .pencil
    @Published var color: UInt32 = 0xF4B860FF
    @Published var brushSize = 1
    @Published var zoom = 8
    @Published var showGrid = true
    @Published var onionSkin = false
    @Published var isPlaying = false
    @Published private(set) var canUndo = false
    @Published private(set) var canRedo = false
    private var savedDocument: PixelDocument
    private var undoStack: [PixelDocument] = []
    private var redoStack: [PixelDocument] = []
    private var strokeStart: PixelPoint?
    private var lastPoint: PixelPoint?
    private var strokeDocument: PixelDocument?
    private var strokeTool: PixelTool = .pencil
    private var strokeLayer = 0
    private var strokeFrame = 0
    private var strokeColor: UInt32 = 0
    private var strokeBrush = 1
    private let historyBudget = 32 * 1024 * 1024

    var isDirty: Bool { document != savedDocument }

    init(document: PixelDocument) {
        self.document = document
        savedDocument = document
        zoom = max(2, min(12, 512 / max(document.width, document.height)))
    }

    func markSaved() { savedDocument = document; objectWillChange.send() }

    func change(_ edit: (inout PixelDocument) -> Void) {
        endStroke()
        let before = document
        var next = document
        edit(&next)
        guard next != before else { return }
        remember(before)
        document = next
        clampSelection()
    }

    private func remember(_ before: PixelDocument) {
        undoStack.append(before)
        redoStack.removeAll()
        trim(&undoStack)
        refreshHistory()
    }

    private func trim(_ stack: inout [PixelDocument]) {
        var bytes = stack.reduce(0) { $0 + $1.byteCount }
        while stack.count > 1 && (bytes > historyBudget || stack.count > 100) {
            bytes -= stack.removeFirst().byteCount
        }
    }

    private func refreshHistory() {
        canUndo = !undoStack.isEmpty
        canRedo = !redoStack.isEmpty
    }

    private func clampSelection() {
        selectedFrame = min(max(selectedFrame, 0), document.frameCount - 1)
        selectedLayer = min(max(selectedLayer, 0), document.layers.count - 1)
    }

    func undo() {
        endStroke()
        guard let previous = undoStack.popLast() else { return }
        redoStack.append(document)
        document = previous
        clampSelection()
        refreshHistory()
    }

    func redo() {
        endStroke()
        guard let next = redoStack.popLast() else { return }
        undoStack.append(document)
        document = next
        clampSelection()
        refreshHistory()
    }

    func beginStroke(at point: PixelPoint) {
        endStroke()
        guard document.contains(point) else { return }
        isPlaying = false
        if tool == .eyedropper {
            color = document.compositedFrame(at: selectedFrame).pixels[point.y * document.width + point.x]
            return
        }
        strokeStart = point
        lastPoint = point
        strokeDocument = document
        strokeTool = tool
        strokeLayer = selectedLayer
        strokeFrame = selectedFrame
        strokeColor = color
        strokeBrush = brushSize
        continueStroke(at: point)
    }

    func continueStroke(at point: PixelPoint) {
        guard let start = strokeStart, let original = strokeDocument else { return }
        guard document.contains(point) else { lastPoint = nil; return }
        let previous = lastPoint ?? point
        if strokeTool == .fill && point != start { return }
        var next = document
        let isShape = [.line, .rectangle, .ellipse].contains(strokeTool)
        if isShape { next = original }
        next.draw(tool: strokeTool, from: isShape ? start : previous, to: point,
                  color: strokeColor, brush: strokeBrush, layer: strokeLayer, frame: strokeFrame)
        document = next
        lastPoint = point
    }

    /// Called for mouse-up, focus loss, save, and document actions.
    func endStroke() {
        if let before = strokeDocument, before != document { remember(before) }
        strokeStart = nil
        lastPoint = nil
        strokeDocument = nil
    }

    func addFrame(duplicate: Bool) {
        guard document.frameCount < Constants.editorFrameCountRange.upperBound else { return }
        let index = selectedFrame
        change { $0.insertFrame(after: index, duplicate: duplicate) }
        selectedFrame = index + 1
    }

    func deleteFrame() {
        let index = selectedFrame
        change { $0.removeFrame(at: index) }
    }

    func moveFrame(by delta: Int) {
        let from = selectedFrame, to = selectedFrame + delta
        guard (0..<document.frameCount).contains(to) else { return }
        change { $0.moveFrame(from: from, to: to) }
        selectedFrame = to
    }

    func addLayer() {
        guard document.layers.count < PixelDocument.maximumLayers else { return }
        change { doc in
            doc.layers.append(PixelLayer(name: "Layer \(doc.layers.count + 1)",
                frames: Array(repeating: PixelFrame(width: doc.width, height: doc.height), count: doc.frameCount)))
        }
        selectedLayer = document.layers.count - 1
    }

    func deleteLayer() {
        guard document.layers.count > 1 else { return }
        let index = selectedLayer
        change { $0.layers.remove(at: index) }
    }

    func moveLayer(by delta: Int) {
        let from = selectedLayer, to = selectedLayer + delta
        guard document.layers.indices.contains(to) else { return }
        change { $0.layers.swapAt(from, to) }
        selectedLayer = to
    }

    func clearFrame() {
        let layer = selectedLayer, frame = selectedFrame
        change { $0.layers[layer].frames[frame] = PixelFrame(width: $0.width, height: $0.height) }
    }

    func flip(horizontal: Bool) {
        let layer = selectedLayer, frame = selectedFrame
        change { $0.flip(layer: layer, frame: frame, horizontal: horizontal) }
    }
}
