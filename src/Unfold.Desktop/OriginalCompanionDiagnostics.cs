using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Real dispatcher playback with isolated data; never moves a pet onto the user's desktop.</summary>
internal static class OriginalCompanionDiagnostics
{
    public static async Task Run(AppRuntime runtime, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var directory = Path.Combine(AppPaths.DataRoot, "verification"); Directory.CreateDirectory(directory);
        Window? review = null;
        try
        {
            runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            await runtime.Start(true, true); runtime.Stop();
            await runtime.UpdateSettings(runtime.Settings with { ReminderSoundsEnabled = false });
            var results = new List<object>();
            foreach (var id in runtime.Characters.Where(character => character.HasOriginalBehavior)
                .Select(character => character.Manifest.Id).ToArray())
            {
                runtime.Stop();
                await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = id, ShowPet = true });
                var pet = runtime.ActivePet!; var character = runtime.Selected!;
                if (!character.HasOriginalBehavior) throw new InvalidOperationException("Original behavior profile missing.");
                using var preview = new AnimationView { Width = 256, Height = 256 };
                var label = Ui.Text(character.Manifest.Name, 24);
                review = new Window { Width = 344, Height = 358, Content = new Border
                { Background = DesignSystem.Shell, Padding = new(32), Child = Ui.Column(label, preview) } };
                AppRuntime.PrepareDiagnosticWindow(review); review.Show();
                var clips = new List<object>();
                foreach (var key in OriginalCompanion.RequiredClips)
                {
                    var frames = await runtime.Clip(key);
                    var repeat = character.Manifest.Animations[key].Loop;
                    label.Text = character.Manifest.Name + " · " + key;
                    preview.SetFrames(frames, repeat, false, true);
                    var elapsed = Stopwatch.StartNew();
                    var reaction = key == "idle" ? Task.CompletedTask : pet.React(key);
                    await Task.Delay(TimeSpan.FromMilliseconds(frames.Sum(frame => frame.Duration.TotalMilliseconds) / 2));
                    Capture(review, Path.Combine(directory, id + "-" + key + ".png"));
                    Capture(pet.PetView, Path.Combine(directory, id + "-" + key + "-pet.png"));
                    await reaction.WaitAsync(TimeSpan.FromSeconds(12));
                    if (pet.ActiveAnimation != "idle") throw new InvalidOperationException("One-shot did not restore idle.");
                    clips.Add(new { key, frames = frames.Count, elapsedMs = elapsed.Elapsed.TotalMilliseconds });
                }
                var windowSize = new Size(pet.Width, pet.Height); var windowPosition = pet.Position;
                if (!pet.HasPointerArt) throw new InvalidOperationException("Pointer art did not load.");
                pet.BeginCompanionPress(); await Until(() => pet.PointerPhase == PetPointerPhase.Held);
                pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
                var heldFrames = await runtime.Clip("held");
                preview.SetFrames([heldFrames[0]], true, false, true);
                var held = pet.PetView.Pose;
                if (held.Lift <= 0) throw new InvalidOperationException("Held pet did not lift.");
                preview.SetPose(held); label.Text = character.Manifest.Name + " · 들어 올림";
                Capture(review, Path.Combine(directory, id + "-held.png"));
                for (var tick = 0; tick < 50; tick++) pet.AdvanceCompanion(.2);
                if (pet.PetView.Pose != held) throw new InvalidOperationException("Held pose was not maintained.");
                pet.ReleaseCompanionPress(false); pet.AdvanceCompanion(.16);
                preview.SetPose(pet.PetView.Pose); label.Text = character.Manifest.Name + " · 첫 접촉";
                Capture(review, Path.Combine(directory, id + "-contact.png"));
                pet.AdvanceCompanion(.18);
                if (pet.PointerPhase != PetPointerPhase.Bouncing || pet.ActiveAnimation != "held" || pet.PetView.Pose.Lift <= held.Lift)
                    throw new InvalidOperationException("Held art changed before the bounce apex.");
                preview.SetPose(pet.PetView.Pose); label.Text = character.Manifest.Name + " · 한 번 튕김";
                Capture(review, Path.Combine(directory, id + "-bounce.png"));
                pet.AdvanceCompanion(.18); pet.AdvanceCompanion(.12);
                if (pet.PointerPhase != PetPointerPhase.Recovering || pet.ActiveAnimation != "land")
                    throw new InvalidOperationException("Recovery did not wait for final landing.");
                var landFrames = await runtime.Clip("land");
                preview.SetFrames([landFrames[1]], true, false, true);
                preview.SetPose(PetPose.Neutral); label.Text = character.Manifest.Name + " · 자세 복귀";
                Capture(review, Path.Combine(directory, id + "-recovering.png"));
                await Until(() => pet.PointerPhase == PetPointerPhase.None && pet.ActiveAnimation == "idle");
                preview.SetFrames(await runtime.Clip("idle"), true, false, true);
                preview.SetPose(pet.PetView.Pose); label.Text = character.Manifest.Name + " · 복귀";
                Capture(review, Path.Combine(directory, id + "-landed.png"));
                if (pet.PetView.Pose != PetPose.Neutral || new Size(pet.Width, pet.Height) != windowSize || pet.Position != windowPosition)
                    throw new InvalidOperationException("Landing did not restore pose within the unchanged window.");
                for (var i = 1; i <= 3; i++)
                {
                    await runtime.ShowReminder(); runtime.SnoozeBreak();
                    if (i == 3 && pet.ActiveAnimation != "sulk") throw new InvalidOperationException("Third snooze did not sulk.");
                }
                await runtime.ShowReminder(); runtime.StartBreak();
                if (pet.ActiveAnimation != "stretch" || pet.IsRoaming) throw new InvalidOperationException("Walking started before stretching.");
                await Until(() => pet.IsRoaming && pet.ActiveAnimation == "walk");
                Capture(pet, Path.Combine(directory, id + "-resting-walk.png"));
                runtime.CompleteBreak();
                if (pet.IsRoaming) throw new InvalidOperationException("Completion did not stop walking.");
                await Until(() => pet.ActiveAnimation == "idle");
                await runtime.ShowReminder(); runtime.StartBreak(); await runtime.HidePet();
                if (pet.IsVisible || pet.IsRoaming) throw new InvalidOperationException("Hidden pet continued walking.");
                results.Add(new { id, clips, pointerPoseCaptured = true, heldAndLandingVerified = true,
                    pointerArt = OriginalCompanion.PointerClips, singleBounceBeforeRecovery = true, thirdSnoozeVerified = true,
                    stretchBeforeWalkVerified = true, completionAndHideStopWalking = true });
                review.Close(); review = null;
            }
            AtomicFile.Write(Path.Combine(directory, "original-pets.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { success = true, pets = results, physicalInput = false, offScreen = true, realTimePlayback = true,
                desktopMovement = false, images = Directory.GetFiles(directory, "*.png").Length }, CharacterLibrary.JsonOptions));
            await runtime.Quit();
        }
        catch (Exception error)
        {
            AtomicFile.Write(Path.Combine(directory, "original-pets.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { success = false, error = error.ToString() }, CharacterLibrary.JsonOptions));
            review?.Close(); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 600 && !ready(); i++) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("Original companion did not finish its reaction.");
    }
    private static void Capture(Control control, string path)
    {
        control.UpdateLayout(); var size = control.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) throw new InvalidOperationException("Empty companion capture.");
        using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(size.Width * 2), (int)Math.Ceiling(size.Height * 2)), new(192, 192));
        bitmap.Render(control); bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }
}
