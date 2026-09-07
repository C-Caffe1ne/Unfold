using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class EditorWindow : Window
{
    public EditorSession Session { get; private set; }
    public PixelCanvas Canvas { get; private set; }
    private readonly CharacterLibrary library;
    private readonly Action<CharacterPackage> onSaved;
    private readonly StackPanel timeline = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly ListBox layers = new() { Height = 150 };
    private readonly TextBox layerName = new();
    private readonly TextBox hex = new() { PlaceholderText = "#AARRGGBB", MaxLength = 9, Width = 125 };
    private readonly TextBlock status = Ui.Text("");
    private readonly Image preview = new() { Height = 160, Width = 180, Stretch = Stretch.Uniform };
    private readonly ScrollViewer canvasHost = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    private readonly NumericUpDown fps = new() { Minimum = 1, Maximum = 24, Increment = 1, FormatString = "0", Width = 120 };
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, TickFrequency = 0.05, IsSnapToTickEnabled = true };
    private readonly DispatcherTimer previewTimer = new();
    private readonly List<(Image Image, Bitmap Bitmap, uint[] Pixels)> thumbnails = [];
    private int previewFrame;
    private bool updating, busy, closing, forceClose, playing;
    private readonly Button undo, redo, play;

    public EditorWindow(CharacterLibrary library, EditorSession session, Action<CharacterPackage> onSaved)
    {
        this.library = library; this.onSaved = onSaved; Session = session; Canvas = new(session);
        Width = 1140; Height = 820; MinWidth = 980; MinHeight = 700; Background = Ui.Background;
        Title = "Unfold · Pixel Editor";
        undo = Ui.Button("Undo", () => Session.Undo()); redo = Ui.Button("Redo", () => Session.Redo());
        play = Ui.Button("Play", () => { playing = !playing; if (playing) previewTimer.Start(); else previewTimer.Stop(); UpdatePreview(); });
        previewTimer.Tick += (_, _) => { previewFrame = (previewFrame + 1) % Session.Document.FrameCount; UpdatePreview(); };
        var header = Ui.Row(Ui.Text("PIXEL EDITOR", 18, Ui.Accent), undo, redo,
            Ui.AsyncButton("New", NewDocument), Ui.AsyncButton("Open…", OpenDocument),
            Ui.AsyncButton("Export Piskel", () => Export(true)), Ui.AsyncButton("Export PNG", () => Export(false)),
            Ui.AsyncButton("Save character", async () => { await SaveDocument(); }));
        var tools = new StackPanel { Spacing = 6, Width = 110 };
        foreach (var tool in Enum.GetValues<PixelTool>())
        {
            var button = Ui.Button(tool.ToString(), () => { Session.EndStroke(); Session.Tool = tool; Canvas.Focus(); Refresh(); });
            tools.Children.Add(button);
        }
        var brush = new ComboBox { ItemsSource = Enumerable.Range(1, 8).ToArray(), SelectedIndex = 0 };
        brush.SelectionChanged += (_, _) => Session.Brush = brush.SelectedIndex + 1;
        tools.Children.Add(Ui.Text("Brush size", 12)); tools.Children.Add(brush);
        var grid = new CheckBox { Content = "Grid", IsChecked = true };
        grid.IsCheckedChanged += (_, _) => { Canvas.Grid = grid.IsChecked == true; Canvas.Refresh(); };
        var onion = new CheckBox { Content = "Onion skin" };
        onion.IsCheckedChanged += (_, _) => { Canvas.OnionSkin = onion.IsChecked == true; Canvas.Refresh(); };
        var zoomLabel = Ui.Text("800%", 12);
        var zoom = Ui.Row(Ui.Button("−", () => { Canvas.Zoom--; zoomLabel.Text = $"{Canvas.Zoom * 100}%"; }), zoomLabel,
            Ui.Button("+", () => { Canvas.Zoom++; zoomLabel.Text = $"{Canvas.Zoom * 100}%"; }));
        tools.Children.Add(grid); tools.Children.Add(onion); tools.Children.Add(zoom);
        tools.Children.Add(Ui.AsyncButton("Resize…", ResizeCanvas));
        tools.Children.Add(Ui.Button("Flip ↔", () => Session.Edit(d => d.Flip(Session.Layer, Session.Frame, true))));
        tools.Children.Add(Ui.Button("Flip ↕", () => Session.Edit(d => d.Flip(Session.Layer, Session.Frame, false))));
        tools.Children.Add(Ui.Button("Clear frame", () => Session.Edit(d => Array.Clear(d.Layers[Session.Layer].Frames[Session.Frame]))));
        canvasHost.Content = new Border { Padding = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = Canvas };
        var frameActions = Ui.Row(Ui.Text("FRAMES", 12, Ui.Accent),
            Ui.Button("+", () => AddFrame(false)), Ui.Button("Duplicate", () => AddFrame(true)),
            Ui.Button("Delete", () => Session.Edit(d => d.DeleteFrame(Session.Frame))),
            Ui.Button("←", () => MoveFrame(-1)), Ui.Button("→", () => MoveFrame(1)));
        var framesPanel = Ui.Column(frameActions, new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = timeline, Height = 94 });
        var center = new DockPanel(); DockPanel.SetDock(framesPanel, Dock.Bottom); center.Children.Add(framesPanel); center.Children.Add(canvasHost);
        var palette = new WrapPanel { MaxWidth = 200 };
        foreach (var color in new uint[] { 0xFF171923, 0xFFFFFFFF, 0xFF9195A3, 0xFFD74949, 0xFFF4B860, 0xFFF3E7A2, 0xFF72B883, 0xFF4B8CBF, 0xFF8A6CBF, 0xFFE89FB6, 0xFF805E49, 0 })
        {
            var button = Ui.Button(color == 0 ? "×" : "", () => { Session.Color = color; Refresh(); });
            button.Background = new SolidColorBrush(Color.FromUInt32(color)); button.Width = 28; button.Height = 28; button.MinWidth = 0; button.Padding = new Thickness(0); button.Margin = new Thickness(2); palette.Children.Add(button);
        }
        var setColor = Ui.Button("Apply", () =>
        {
            var raw = (hex.Text ?? "").Trim().TrimStart('#');
            if (raw.Length == 6) raw = "FF" + raw;
            if (raw.Length == 8 && uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color)) { Session.Color = color; Refresh(); }
            else status.Text = "Enter #RRGGBB or #AARRGGBB.";
        });
        fps.ValueChanged += (_, _) => { if (!updating && fps.Value is decimal value) Session.Edit(d => d.Fps = (double)value); };
        layers.SelectionChanged += (_, _) => { if (!updating && layers.SelectedIndex >= 0) { Session.EndStroke(); Session.Layer = Session.Document.Layers.Count - 1 - layers.SelectedIndex; Refresh(); } };
        var rename = Ui.Button("Rename", () => { var name = layerName.Text?.Trim(); if (!string.IsNullOrEmpty(name)) Session.Edit(d => d.Layers[Session.Layer].Name = name); });
        opacity.ValueChanged += (_, _) => { if (!updating) Session.Edit(d => d.Layers[Session.Layer].Opacity = opacity.Value); };
        var inspector = Ui.Column(Ui.Text("PREVIEW", 12, Ui.Accent), preview, Ui.Row(play, fps, Ui.Text("FPS", 12)),
            Ui.Text("COLOR", 12, Ui.Accent), palette, Ui.Row(hex, setColor), Ui.Text("LAYERS", 12, Ui.Accent), layers,
            Ui.Row(Ui.Button("+", () => { Session.Edit(d => d.AddLayer()); Session.Layer = Session.Document.Layers.Count - 1; Refresh(); }),
                Ui.Button("Delete", () => Session.Edit(d => { if (d.Layers.Count > 1) d.Layers.RemoveAt(Session.Layer); })),
                Ui.Button("↑", () => MoveLayer(1)), Ui.Button("↓", () => MoveLayer(-1))), layerName, rename,
            Ui.Text("Opacity", 12), opacity);
        var content = new Grid { ColumnDefinitions = new("122,*,224"), ColumnSpacing = 10 };
        content.Children.Add(tools); Grid.SetColumn(center, 1); content.Children.Add(center);
        var right = new ScrollViewer { Content = inspector }; Grid.SetColumn(right, 2); content.Children.Add(right);
        var root = new DockPanel { Margin = new Thickness(14), LastChildFill = true };
        header.Margin = new Thickness(0, 0, 0, 14); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        status.Margin = new Thickness(0, 10, 0, 0); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status); root.Children.Add(content); Content = root;
        Session.Changed += Refresh; Refresh();
        Closing += async (_, e) =>
        {
            if (forceClose) return; e.Cancel = true; if (closing || busy) return;
            closing = true;
            try { if (await CanCloseDocument()) { forceClose = true; Close(); } } finally { closing = false; }
        };
        Closed += (_, _) => { previewTimer.Stop(); Session.Changed -= Refresh; Canvas.Dispose(); ClearThumbnails(); };
        Deactivated += (_, _) => { Session.EndStroke(); playing = false; previewTimer.Stop(); UpdatePreview(); };
        KeyDown += OnKey;
    }
    private void OnKey(object? sender, KeyEventArgs e)
    {
        // Text boxes keep their own editing shortcuts.
        if (e.Source is TextBox || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        var command = e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        if (command)
        {
            if (e.Key == Key.Z) { if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Session.Redo(); else Session.Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { Session.Redo(); e.Handled = true; }
            else if (e.Key == Key.S) { _ = SaveDocument(); e.Handled = true; }
            return;
        }
        var tool = e.Key switch { Key.B => PixelTool.Pencil, Key.E => PixelTool.Eraser, Key.F => PixelTool.Fill,
            Key.I => PixelTool.Eyedropper, Key.L => PixelTool.Line, Key.R => PixelTool.Rectangle, Key.O => PixelTool.Ellipse, _ => (PixelTool?)null };
        if (tool.HasValue) { Session.EndStroke(); Session.Tool = tool.Value; Refresh(); e.Handled = true; }
    }
    private void AddFrame(bool duplicate)
    {
        if (Session.Document.FrameCount >= PixelDocument.MaxFrames) return;
        var index = Session.Frame; Session.Edit(d => d.AddFrame(index, duplicate)); Session.Frame = index + 1; Refresh();
    }
    private void MoveFrame(int delta)
    {
        var from = Session.Frame; var to = from + delta;
        if (to < 0 || to >= Session.Document.FrameCount) return;
        Session.Edit(d => d.MoveFrame(from, to)); Session.Frame = to; Refresh();
    }
    private void MoveLayer(int delta)
    {
        var from = Session.Layer; var to = from + delta;
        if (to < 0 || to >= Session.Document.Layers.Count) return;
        Session.Edit(d => (d.Layers[from], d.Layers[to]) = (d.Layers[to], d.Layers[from])); Session.Layer = to; Refresh();
    }
    private void ClearThumbnails() { foreach (var item in thumbnails) item.Bitmap.Dispose(); thumbnails.Clear(); timeline.Children.Clear(); }
    private void Refresh()
    {
        if (updating) return; updating = true;
        try
        {
            var doc = Session.Document; Title = $"{doc.Name}{(Session.IsDirty ? " *" : "")} — Unfold Pixel Editor";
            status.Text = $"{doc.Width} × {doc.Height} px  ·  Frame {Session.Frame + 1}/{doc.FrameCount}  ·  {Session.Tool}  ·  {(Session.IsDirty ? "Unsaved changes" : "Saved")}";
            undo.IsEnabled = Session.CanUndo; redo.IsEnabled = Session.CanRedo;
            fps.Value = (decimal)doc.Fps; previewTimer.Interval = TimeSpan.FromSeconds(1 / doc.Fps);
            var names = doc.Layers.AsEnumerable().Reverse().Select(l => l.Name).ToArray();
            if (layers.ItemsSource is not string[] previous || !previous.SequenceEqual(names)) layers.ItemsSource = names;
            layers.SelectedIndex = doc.Layers.Count - 1 - Session.Layer;
            layerName.Text = doc.Layers[Session.Layer].Name; opacity.Value = doc.Layers[Session.Layer].Opacity;
            hex.Text = $"#{Session.Color:X8}";
            if (thumbnails.Count != doc.FrameCount) ClearThumbnails();
            for (var i = 0; i < doc.FrameCount; i++)
            {
                var pixels = Session.Composite(i);
                if (thumbnails.Count <= i)
                {
                    var index = i; var bitmap = Ui.Bitmap(new(doc.Width, doc.Height, pixels));
                    var image = new Image { Source = bitmap, Width = 50, Height = 50, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
                    var button = Ui.Button("", () => { Session.EndStroke(); Session.Frame = index; Refresh(); });
                    button.Content = Ui.Column(image, Ui.Text($"{index + 1}", 11)); button.Padding = new Thickness(5);
                    timeline.Children.Add(button); thumbnails.Add((image, bitmap, (uint[])pixels.Clone()));
                }
                else if (!thumbnails[i].Pixels.AsSpan().SequenceEqual(pixels) || thumbnails[i].Bitmap.PixelSize.Width != doc.Width || thumbnails[i].Bitmap.PixelSize.Height != doc.Height)
                {
                    var item = thumbnails[i]; var bitmap = Ui.Bitmap(new(doc.Width, doc.Height, pixels));
                    item.Image.Source = bitmap; item.Bitmap.Dispose(); thumbnails[i] = (item.Image, bitmap, (uint[])pixels.Clone());
                }
                ((Button)timeline.Children[i]).BorderBrush = i == Session.Frame ? Ui.Accent : Brushes.Transparent;
                ((Button)timeline.Children[i]).BorderThickness = new Thickness(2);
            }
            Canvas.Refresh(); UpdatePreview();
        }
        finally { updating = false; }
    }
    private void UpdatePreview()
    {
        var doc = Session.Document; var index = playing ? previewFrame % doc.FrameCount : Session.Frame;
        if (thumbnails.Count > index) { preview.Source = thumbnails[index].Bitmap; }
        RenderOptions.SetBitmapInterpolationMode(preview, BitmapInterpolationMode.None); play.Content = playing ? "Pause" : "Play";
    }
    private async Task NewDocument() { if (await CanCloseDocument()) Replace(new PixelDocument()); }
    private void Replace(PixelDocument doc)
    {
        var previous = Session; var zoom = Canvas.Zoom; var grid = Canvas.Grid; var onion = Canvas.OnionSkin;
        playing = false; previewTimer.Stop(); Session.Changed -= Refresh; Canvas.Dispose(); ClearThumbnails();
        Session = new(doc) { Tool = previous.Tool, Color = previous.Color, Brush = previous.Brush };
        Canvas = new(Session) { Zoom = zoom, Grid = grid, OnionSkin = onion }; canvasHost.Content = new Border { Padding = new Thickness(24), Child = Canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Session.Changed += Refresh; Refresh();
    }
    public async Task<bool> CanCloseDocument()
    {
        if (busy) return false; Session.EndStroke(); if (!Session.IsDirty) return true;
        var choice = await Ui.Confirm(this, "Save your pixel art?", "Discarding changes cannot be undone after closing this document.", "Save", "Discard", "Cancel");
        return choice == 1 || choice == 0 && await SaveDocument();
    }
    public void CloseAfterApproval() { forceClose = true; Close(); }
    public async Task<bool> SaveDocument()
    {
        if (busy) return false; Session.EndStroke(); busy = true;
        try
        {
            var name = Session.CharacterId is null ? await Ui.Prompt(this, "Name your character", Session.Document.Name) : Session.Document.Name;
            if (name is null) return false;
            IsEnabled = false;
            var snapshot = Session.Document.Clone(); snapshot.Name = name;
            var result = await Task.Run(() => library.Save(snapshot, Session.CharacterId, Session.Revision));
            Session.CharacterId = result.Manifest.Id;
            // A committed save stays successful even if reading its new revision fails.
            try { Session.Revision = CharacterLibrary.Revision(result.DirectoryPath); } catch (IOException error) { Session.Revision = null; AppPaths.Log(error); }
            Session.Edit(d => d.Name = name); Session.MarkSaved(); onSaved(result); return true;
        }
        catch (Exception error) { await Ui.Error(this, error); return false; }
        finally { busy = false; IsEnabled = true; }
    }
    private async Task OpenDocument()
    {
        if (busy) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open Piskel or PNG", AllowMultiple = false,
                FileTypeFilter = [new("Pixel art") { Patterns = ["*.piskel", "*.png"] }] });
            var path = files.FirstOrDefault()?.TryGetLocalPath(); if (path is null) return;
            var doc = await Task.Run(() => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? PiskelCodec.ImportPng(path) : PiskelCodec.Load(path));
            if (await CanCloseDocument()) Replace(doc);
        }
        catch (Exception error) { await Ui.Error(this, error); }
    }
    private async Task Export(bool piskel)
    {
        try
        {
            Session.EndStroke(); var snapshot = Session.Document.Clone();
            var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export pixel art", SuggestedFileName = piskel ? "character.piskel" : "spritesheet.png",
                DefaultExtension = piskel ? "piskel" : "png", ShowOverwritePrompt = true });
            var path = file?.TryGetLocalPath(); if (path is null) return;
            await Task.Run(() => AtomicFile.Write(path, piskel ? PiskelCodec.Encode(snapshot) : ImageCodec.EncodePng(PiskelCodec.CompositeSheet(snapshot))));
        }
        catch (Exception error) { await Ui.Error(this, error); }
    }
    private async Task ResizeCanvas()
    {
        var size = await Ui.Prompt(this, "Canvas size (width × height)", $"{Session.Document.Width}x{Session.Document.Height}");
        if (size is null) return;
        var parts = size.ToLowerInvariant().Replace('×', 'x').Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height)) return;
        try
        {
            PixelDocument.CheckSize(width, height);
            if ((width < Session.Document.Width || height < Session.Document.Height) &&
                await Ui.Confirm(this, "Crop the canvas?", "Pixels outside the new size will be cropped from every layer and frame. Undo can restore them.", "Crop", "Cancel") != 0) return;
            Session.Edit(d => d.Resize(width, height));
        }
        catch (Exception error) { await Ui.Error(this, error); }
    }
}
