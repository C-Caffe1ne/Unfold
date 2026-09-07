using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unfold-tests-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() { Directory.CreateDirectory(Path); }
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
}

public class PixelTests
{
    [Fact] public void FastStrokeAndSingleErasePreserveOtherPixels()
    {
        var doc = new PixelDocument(8, 8); doc.Draw(PixelTool.Pencil, new(0, 0), new(7, 7), 0xFFFF0000, 1, 0, 0);
        for (var i = 0; i < 8; i++) Assert.Equal(0xFFFF0000, doc.Composite(0)[i * 8 + i]);
        doc.Draw(PixelTool.Eraser, new(3, 3), new(3, 3), 0, 1, 0, 0);
        Assert.Equal(7, doc.Composite(0).Count(p => p != 0));
    }
    [Fact] public void FloodFillRespectsBoundary()
    {
        var doc = new PixelDocument(7, 7); doc.Draw(PixelTool.Rectangle, new(1, 1), new(5, 5), 0xFFFFFFFF, 1, 0, 0);
        doc.Draw(PixelTool.Fill, new(3, 3), new(3, 3), 0xFF00FF00, 1, 0, 0);
        Assert.Equal(9, doc.Composite(0).Count(p => p == 0xFF00FF00)); Assert.Equal(0u, doc.Composite(0)[0]);
    }
    [Fact] public void LayerOpacityMatchesExpectedBlend()
    {
        var doc = new PixelDocument(1, 1); doc.Layers[0].Frames[0][0] = 0xFFFF0000;
        doc.AddLayer(); doc.Layers[1].Frames[0][0] = 0xFF0000FF; doc.Layers[1].Opacity = 0.5;
        Assert.Equal(0xFF800080, doc.Composite(0)[0]); doc.Layers[1].Opacity = 0; Assert.Equal(0xFFFF0000, doc.Composite(0)[0]);
    }
    [Fact] public void DuplicateFramesDoNotAliasAndEveryLayerRemainsAligned()
    {
        var doc = new PixelDocument(2, 1); doc.AddLayer(); doc.Layers[0].Frames[0][0] = 0xFFFF0000; doc.AddFrame(0, true);
        doc.Layers[0].Frames[1][0] = 0xFF00FF00;
        Assert.Equal(0xFFFF0000, doc.Layers[0].Frames[0][0]); doc.MoveFrame(1, 0); Assert.Equal(0xFF00FF00, doc.Layers[0].Frames[0][0]);
        doc.DeleteFrame(1); doc.DeleteFrame(0); Assert.All(doc.Layers, l => Assert.Single(l.Frames));
    }
    [Fact] public void ResizeAndFlipPreserveTopLeftGeometry()
    {
        var doc = new PixelDocument(2, 2); doc.Layers[0].Frames[0] = [1, 2, 3, 4]; doc.Resize(3, 2);
        Assert.Equal(new uint[] { 1, 2, 0, 3, 4, 0 }, doc.Layers[0].Frames[0]); doc.Flip(0, 0, true);
        Assert.Equal(new uint[] { 0, 2, 1, 0, 4, 3 }, doc.Layers[0].Frames[0]);
    }
    [Theory] [InlineData(0, 1)] [InlineData(1, 129)] [InlineData(int.MaxValue, 2)]
    public void InvalidSizeRejectedBeforeAllocation(int width, int height) => Assert.Throws<InvalidDataException>(() => new PixelDocument(width, height));
    [Fact] public void StrokeUndoIsOneTransactionAndBranchDropsRedo()
    {
        var session = new EditorSession(new(8, 8)); session.BeginStroke(new(0, 0)); session.ContinueStroke(new(7, 7)); session.EndStroke();
        var result = session.Document.Clone(); session.Undo(); Assert.False(session.IsDirty); Assert.False(session.CanUndo);
        session.Redo(); Assert.True(session.Document.ContentEquals(result)); session.MarkSaved(); Assert.False(session.IsDirty);
        session.Undo(); Assert.True(session.IsDirty); session.Edit(d => d.AddFrame(0, true)); Assert.False(session.CanRedo);
    }
    [Fact] public void ShapePreviewReplacesPreviousOutline()
    {
        var session = new EditorSession(new(8, 8)) { Tool = PixelTool.Rectangle };
        session.BeginStroke(new(0, 0)); session.ContinueStroke(new(3, 3)); session.ContinueStroke(new(5, 5)); session.EndStroke();
        Assert.Equal(0u, session.Composite(0)[27]); Assert.NotEqual(0u, session.Composite(0)[45]);
    }
    [Fact] public void CanvasReentryDoesNotBridgeOutsideGap()
    {
        var session = new EditorSession(new(8, 8)); session.BeginStroke(new(0, 0)); session.ContinueStroke(new(-1, -1)); session.ContinueStroke(new(7, 7)); session.EndStroke();
        Assert.Equal(2, session.Composite(0).Count(p => p != 0));
    }
    [Fact] public void UndoClampsSelectionAndHistoryStaysBounded()
    {
        var session = new EditorSession(new(64, 64)); session.Edit(d => d.AddLayer()); session.Layer = 1; session.Undo(); Assert.Equal(0, session.Layer);
        for (var i = 0; i < 140; i++) { var index = i; session.Edit(d => d.Layers[0].Frames[0][0] = (uint)index); }
        Assert.True(session.HistoryBytes <= 32 * 1024 * 1024);
        var undos = 0; while (session.CanUndo) { session.Undo(); undos++; } Assert.InRange(undos, 1, 100);
    }
}

public class CodecTests
{
    internal static PixelDocument Fixture()
    {
        var doc = new PixelDocument(2, 3) { Name = "마리 \"pixel\"", Description = "Test" };
        doc.Layers[0].Frames[0] = [0xFFFF0000, 0, 0xFF00FF00, 0x80FF0000, 0, 0xFF0000FF];
        doc.AddFrame(0, true); doc.Layers[0].Frames[1][0] = 0xFFFFFFFF; doc.AddLayer();
        doc.Layers[1].Opacity = 0.5; doc.Layers[1].Frames[0][0] = 0xFF0000FF; return doc;
    }
    [Fact] public void PiskelRoundTripPreservesLayerFramesAndOrientation()
    { var doc = Fixture(); Assert.True(PiskelCodec.Decode(PiskelCodec.Encode(doc)).ContentEquals(doc)); }
    [Fact] public void PngPreservesUnpremultipliedLowAlphaSamples()
    {
        uint[] pixels = [0xFFFF0000, 0x80112233, 0x01123456, 0xFF00FF00, 0, 0xFF0000FF];
        var result = ImageCodec.DecodePng(ImageCodec.EncodePng(new(2, 3, pixels)));
        Assert.Equal(pixels, result.Pixels); Assert.Equal(2, result.Width); Assert.Equal(3, result.Height);
    }
    [Fact] public void ExportedSheetEqualsPreview()
    {
        var doc = Fixture(); var sheet = ImageCodec.DecodePng(ImageCodec.EncodePng(PiskelCodec.CompositeSheet(doc)));
        for (var f = 0; f < doc.FrameCount; f++) for (var y = 0; y < doc.Height; y++) for (var x = 0; x < doc.Width; x++)
            Assert.Equal(doc.Composite(f)[y * doc.Width + x], sheet.Pixels[y * sheet.Width + f * doc.Width + x]);
    }
    private static byte[] Source(int[][] layout, string png, int count = 4)
    {
        var layer = JsonSerializer.Serialize(new { name = "Layer", opacity = 1, frameCount = count, chunks = new[] { new { layout, base64PNG = png } } });
        return JsonSerializer.SerializeToUtf8Bytes(new { modelVersion = 2, piskel = new { width = 1, height = 1, fps = 12, layers = new[] { layer } } });
    }
    [Fact] public void ColumnMajorPiskelLayoutIsRespected()
    {
        uint a = 0xFFFF0000, b = 0xFF00FF00, c = 0xFF0000FF, d = 0xFFFFFFFF;
        var png = "data:image/png;base64," + Convert.ToBase64String(ImageCodec.EncodePng(new(2, 2, [a, c, b, d])));
        var doc = PiskelCodec.Decode(Source([[0, 1], [2, 3]], png)); Assert.Equal(new[] { a, b, c, d }, doc.Layers[0].Frames.Select(f => f[0]));
    }
    [Fact] public void CorruptLayoutsAndTruncatedPngAreRejected()
    {
        var bytes = ImageCodec.EncodePng(new(2, 2, new uint[4])); var png = "data:image/png;base64," + Convert.ToBase64String(bytes);
        Assert.Throws<InvalidDataException>(() => PiskelCodec.Decode(Source([[0, 1], [2, 2]], png)));
        Assert.Throws<InvalidDataException>(() => PiskelCodec.Decode(Source([[0, 1], [2]], png)));
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePng(bytes[..^12]));
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePng(bytes, 1, 1));
    }
    [Fact] public void ActualBundledCatAndGifDecode()
    {
        var package = CharacterLibrary.LoadPackage(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "default-cat"), true);
        Assert.Equal(8, package.LoadAnimation("idle").Count);
        var gif = package.LoadAnimation("stretch"); Assert.True(gif.Count > 1);
        Assert.All(gif, f => Assert.True(f.Duration.TotalMilliseconds >= 20));
        Assert.Contains(gif[0].Image.Pixels, p => p >> 24 != 0);
    }
}

public class LibraryTests
{
    [Fact] public void SaveReloadOverwriteAndConflictDetection()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path); var doc = CodecTests.Fixture();
        var character = library.Save(doc); var opened = library.OpenForEditing(character.Manifest.Id);
        Assert.True(doc.ContentEquals(opened.Document));
        doc.Layers[0].Frames[0][0] = 0xFF123456;
        var replacement = library.Save(doc, character.Manifest.Id, opened.Revision); Assert.Equal(character.Manifest.Id, replacement.Manifest.Id);
        Assert.Single(library.List()); Assert.True(library.OpenForEditing(character.Manifest.Id).Document.ContentEquals(doc));
        Assert.Throws<IOException>(() => library.Save(doc, character.Manifest.Id, opened.Revision));
    }
    [Fact] public void DeletedCharacterIsNotSilentlyRecreatedByEditor()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path); var doc = CodecTests.Fixture();
        var saved = library.Save(doc); var revision = CharacterLibrary.Revision(saved.DirectoryPath); library.Delete(saved.Manifest.Id);
        Assert.Throws<IOException>(() => library.Save(doc, saved.Manifest.Id, revision)); Assert.Empty(library.List());
    }
    [Fact] public void RecoveryRestoresBackupAfterInterruptedDirectorySwap()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path); var saved = library.Save(CodecTests.Fixture());
        Directory.Move(saved.DirectoryPath, System.IO.Path.Combine(temp.Path, ".backup-" + saved.Manifest.Id));
        var stage = System.IO.Path.Combine(temp.Path, ".stage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage); File.WriteAllText(System.IO.Path.Combine(stage, "partial"), "incomplete");
        var recovered = new CharacterLibrary(temp.Path).List(); Assert.Single(recovered); Assert.True(Directory.Exists(saved.DirectoryPath)); Assert.False(Directory.Exists(stage));
    }
    [Theory] [InlineData("../outside.png")] [InlineData("C:\\outside.png")] [InlineData("/outside.png")] [InlineData("a/../../outside.png")] [InlineData("a\\..\\outside.png")]
    public void RejectsTraversal(string relative)
    { using var temp = new TempDirectory(); Assert.Throws<InvalidDataException>(() => CharacterLibrary.AssetPath(temp.Path, relative)); }
    [Fact] public void AtomicSettingsRoundTrip()
    {
        using var temp = new TempDirectory(); var file = System.IO.Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { IntervalMinutes = 45, PetX = -300, PetY = 100 }; settings.Save(file); Assert.Equal(settings, AppSettings.Load(file));
    }
}

public class ClockTests
{
    [Fact] public void TracksActiveTimeAndFiresOnce()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Tick(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        var fired = 0; for (var i = 1; i <= 305; i++) if (clock.Tick(TimeSpan.FromSeconds(i), TimeSpan.Zero, TimeSpan.FromMinutes(5))) fired++;
        Assert.Equal(1, fired); Assert.Equal(TimeSpan.FromSeconds(295), clock.Remaining);
    }
    [Fact] public void PausedIdleAndSleepGapsDoNotCount()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Reset(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(1)); clock.Tick(TimeSpan.FromSeconds(2), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(2)); clock.Tick(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.Tick(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromSeconds(299), clock.Remaining);
    }
    [Fact] public void IdleThresholdCrossingOnlyExcludesExcessTime()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Reset(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300.4), TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(299.4), clock.Remaining);
    }
}
