using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Avalonia.Threading;
using SkiaSharp;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class NavigationIconTests
{
    [AvaloniaFact]
    public void BundledSvgSilhouettesKeepTheirViewportAndFollowSelectedThemeColors()
    {
        var window = new Window();
        try
        {
            foreach (var name in new[] { "Navigation/home", "Navigation/pet-add", "Navigation/history", "Navigation/settings", "Navigation/theme", "Navigation/exit",
                "Directions/up", "Directions/down", "Directions/left", "Directions/right",
                "Playback/play", "Playback/pause", "Playback/stop",
                "Sound/play", "Sound/stop", "Sound/volume-high", "Sound/volume-low", "Sound/volume-muted" })
            {
                SvgIcon icon = name.StartsWith("Navigation/") ? new NavigationIcon(name.Split('/')[1])
                    : name.StartsWith("Directions/") ? new DirectionIcon(Enum.Parse<IconDirection>(name.Split('/')[1], true))
                    : name.StartsWith("Playback/") ? new PlaybackIcon(Enum.Parse<PlaybackGlyph>(name.Split('/')[1], true))
                    : name.StartsWith("Sound/volume-") ? VolumeIcon(name)
                    : new SoundPreviewIcon { IsPlaying = name == "Sound/stop" };
                var button = Ui.Action(""); button.Width = button.Height = 46;
                button.Padding = new(9); button.Content = icon; window.Content = button; window.Show();
                var reference = new SvgReference(name) { Width = icon.Width, Height = icon.Height };
                reference.Measure(new Size(icon.Width, icon.Height)); reference.Arrange(new Rect(reference.DesiredSize));

                foreach (var palette in DesignSystem.Themes)
                foreach (var selected in new[] { false, true })
                {
                    DesignSystem.ApplyTheme(palette.Id); button.Classes.Set("primary", selected);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    Assert.Equal(new Size(icon.Width, icon.Height), icon.Bounds.Size);
                    var origin = icon.TranslatePoint(default, button)!.Value;
                    Assert.InRange(Math.Abs(origin.X + icon.Width / 2 - button.Bounds.Width / 2), 0, .5);
                    Assert.InRange(Math.Abs(origin.Y + icon.Height / 2 - button.Bounds.Height / 2), 0, .5);
                    var expectedColor = Color.Parse(selected ? palette.OnAccent : palette.Text);
                    Assert.Equal(expectedColor, Assert.IsAssignableFrom<ISolidColorBrush>(icon.Foreground).Color);

                    foreach (var scale in new[] { 1, 2 })
                    {
                        using var actual = Render(icon, scale);
                        using var expected = Render(reference, scale);
                        var solidPixels = 0;
                        for (var y = 0; y < actual.Height; y++)
                        for (var x = 0; x < actual.Width; x++)
                        {
                            var pixel = actual.GetPixel(x, y);
                            if (Math.Abs(pixel.Alpha - expected.GetPixel(x, y).Alpha) > 2)
                            {
                                var directory = Path.Combine(AppPaths.DataRoot, "navigation-mask"); Directory.CreateDirectory(directory);
                                using var actualFile = File.Create(Path.Combine(directory, "actual.png"));
                                using var expectedFile = File.Create(Path.Combine(directory, "expected.png"));
                                actual.Encode(actualFile, SKEncodedImageFormat.Png, 100); expected.Encode(expectedFile, SKEncodedImageFormat.Png, 100);
                                Assert.Fail($"{name}, scale {scale}, pixel {x},{y}: alpha {pixel.Alpha} vs {expected.GetPixel(x, y).Alpha}; {directory}");
                            }
                            if (pixel.Alpha < 254) continue;
                            solidPixels++;
                            Assert.InRange(Math.Abs(pixel.Red - expectedColor.R), 0, 1);
                            Assert.InRange(Math.Abs(pixel.Green - expectedColor.G), 0, 1);
                            Assert.InRange(Math.Abs(pixel.Blue - expectedColor.B), 0, 1);
                        }
                        Assert.True(solidPixels > 20, $"{name} must render a non-empty SVG silhouette.");
                    }
                }
            }
        }
        finally { window.Close(); DesignSystem.ApplyTheme(AppSettings.DefaultTheme); }

        static SoundVolumeIcon VolumeIcon(string asset)
        {
            var icon = new SoundVolumeIcon();
            icon.SetVolume(asset == "Sound/volume-muted" ? 0 : asset == "Sound/volume-low" ? 25 : 100);
            return icon;
        }
    }

    private static SKBitmap Render(Control control, int scale)
    {
        using var target = new RenderTargetBitmap(new PixelSize((int)control.Width * scale, (int)control.Height * scale), new Vector(96 * scale, 96 * scale));
        target.Render(control);
        using var stream = new MemoryStream(); target.Save(stream, new PngBitmapEncoderOptions()); stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private sealed class SvgReference(string name) : Control
    {
        private readonly IImage image = new SvgImage { Source = SvgSource.Load($"avares://Unfold/Assets/Icons/{name}.svg", null) };
        public override void Render(DrawingContext context)
        {
            var offset = new Point((Bounds.Width - image.Size.Width) / 2, (Bounds.Height - image.Size.Height) / 2);
            image.Draw(context, new Rect(image.Size), new Rect(offset, image.Size));
        }
    }
}
