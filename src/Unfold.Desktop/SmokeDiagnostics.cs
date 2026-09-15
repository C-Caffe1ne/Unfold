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
            var settingsLayout = await VerifySettingsLayout(settings, directory);
            var intervalInput = settings.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
            if (intervalInput.IsEnabled) throw new InvalidOperationException("The running timer allowed interval editing.");
            Press(settings, "TimerToggle"); await Task.Delay(100);
            if (!runtime.Clock.Paused || !intervalInput.IsEnabled) throw new InvalidOperationException("Pause did not enable interval editing.");
            intervalInput.Value = 25; await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes == 25) throw new InvalidOperationException("Changing the interval applied before pressing Apply.");
            Press(settings, "ApplyReminderSettings"); await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes != 25 || AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).IntervalMinutes != 25)
                throw new InvalidOperationException("Apply did not update/persist the timer setting.");
            await Task.Delay(1200);
            if (!runtime.Clock.Paused || runtime.Clock.Remaining != TimeSpan.FromMinutes(25))
                throw new InvalidOperationException("Applying the interval started the paused timer or ignored the new interval.");
            Capture(settings, Path.Combine(directory, "timer-paused.png"));
            Press(settings, "TimerToggle"); if (runtime.Clock.Paused) throw new InvalidOperationException("Play did not resume the timer.");
            Press(settings, "TimerStop"); await Task.Delay(1200);
            if (!runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.Zero) throw new InvalidOperationException("Stop did not keep the timer stopped.");
            Capture(settings, Path.Combine(directory, "timer-stopped.png"));
            intervalInput.Value = 30; await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes == 30 || !runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.Zero)
                throw new InvalidOperationException("Editing an interval applied early or restarted a stopped timer.");
            Press(settings, "ApplyReminderSettings"); await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes != 30 || !runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.Zero)
                throw new InvalidOperationException("Applying an interval changed the stopped state.");
            Press(settings, "TimerToggle");
            var routineEditor = new RoutineEditorWindow(null, routine => runtime.UpdateSettings(runtime.Settings with
                { CustomRoutine = routine, BreakRoutineId = routine.Id }));
            AppRuntime.PrepareDiagnosticWindow(routineEditor); routineEditor.Show();
            await Task.Delay(100);
            var fields = routineEditor.GetVisualDescendants().OfType<TextBox>().Where(input => input.Name is "Step1" or "Step2" or "Step3").ToDictionary(input => input.Name!);
            fields["Step1"].Text = "Look away and enjoy a short pause."; fields["Step2"].Text = ""; fields["Step3"].Text = "";
            Capture(routineEditor, Path.Combine(directory, "routine-editor.png")); Press(routineEditor, "내 루틴 저장");
            await Task.Delay(100);
            if (AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).CustomRoutine?.DurationSeconds != 20)
                throw new InvalidOperationException("Custom routine was not saved.");
            var writing = new BreakRoutine("diagnostic-writing", "Writing pause", [new("Let my hands rest.", 20)]);
            var additionalEditor = new RoutineEditorWindow(writing, routine => runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine)));
            AppRuntime.PrepareDiagnosticWindow(additionalEditor); additionalEditor.Show(); await Task.Delay(100);
            Capture(additionalEditor, Path.Combine(directory, "additional-routine.png")); Press(additionalEditor, "내 루틴 저장");
            await Task.Delay(100);
            var profileEditor = new ProfileEditorWindow(runtime.Settings, null, profile => runtime.UpdateSettings(runtime.Settings.SaveProfile(profile)));
            AppRuntime.PrepareDiagnosticWindow(profileEditor); profileEditor.Show(); await Task.Delay(100);
            profileEditor.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ProfileInterval").Value = 45;
            Capture(profileEditor, Path.Combine(directory, "profile-editor.png")); Press(profileEditor, "프로필 저장"); await Task.Delay(100);
            var personalization = new PersonalizationWindow(() => runtime.Settings, runtime.UpdateSettings);
            AppRuntime.PrepareDiagnosticWindow(personalization); personalization.Show(); await Task.Delay(100);
            Capture(personalization, Path.Combine(directory, "routine-library.png"));
            personalization.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 1; await Task.Delay(100);
            Capture(personalization, Path.Combine(directory, "work-profiles.png"));
            await VerifySharedDialogs(personalization, directory);
            runtime.TogglePause(); Press(personalization, "프로필 적용"); await Task.Delay(100);
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
            // SaveDocument starts the catalog refresh via its callback. Wait for selection before taking the library lock again.
            await Until(() => runtime.Selected?.Manifest.Id == saved.Manifest.Id);
            if (!runtime.Library.OpenForEditing(saved.Manifest.Id).Document.ContentEquals(editor.Session.Document)) throw new InvalidOperationException("Saved drawing differs from reopened source.");
            await Task.Delay(250); Capture(editor, Path.Combine(directory, "editor.png"));
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = "default-cat" });
            await runtime.ShowReminder();
            var reminder = runtime.ActiveBreakReminder ?? throw new InvalidOperationException("Reminder did not open.");
            if (reminder.Session.Routine.Id != writing.Id || reminder.Session.ProfileId != applied.ActiveProfileId) throw new InvalidOperationException("Profile was not applied to the session.");
            await runtime.ShowReminder();
            if (runtime.ActiveBreakReminder != reminder) throw new InvalidOperationException("Duplicate reminder created a new session.");
            await Task.Delay(150); Capture(reminder, Path.Combine(directory, "reminder.png"));
            Press(reminder, "20초 휴식 시작");
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
            Press(reminder, "잘 쉬었어요");
            var historyFile = Path.Combine(AppPaths.DataRoot, "break-history.json");
            if (runtime.ActiveReminder is not null || BreakHistory.Load(historyFile).Completions.Count != 1)
                throw new InvalidOperationException("Confirmed break was not saved exactly once.");
            var completed = BreakHistory.Load(historyFile).Completions.Single();
            if (completed.RoutineName != "Writing pause" || completed.ProfileId != applied.ActiveProfileId || completed.Seconds != 20)
                throw new InvalidOperationException("History lost the original routine/profile context.");
            await runtime.ShowReminder(); Press(runtime.ActiveReminder!, "5분 뒤에");
            if (runtime.Clock.Remaining != TimeSpan.FromMinutes(5)) throw new InvalidOperationException("Snooze did not schedule five minutes.");
            await runtime.ShowReminder(); runtime.ActiveReminder!.Close();
            if (runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Snooze or close was recorded as a completion.");
            Capture(desktop.MainWindow!, Path.Combine(directory, "settings-completed.png"));
            var reviewWindow = new BreakReviewWindow(runtime.BreakHistory.Review, () => runtime.BreakHistoryError, snapshot =>
            {
                var path = Path.Combine(directory, "review.csv"); AtomicFile.Write(path, snapshot.Csv()); return Task.FromResult<string?>(path);
            });
            AppRuntime.PrepareDiagnosticWindow(reviewWindow); reviewWindow.Show(); await Task.Delay(100); Press(reviewWindow, "CSV 내보내기");
            if (reviewWindow.Review.Entries.Count != 1 || !File.ReadAllText(Path.Combine(directory, "review.csv")).Contains("Writing pause"))
                throw new InvalidOperationException("Review/export differs from completed history.");
            Capture(reviewWindow, Path.Combine(directory, "weekly-review.png")); reviewWindow.Close();
            await runtime.ShowReminder(); Press(settings, "TimerStop");
            if (runtime.ActiveReminder is not null || runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Stop did not dismiss the break without recording completion.");
            await runtime.Reload(); var pendingReminder = runtime.ShowReminder(); var stopWhileOpening = !pendingReminder.IsCompleted;
            Press(settings, "TimerStop"); await pendingReminder;
            if (runtime.ActiveReminder is not null || !runtime.Clock.Stopped) throw new InvalidOperationException("A reminder survived Stop.");
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
                timerPausedApplyWaitSeconds = 1.2, timerStopWaitSeconds = 1.2, timerControlsVerified = true, stopWhileOpening,
                petPackInstallUpdateRepairVerified = true, simulatedPackPicker = true, settingsLayout,
                sharedDesignDialogsVerified = true, pinnedPageActionsVerified = true,
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
    private static async Task VerifySharedDialogs(Window owner, string directory)
    {
        async Task<Window> PrepareDialog(double height)
        {
            await Until(() => owner.OwnedWindows.Any());
            var dialog = owner.OwnedWindows.Single();
            // Native off-screen auto-sized dialogs need explicit capture bounds.
            dialog.SizeToContent = SizeToContent.Manual; dialog.Height = height;
            AppRuntime.PrepareDiagnosticWindow(dialog); await Task.Delay(100);
            return dialog;
        }
        var confirmTask = Ui.Confirm(owner, "프로필을 삭제할까요?", "‘집중하는 시간’ 프로필을 삭제할까요? 현재 알림 설정과 기록은 유지돼요.", "삭제", "취소");
        var confirm = await PrepareDialog(300);
        Capture(confirm, Path.Combine(directory, "dialog-confirm.png"));
        Press(confirm, "취소"); if (await confirmTask != 1) throw new InvalidOperationException("Dialog cancel selected a destructive action.");
        var promptTask = Ui.Prompt(owner, "이름 바꾸기", "나만의 휴식");
        var prompt = await PrepareDialog(280);
        Capture(prompt, Path.Combine(directory, "dialog-prompt.png")); prompt.Close();
        if (await promptTask is not null) throw new InvalidOperationException("Closing the name dialog saved a value.");
        var errorTask = Ui.Error(owner, new IOException("Design system diagnostic"));
        var error = await PrepareDialog(300);
        Capture(error, Path.Combine(directory, "dialog-error.png")); Press(error, "확인"); await errorTask;
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
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "설치 완료"));
            if (runtime.Selected?.Manifest.Id != manifest.Id) throw new InvalidOperationException("Installed companion was not selected.");
            Capture(window, Path.Combine(directory, "pack-installed.png"));
            selectedFile = second; Press(window, "OpenPetPack"); await Until(() => InstallButton().IsEnabled);
            if (!Equals(InstallButton().Content, "업데이트")) throw new InvalidOperationException("New pack version was not recognized.");
            Capture(window, Path.Combine(directory, "pack-update.png"));
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "설치 완료"));
            var sheet = Path.Combine(runtime.Library.PackagePath(manifest.Id), "spritesheet.png"); File.WriteAllText(sheet, "diagnostic corruption");
            Press(window, "OpenPetPack"); await Until(() => InstallButton().IsEnabled);
            if (!Equals(InstallButton().Content, "재설치")) throw new InvalidOperationException("Repair was not offered for the installed version.");
            Capture(window, Path.Combine(directory, "pack-reinstall.png"));
            Press(window, "InstallPetPack"); await Until(() => Equals(InstallButton().Content, "설치 완료"));
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
    private static async Task<object> VerifySettingsLayout(Window window, string directory)
    {
        var width = window.Width; var height = window.Height;
        var minWidth = window.MinWidth; var minHeight = window.MinHeight;
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "SettingsDetailsScroll");
        try
        {
            window.MinWidth = 860; window.MinHeight = 680; window.Width = 860; window.Height = 680;
            await Task.Delay(200); window.UpdateLayout();
            if (Math.Abs(window.ClientSize.Width - 860) > 1 || Math.Abs(window.ClientSize.Height - 680) > 1)
                throw new InvalidOperationException($"Settings did not reach the minimum diagnostic size: {window.ClientSize}.");
            foreach (var name in new[] { "SettingsCompanionCard", "SettingsTimerCard", "TimerToggle", "TimerStop", "ApplyReminderSettings", "SettingsQuit", "LaunchAtLogin" })
            {
                var control = window.GetVisualDescendants().OfType<Control>().Single(item => item.Name == name);
                var origin = control.TranslatePoint(default, window)!.Value;
                if (origin.X < 0 || origin.Y < 0 || origin.X + control.Bounds.Width > window.ClientSize.Width + 1 ||
                    origin.Y + control.Bounds.Height > window.ClientSize.Height + 1)
                    throw new InvalidOperationException($"Settings control is clipped: {name}.");
            }
            if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new InvalidOperationException("Settings details overflow horizontally.");
            Capture(window, Path.Combine(directory, "settings-minimum.png"));
            scroll.ScrollToEnd(); await Task.Delay(100); window.UpdateLayout();
            var review = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsOpenReview");
            var reviewOrigin = review.TranslatePoint(default, window)!.Value;
            if (reviewOrigin.Y < 0 || reviewOrigin.Y + review.Bounds.Height > window.ClientSize.Height + 1)
                throw new InvalidOperationException("The final settings action cannot be reached by scrolling.");
            Capture(window, Path.Combine(directory, "settings-minimum-scrolled.png"));
            return new { minimumWidth = window.ClientSize.Width, minimumHeight = window.ClientSize.Height,
                timerAndPetVisible = true, detailsScrollVerified = true, noHorizontalOverflow = true };
        }
        finally
        {
            window.Width = width; window.Height = height; window.MinWidth = minWidth; window.MinHeight = minHeight;
            scroll.ScrollToHome(); await Task.Delay(100);
        }
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
        if (window.GetVisualDescendants().OfType<Border>().FirstOrDefault(item => item.Name == "PageActions") is { } actions)
        {
            foreach (var control in actions.GetVisualDescendants().OfType<Button>())
            {
                var origin = control.TranslatePoint(default, window)!.Value;
                if (origin.X < 0 || origin.Y < 0 || origin.X + control.Bounds.Width > window.ClientSize.Width + 1 ||
                    origin.Y + control.Bounds.Height > window.ClientSize.Height + 1)
                    throw new InvalidOperationException($"Page action is clipped: {window.Title} / {control.Content}.");
            }
        }
        var size = window.ClientSize;
        if (size.Width < 100 || size.Height < 100) throw new InvalidOperationException($"Diagnostic window collapsed before capture: {window.Title} ({size}).");
        using var image = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)), new Vector(96, 96));
        image.Render(window); image.Save(path, PngBitmapEncoderOptions.Default);
    }
}
