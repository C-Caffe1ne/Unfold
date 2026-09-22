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
            var poses = new[] { PetPose.Neutral, PetPose.Press(.1), PetPose.Release(.06, PetPose.Press(.1)),
                PetPose.Release(.12, PetPose.Press(.1)), PetPose.Release(.3, PetPose.Press(.1)), PetPose.Neutral };
            view.SetFrames([idle], true, false, true);
            foreach (var pose in poses)
            {
                view.SetPose(pose);
                AssertMatchesFreshFrame(window);
            }
            view.SetPose(PetPose.Press(.1), true); AssertMatchesFreshFrame(window);
            view.SetPose(PetPose.Neutral);
            foreach (var frame in click)
            {
                view.SetFrames([frame], false, false, true);
                AssertMatchesFreshFrame(window);
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
