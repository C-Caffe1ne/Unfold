using System.Text.Json;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class CustomPetInteractionTests
{
    [AvaloniaFact]
    public void PingPongKeepsEndpointsOnceAndReversesInteriorFramesWithTheirTiming()
    {
        using var view = new AnimationView();
        var frames = Enumerable.Range(0, 4).Select(i => new AnimationFrame(
            new PixelImage(1, 1, [0xFF000000u + (uint)i]), TimeSpan.FromMilliseconds(20 + i * 10))).ToArray();
        view.SetFrames(frames, true, pingPong: true);
        var actual = (IReadOnlyList<AnimationFrame>)typeof(AnimationView).GetField("frames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        Assert.Equal(new[] { 0, 1, 2, 3, 2, 1 }, actual.Select(frame => (int)(frame.Image.Pixels[0] & 255)));
        Assert.Equal(new[] { 20d, 30, 40, 50, 40, 30 }, actual.Select(frame => frame.Duration.TotalMilliseconds));
        view.SetFrames(frames, false);
        Assert.Equal(4, ((IReadOnlyList<AnimationFrame>)typeof(AnimationView).GetField("frames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!).Count);
    }

    [AvaloniaFact]
    public async Task CustomPointerClipsRespondToHoverPressReleaseAndHoverExit()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        try
        {
            var document = new PixelDocument(16, 16) { Name = "포인터 펫" };
            Array.Fill(document.Layers[0].Frames[0], 0xFFFFFFFFu);
            var package = runtime.Library.Save(document);
            var manifest = package.Manifest with { Animations = CustomPetDraft.Actions.ToDictionary(key => key,
                key => new AnimationDefinition([0, 0], 10, Loop: true)) };
            AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await runtime.Reload(); runtime.Stop();
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false, BubbleDirection = BubbleDirection.Bottom });
            var pet = runtime.ActivePet!; pet.Position = new(500, 400); Layout(pet);
            Point Center() => pet.PetView.TranslatePoint(new(96, 96), pet)!.Value;
            void FixedCanvas()
            {
                pet.AdvanceCompanion(.2);
                Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
                Assert.Equal(Matrix.Identity, pet.PetView.RenderTransform?.Value ?? Matrix.Identity);
            }
            pet.MouseMove(Center()); await Until(() => pet.ActiveAnimation == "hover"); FixedCanvas();
            pet.MouseMove(new(-10, -10)); await Until(() => pet.ActiveAnimation == "idle"); FixedCanvas();
            pet.MouseMove(Center()); await Until(() => pet.ActiveAnimation == "hover"); FixedCanvas();
            pet.MouseDown(Center(), MouseButton.Left); await Until(() => pet.ActiveAnimation == "pointerDown"); FixedCanvas();
            pet.MouseUp(Center(), MouseButton.Left); await Until(() => pet.ActiveAnimation == "pointerUp"); FixedCanvas();
            pet.HidePet(); Assert.False(pet.IsVisible);
            var noRelease = manifest with { Animations = new(manifest.Animations) };
            noRelease.Animations.Remove("pointerUp"); noRelease.Animations.Remove("click");
            AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(noRelease, CharacterLibrary.JsonOptions));
            await runtime.Reload(); await runtime.UpdateSettings(runtime.Settings with { ShowPet = true }); Layout(pet);
            pet.MouseMove(Center()); await Until(() => pet.ActiveAnimation == "hover"); FixedCanvas();
            pet.MouseDown(Center(), MouseButton.Left); await Until(() => pet.ActiveAnimation == "pointerDown"); FixedCanvas();
            pet.MouseUp(Center(), MouseButton.Left); await Until(() => pet.ActiveAnimation == "idle"); FixedCanvas();
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(condition());
    }
}
