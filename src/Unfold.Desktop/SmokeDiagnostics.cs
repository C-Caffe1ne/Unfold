using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
            var breakDurationInput = settings.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "BreakDurationMinutes");
            if (intervalInput.IsEnabled) throw new InvalidOperationException("The running timer allowed interval editing.");
            if (!breakDurationInput.IsEnabled) throw new InvalidOperationException("The running timer blocked the next break duration.");
            Press(settings, "TimerToggle"); await Task.Delay(100);
            if (!runtime.Clock.Paused || !intervalInput.IsEnabled) throw new InvalidOperationException("Pause did not enable interval editing.");
            intervalInput.Value = 25; breakDurationInput.Value = 3; await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes == 25 || runtime.Settings.BreakDurationMinutes == 3)
                throw new InvalidOperationException("Changing home time fields applied before pressing Apply.");
            Press(settings, "ApplyHomeTimingSettings"); await Task.Delay(100);
            var appliedHomeTimes = AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json"));
            if (runtime.Settings.IntervalMinutes != 25 || runtime.Settings.BreakDurationMinutes != 3 ||
                appliedHomeTimes.IntervalMinutes != 25 || appliedHomeTimes.BreakDurationMinutes != 3)
                throw new InvalidOperationException("Apply did not update/persist the home time settings.");
            await Task.Delay(1200);
            if (!runtime.Clock.Paused || runtime.Clock.Remaining != TimeSpan.FromMinutes(25))
                throw new InvalidOperationException("Applying the interval started the paused timer or ignored the new interval.");
            Capture(settings, Path.Combine(directory, "timer-paused.png"));
            Press(settings, "TimerToggle"); if (runtime.Clock.Paused) throw new InvalidOperationException("Play did not resume the timer.");
            Press(settings, "TimerStop"); await Task.Delay(1200);
            var stoppedCountdown = settings.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerCountdown");
            if (!runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.FromMinutes(25) || stoppedCountdown.Text != "25:00" ||
                settings.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "TimerStateDetail"))
                throw new InvalidOperationException("Stop did not keep the configured interval visible.");
            Capture(settings, Path.Combine(directory, "timer-stopped.png"));
            intervalInput.Value = 30; await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes == 30 || !runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.FromMinutes(25))
                throw new InvalidOperationException("Editing an interval applied early or restarted a stopped timer.");
            Press(settings, "ApplyHomeTimingSettings"); await Task.Delay(100);
            if (runtime.Settings.IntervalMinutes != 30 || !runtime.Clock.Stopped || runtime.Clock.Remaining != TimeSpan.FromMinutes(30))
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
            var writing = new BreakRoutine("diagnostic-writing", "글쓰기 휴식", [new("손을 편안하게 쉬어 주세요.", 20)]);
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
            Press(desktop.MainWindow!, "SettingsNavTimer"); await Task.Delay(100);
            if (desktop.MainWindow!.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval").Value != 45)
                throw new InvalidOperationException("Home did not reflect the applied profile interval.");
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
            var windowsBeforeReminder = desktop.Windows.Count;
            await runtime.ShowReminder();
            if (desktop.Windows.Count != windowsBeforeReminder) throw new InvalidOperationException("Reminder created a floating window.");
            var session = runtime.Reminder.Session ?? throw new InvalidOperationException("Reminder did not open.");
            var reminder = runtime.ActivePet ?? throw new InvalidOperationException("Pet did not open.");
            if (session.Routine.Id != writing.Id || session.ProfileId != applied.ActiveProfileId || session.DurationSeconds != 180)
                throw new InvalidOperationException("Profile or configured break duration was not applied to the session.");
            var dueSounds = runtime.DueSoundRequests;
            if (dueSounds != 1) throw new InvalidOperationException("Due reminder did not request exactly one sound.");
            await runtime.ShowReminder();
            if (runtime.Reminder.Session != session || runtime.DueSoundRequests != dueSounds)
                throw new InvalidOperationException("Duplicate reminder restarted the session or sound.");
            VerifySpeechBubble(reminder, "스트레칭할 시간이에요", DesignSystem.SpeechInvitationHeight,
                session.Routine.Name, session.CurrentStep.Instruction, "준비되면 휴식을 시작해 주세요.");
            var themes = await VerifyThemes(settings, runtime, directory);
            foreach (var direction in Enum.GetValues<BubbleDirection>())
            {
                await runtime.UpdateSettings(runtime.Settings with { BubbleDirection = direction });
                await Task.Delay(100); reminder.UpdateLayout();
                Capture(reminder, Path.Combine(directory, "speech-" + direction.ToString().ToLowerInvariant() + ".png"));
            }
            await runtime.UpdateSettings(runtime.Settings with { BubbleDirection = BubbleDirection.Top });
            Press(reminder, "PetBreakStart");
            if (session.State != BreakSessionState.InProgress) throw new InvalidOperationException("Start button did not begin the break.");
            VerifySpeechBubble(reminder, "함께 쉬어 가요", DesignSystem.SpeechRestingHeight, session.CurrentStep.Instruction);
            await runtime.UpdateSettings(runtime.Settings.SaveRoutine(writing with { Name = "Revised writing pause", Steps = [new("Different next time.", 40)] }));
            if (session.Routine.DurationSeconds != 20 || session.DurationSeconds != 180 || session.ProfileId != applied.ActiveProfileId)
                throw new InvalidOperationException("Editing a routine changed an in-progress break.");
            // Establish a synthetic monotonic origin, then advance in valid increments.
            var simulated = TimeSpan.FromDays(1); session.Tick(simulated);
            for (var seconds = 10; seconds <= 190; seconds += 10) session.Tick(simulated + TimeSpan.FromSeconds(seconds));
            reminder.RefreshSpeech();
            if (session.State != BreakSessionState.AwaitingConfirmation || runtime.BreakHistory.Completions.Count != 0 || !PetReminder.TimerText(session).StartsWith('+'))
                throw new InvalidOperationException("Overtime or explicit confirmation failed.");
            VerifySpeechBubble(reminder, "조금 더 쉬어도 좋아요", DesignSystem.SpeechRestingHeight, session.CurrentStep.Instruction);
            await Task.Delay(100); Capture(reminder, Path.Combine(directory, "speech-overtime.png"));
            var petMenu = reminder.ContextMenu?.Items.OfType<MenuItem>().ToArray() ?? [];
            if (petMenu.Length != 1 || petMenu[0].Header as string != "설정" || runtime.Reminder.Session != session)
                throw new InvalidOperationException("The pet context menu still exposes reminder hiding or lost the active session.");
            Press(reminder, "PetBreakComplete");
            var historyFile = Path.Combine(AppPaths.DataRoot, "break-history.json");
            if (runtime.ActiveReminder is not null || BreakHistory.Load(historyFile).Completions.Count != 1 || runtime.CompletionSoundRequests != 1)
                throw new InvalidOperationException("Confirmed break was not saved/sounded exactly once.");
            VerifySpeechBubble(reminder, "스트레칭을 마쳤어요!", DesignSystem.SpeechCompletedHeight, "쉬었어요", "다음 휴식 때");
            await Task.Delay(100); Capture(reminder, Path.Combine(directory, "speech-completed.png"));
            var completed = BreakHistory.Load(historyFile).Completions.Single();
            if (completed.RoutineName != "글쓰기 휴식" || completed.ProfileId != applied.ActiveProfileId || completed.Seconds != 180 || completed.ActualSeconds < 190)
                throw new InvalidOperationException("History lost planned/actual duration or routine/profile context.");
            await runtime.ShowReminder(); Press(reminder, "PetBreakSnooze");
            if (runtime.Clock.Remaining != TimeSpan.FromMinutes(5)) throw new InvalidOperationException("Snooze did not schedule five minutes.");
            await runtime.ShowReminder(); runtime.Stop();
            if (runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Snooze or stop was recorded as a completion.");
            Capture(desktop.MainWindow!, Path.Combine(directory, "settings-completed.png"));
            var reviewWindow = new BreakReviewWindow(runtime.BreakHistory.Review, () => runtime.BreakHistoryError, snapshot =>
            {
                var path = Path.Combine(directory, "review.csv"); AtomicFile.Write(path, snapshot.Csv()); return Task.FromResult<string?>(path);
            });
            AppRuntime.PrepareDiagnosticWindow(reviewWindow); reviewWindow.Show(); await Task.Delay(100); Press(reviewWindow, "ReviewExport");
            if (reviewWindow.Review.Entries.Count != 1 || !File.ReadAllText(Path.Combine(directory, "review.csv")).Contains("글쓰기 휴식"))
                throw new InvalidOperationException("Review/export differs from completed history.");
            Capture(reviewWindow, Path.Combine(directory, "weekly-review.png"));
            var weeklyReview = VerifyExpandedReview(reviewWindow, completed, directory);
            var reviewLayout = await VerifyReviewLayout(settings, reviewWindow, completed, directory);
            reviewWindow.Close();
            await runtime.ShowReminder(); Press(settings, "TimerStop");
            if (runtime.ActiveReminder is not null || runtime.BreakHistory.Completions.Count != 1) throw new InvalidOperationException("Stop did not dismiss the break without recording completion.");
            await runtime.Reload(); var pendingReminder = runtime.ShowReminder(); var stopWhileOpening = !pendingReminder.IsCompleted;
            Press(settings, "TimerStop"); await pendingReminder;
            if (runtime.ActiveReminder is not null || !runtime.Clock.Stopped) throw new InvalidOperationException("A reminder survived Stop.");
            var soundsBeforeMute = runtime.DueSoundRequests;
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = false, ReminderSoundsEnabled = false });
            if (reminder.IsVisible) throw new InvalidOperationException("Hidden pet did not hide.");
            await runtime.ShowReminder();
            if (!reminder.IsVisible || runtime.Settings.ShowPet) throw new InvalidOperationException("Hidden pet reminder changed its saved visibility.");
            if (runtime.DueSoundRequests != soundsBeforeMute) throw new InvalidOperationException("Muted reminder requested a sound.");
            runtime.SnoozeBreak();
            if (reminder.IsVisible) throw new InvalidOperationException("Temporary reminder pet did not hide after snooze.");
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = true, ReminderSoundsEnabled = true });
            runtime.Reminder.ShowAdvance(TimeSpan.FromDays(2)); reminder.RefreshSpeech();
            VerifySpeechBubble(reminder, "5분 뒤에 스트레칭해요", DesignSystem.SpeechAdvanceHeight, "하던 일을");
            await Task.Delay(100); Capture(reminder, Path.Combine(directory, "speech-five-minutes.png"));
            runtime.Stop();
            var previewRemaining = runtime.Clock.Remaining;
            var previewHistory = runtime.BreakHistory.Completions.Count;
            var previewDueSounds = runtime.DueSoundRequests; var previewCompletionSounds = runtime.CompletionSoundRequests;
            foreach (var notice in new[] { PetNotice.Advance, PetNotice.Invitation, PetNotice.Resting, PetNotice.Completed })
            {
                await runtime.ShowReminderPreview(notice); reminder.RefreshSpeech(); await Task.Delay(50);
                if (runtime.PreviewNotice != notice || runtime.Reminder.HasNotice || runtime.Clock.Remaining != previewRemaining ||
                    runtime.BreakHistory.Completions.Count != previewHistory || runtime.DueSoundRequests != previewDueSounds ||
                    runtime.CompletionSoundRequests != previewCompletionSounds)
                    throw new InvalidOperationException("A debug preview changed live timer, reminder, history or sound state.");
            }
            runtime.CloseReminderPreview();
            foreach (var scale in new[] { 50, 100, 150 })
            {
                await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = scale }); reminder.RefreshSpeech();
                var expected = DesignSystem.PetBaseSize * scale / 100d;
                if (Math.Abs(reminder.PetView.Width - expected) > .5 || Math.Abs(reminder.PetView.Height - expected) > .5)
                    throw new InvalidOperationException("The desktop pet did not apply the saved scale.");
            }
            await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 100 });
            var soundLibrary = new ReminderSounds(Path.Combine(directory, "sounds"));
            foreach (var sound in Enum.GetValues<ReminderSound>()) ReminderSounds.Validate(File.ReadAllBytes(soundLibrary.Resolve(sound, null)));
            editor.CloseAfterApproval();
            await VerifyPetPacks(runtime, saved, directory);
            await VerifyCustomPet(runtime, directory);
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
                timerPausedApplyWaitSeconds = 1.2, timerStopWaitSeconds = 1.2, timerControlsVerified = true, petSpeechDirectionsVerified = true,
                petContextMenuVerified = true, petScaleAndDebugPreviewVerified = true, speechTitleOnlyGeometryVerified = true,
                soundRequestsVerified = true, hiddenPetNoticeVerified = true, stopWhileOpening,
                petPackInstallUpdateRepairVerified = true, simulatedPackPicker = true, settingsLayout, weeklyReview, reviewLayout, themes,
                customPetGifAuthoringVerified = true, petCardPreviewVerified = true,
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
    private static void VerifySpeechBubble(PetWindow window, string expectedTitle, double expectedHeight, params string[] forbiddenBodyText)
    {
        window.UpdateLayout();
        var bubble = window.GetVisualDescendants().OfType<PetSpeechBubble>().Single();
        var title = bubble.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTitle");
        if (Math.Abs(bubble.Width - DesignSystem.SpeechBubbleWidth) > .5 || Math.Abs(bubble.Height - expectedHeight) > .5)
            throw new InvalidOperationException("Speech bubble dimensions differ from the approved state geometry.");
        if (title.Text != expectedTitle || title.TextAlignment != Avalonia.Media.TextAlignment.Center ||
            Math.Abs(title.FontSize - DesignSystem.Section) > .5)
            throw new InvalidOperationException("Speech bubble title differs from the approved centered hierarchy.");
        var texts = bubble.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? "").ToArray();
        if (forbiddenBodyText.Any(body => texts.Any(text => text.Contains(body, StringComparison.Ordinal))))
            throw new InvalidOperationException("Speech bubble still exposes removed instruction body text.");
        if (bubble.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible)
            .Any(button => Math.Abs(button.Bounds.Height - DesignSystem.SpeechControlHeight) > .5))
            throw new InvalidOperationException("Speech bubble controls differ from the approved height.");
    }
    private static async Task<object> VerifyThemes(Window window, AppRuntime runtime, string directory)
    {
        var originalTheme = runtime.Settings.Theme;
        var width = window.Width; var height = window.Height;
        var minWidth = window.MinWidth; var minHeight = window.MinHeight;
        var button = window.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "SettingsTheme");
        var flyout = (Flyout)button.Flyout!;
        var choices = (Control)flyout.Content!;
        var verified = new List<string>();
        try
        {
            window.MinWidth = 860; window.MinHeight = 680;
            window.Width = 860; window.Height = 680; await Task.Delay(100); window.UpdateLayout();
            var quit = window.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "SettingsQuit");
            var origin = button.TranslatePoint(default, window)!.Value;
            var quitOrigin = quit.TranslatePoint(default, window)!.Value;
            if (origin.Y < 0 || origin.Y + button.Bounds.Height >= quitOrigin.Y || quitOrigin.Y + quit.Bounds.Height > window.ClientSize.Height)
                throw new InvalidOperationException("Theme/quit buttons overlap or clip at minimum size.");
            window.Width = 1120; window.Height = 800;
            foreach (var palette in DesignSystem.Themes)
            {
                Press(window, "SettingsNavTimer"); await Task.Delay(100);
                flyout.ShowAt(button); await Task.Delay(80);
                choices.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "Theme" + palette.Id)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(100); window.UpdateLayout();
                if (runtime.Settings.Theme != palette.Id || AppSettings.Load(Path.Combine(AppPaths.DataRoot, "settings.json")).Theme != palette.Id)
                    throw new InvalidOperationException("Theme selection did not persist: " + palette.Name);
                Capture(window, Path.Combine(directory, "theme-" + palette.Id + "-home.png"));
                Capture(runtime.ActivePet!, Path.Combine(directory, "theme-" + palette.Id + "-speech.png"));
                flyout.ShowAt(button); await Task.Delay(80);
                var presenter = choices.GetVisualAncestors().OfType<FlyoutPresenter>().Single();
                using (var image = new RenderTargetBitmap(new PixelSize((int)presenter.Bounds.Width, (int)presenter.Bounds.Height), new Vector(96, 96)))
                { image.Render(presenter); image.Save(Path.Combine(directory, "theme-" + palette.Id + "-picker.png"), PngBitmapEncoderOptions.Default); }
                flyout.Hide();
                foreach (var (tab, file) in new[] { ("SettingsNavSettings", "settings"), ("SettingsNavReview", "review"), ("SettingsNavPacks", "pets") })
                {
                    Press(window, tab); await Task.Delay(100); window.UpdateLayout();
                    Capture(window, Path.Combine(directory, "theme-" + palette.Id + "-" + file + ".png"));
                }
                var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(item => item.Name == "PetManagementTabs");
                tabs.SelectedIndex = 1; await Task.Delay(100);
                Capture(window, Path.Combine(directory, "theme-" + palette.Id + "-builder.png")); tabs.SelectedIndex = 0;
                verified.Add(palette.Name);
            }
        }
        finally
        {
            flyout.Hide(); runtime.SetTheme(originalTheme); Press(window, "SettingsNavTimer");
            window.MinWidth = minWidth; window.MinHeight = minHeight; window.Width = width; window.Height = height;
        }
        return new { palettes = verified, persisted = true, minimumNavigationFits = true, simulatedSelection = true };
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
    private static async Task VerifyCustomPet(AppRuntime runtime, string directory)
    {
        var gif = Path.Combine(AppPaths.BuiltInRoot, "default-cat", "stretch.gif");
        var output = Path.Combine(directory, "custom-pet.unfoldpet");
        var window = new CustomPetWindow(gif, () => Task.FromResult<string?>(gif), () => Task.FromResult<string?>(output));
        AppRuntime.PrepareDiagnosticWindow(window); window.Show();
        try
        {
            window.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "CustomPetName").Text = "나만의 펫";
            Press(window, "AssignPetMedia");
            Button Create() => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CreateCustomPetPack");
            await Until(() => Create().IsEnabled);
            Press(window, "CustomPetFile_click"); await Until(() => Create().IsEnabled);
            Press(window, "CustomPetPreview_idle");
            await Until(() => window.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Name == "CustomPetPreviewAction").Text == "미리보기 · 쉬는 모습");
            window.UpdateLayout();
            var selectedCard = window.GetVisualDescendants().OfType<Border>().Single(card => card.Name == "CustomPetSlot_idle");
            var cardPreview = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CustomPetPreview_idle");
            if (selectedCard.BorderBrush != DesignSystem.Cream || cardPreview.Content is PathIcon ||
                Math.Abs(cardPreview.Bounds.Width - selectedCard.Bounds.Width + 2) > 1)
                throw new InvalidOperationException("The selected action does not use a full-card preview control.");
            Capture(window, Path.Combine(directory, "custom-pet-editor.png"));
            VerifyBodyScrollGutter(window, PetBuilderControls);
            VerifyPetActionCards(window, expectWrap: true);
            window.Width = 590; window.Height = 750; await Task.Delay(200); window.UpdateLayout();
            VerifyBodyScrollGutter(window, PetBuilderControls);
            VerifyPetActionCards(window, expectWrap: true);
            window.MinWidth = window.Width = 520; window.MinHeight = window.Height = 620; await Task.Delay(200); window.UpdateLayout();
            if (Math.Abs(window.ClientSize.Width - 520) > 1 || Math.Abs(window.ClientSize.Height - 620) > 1)
                throw new InvalidOperationException("Custom pet form did not reach its minimum size.");
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
            if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new InvalidOperationException("Custom pet form overflows horizontally.");
            var origin = Create().TranslatePoint(default, window)!.Value;
            if (origin.Y + Create().Bounds.Height > window.ClientSize.Height) throw new InvalidOperationException("Custom pet create button is outside the window.");
            VerifyBodyScrollGutter(window, PetBuilderControls);
            VerifyPetActionCards(window, expectWrap: true);
            Capture(window, Path.Combine(directory, "custom-pet-minimum.png"));
            scroll.ScrollToEnd(); await Task.Delay(100); window.UpdateLayout();
            VerifyBodyScrollGutter(window, PetBuilderControls);
            Capture(window, Path.Combine(directory, "custom-pet-minimum-scrolled.png"));
            Press(window, "CreateCustomPetPack"); await Until(() => window.CreatedPackPath is not null);
        }
        finally { window.Close(); }
        using var pack = CharacterPack.Open(output);
        if (pack.Character.Manifest.Animations.Count != 2 || pack.Character.Manifest.Animations["click"].Loop)
            throw new InvalidOperationException("Custom pet action mapping changed during export.");
        var installWindow = new PetPackWindow(runtime.Library, runtime.SelectInstalledCharacter, () => Task.FromResult<string?>(output));
        AppRuntime.PrepareDiagnosticWindow(installWindow); installWindow.Show();
        try
        {
            Button Install() => installWindow.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "InstallPetPack");
            Press(installWindow, "OpenPetPack"); await Until(() => Install().IsEnabled);
            await Until(() => installWindow.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PausePackPreview").IsEnabled);
            await Task.Delay(100);
            Capture(installWindow, Path.Combine(directory, "custom-pet-preview.png"));
            Press(installWindow, "InstallPetPack"); await Until(() => Equals(Install().Content, "설치 완료"));
            if (runtime.Selected?.Manifest.Id != pack.Id || !runtime.Clock.Paused)
                throw new InvalidOperationException("Custom pet installation did not select the pet or changed timer pause.");
        }
        finally { installWindow.Close(); }
    }
    // UI-04: the shared page body reserves the vertical scrollbar's track instead of letting
    // Fluent paint it over the content. Compare the real right edge of each control with the
    // scrollbar's left edge; visibility alone does not prove the two do not share pixels.
    private static void VerifyBodyScrollGutter(Window window, params string[] names)
    {
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
        if (scroll.Extent.Width > scroll.Viewport.Width + 1)
            throw new InvalidOperationException($"Page body overflows horizontally: {window.Title}.");
        var body = (Control)scroll.Content!;
        var bodyRight = body.TranslatePoint(default, window)!.Value.X + body.Bounds.Width;
        var bar = scroll.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
            .Single(item => item.Orientation == Avalonia.Layout.Orientation.Vertical &&
                item.GetVisualAncestors().OfType<ScrollViewer>().First() == scroll);
        if (!bar.IsVisible)
        {
            if (Math.Abs(body.Bounds.Width - scroll.Viewport.Width) > 1)
                throw new InvalidOperationException($"Page body lost width without a scrollbar: {window.Title}.");
            return;
        }
        var barLeft = bar.TranslatePoint(default, window)!.Value.X;
        if (Math.Abs(barLeft - bodyRight - Ui.ScrollGutter) > 1)
            throw new InvalidOperationException($"Page body gutter is {barLeft - bodyRight:0.#}, not {Ui.ScrollGutter}: {window.Title}.");
        foreach (var name in names)
        {
            var control = window.GetVisualDescendants().OfType<Control>().Single(item => item.Name == name);
            if (!control.IsVisible || control.Bounds.Width <= 0)
                throw new InvalidOperationException($"Page control is not laid out: {name}.");
            var right = control.TranslatePoint(default, window)!.Value.X + control.Bounds.Width;
            if (right > barLeft - Ui.ScrollGutter + .5)
                throw new InvalidOperationException($"Scrollbar covers {name}: it ends at {right:0.#} and the track starts at {barLeft:0.#}.");
        }
    }
    private static readonly string[] PetBuilderControls =
        ["CustomPetName", "CustomPetFile_idle", "CustomPetRemove_idle", "CustomPetPreview_idle"];
    private static object VerifyExpandedReview(BreakReviewWindow window, CompletedBreak completed, string directory)
    {
        var day = DateOnly.FromDateTime(completed.CompletedAt.Date);
        var header = window.GetVisualDescendants().OfType<ToggleButton>()
            .Single(control => control.Name == $"ReviewDateHeader_{day:yyyyMMdd}");
        header.IsChecked = true; window.UpdateLayout();
        var details = window.GetVisualDescendants().OfType<StackPanel>()
            .Single(control => control.Name == $"ReviewDetails_{day:yyyyMMdd}");
        var texts = details.GetVisualDescendants().OfType<TextBlock>().Where(text =>
        {
            var origin = text.TranslatePoint(default, window);
            return text.IsVisible && origin is not null && origin.Value.X >= 0 && origin.Value.Y >= 0 &&
                origin.Value.X + text.Bounds.Width <= window.ClientSize.Width + .5 &&
                origin.Value.Y + text.Bounds.Height <= window.ClientSize.Height + .5;
        }).Select(text => text.Text).ToArray();
        var routineName = completed.RoutineName ?? completed.RoutineId;
        var completionTime = $"{completed.CompletedAt.ToString("HH:mm", CultureInfo.InvariantCulture)} 완료";
        if (completed.ActualSeconds is not int actualSeconds)
            throw new InvalidOperationException("The diagnostic completion has no actual break duration.");
        var actualDuration = actualSeconds < 60
            ? $"실제 휴식 {actualSeconds}초"
            : $"실제 휴식 {actualSeconds / 60}분 {actualSeconds % 60:00}초";
        var routineNameVisible = texts.Contains(routineName);
        var completionTimeVisible = texts.Contains(completionTime);
        var actualDurationVisible = texts.Contains(actualDuration);
        if (header.IsChecked != true || !details.IsVisible || routineNameVisible || texts.Length != 2 || !completionTimeVisible || !actualDurationVisible)
            throw new InvalidOperationException("The expanded weekly review must show only completion time and actual break duration.");
        const string captureFile = "weekly-review-expanded.png";
        Capture(window, Path.Combine(directory, captureFile));
        return new { expandedCaptured = true, captureFile, routineNameVisible, completionTimeVisible, actualDurationVisible };
    }
    private static async Task<object> VerifyReviewLayout(Window settings, BreakReviewWindow compatibility, CompletedBreak completed, string directory)
    {
        var measurements = new List<object>();
        var day = DateOnly.FromDateTime(completed.CompletedAt.Date);
        settings.MinWidth = 860; settings.MinHeight = 680;
        Press(settings, "SettingsNavReview");
        compatibility.MinWidth = 560; compatibility.MinHeight = 600;
        foreach (var item in new[] { (settings, new Size(1120, 800), "review-default.png"),
            (settings, new Size(860, 680), "review-minimum.png"),
            ((Window)compatibility, new Size(620, 650), "review-compatibility.png"),
            ((Window)compatibility, new Size(560, 600), "review-compatibility-minimum.png") })
        {
            var (window, size, file) = item;
            window.Width = size.Width; window.Height = size.Height; await Task.Delay(100); window.UpdateLayout();
            if (window.ClientSize != size) throw new InvalidOperationException($"Review diagnostic size differs: {window.ClientSize} instead of {size}.");
            T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
            Find<ToggleButton>($"ReviewDateHeader_{day:yyyyMMdd}").IsChecked = true;
            var scroll = Find<ScrollViewer>("PageBodyScroll"); scroll.ScrollToHome(); window.UpdateLayout();
            var cardWidth = Find<Border>("ReviewSummaryCard").Bounds.Width;
            if (cardWidth > DesignSystem.ReviewContentWidth + .5 || scroll.Extent.Width > scroll.Viewport.Width + .5)
                throw new InvalidOperationException("Review exceeds its content width or scrolls horizontally.");
            var fields = Find<Border>($"ReviewEntry_{completed.SessionId:N}").GetVisualDescendants().OfType<TextBlock>().ToArray();
            if (fields.Length != 2 || fields.Any(text => text.Text == completed.RoutineName))
                throw new InvalidOperationException("Review detail exposes fields other than completion time and actual duration.");
            foreach (var name in new[] { "ReviewPrevious", "ReviewNext", "ReviewRefresh" })
                if (Find<Button>(name).Bounds.Size != new Size(40, 40))
                    throw new InvalidOperationException("Review period controls differ from the approved 40px size.");
            var footer = Find<Grid>("ReviewFooter"); var footerTop = footer.TranslatePoint(default, window)!.Value.Y;
            if (footerTop < scroll.TranslatePoint(default, window)!.Value.Y + scroll.Bounds.Height ||
                footerTop + footer.Bounds.Height > window.ClientSize.Height + .5)
                throw new InvalidOperationException("Review footer is not fully visible below the scroll area.");
            Capture(window, Path.Combine(directory, file));
            scroll.ScrollToEnd(); window.UpdateLayout();
            if (footer.TranslatePoint(default, window)!.Value.Y != footerTop)
                throw new InvalidOperationException("Review footer moved while the body scrolled.");
            measurements.Add(new { width = size.Width, height = size.Height, cardWidth, footerTop,
                scrollRequired = scroll.Extent.Height > scroll.Viewport.Height, capture = file });
        }
        settings.Width = 1120; settings.Height = 800; Press(settings, "SettingsNavTimer");
        await Task.Delay(100); settings.UpdateLayout();
        return new { measurements, completionAndActualOnly = true, noHorizontalOverflow = true, pinnedFooter = true };
    }
    private static void VerifySettingsPageHeaderRemoved(Window window)
    {
        if (window.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "PageHeader") ||
            window.GetVisualDescendants().OfType<TextBlock>().Any(text =>
                (text.Text ?? "").StartsWith("UNFOLD /", StringComparison.Ordinal) || text.Text is
                    "잠깐의 여유를 만들어 보세요." or "알림과 타이머를 설정하세요." or
                    "나를 위해 만든 여유." or "새로운 친구를 만나 보세요." or
                    "나만의 펫을 만들어 보세요." or "나의 페이스대로"))
            throw new InvalidOperationException("A removed settings tab page header remains visible.");
    }
    private static void VerifyPetActionCards(Window window, bool expectWrap)
    {
        if (window.GetVisualDescendants().OfType<ScrollViewer>().Any(view => view.Name == "CustomPetActionSlotsScroll"))
            throw new InvalidOperationException("The pet action cards still use a horizontal scrollbar.");
        var slots = window.GetVisualDescendants().OfType<WrapPanel>()
            .Single(panel => panel.Name == "CustomPetActionSlots");
        var rows = slots.Children.Select(card => Math.Round(card.Bounds.Y, 1)).Distinct().Count();
        if (expectWrap && rows <= 1)
            throw new InvalidOperationException("The pet action cards did not wrap at a narrow window size.");
        if (!expectWrap && rows != 1)
            throw new InvalidOperationException("The five pet action cards do not fit on one row.");
        foreach (var card in slots.Children)
            if (card.Bounds.X < -.5 || card.Bounds.Right > slots.Bounds.Width + .5)
                throw new InvalidOperationException($"The pet action card {card.Name} overflows horizontally.");
        var page = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
        if (page.Extent.Width > page.Viewport.Width + 1)
            throw new InvalidOperationException("The wrapped pet action cards overflow the page horizontally.");
    }
    private static async Task<object> VerifySettingsLayout(Window window, string directory)
    {
        var width = window.Width; var height = window.Height;
        var minWidth = window.MinWidth; var minHeight = window.MinHeight;
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "SettingsDetailsScroll");
        try
        {
            VerifySettingsPageHeaderRemoved(window);
            var homeDashboard = await VerifyHomeDashboard(window, directory);
            var navigation = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Name?.StartsWith("SettingsNav", StringComparison.Ordinal) == true).ToArray();
            if (navigation.Length != 4 || !navigation.Any(button => button.Name == "SettingsNavPacks") ||
                !navigation.Any(button => button.Name == "SettingsNavSettings") ||
                navigation.Any(button => button.Name == "SettingsNavRoutines"))
                throw new InvalidOperationException("Settings sidebar does not contain the four expected content tabs.");
            if (window.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "SettingsInstallPack"))
                throw new InvalidOperationException("The old pet-card add button remains.");
            var picker = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "CharacterPicker");
            if (picker.Bounds.Width != 200) throw new InvalidOperationException("Pet selector is not compact.");
            if (window.GetVisualDescendants().OfType<Control>().Any(control => control.Name is
                "RoutinePicker" or "SettingsEditRoutine" or "SettingsOpenLibrary" or "ApplyRoutineSettings" or "PersonalizationTabs"))
                throw new InvalidOperationException("Routine or work-profile controls remain reachable from settings.");
            Press(window, "SettingsNavReview"); await Task.Delay(100); window.UpdateLayout();
            VerifySettingsPageHeaderRemoved(window);
            if (window.OwnedWindows.Count != 0 || !window.GetVisualDescendants().OfType<TextBlock>().Any(control => control.Name == "ReviewStatus"))
                throw new InvalidOperationException("Review navigation opened a window instead of the in-window tab.");
            Capture(window, Path.Combine(directory, "settings-review-tab.png"));
            Press(window, "SettingsNavPacks"); await Task.Delay(100); window.UpdateLayout();
            VerifySettingsPageHeaderRemoved(window);
            var petTabs = window.GetVisualDescendants().OfType<TabControl>().Single(control => control.Name == "PetManagementTabs");
            if (window.OwnedWindows.Count != 0 || petTabs.ItemCount != 2)
                throw new InvalidOperationException("Pet navigation did not open the two in-window tabs.");
            var packPreview = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PackPreviewSurface");
            var packClip = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "PackClip");
            if (Math.Abs(packPreview.Bounds.Width - 520) > 1 || Math.Abs(packClip.Bounds.Width - 200) > 1)
                throw new InvalidOperationException("The pet pack preview or action picker is not compact.");
            Capture(window, Path.Combine(directory, "settings-pet-open-tab.png"));
            petTabs.SelectedIndex = 1; await Task.Delay(100); window.UpdateLayout();
            VerifySettingsPageHeaderRemoved(window);
            var petName = window.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "CustomPetName");
            if (Math.Abs(petName.Bounds.Width - 320) > 1 ||
                window.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "ImportPetMedia"))
                throw new InvalidOperationException("The pet name field is not compact or the removed import button remains.");
            petName.Text = "작성 중인 펫";
            VerifyPetActionCards(window, expectWrap: false);
            VerifyBodyScrollGutter(window, PetBuilderControls);
            window.Width = 990; window.Height = 740; await Task.Delay(200); window.UpdateLayout();
            VerifyPetActionCards(window, expectWrap: false);
            VerifyBodyScrollGutter(window, PetBuilderControls);
            window.Width = 1120; window.Height = 800; await Task.Delay(200); window.UpdateLayout();
            Capture(window, Path.Combine(directory, "settings-pet-create-tab.png"));
            Press(window, "SettingsNavTimer"); await Task.Delay(100);
            Press(window, "SettingsNavPacks"); await Task.Delay(100); window.UpdateLayout();
            if (petTabs.SelectedIndex != 1 || petName.Text != "작성 중인 펫")
                throw new InvalidOperationException("Pet draft was lost during sidebar navigation.");
            window.MinWidth = 860; window.MinHeight = 680; window.Width = 860; window.Height = 680;
            await Task.Delay(200); window.UpdateLayout();
            VerifyPetActionCards(window, expectWrap: false);
            Capture(window, Path.Combine(directory, "settings-pet-create-minimum.png"));
            var petScroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PageBodyScroll");
            if (petScroll.Extent.Width > petScroll.Viewport.Width + 1)
                throw new InvalidOperationException("Pet builder overflows horizontally.");
            foreach (var key in CustomPetDraft.Actions)
            {
                var add = window.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "CustomPetFile_" + key);
                var location = add.TranslatePoint(default, petScroll)!.Value;
                if (location.Y < 0 || location.Y + add.Bounds.Height > petScroll.Viewport.Height + 1)
                    throw new InvalidOperationException($"The {key} action needs scrolling at the minimum settings size.");
            }
            VerifyBodyScrollGutter(window, PetBuilderControls);
            petScroll.ScrollToEnd(); await Task.Delay(100); window.UpdateLayout();
            VerifyBodyScrollGutter(window, PetBuilderControls);
            var lastSlot = window.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "CustomPetFile_click");
            var lastSlotOrigin = lastSlot.TranslatePoint(default, window)!.Value;
            if (lastSlotOrigin.Y < 0 || lastSlotOrigin.Y + lastSlot.Bounds.Height > window.ClientSize.Height)
                throw new InvalidOperationException("The last pet action cannot be reached by scrolling.");
            Capture(window, Path.Combine(directory, "settings-pet-create-minimum-scrolled.png"));
            petName.Text = "";
            petTabs.SelectedIndex = 0; await Task.Delay(100); window.UpdateLayout();
            Capture(window, Path.Combine(directory, "settings-pet-open-minimum.png"));
            Press(window, "SettingsNavTimer"); await Task.Delay(100); window.UpdateLayout();
            VerifySettingsPageHeaderRemoved(window);
            window.MinWidth = 860; window.MinHeight = 680; window.Width = 860; window.Height = 680;
            await Task.Delay(200); window.UpdateLayout();
            if (Math.Abs(window.ClientSize.Width - 860) > 1 || Math.Abs(window.ClientSize.Height - 680) > 1)
                throw new InvalidOperationException($"Settings did not reach the minimum diagnostic size: {window.ClientSize}.");
            foreach (var name in new[] { "SettingsCompanionCard", "SettingsTimerCard", "SettingsHomeTimingCard", "ReminderInterval", "BreakDurationMinutes", "TimerToggle", "TimerStop", "SettingsQuit" })
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
            Press(window, "SettingsNavSettings"); await Task.Delay(100); window.UpdateLayout();
            VerifySettingsPageHeaderRemoved(window);
            var preferences = window.GetVisualDescendants().OfType<Grid>().Single(control => control.Name == "SettingsPreferencesPage");
            var notificationSettings = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "SettingsNotificationCard");
            var timerSettings = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "SettingsTimerSettingsCard");
            var appBehaviorSettings = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "SettingsAppBehaviorCard");
            var debugSettings = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "SettingsDebugToolsCard");
            if (!preferences.IsVisible || !notificationSettings.IsVisible || !timerSettings.IsVisible || !appBehaviorSettings.IsVisible || !debugSettings.IsVisible)
                throw new InvalidOperationException("The settings tab did not expose all settings sections.");
            foreach (var name in new[] { "ShowPetOnDesktop", "LaunchAtLogin" })
            {
                var option = window.GetVisualDescendants().OfType<CheckBox>().Single(control => control.Name == name);
                if (!appBehaviorSettings.GetVisualDescendants().Contains(option))
                    throw new InvalidOperationException($"The app behavior option is outside its settings card: {name}.");
            }
            if (timerSettings.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "스트레칭 시간 (분)"))
                throw new InvalidOperationException("The stretch interval still appears in the Settings tab.");
            var preferencesScroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "SettingsPreferencesScroll");
            if (preferencesScroll.Extent.Width > preferencesScroll.Viewport.Width + 1)
                throw new InvalidOperationException("The settings tab overflows horizontally.");
            var settingsOptionsExtentHeight = preferencesScroll.Extent.Height;
            var settingsOptionsViewportHeight = preferencesScroll.Viewport.Height;
            var settingsOptionsScrollRequired = settingsOptionsExtentHeight > settingsOptionsViewportHeight;
            var settingsOptionsTopOffset = 0d; double? settingsOptionsBottomOffset = null;
            bool? settingsOptionsCapturesDiffer = null;
            string[] settingsOptionsCaptureFiles;
            preferencesScroll.ScrollToHome(); await Task.Delay(100); window.UpdateLayout();
            settingsOptionsTopOffset = preferencesScroll.Offset.Y;
            if (Math.Abs(settingsOptionsTopOffset) > .5)
                throw new InvalidOperationException($"The settings options top offset is not zero: {settingsOptionsTopOffset:0.##}.");
            if (settingsOptionsScrollRequired)
            {
                const string topFile = "settings-options-top.png";
                const string bottomFile = "settings-options-bottom.png";
                var topPath = Path.Combine(directory, topFile); var bottomPath = Path.Combine(directory, bottomFile);
                Capture(window, topPath);
                preferencesScroll.ScrollToEnd(); await Task.Delay(100); window.UpdateLayout();
                var bottomOffset = preferencesScroll.Offset.Y; settingsOptionsBottomOffset = bottomOffset;
                if (bottomOffset <= 0)
                    throw new InvalidOperationException("The settings options require scrolling but did not reach a positive bottom offset.");
                var appBehaviorOrigin = appBehaviorSettings.TranslatePoint(default, preferencesScroll);
                if (appBehaviorOrigin is null || appBehaviorOrigin.Value.Y < -.5 ||
                    appBehaviorOrigin.Value.Y + appBehaviorSettings.Bounds.Height > settingsOptionsViewportHeight + .5)
                    throw new InvalidOperationException("The app behavior card is not fully visible at the bottom scroll offset.");
                Capture(window, bottomPath);
                settingsOptionsCapturesDiffer = !File.ReadAllBytes(topPath).SequenceEqual(File.ReadAllBytes(bottomPath));
                if (settingsOptionsCapturesDiffer != true)
                    throw new InvalidOperationException("The settings options top and bottom captures are byte-identical.");
                settingsOptionsCaptureFiles = [topFile, bottomFile];
            }
            else
            {
                const string overviewFile = "settings-options-overview.png";
                Capture(window, Path.Combine(directory, overviewFile));
                settingsOptionsCaptureFiles = [overviewFile];
            }
            var settingsPreferencesStyling = await VerifyPreferencesStyling(window, directory);
            Press(window, "SettingsNavTimer"); await Task.Delay(100);
            return new { minimumWidth = window.ClientSize.Width, minimumHeight = window.ClientSize.Height,
                timerAndPetVisible = true, detailsScrollVerified = true, noHorizontalOverflow = true,
                inWindowTabsVerified = true, settingsTabVerified = true, homeTimingVerified = true, petManagementTabsVerified = true,
                routineProfileEntryRemoved = true,
                petDraftPreserved = true, petCardAddButtonRemoved = true, petSelectorWidth = picker.Bounds.Width,
                settingsOptionsScrollRequired, settingsOptionsExtentHeight, settingsOptionsViewportHeight,
                settingsOptionsTopOffset, settingsOptionsBottomOffset, settingsOptionsCapturesDiffer, settingsOptionsCaptureFiles,
                settingsPreferencesStyling, homeDashboard };
        }
        finally
        {
            window.Width = width; window.Height = height; window.MinWidth = minWidth; window.MinHeight = minHeight;
            scroll.ScrollToHome(); await Task.Delay(100);
        }
    }
    private static async Task<object> VerifyHomeDashboard(Window window, string directory)
    {
        T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
        var measurements = new List<object>();
        // PrepareDiagnosticWindow initially pins the minimum to the startup size.
        window.MinWidth = 860; window.MinHeight = 680;
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680) })
        {
            window.Width = size.Width; window.Height = size.Height; await Task.Delay(100); window.UpdateLayout();
            if (window.ClientSize != size) throw new InvalidOperationException($"Home diagnostic size differs: {window.ClientSize} instead of {size}.");
            var timer = Find<Border>("SettingsTimerCard"); var pet = Find<Border>("SettingsCompanionCard");
            var timing = Find<Border>("SettingsHomeTimingCard");
            var timerTop = timer.TranslatePoint(default, window)!.Value.Y;
            var petTop = pet.TranslatePoint(default, window)!.Value.Y;
            if (Math.Abs(timer.Bounds.Height - 196) > .5 || Math.Abs(petTop - timerTop - timer.Bounds.Height - 20) > .5 ||
                Math.Abs(timing.TranslatePoint(default, window)!.Value.Y - timerTop) > .5)
                throw new InvalidOperationException("Home does not keep the timer above the pet with aligned timing settings.");
            foreach (var name in new[] { "ReminderInterval", "BreakDurationMinutes" })
                if (Find<NumericUpDown>(name).Bounds.Size != new Size(160, 40))
                    throw new InvalidOperationException("Home timing input dimensions differ from the approved design.");
            var picker = Find<ComboBox>("CharacterPicker");
            if (picker.Bounds.Size != new Size(200, 40)) throw new InvalidOperationException("Home pet selector dimensions differ.");
            var scaleLabel = Find<TextBlock>("PetScaleLabel"); var scale = Find<Slider>("PetScale");
            var scaleValue = Find<TextBlock>("PetScaleValue");
            var scaleLabelTop = scaleLabel.TranslatePoint(default, pet)!.Value.Y;
            var scaleTop = scale.TranslatePoint(default, pet)!.Value.Y;
            var scaleValueTop = scaleValue.TranslatePoint(default, pet)!.Value.Y;
            if (scaleLabelTop + scaleLabel.Bounds.Height > scaleTop + .5 ||
                scaleTop + scale.Bounds.Height > scaleValueTop + .5)
                throw new InvalidOperationException("Home pet scale label and percentage are not stacked around the slider.");
            VerifyHomeActions(window);
            var file = size.Width == 860 ? "home-minimum.png" : "home-default.png";
            Capture(window, Path.Combine(directory, file));
            measurements.Add(new { width = window.ClientSize.Width, height = window.ClientSize.Height,
                timerTop, timerHeight = timer.Bounds.Height, petTop, petHeight = pet.Bounds.Height, capture = file });
        }
        // Layout-only stress fixture. Save failure and state transitions are exercised separately by tests.
        var nameText = Find<TextBlock>("CompanionName"); var status = Find<TextBlock>("HomeTimingStatus");
        var nameBefore = nameText.Text; var statusBefore = status.Text; var statusBrush = status.Foreground; var statusVisible = status.IsVisible;
        nameText.Text = "오래 함께할 아주 긴 이름을 가진 나만의 새로운 고양이 친구";
        status.Text = "저장하지 못했어요."; status.Foreground = DesignSystem.Error; status.IsVisible = true;
        window.UpdateLayout(); VerifyHomeActions(window);
        Capture(window, Path.Combine(directory, "home-minimum-long-name-error.png"));
        nameText.Text = nameBefore; status.Text = statusBefore; status.Foreground = statusBrush; status.IsVisible = statusVisible;
        var choice = Find<ComboBox>("CharacterPicker"); choice.IsDropDownOpen = true;
        await Task.Delay(100); window.UpdateLayout();
        try
        {
            var popup = choice.GetVisualDescendants().OfType<Popup>().Single();
            if (popup.Child is not Border surface || Math.Abs(surface.Bounds.Width - 200) > 1 || surface.Bounds.Height <= 0)
                throw new InvalidOperationException("The home dropdown did not create a compact visible popup.");
            using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(surface.Bounds.Width), (int)Math.Ceiling(surface.Bounds.Height)), new Vector(96, 96));
            image.Render(surface); image.Save(Path.Combine(directory, "home-pet-picker.png"), PngBitmapEncoderOptions.Default);
        }
        finally { choice.IsDropDownOpen = false; }
        window.Width = 1120; window.Height = 800; await Task.Delay(100); window.UpdateLayout();
        return new { timerFirst = true, measurements, longNameAndErrorLayoutFixture = true, compactControls = true };
    }

    private static void VerifyHomeActions(Window window)
    {
        foreach (var pair in new[] { ("TimerToggle", "SettingsTimerCard"), ("TimerStop", "SettingsTimerCard"),
            ("CharacterPicker", "SettingsCompanionCard"), ("PetScale", "SettingsCompanionCard") })
        {
            var control = window.GetVisualDescendants().OfType<Control>().Single(item => item.Name == pair.Item1);
            var card = window.GetVisualDescendants().OfType<Border>().Single(item => item.Name == pair.Item2);
            var position = control.TranslatePoint(default, card)!.Value;
            if (position.X < 0 || position.Y < 0 || position.X + control.Bounds.Width > card.Bounds.Width + .5 ||
                position.Y + control.Bounds.Height > card.Bounds.Height + .5)
                throw new InvalidOperationException($"Home control is outside its card: {pair.Item1}.");
        }
    }

    private static async Task<object> VerifyPreferencesStyling(Window window, string directory)
    {
        T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
        var save = Find<Button>("SavePreferences"); var cancel = Find<Button>("CancelPreferences");
        var scroll = Find<ScrollViewer>("SettingsPreferencesScroll");
        var idle = Find<NumericUpDown>("ReminderIdle"); var oldIdle = idle.Value;
        var choice = Find<ComboBox>("BubbleDirection");
        if (save.IsEnabled) throw new InvalidOperationException("The unchanged preferences form enabled Save.");
        foreach (var size in new[] { new Size(860, 680), new Size(1120, 800) })
        {
            window.Width = size.Width; window.Height = size.Height; await Task.Delay(100); window.UpdateLayout();
            scroll.ScrollToHome(); await Task.Delay(50); window.UpdateLayout();
            var top = scroll.TranslatePoint(default, window)!.Value;
            var action = save.TranslatePoint(default, window)!.Value;
            if (action.Y < top.Y + scroll.Bounds.Height || action.Y + save.Bounds.Height > window.ClientSize.Height ||
                action.X + save.Bounds.Width > window.ClientSize.Width || cancel.TranslatePoint(default, window)!.Value.X >= action.X)
                throw new InvalidOperationException("Preferences actions are clipped or not pinned below the body.");
            scroll.ScrollToEnd(); await Task.Delay(50); window.UpdateLayout();
            if (save.TranslatePoint(default, window)!.Value != action || scroll.Extent.Width > scroll.Viewport.Width + 1)
                throw new InvalidOperationException("Preferences actions moved with the body or it overflowed horizontally.");
            scroll.ScrollToHome(); await Task.Delay(50); window.UpdateLayout();
            Capture(window, Path.Combine(directory, size.Width == 860 ? "settings-options-minimum.png" : "settings-options-default.png"));
        }
        window.Width = 860; window.Height = 680; await Task.Delay(100); window.UpdateLayout();
        idle.Value = oldIdle == 60 ? 59 : oldIdle + 1;
        if (!save.IsEnabled) throw new InvalidOperationException("Editing preferences did not enable Save.");
        Capture(window, Path.Combine(directory, "settings-options-dirty.png"));
        Press(window, "CancelPreferences");
        if (save.IsEnabled || idle.Value != oldIdle) throw new InvalidOperationException("Cancel did not restore saved preferences.");
        choice.IsDropDownOpen = true; await Task.Delay(150); window.UpdateLayout();
        try
        {
            var popup = choice.GetVisualDescendants().OfType<Popup>().Single();
            if (popup.Child is not Border surface || surface.Bounds.Width <= 0 || surface.Bounds.Height <= 0)
                throw new InvalidOperationException("The preferences dropdown did not create a visible surface.");
            using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(surface.Bounds.Width), (int)Math.Ceiling(surface.Bounds.Height)), new Vector(96, 96));
            image.Render(surface); image.Save(Path.Combine(directory, "settings-direction-popup.png"), PngBitmapEncoderOptions.Default);
        }
        finally { choice.IsDropDownOpen = false; }
        return new { pinnedActions = true, dirtySave = true, cancelRestores = true, dropdownCaptured = true };
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
