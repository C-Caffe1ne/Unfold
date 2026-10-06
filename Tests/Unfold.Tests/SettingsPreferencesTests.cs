using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class SettingsPreferencesTests
{
    [AvaloniaFact]
    public void MasterAndIndividualVolumesApplyImmediatelyAndPreviewThePersistedMix()
    {
        using var scope = new Scope(); var window = scope.Window;
        var master = Find<Slider>(window, "ReminderVolumePercent");
        var due = Find<Slider>(window, "ReminderSoundVolumePercent");
        var completed = Find<Slider>(window, "CompletionSoundVolumePercent");
        var previews = new List<(ReminderSound Sound, int Master, int Due, int Completed)>();
        window.PlaySoundPreview = (sound, settings, _) =>
        { previews.Add((sound, settings.ReminderVolumePercent, settings.ReminderSoundVolumePercent, settings.CompletionSoundVolumePercent)); return Task.CompletedTask; };
        master.Value = 50; due.Value = 25; completed.Value = 80;
        Assert.Equal(SoundVolumeGlyph.Low, Find<SoundVolumeIcon>(window, "ReminderSoundVolumeIcon").Glyph);
        Assert.Equal(SoundVolumeGlyph.High, Find<SoundVolumeIcon>(window, "CompletionSoundVolumeIcon").Glyph);
        Press(window, "PreviewDueSound"); Press(window, "PreviewCompletionSound");
        Assert.Equal(new[] { (ReminderSound.Due, 50, 25, 80), (ReminderSound.Completed, 50, 25, 80) }, previews);
        var saved = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(50, saved.ReminderVolumePercent); Assert.Equal(25, saved.ReminderSoundVolumePercent); Assert.Equal(80, saved.CompletionSoundVolumePercent);
        completed.Value = 0; Assert.Equal(0, scope.Runtime.Settings.CompletionSoundVolumePercent);
    }

    [AvaloniaFact]
    public void ChangingAnyVolumeStopsTheCurrentPreviewAndKeepsOtherVolumes()
    {
        using var scope = new Scope(); var window = scope.Window;
        var tokens = new List<CancellationToken>();
        window.PlaySoundPreview = (_, _, token) => { tokens.Add(token); return Task.Delay(Timeout.Infinite, token); };
        foreach (var name in new[] { "ReminderVolumePercent", "ReminderSoundVolumePercent", "CompletionSoundVolumePercent" })
        {
            Press(window, "PreviewDueSound"); var token = tokens[^1]; Assert.False(token.IsCancellationRequested);
            Find<Slider>(window, name).Value = 50; Layout(window); Assert.True(token.IsCancellationRequested);
            Assert.False(Assert.IsType<SoundPreviewIcon>(Find<Button>(window, "PreviewDueSound").Content).IsPlaying);
        }
        Assert.Equal(50, Find<Slider>(window, "ReminderSoundVolumePercent").Value);
        Assert.Equal(50, Find<Slider>(window, "CompletionSoundVolumePercent").Value);
    }

    [AvaloniaFact]
    public void EachVolumeIconTracksMuteAndFiftyPercentBoundaryWithoutVisiblePercentText()
    {
        using var scope = new Scope(); var window = scope.Window;
        foreach (var (sliderName, iconName) in new[]
        {
            ("ReminderVolumePercent", "ReminderVolumeIcon"),
            ("ReminderSoundVolumePercent", "ReminderSoundVolumeIcon"),
            ("CompletionSoundVolumePercent", "CompletionSoundVolumeIcon")
        })
        {
            var slider = Find<Slider>(window, sliderName); var icon = Find<SoundVolumeIcon>(window, iconName);
            foreach (var (value, glyph) in new[] { (0, SoundVolumeGlyph.Muted), (1, SoundVolumeGlyph.Low),
                (49, SoundVolumeGlyph.Low), (50, SoundVolumeGlyph.High), (100, SoundVolumeGlyph.High) })
            {
                slider.Value = value; Layout(window);
                Assert.Equal(glyph, icon.Glyph);
            }
            slider.Value = 0; slider.Value = 100; Layout(window); Assert.Equal(SoundVolumeGlyph.High, icon.Glyph);
        }
        var card = Find<Border>(window, "SettingsNotificationCard");
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Name != "BubbleOpacityValue" && text.Text?.Contains('%') == true);
    }

    [AvaloniaFact]
    public async Task PreviewIconsFollowStopSwitchAndCompletionWithoutAnOldPlaybackResettingTheNewOne()
    {
        using var scope = new Scope(); var window = scope.Window;
        var requests = new List<(TaskCompletionSource Completion, CancellationToken Token)>();
        window.PlaySoundPreview = (_, _, token) =>
        {
            var completion = new TaskCompletionSource(); requests.Add((completion, token)); return completion.Task;
        };
        var due = Find<Button>(window, "PreviewDueSound"); var completed = Find<Button>(window, "PreviewCompletionSound");
        var dueIcon = Assert.IsType<SoundPreviewIcon>(due.Content); var completedIcon = Assert.IsType<SoundPreviewIcon>(completed.Content);
        Assert.False(dueIcon.IsPlaying); Assert.False(completedIcon.IsPlaying);
        Press(window, "PreviewDueSound"); Assert.True(dueIcon.IsPlaying); Assert.False(completedIcon.IsPlaying);
        Assert.Equal("스트레칭 알림 미리듣기 중지", AutomationProperties.GetName(due));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name is "SavePreferences" or "CancelPreferences");
        Press(window, "PreviewDueSound"); Assert.False(dueIcon.IsPlaying);
        Assert.True(requests[0].Token.IsCancellationRequested); Assert.Single(requests);
        requests[0].Completion.SetResult(); Layout(window);
        Press(window, "PreviewDueSound"); Assert.True(dueIcon.IsPlaying);
        Press(window, "PreviewCompletionSound"); Assert.False(dueIcon.IsPlaying); Assert.True(completedIcon.IsPlaying);
        Assert.True(requests[1].Token.IsCancellationRequested);
        requests[1].Completion.SetResult(); Layout(window);
        Assert.True(completedIcon.IsPlaying);
        requests[2].Completion.SetResult(); await Until(() => !completedIcon.IsPlaying);
        Assert.Equal("완료 알림 효과음 미리듣기", AutomationProperties.GetName(completed));
        Press(window, "PreviewDueSound"); window.HideToTray();
        Assert.True(requests[3].Token.IsCancellationRequested); Assert.False(dueIcon.IsPlaying);
        requests[3].Completion.SetResult(); Layout(window);
    }

    [AvaloniaFact]
    public void FailedPlaybackRestoresThePlayIconAndKeepsTheAppliedVolume()
    {
        using var scope = new Scope(); var window = scope.Window;
        Find<Slider>(window, "ReminderSoundVolumePercent").Value = 25;
        window.PlaySoundPreview = (_, _, _) => Task.FromException(new IOException("test preview failure"));
        Press(window, "PreviewDueSound");
        Assert.False(Assert.IsType<SoundPreviewIcon>(Find<Button>(window, "PreviewDueSound").Content).IsPlaying);
        Assert.True(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        Assert.Equal(25, Find<Slider>(window, "ReminderSoundVolumePercent").Value);
        Assert.Equal(25, scope.Runtime.Settings.ReminderSoundVolumePercent);
    }

    [AvaloniaFact]
    public void VolumeIconsStayLeftAndIndividualPreviewsStayRightAcrossWindowWidths()
    {
        using var scope = new Scope(); var window = scope.Window;
        foreach (var width in new[] { 640, 760, 860, 1120 })
        {
            window.Width = width; window.Height = 560; Layout(window);
            Assert.Empty(Find<Grid>(window, "ReminderVolumeControls").Children.OfType<Button>());
            foreach (var (sliderName, iconName, buttonName) in new[]
            {
                ("ReminderSoundVolumePercent", "ReminderSoundVolumeIcon", "PreviewDueSound"),
                ("CompletionSoundVolumePercent", "CompletionSoundVolumeIcon", "PreviewCompletionSound")
            })
            {
                var slider = Find<Slider>(window, sliderName); var icon = Find<SoundVolumeIcon>(window, iconName); var preview = Find<Button>(window, buttonName);
                var x = slider.TranslatePoint(default, window)!.Value.X; var iconPoint = icon.TranslatePoint(default, window)!.Value;
                var previewPoint = preview.TranslatePoint(default, window)!.Value;
                Assert.True(slider.Bounds.Width > 0); Assert.True(iconPoint.X + icon.Bounds.Width <= x);
                Assert.True(previewPoint.X >= x + slider.Bounds.Width);
                Assert.InRange(Math.Abs(iconPoint.Y + icon.Bounds.Height / 2 - previewPoint.Y - preview.Bounds.Height / 2), 0, 1);
                Assert.Same(icon.GetVisualParent(), preview.GetVisualParent());
                Assert.Equal(new Size(24, 24), icon.Bounds.Size);
                Assert.Equal(new Size(40, 40), preview.Bounds.Size);
                Assert.Equal(new Size(16, 16), Assert.IsType<SoundPreviewIcon>(preview.Content).Bounds.Size);
                var row = Find<Grid>(window, buttonName == "PreviewDueSound" ? "DueSoundRow" : "CompletionSoundRow");
                Assert.True(previewPoint.X + preview.Bounds.Width <= row.TranslatePoint(default, window)!.Value.X + row.Bounds.Width + 1);
            }
            var scroll = Find<ScrollViewer>(window, "SettingsPreferencesScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            Assert.True(Find<Grid>(window, "ReminderVolumeRow").TranslatePoint(default, window)!.Value.Y < Find<Grid>(window, "DueSoundRow").TranslatePoint(default, window)!.Value.Y);
        }
    }

    [AvaloniaFact]
    public void RapidVolumeChangesPersistTheLatestValueAcrossNavigationAndHiding()
    {
        using var scope = new Scope(); var window = scope.Window;
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        Assert.Equal(0, volume.Minimum); Assert.Equal(100, volume.Maximum);
        Assert.Equal("전체 소리, 퍼센트", AutomationProperties.GetName(volume));
        for (var value = 0; value <= 100; value++) volume.Value = value;
        volume.Value = 37; window.HideToTray();
        Assert.Equal(37, scope.Runtime.Settings.ReminderVolumePercent);
        Assert.Equal(37, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).ReminderVolumePercent);
        window.Show(); Press(window, "SettingsNavTimer"); Press(window, "SettingsNavSettings");
        Assert.Equal(37, volume.Value); Assert.Empty(window.OwnedWindows);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name is "SavePreferences" or "CancelPreferences");
    }

    [AvaloniaFact]
    public void NotificationAndTimerPreferencesApplyWithoutChangingTheHomeTimingDraft()
    {
        using var scope = new Scope(); var window = scope.Window;
        var direction = Find<Slider>(window, "BubbleOpacityPercent");
        var sounds = Find<CheckBox>(window, "ReminderSoundsEnabled");
        var idle = Find<NumericUpDown>(window, "ReminderIdle");
        var snooze = Find<NumericUpDown>(window, "SnoozeMinutes");
        direction.Value = 70; sounds.IsChecked = false; idle.Value = 8; snooze.Value = 12;
        var persisted = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(70, persisted.BubbleOpacityPercent); Assert.False(persisted.ReminderSoundsEnabled);
        Assert.Equal(8, persisted.IdleMinutes); Assert.Equal(12, persisted.SnoozeMinutes);
        Press(window, "SettingsNavTimer");
        Find<NumericUpDown>(window, "BreakDurationMinutes").Value = 3;
        Press(window, "SettingsNavSettings"); idle.Value = 20;
        Assert.Equal(20, scope.Runtime.Settings.IdleMinutes); Assert.Equal(1, scope.Runtime.Settings.BreakDurationMinutes);
        Press(window, "SettingsNavTimer"); Assert.Equal(3, Find<NumericUpDown>(window, "BreakDurationMinutes").Value);
    }

    [AvaloniaFact]
    public void FailedAutomaticSavePreservesEditorsAndRetriesOnTheNextEdit()
    {
        using var scope = new Scope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "settings.json"); Directory.CreateDirectory(path);
        Find<Slider>(window, "BubbleOpacityPercent").Value = 40;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 17;
        var volume = Find<Slider>(window, "ReminderVolumePercent"); volume.Value = 23;
        Assert.Contains("저장하지 못했어요", Find<TextBlock>(window, "PreferencesStatus").Text);
        Assert.True(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        Assert.Equal(100, scope.Runtime.Settings.BubbleOpacityPercent); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Assert.Equal(100, scope.Runtime.Settings.ReminderVolumePercent); Assert.Equal(23, volume.Value);
        Directory.Delete(path); volume.Value = 24;
        Assert.Equal(17, AppSettings.Load(path).IdleMinutes); Assert.Equal(24, AppSettings.Load(path).ReminderVolumePercent);
        Assert.Equal(40, scope.Runtime.Settings.BubbleOpacityPercent);
        Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
    }

    [AvaloniaFact]
    public void InvalidNumbersKeepTheLastValidValueWhileOtherSettingsApply()
    {
        using var scope = new Scope(); var window = scope.Window;
        var idle = Find<NumericUpDown>(window, "ReminderIdle");
        foreach (var invalid in new[] { "", "abc", "2.5" })
        {
            idle.Text = invalid; Layout(window);
            Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
            Assert.True(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        }
        Find<Slider>(window, "BubbleOpacityPercent").Value = 40;
        Assert.Equal(40, scope.Runtime.Settings.BubbleOpacityPercent); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        idle.Value = 9; idle.Text = "9"; Layout(window);
        Assert.Equal(9, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IdleMinutes);
        Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
    }

    [AvaloniaFact]
    public void ValidTypedMinutesApplyBeforeLeavingTheField()
    {
        using var scope = new Scope();
        Find<NumericUpDown>(scope.Window, "ReminderIdle").Text = "14";
        Find<NumericUpDown>(scope.Window, "SnoozeMinutes").Text = "7";
        Layout(scope.Window);
        var saved = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(14, saved.IdleMinutes); Assert.Equal(7, saved.SnoozeMinutes);
    }

    [AvaloniaFact]
    public async Task CancelledImportKeepsPreviouslyAppliedSettings()
    {
        using var scope = new Scope(); var window = scope.Window;
        var selectedFile = new TaskCompletionSource<string?>(); window.ChooseSoundFile = _ => selectedFile.Task;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 11;
        Press(window, "ImportDueSound"); Assert.False(Find<StackPanel>(window, "SettingsPreferencesSections").IsEnabled);
        selectedFile.SetResult(null); await Until(() => Find<Button>(window, "ImportDueSound").IsEnabled);
        Assert.True(Find<StackPanel>(window, "SettingsPreferencesSections").IsEnabled);
        Assert.Equal(11, scope.Runtime.Settings.IdleMinutes); Assert.Null(scope.Runtime.Settings.ReminderSoundId);
    }

    [AvaloniaFact]
    public async Task ImportedSoundsAndResetApplyImmediatelyAndStopPlayback()
    {
        using var scope = new Scope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "완료 알림 원본.wav"); File.WriteAllBytes(path, ReminderSounds.Default(ReminderSound.Completed));
        window.ChooseSoundFile = _ => Task.FromResult<string?>(path);
        CancellationToken playing = default;
        window.PlaySoundPreview = (_, _, token) => { playing = token; return Task.Delay(Timeout.Infinite, token); };
        Press(window, "PreviewDueSound"); Press(window, "ImportCompletionSound");
        await Until(() => scope.Runtime.Settings.CompletionSoundId is not null);
        Assert.True(playing.IsCancellationRequested);
        Assert.Equal("완료 알림 원본.wav", AppSettings.Load(Path.Combine(scope.Root, "settings.json")).CompletionSoundName);
        var id = scope.Runtime.Settings.CompletionSoundId;
        Press(window, "ResetCompletionSound"); Assert.Null(scope.Runtime.Settings.CompletionSoundId);
        Assert.Equal("기본 효과음", Find<TextBlock>(window, "CompletionSoundName").Text);
        Assert.False(File.Exists(Path.Combine(scope.Root, "Sounds", id + ".wav"))); Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public void MinimumLayoutKeepsRowsStableAndPopupHasOneSurface()
    {
        using var scope = new Scope(); var window = scope.Window;
        window.Width = 860; window.Height = 680; Layout(window);
        var import = Find<Button>(window, "ImportDueSound"); var before = import.TranslatePoint(default, window)!.Value;
        var filename = Find<TextBlock>(window, "DueSoundName");
        filename.Text = new string('가', 100) + ".wav"; Layout(window);
        Assert.Equal(before, import.TranslatePoint(default, window)!.Value);
        Assert.Equal(Find<Button>(window, "ImportCompletionSound").Bounds.Width, import.Bounds.Width);
        var complete = Find<Grid>(window, "CompletionSoundRow"); var enabled = Find<CheckBox>(window, "ReminderSoundsEnabled");
        Assert.True(enabled.TranslatePoint(default, window)!.Value.Y > complete.TranslatePoint(default, window)!.Value.Y + complete.Bounds.Height);
        var volume = Find<Slider>(window, "ReminderVolumePercent"); var volumeIcon = Find<SoundVolumeIcon>(window, "ReminderVolumeIcon");
        Assert.True(volume.Bounds.Width > 0);
        Assert.True(volumeIcon.TranslatePoint(default, window)!.Value.X + volumeIcon.Bounds.Width <= volume.TranslatePoint(default, window)!.Value.X);
        var opacity = Find<Slider>(window, "BubbleOpacityPercent");
        Assert.Equal(0, opacity.Minimum); Assert.Equal(100, opacity.Maximum);

    }

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window); Press(Window, "SettingsNavSettings");
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
