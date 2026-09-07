using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Unfold.Core;

namespace Unfold.Desktop;

public static class Ui
{
    public static readonly IBrush Background = Brush.Parse("#141820"), Panel = Brush.Parse("#1E2430"), Accent = Brush.Parse("#F4B860");
    public static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6) };
        button.Click += (_, _) => action(); return button;
    }
    public static Button AsyncButton(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6) };
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } finally { button.IsEnabled = true; } }; return button;
    }
    public static TextBlock Text(string text, double size = 14, IBrush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Brushes.White, VerticalAlignment = VerticalAlignment.Center };
    public static StackPanel Row(params Control[] controls) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }.With(controls);
    public static StackPanel Column(params Control[] controls) => new StackPanel { Spacing = 12 }.With(controls);
    private static StackPanel With(this StackPanel panel, Control[] controls) { foreach (var control in controls) panel.Children.Add(control); return panel; }
    public static unsafe Bitmap Bitmap(PixelImage image)
    {
        // Transfer raw pixels once; avoid PNG encoding/decoding on every
        // thumbnail edit and on every animation consumer's first load.
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var locked = bitmap.Lock();
        for (var y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(new Span<uint>((byte*)locked.Address + y * locked.RowBytes, image.Width));
        return bitmap;
    }

    public static async Task<int> Confirm(Window owner, string title, string message, params string[] choices)
    {
        var dialog = new Window { Title = title, Width = 440, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var result = -1;
        for (var i = 0; i < choices.Length; i++) { var index = i; buttons.Children.Add(Button(choices[i], () => { result = index; dialog.Close(); })); }
        dialog.Content = new Border { Padding = new Thickness(22), Child = Column(Text(title, 20, Accent),
            new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12) }, buttons) };
        // Closing the dialog using its titlebar must always cancel, never select Save/Delete.
        await dialog.ShowDialog(owner); return result;
    }
    public static Task Error(Window owner, Exception error)
    {
        AppPaths.Log(error); return Confirm(owner, "Unfold", error.Message, "OK");
    }
    public static async Task<string?> Prompt(Window owner, string title, string value)
    {
        var input = new TextBox { Text = value, MinWidth = 280 };
        var dialog = new Window { Title = title, Width = 380, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.Content = new Border { Padding = new Thickness(22), Child = Column(Text(title, 20, Accent), input,
            Row(Button("Cancel", () => dialog.Close()), Button("Save", () => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.Close(input.Text.Trim()); }))) };
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return await dialog.ShowDialog<string?>(owner);
    }
}
