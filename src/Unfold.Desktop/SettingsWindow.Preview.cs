using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private CharacterPackage? previewCharacter, previewTarget;
    private readonly DispatcherTimer previewRetry = new() { Interval = TimeSpan.FromSeconds(1) };
    private int previewGeneration, previewAttempts;
    private bool previewLoading;
    private bool CanRunPreview => !disposed && IsVisible && settingsPageHost.Content == dashboardPage;

    public void ResumePreview()
    {
        if (!CanRunPreview) return;
        if (!previewLoading) { previewAttempts = 0; previewRetry.Stop(); }
        preview.SetRunning(true);
        _ = RefreshPreview();
    }

    private void SuspendPreview()
    {
        previewGeneration++; previewLoading = false; previewRetry.Stop(); preview.SetRunning(false);
    }

    private async Task RefreshPreview()
    {
        if (!CanRunPreview) return;
        var selected = runtime.Selected;
        if (previewTarget != selected)
        {
            previewGeneration++; previewLoading = false; previewAttempts = 0; previewRetry.Stop();
            previewTarget = selected; previewCharacter = null;
            // Clear the old character immediately, including while a new GIF is still loading.
            if (selected is null) preview.SetFrames([], true);
            else
            {
                var sprite = selected.Manifest.SpriteSheet; var sheet = selected.Sheet;
                var pixels = new uint[sprite.FrameWidth * sprite.FrameHeight];
                for (var row = 0; row < sprite.FrameHeight; row++)
                    Array.Copy(sheet.Pixels, row * sheet.Width, pixels, row * sprite.FrameWidth, sprite.FrameWidth);
                preview.SetFrames([new(new(sprite.FrameWidth, sprite.FrameHeight, pixels), TimeSpan.FromSeconds(1))],
                    true, selected.Manifest.RenderStyle == "pixel", selected.HasOriginalBehavior);
            }
        }
        if (selected is null || previewCharacter == selected || previewLoading || previewRetry.IsEnabled || previewAttempts >= 3) return;
        var generation = ++previewGeneration;
        previewLoading = true; previewAttempts++;
        try
        {
            var frames = await runtime.Clip("idle");
            if (generation != previewGeneration || !CanRunPreview || runtime.Selected != selected) return;
            preview.SetFrames(frames, true, selected.Manifest.RenderStyle == "pixel", selected.HasOriginalBehavior);
            previewCharacter = selected;
        }
        catch (Exception error)
        {
            if (generation != previewGeneration || !CanRunPreview || runtime.Selected != selected) return;
            AppPaths.Log(error);
            if (previewAttempts < 3) previewRetry.Start();
        }
        finally { if (generation == previewGeneration) previewLoading = false; }
    }
}
