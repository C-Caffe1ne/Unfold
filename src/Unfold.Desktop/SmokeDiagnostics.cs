using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Opt-in packaged-app verification with isolated data and off-screen windows.</summary>
internal static class SmokeDiagnostics
{
    public static async Task Run(AppRuntime runtime, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var elapsed = Stopwatch.StartNew();
        var directory = Path.Combine(AppPaths.DataRoot, "verification"); Directory.CreateDirectory(directory);
        try
        {
            await runtime.Start(false, true);
            await Task.Delay(250);
            var startupMs = elapsed.Elapsed.TotalMilliseconds;
            if (runtime.Characters.Count == 0 || runtime.ActivePet is null) throw new InvalidOperationException("Startup did not load the character/pet.");
            Capture(desktop.MainWindow!, Path.Combine(directory, "settings.png"));
            Capture(runtime.ActivePet, Path.Combine(directory, "pet.png"));
            var settings = desktop.MainWindow!;
            var intervalInput = settings.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
            intervalInput.Value = 25; await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes != 25 || AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).IntervalMinutes != 25)
                throw new InvalidOperationException("Changing the interval did not update/persist the timer setting.");
            Press(settings, "TimerReset"); await Task.Delay(1200);
            if (!runtime.Clock.Paused || runtime.Clock.Remaining != TimeSpan.FromMinutes(25))
                throw new InvalidOperationException("Reset started the timer or ignored the new interval.");
            Capture(settings, Path.Combine(directory, "timer-reset.png"));
            Press(settings, "TimerToggle"); if (runtime.Clock.Paused) throw new InvalidOperationException("Play did not resume the timer.");
            Press(settings, "TimerStop"); await Task.Delay(1200);
            if (!runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.Zero) throw new InvalidOperationException("Stop did not keep the timer stopped.");
            Capture(settings, Path.Combine(directory, "timer-stopped.png"));
            intervalInput.Value = 30; await Task.Delay(100);
            if (!runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.Zero) throw new InvalidOperationException("Editing an interval restarted a stopped timer.");
            Press(settings, "TimerReset"); Press(settings, "TimerToggle");
            var routineEditor = new RoutineEditorWindow(null, routine => runtime.UpdateSettings(runtime.Settings with
                { CustomRoutine = routine, BreakRoutineId = routine.Id }));
            AppRuntime.PrepareDiagnosticWindow(routineEditor); routineEditor.Show();
            await Task.Delay(100);
            var fields = routineEditor.GetVisualDescendants().OfType<TextBox>().Where(input => input.Name is "Step1" or "Step2" or "Step3").ToDictionary(input => input.Name!);
            fields["Step1"].Text = "Look away and enjoy a short pause."; fields["Step2"].Text = ""; fields["Step3"].Text = "";
            Capture(routineEditor, Path.Combine(directory, "routine-editor.png")); Press(routineEditor, "Save my routine");
            await Task.Delay(100);
            if (AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).CustomRoutine?.DurationSeconds != 20)
                throw new InvalidOperationException("Custom routine was not saved.");
            var writing = new BreakRoutine("diagnostic-writing", "Writing pause", [new("Let my hands rest.", 20)]);
            var additionalEditor = new RoutineEditorWindow(writing, routine => runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine)));
            AppRuntime.PrepareDiagnosticWindow(additionalEditor); additionalEditor.Show(); await Task.Delay(100);
            Capture(additionalEditor, Path.Combine(directory, "additional-routine.png")); Press(additionalEditor, "Save my routine");
            await Task.Delay(100);
            var profileEditor = new ProfileEditorWindow(runtime.Settings, null, profile => runtime.UpdateSettings(runtime.Settings.SaveProfile(profile)));
            AppRuntime.PrepareDiagnosticWindow(profileEditor); profileEditor.Show(); await Task.Delay(100);
            profileEditor.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ProfileInterval").Value = 45;
            Capture(profileEditor, Path.Combine(directory, "profile-editor.png")); Press(profileEditor, "Save profile"); await Task.Delay(100);
            var personalization = new PersonalizationWindow(() => runtime.Settings, runtime.UpdateSettings);
            AppRuntime.PrepareDiagnosticWindow(personalization); personalization.Show(); await Task.Delay(100);
            Capture(personalization, Path.Combine(directory, "routine-library.png"));
            personalization.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 1; await Task.Delay(100);
            Capture(personalization, Path.Combine(directory, "work-profiles.png"));
            runtime.TogglePause(); Press(personalization, "Apply profile"); await Task.Delay(100);
            if (!runtime.Clock.Paused || runtime.Clock.Remaining != TimeSpan.FromMinutes(45)) throw new InvalidOperationException("Applying a profile changed Pause or missed its interval.");
            if (desktop.MainWindow!.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval").Value != 45)
                throw new InvalidOperationException("Settings did not reflect the applied profile.");
            runtime.TogglePause(); personalization.Close();
            var applied = AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json"));
            if (applied.AdditionalRoutines.Count != 1 || applied.ActiveProfileId is null || applied.BreakRoutineId != writing.Id)
                throw new InvalidOperationException("Routine library/profile did not persist.");
            var doc = new PixelDocument(32, 32) { Name = "Smoke verification" };
            doc.Draw(PixelTool.Rectangle, new(5, 5), new(25, 25), 0xFFF4B860, 1, 0, 0);
            var saved = runtime.Library.Save(doc); await runtime.Reload();
            await runtime.OpenEditor(saved);
            var editor = runtime.ActiveEditor ?? throw new InvalidOperationException("Editor did not open.");
            editor.Session.BeginStroke(new(1, 1)); editor.Session.ContinueStroke(new(25, 1)); editor.Session.EndStroke();
            if (!await editor.SaveDocument()) throw new InvalidOperationException("Editor save failed.");
            if (!runtime.Library.OpenForEditing(saved.Manifest.Id).Document.ContentEquals(editor.Session.Document)) throw new InvalidOperationException("Saved drawing differs from reopened source.");
            await Task.Delay(250); Capture(editor, Path.Combine(directory, "editor.png"));
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = "default-cat" });
            await runtime.ShowReminder();
            var reminder = runtime.ActiveBreakReminder ?? throw new InvalidOperationException("Reminder did not open.");
            if (reminder.Session.Routine.Id != writing.Id || reminder.Session.ProfileId != applied.ActiveProfileId) throw new InvalidOperationException("Profile was not applied to the session.");
            await runtime.ShowReminder();
            if (runtime.ActiveBreakReminder != reminder) throw new InvalidOperationException("Duplicate reminder created a new session.");
            await Task.Delay(150); Capture(reminder, Path.Combine(directory, "reminder.png"));
            Press(reminder, "Start 20-second break");
            if (reminder.Session.State != BreakSessionState.InProgress) throw new InvalidOperationException("Start button did not begin the break.");
            await runtime.UpdateSettings(runtime.Settings.SaveRoutine(writing with { Name = "Revised writing pause", Steps = [new("Different next time.", 40)] }));
            if (reminder.Session.Routine.DurationSeconds != 20 || reminder.Session.ProfileId != applied.ActiveProfileId)
                throw new InvalidOperationException("Editing a routine changed an in-progress break.");
            // Advance the session clock programmatically: this checks state wiring, not real elapsed time or body movement.
            for (var seconds = 10; seconds <= 30; seconds += 10) reminder.Session.Tick(TimeSpan.FromSeconds(seconds));
            reminder.RefreshProgress();
            if (reminder.Session.State != BreakSessionState.AwaitingConfirmation || runtime.BreakHistory.Completions.Count != 0)
                throw new InvalidOperationException("A break was counted without confirmation.");
            await Task.Delay(100); Capture(reminder, Path.Combine(directory, "break-confirm.png"));
            Press(reminder, "I'm refreshed");
            var historyFile = Path.Combine(AppPaths.DataRoot, "break-history.json");
            if (runtime.ActiveReminder is not null || BreakHistory.Load(historyFile).Completions.Count != 1)
                throw new InvalidOperationException("Confirmed break was not saved exactly once.");
            var completed = BreakHistory.Load(historyFile).Completions.Single();
            if (completed.RoutineName != "Writing pause" || completed.ProfileId != applied.ActiveProfileId || completed.Seconds != 20)
                throw new InvalidOperationException("History lost the original routine/profile context.");
            await runtime.ShowReminder(); Press(runtime.ActiveReminder!, "In 5 minutes");
            if (runtime.Clock.Remaining != TimeSpan.FromMinutes(5)) throw new InvalidOperationException("Snooze did not schedule five minutes.");
            await runtime.ShowReminder(); runtime.ActiveReminder!.Close();
            if (runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Snooze or close was recorded as a completion.");
            Capture(desktop.MainWindow!, Path.Combine(directory, "settings-completed.png"));
            var reviewWindow = new BreakReviewWindow(runtime.BreakHistory.Review, () => runtime.BreakHistoryError, snapshot =>
            {
                var path = Path.Combine(directory, "review.csv"); AtomicFile.Write(path, snapshot.Csv()); return Task.FromResult<string?>(path);
            });
            AppRuntime.PrepareDiagnosticWindow(reviewWindow); reviewWindow.Show(); await Task.Delay(100); Press(reviewWindow, "Export CSV");
            if (reviewWindow.Review.Entries.Count != 1 || !File.ReadAllText(Path.Combine(directory, "review.csv")).Contains("Writing pause"))
                throw new InvalidOperationException("Review/export differs from completed history.");
            Capture(reviewWindow, Path.Combine(directory, "weekly-review.png")); reviewWindow.Close();
            await runtime.ShowReminder(); Press(settings, "TimerStop");
            if (runtime.ActiveReminder is not null || runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Stop did not dismiss the break without recording completion.");
            await runtime.Reload(); var pendingReminder = runtime.ShowReminder(); var resetWhileOpening = !pendingReminder.IsCompleted;
            Press(settings, "TimerReset"); await pendingReminder;
            if (runtime.ActiveReminder is not null || !runtime.Clock.Paused) throw new InvalidOperationException("A reminder survived Reset.");
            editor.CloseAfterApproval();
            await VerifyPetPacks(runtime, saved, directory);
            runtime.HideSettingsForDiagnostics();
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = "default-cat", ShowPet = true });
            await Task.Delay(1000);
            var process = Process.GetCurrentProcess(); var cpuBefore = process.TotalProcessorTime; var sample = Stopwatch.StartNew();
            await Task.Delay(2000); process.Refresh();
            var report = new { success = true, startupMs, totalMs = elapsed.Elapsed.TotalMilliseconds,
                workingSetBytes = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false),
                oneCoreCpuPercent = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / sample.Elapsed.TotalMilliseconds * 100,
                characters = runtime.Characters.Count, imageFiles = Directory.GetFiles(directory, "*.png").Length,
                completedBreaks = runtime.BreakHistory.Completions.Count, simulatedSessionTiming = true,
                savedCustomRoutines = runtime.Settings.AdditionalRoutines.Count + (runtime.Settings.CustomRoutine is null ? 0 : 1),
                workProfiles = runtime.Settings.WorkProfiles.Count, exportedBreaks = 1, simulatedExportDestination = true,
                timerResetWaitSeconds = 1.2, timerStopWaitSeconds = 1.2, timerControlsVerified = true, resetWhileOpening,
                petPackInstallUpdateRepairVerified = true, simulatedPackPicker = true,
                idleSeconds = PlatformServices.IdleTime().TotalSeconds, os = Environment.OSVersion.ToString(), framework = Environment.Version.ToString() };
            AtomicFile.Write(Path.Combine(directory, "smoke.json"), JsonSerializer.SerializeToUtf8Bytes(report, CharacterLibrary.JsonOptions));
            await runtime.Quit();
        }
        catch (Exception error)
        {
            AtomicFile.Write(Path.Combine(directory, "smoke.json"), JsonSerializer.SerializeToUtf8Bytes(new { success = false, error = error.ToString() }, CharacterLibrary.JsonOptions));
            AppPaths.Log(error); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    private static async Task VerifyPetPacks(AppRuntime runtime, CharacterPackage fixture, string directory)
    {
        var source = Path.Combine(AppPaths.DataRoot, "pack-source", "diagnostic-pack"); Directory.CreateDirectory(source);
        File.Copy(Path.Combine(fixture.DirectoryPath, "spritesheet.png"), Path.Combine(source, "spritesheet.png"));
        var manifest = fixture.Manifest with { Id = "diagnostic-pack", Name = "Diagnostic companion", Animations = new(fixture.Manifest.Animations) };
        foreach (var key in new[] { "attention", "stretch", "celebrate", "click" }) manifest.Animations[key] = new([0], 8, Loop: false);
        AtomicFile.Write(Path.Combine(source, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var first = Path.Combine(AppPaths.DataRoot, "diagnostic-v1.unfoldpet");
        var second = Path.Combine(AppPaths.DataRoot, "diagnostic-v2.unfoldpet");
        await Task.Run(() => { CharacterPack.Create(source, "1.0.0", first); CharacterPack.Create(source, "1.1.0", second); });
        var selectedFile = first;
        var window = new PetPackWindow(runtime.Library, runtime.SelectInstalledCharacter, () => Task.FromResult<string?>(selectedFile));
        AppRuntime.PrepareDiagnosticWindow(window); window.Show();
        Button InstallButton() => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "InstallPetPack");
        try
        {
            Press(window, "OpenPetPack"); await Until(() => InstallButton().IsEnabled);
            if (runtime.Characters.Any(character => character.Manifest.Id == manifest.Id)) throw new InvalidOperationException("Preview installed a companion without confirmation.");
            Capture(window, Path.Combine(directory, "pack-preview.png"));
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "Installed"));
            if (runtime.Selected?.Manifest.Id != manifest.Id) throw new InvalidOperationException("Installed companion was not selected.");
            Capture(window, Path.Combine(directory, "pack-installed.png"));
            selectedFile = second; Press(window, "OpenPetPack"); await Until(() => InstallButton().IsEnabled);
            if (!Equals(InstallButton().Content, "Update")) throw new InvalidOperationException("New pack version was not recognized.");
            Capture(window, Path.Combine(directory, "pack-update.png"));
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "Installed"));
            var sheet = Path.Combine(runtime.Library.PackagePath(manifest.Id), "spritesheet.png"); File.WriteAllText(sheet, "diagnostic corruption");
            Press(window, "OpenPetPack"); await Until(() => InstallButton().IsEnabled);
            if (!Equals(InstallButton().Content, "Reinstall")) throw new InvalidOperationException("Repair was not offered for the installed version.");
            Capture(window, Path.Combine(directory, "pack-reinstall.png"));
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "Installed"));
            if (!File.ReadAllBytes(sheet).SequenceEqual(File.ReadAllBytes(Path.Combine(source, "spritesheet.png"))))
                throw new InvalidOperationException("Reinstall did not restore the companion's image.");
            selectedFile = Path.Combine(AppPaths.DataRoot, "broken.unfoldpet"); File.WriteAllText(selectedFile, "not a zip");
            Press(window, "OpenPetPack"); await Until(() => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "OpenPetPack").IsEnabled);
            if (InstallButton().IsEnabled || !runtime.Clock.Paused) throw new InvalidOperationException("Invalid pack remained installable or installation resumed the timer.");
            Capture(window, Path.Combine(directory, "pack-error.png"));
            _ = CharacterLibrary.LoadPackage(runtime.Library.PackagePath(manifest.Id)).LoadAnimation("idle");
        }
        finally { window.Close(); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var attempt = 0; attempt < 400 && !ready(); attempt++) await Task.Delay(25);
        if (!ready()) throw new TimeoutException("Pet pack diagnostic did not finish.");
    }
    private static void Press(Window window, string label)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label) || button.Name == label);
        if (!button.IsEnabled) throw new InvalidOperationException($"Button was disabled: {label}");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static void Capture(Window window, string path)
    {
        var size = window.ClientSize;
        if (size.Width < 100 || size.Height < 100) throw new InvalidOperationException($"Diagnostic window collapsed before capture: {window.Title} ({size}).");
        using var image = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)), new Vector(96, 96));
        image.Render(window); image.Save(path, PngBitmapEncoderOptions.Default);
    }
}
