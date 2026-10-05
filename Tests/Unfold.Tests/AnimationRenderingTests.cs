using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AnimationRenderingTests
{
    [AvaloniaTheory]
    [InlineData("bori-rabbit")]
    [InlineData("default-cat")]
    [InlineData("puppy-dog")]
    [InlineData("hedgehog")]
    [InlineData("penguin")]
    public void ChangingPoseAndFrameClearsThePreviousSilhouette(string id)
    {
        using var view = new AnimationView { Width = 192, Height = 192 };
        var window = new Window
        {
            Width = 192, Height = 192, WindowDecorations = WindowDecorations.None,
            Background = Brushes.White, Content = new Canvas { Children = { view } }
        };
        try
        {
            window.Show(); view.SetRunning(false);
            var character = CharacterLibrary.LoadPackage(Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", id));
            var idle = character.LoadAnimation("idle")[0];
            var click = character.LoadAnimation("click");
            var poses = new[] { PetPose.Neutral, new PetPose(1.12, .82, 0),
                new PetPose(.94, 1.06, .07), new PetPose(1.06, .94, 0), PetPose.Neutral };
            view.SetFrames([idle], true, false, true);
            foreach (var pose in poses)
            {
                view.SetPose(pose);
                AssertMatchesFreshFrame(window);
            }
            view.SetPose(new PetPose(1.12, .82, 0), true); AssertMatchesFreshFrame(window);
            view.SetPose(PetPose.Neutral);
            foreach (var frame in click)
            {
                view.SetFrames([frame], false, false, true);
                AssertMatchesFreshFrame(window);
            }
            foreach (var key in OriginalCompanion.PointerClips)
                foreach (var frame in character.LoadAnimation(key))
                {
                    view.SetFrames([frame], false, false, true);
                    foreach (var pose in new[] { new PetPose(1, 1, .06), new PetPose(1.10, .88, 0), PetPose.Neutral })
                    {
                        view.SetPose(pose); AssertMatchesFreshFrame(window);
                    }
                }
        }
        finally { window.Close(); }
    }

    private static void AssertMatchesFreshFrame(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var actual = window.CaptureRenderedFrame(); Assert.NotNull(actual);
        using var fresh = new RenderTargetBitmap(actual.PixelSize, new Vector(96, 96)); fresh.Render(window);
        static uint[] Pixels(Bitmap bitmap)
        {
            using var stream = new MemoryStream(); bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            return ImageCodec.DecodePng(stream.ToArray()).Pixels;
        }
        Assert.Equal(Pixels(fresh), Pixels(actual));
    }
}
