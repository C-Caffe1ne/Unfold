namespace Unfold.Core;

public sealed class EditorSession
{
    public PixelDocument Document { get; private set; }
    public PixelTool Tool { get; set; }
    public uint Color { get; set; } = 0xFFF4B860;
    public int Brush { get; set; } = 1;
    public int Frame { get; set; }
    public int Layer { get; set; }
    public string? CharacterId { get; set; }
    public string? Revision { get; set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsDirty => !Document.ContentEquals(saved);
    public long HistoryBytes => undo.Concat(redo).Sum(d => d.ByteCount);
    public event Action? Changed;
    private PixelDocument saved;
    private readonly List<PixelDocument> undo = [], redo = [];
    private PixelDocument? stroke;
    private PixelPoint? origin, previous;
    private PixelTool strokeTool;
    private int strokeLayer, strokeFrame, strokeBrush;
    private uint strokeColor;
    private readonly Dictionary<int, uint[]> compositeCache = [];

    public EditorSession(PixelDocument document) { document.Validate(); Document = document; saved = document.Clone(); }
    public uint[] Composite(int frame) => compositeCache.TryGetValue(frame, out var pixels) ? pixels : compositeCache[frame] = Document.Composite(frame);
    private void Notify(int? changedFrame = null)
    {
        if (changedFrame is int frame) compositeCache.Remove(frame); else compositeCache.Clear();
        Clamp(); Changed?.Invoke();
    }
    private void Clamp() { Frame = Math.Clamp(Frame, 0, Document.FrameCount - 1); Layer = Math.Clamp(Layer, 0, Document.Layers.Count - 1); }
    public void MarkSaved() { EndStroke(); saved = Document.Clone(); Changed?.Invoke(); }
    private void Remember(PixelDocument before)
    {
        undo.Add(before); redo.Clear();
        var bytes = undo.Sum(d => d.ByteCount);
        while (undo.Count > 1 && (undo.Count > 100 || bytes > 32 * 1024 * 1024)) { bytes -= undo[0].ByteCount; undo.RemoveAt(0); }
    }
    public void Edit(Action<PixelDocument> edit)
    {
        EndStroke(); var next = Document.Clone(); edit(next); next.Validate();
        if (next.ContentEquals(Document)) return;
        Remember(Document); Document = next; Notify();
    }
    public void Undo()
    {
        EndStroke(); if (undo.Count == 0) return;
        redo.Add(Document); Document = undo[^1]; undo.RemoveAt(undo.Count - 1); Notify();
    }
    public void Redo()
    {
        EndStroke(); if (redo.Count == 0) return;
        undo.Add(Document); Document = redo[^1]; redo.RemoveAt(redo.Count - 1); Notify();
    }
    public void BeginStroke(PixelPoint point)
    {
        EndStroke(); if (!Document.Contains(point)) return;
        if (Tool == PixelTool.Eyedropper) { Color = Composite(Frame)[point.Y * Document.Width + point.X]; Changed?.Invoke(); return; }
        stroke = Document.Clone(); origin = previous = point;
        strokeTool = Tool; strokeLayer = Layer; strokeFrame = Frame; strokeBrush = Brush; strokeColor = Color;
        ContinueStroke(point);
    }
    public void ContinueStroke(PixelPoint point)
    {
        if (stroke is null || origin is null) return;
        if (!Document.Contains(point)) { previous = null; return; }
        if (strokeTool == PixelTool.Fill && point != origin) return;
        var shape = strokeTool is PixelTool.Line or PixelTool.Rectangle or PixelTool.Ellipse;
        if (shape) Document.Layers[strokeLayer].Frames[strokeFrame] = (uint[])stroke.Layers[strokeLayer].Frames[strokeFrame].Clone();
        Document.Draw(strokeTool, shape ? origin.Value : previous ?? point, point, strokeColor, strokeBrush, strokeLayer, strokeFrame);
        previous = point; Notify(strokeFrame);
    }
    public void EndStroke()
    {
        if (stroke is null) return;
        if (!Document.ContentEquals(stroke)) Remember(stroke);
        stroke = null; origin = previous = null; Changed?.Invoke();
    }
}
