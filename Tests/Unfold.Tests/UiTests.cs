using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

[assembly: AvaloniaTestApplication(typeof(Unfold.Tests.TestBootstrap))]

namespace Unfold.Tests;

public static class TestBootstrap
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false });
}
public sealed class TestApplication : Application
{
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Dark; Styles.Add(new FluentTheme()); }
}

public class UiTests
{
    [AvaloniaFact]
    public void MouseDrawingUndoAndSingleClickEraseWorkThroughRealControls()
    {
        using var temp = new TempDirectory(); var session = new EditorSession(new PixelDocument(8, 8));
        var window = new EditorWindow(new(temp.Path), session, _ => { }); window.Show();
        Dispatcher.UIThread.RunJobs();
        var canvas = window.Canvas;
        var first = canvas.TranslatePoint(new Point(4, 4), window)!.Value;
        var last = canvas.TranslatePoint(new Point(60, 4), window)!.Value;
        window.MouseDown(first, MouseButton.Left); window.MouseMove(last); window.MouseUp(last, MouseButton.Left);
        Assert.Equal(8, session.Composite(0).Count(p => p != 0));
        session.Tool = PixelTool.Eraser; window.MouseDown(first, MouseButton.Left); window.MouseUp(first, MouseButton.Left);
        Assert.Equal(7, session.Composite(0).Count(p => p != 0));
        session.Undo(); Assert.Equal(8, session.Composite(0).Count(p => p != 0));
        session.Undo(); Assert.False(session.IsDirty); window.CloseAfterApproval();
    }

    [AvaloniaFact]
    public void FrameAndLayerButtonsUpdateSessionWithoutSelectionCrashes()
    {
        using var temp = new TempDirectory(); var session = new EditorSession(new PixelDocument(8, 8));
        var window = new EditorWindow(new(temp.Path), session, _ => { }); window.Show(); Dispatcher.UIThread.RunJobs();
        var duplicate = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Duplicate"));
        duplicate.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, session.Document.FrameCount); Assert.Equal(1, session.Frame);
        session.Undo(); Assert.Equal(0, session.Frame); Assert.Single(session.Document.Layers[0].Frames);
        session.Edit(d => d.AddLayer()); session.Layer = 1; session.Undo(); Assert.Equal(0, session.Layer);
        window.CloseAfterApproval();
    }

    [AvaloniaFact]
    public void EditorRendersAtDefaultAndMinimumWindowSizes()
    {
        using var temp = new TempDirectory(); var doc = new PixelDocument();
        // Small pixel companion for layout verification, drawn by the editor's own tools.
        doc.Draw(PixelTool.Rectangle, new(17, 18), new(46, 49), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Fill, new(25, 25), new(25, 25), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Rectangle, new(17, 10), new(24, 22), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Fill, new(20, 14), new(20, 14), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Rectangle, new(39, 10), new(46, 22), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Fill, new(41, 14), new(41, 14), 0xFFF4B860, 1, 0, 0);
        doc.Draw(PixelTool.Pencil, new(25, 30), new(25, 31), 0xFF141820, 2, 0, 0);
        doc.Draw(PixelTool.Pencil, new(38, 30), new(38, 31), 0xFF141820, 2, 0, 0);
        var window = new EditorWindow(new(temp.Path), new(doc), _ => { }); window.Show(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); Assert.True(frame.PixelSize.Width >= 980);
        var artifacts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/verification"));
        Directory.CreateDirectory(artifacts); frame.Save(System.IO.Path.Combine(artifacts, "pixel-editor.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        window.Width = 980; window.Height = 700; Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var minimum = window.CaptureRenderedFrame(); Assert.NotNull(minimum); minimum.Save(System.IO.Path.Combine(artifacts, "pixel-editor-minimum.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        var save = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save character"));
        var rect = save.TranslatePoint(default, window)!.Value;
        Assert.True(rect.X >= 0 && rect.X + save.Bounds.Width <= window.ClientSize.Width, "Save button must fit inside minimum window width.");
        window.CloseAfterApproval();
    }
}
