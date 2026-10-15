using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetSpeechSurfaceTests
{
    [AvaloniaTheory]
    [InlineData(AppTheme.Plum, BubbleDirection.Left)]
    [InlineData(AppTheme.Plum, BubbleDirection.Right)]
    [InlineData(AppTheme.OatLatte, BubbleDirection.Left)]
    [InlineData(AppTheme.OatLatte, BubbleDirection.Right)]
    [InlineData(AppTheme.Sage, BubbleDirection.Left)]
    [InlineData(AppTheme.Sage, BubbleDirection.Right)]
    [InlineData(AppTheme.MidnightBlue, BubbleDirection.Left)]
    [InlineData(AppTheme.MidnightBlue, BubbleDirection.Right)]
    public async Task BodyAndTailHaveNoInternalBorderAcrossThemesStatesOpacityAndDpi(AppTheme theme, BubbleDirection direction)
    {
        using var temp = new TempDirectory();
        var previousRoot = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        var previousTheme = DesignSystem.CurrentTheme;
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        var runtime = new AppRuntime(lifetime);
        try
        {
            var sprite = new SpriteFixture(16, 16) { Name = "Speech surface fixture" };
            Array.Fill(sprite.Pixels, uint.MaxValue);
            var package = runtime.Library.InstallFixture(sprite); await runtime.Reload();
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = package.Manifest.Id,
                ReminderSoundsEnabled = false, ShowPetTimer = false, Theme = theme });
            var pet = runtime.ActivePet!;
            pet.Position = new(direction == BubbleDirection.Right ? 200 : 1400, 400);
            foreach (var notice in new[] { PetNotice.Invitation, PetNotice.Advance, PetNotice.Resting, PetNotice.Completed })
            foreach (var opacity in new[] { 100, 37 })
            {
                await runtime.UpdateSettings(runtime.Settings with { BubbleOpacityPercent = opacity });
                runtime.Reminder.Cancel();
                if (notice == PetNotice.Advance) runtime.Reminder.ShowAdvance(TimeSpan.Zero);
                else
                {
                    runtime.Reminder.Invite(new(BreakRoutines.All[0], package.Manifest.Id));
                    if (notice is PetNotice.Resting or PetNotice.Completed) runtime.Reminder.Start(TimeSpan.Zero);
                    if (notice == PetNotice.Completed) runtime.Reminder.Complete(TimeSpan.Zero);
                }
                pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.Overlay.UpdateLayout();
                Assert.Equal(notice, runtime.PresentedReminder.Notice);
                var overlay = pet.Overlay; var bubble = overlay.Bubble;
                Assert.Equal(direction, overlay.Direction);
                var left = Canvas.GetLeft(bubble); var top = Canvas.GetTop(bubble);
                var edge = direction == BubbleDirection.Right ? left : left + bubble.Width;
                var sign = direction == BubbleDirection.Right ? 1 : -1;
                var center = top + bubble.Height / 2;
                foreach (var scale in new[] { 2d, 1d, 1.25d })
                {
                    using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(overlay.Width * scale),
                        (int)Math.Ceiling(overlay.Height * scale)), new(96 * scale, 96 * scale));
                    bitmap.Render(overlay);
                    if (theme == AppTheme.Plum && direction == BubbleDirection.Left && notice == PetNotice.Invitation && opacity == 100 && scale == 2 &&
                        Environment.GetEnvironmentVariable("UNFOLD_BUBBLE_QA_DIR") is { Length: > 0 } output)
                    {
                        Directory.CreateDirectory(output);
                        bitmap.Save(Path.Combine(output, "bubble.png"), PngBitmapEncoderOptions.Default);
                    }
                    using var stream = new MemoryStream(); bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                    var image = ImageCodec.DecodePng(stream.ToArray());
                    uint At(double x, double y) => image.Pixels[(int)Math.Floor(y * scale) * image.Width + (int)Math.Floor(x * scale)];
                    var fill = At(edge + sign * 6, center);
                    Assert.True(fill >> 24 > 0);
                    foreach (var dy in new[] { -3d, 0, 3d })
                    {
                        var join = At(edge + sign * .5, center + dy);
                        for (var shift = 0; shift <= 24; shift += 8)
                            Assert.InRange(Math.Abs((int)((join >> shift) & 255) - (int)((fill >> shift) & 255)), 0, 2);
                    }
                }
            }
        }
        finally
        {
            runtime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousRoot);
            DesignSystem.ApplyTheme(previousTheme);
        }
    }
}
