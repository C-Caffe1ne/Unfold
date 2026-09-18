using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class UiAuditRegressionTests
{
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Control root, string name) => Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Choice(Window dialog, string label) => dialog.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 300 && !predicate(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(predicate());
    }

    [AvaloniaFact]
    public async Task ReviewFollowsNewDayButPreservesPastBrowsingAndExportsVisiblePeriod()
    {
        var today = new DateOnly(2026, 9, 17); BreakReview? exported = null;
        var window = new BreakReviewWindow(new BreakHistory().Review, getToday: () => today,
            exportReview: review => { exported = review; return Task.FromResult<string?>("audit.csv"); });
        try
        {
            window.Show(); Layout(window); today = today.AddDays(1);
            Choice(window, "새로고침"); Assert.Equal(today, window.Review.EndDay);
            Choice(window, "이전 7일"); var past = window.Review.EndDay;
            today = today.AddDays(1); Choice(window, "새로고침"); Assert.Equal(past, window.Review.EndDay);
            Choice(window, "CSV 내보내기"); await Until(() => exported is not null); Assert.Equal(past, exported!.EndDay);
            Choice(window, "다음 7일"); Choice(window, "다음 7일"); Assert.Equal(today, window.Review.EndDay);
            Assert.False(window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "다음 7일")).IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task PetDraftProtectsCancelFailedSaveAndSuccessfulSaveThenTracksEdits()
    {
        using var data = new DataScope();
        using var temp = new TempDirectory(); string? output = null;
        var owner = new Window { Width = 860, Height = 680 };
        using var view = new CustomPetView(owner, _ => Task.CompletedTask,
            chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()), chooseOutput: () => Task.FromResult(output), showHeader: false);
        owner.Content = view; owner.Show(); Layout(owner);
        try
        {
            Assert.False(view.HasUnsavedChanges);
            Find<TextBox>(owner, "CustomPetName").Text = "새 친구";
            var incomplete = view.CanCloseDraft(); Layout(owner); Choice(Assert.Single(owner.OwnedWindows), "계속 작성");
            Assert.False(await incomplete); Assert.True(view.HasUnsavedChanges);
            Press(owner, "CustomPetFile_idle"); await Until(() => !view.IsBusy); Assert.True(view.HasUnsavedChanges);
            var cancel = view.CanCloseDraft(); Layout(owner);
            var dialog = Assert.Single(owner.OwnedWindows);
            Assert.True(dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "취소")).IsFocused);
            dialog.Close(); Assert.False(await cancel); Assert.True(view.HasUnsavedChanges);
            var saveCancelled = view.CanCloseDraft(); Layout(owner); Choice(Assert.Single(owner.OwnedWindows), "저장하고 종료");
            Assert.False(await saveCancelled); Assert.True(view.HasUnsavedChanges);
            output = temp.Path; // A directory cannot be replaced by the output archive.
            var failed = view.CanCloseDraft(); Layout(owner); Choice(Assert.Single(owner.OwnedWindows), "저장하고 종료");
            Assert.False(await failed); Assert.True(view.HasUnsavedChanges);
            output = Path.Combine(temp.Path, "friend.unfoldpet");
            var saved = view.CanCloseDraft(); Layout(owner); Choice(Assert.Single(owner.OwnedWindows), "저장하고 종료");
            Assert.True(await saved); Assert.False(view.HasUnsavedChanges); Assert.True(File.Exists(output));
            Assert.True(await view.CanCloseDraft()); Assert.Empty(owner.OwnedWindows);
            Find<TextBox>(owner, "CustomPetName").Text = "다른 이름"; Assert.True(view.HasUnsavedChanges);
            Find<TextBox>(owner, "CustomPetName").Text = "새 친구"; Assert.False(view.HasUnsavedChanges);
            Press(owner, "CustomPetRemove_idle"); Assert.True(view.HasUnsavedChanges);
            var discard = view.CanCloseDraft(); Layout(owner); Choice(Assert.Single(owner.OwnedWindows), "버리기"); Assert.True(await discard);
        }
        finally { foreach (var dialog in owner.OwnedWindows.ToArray()) dialog.Close(); owner.Close(); }
    }

    [AvaloniaFact]
    public async Task RemovingAnotherActionKeepsTheCurrentPreview()
    {
        using var data = new DataScope();
        var owner = new Window { Width = 860, Height = 680 };
        using var view = new CustomPetView(owner, _ => Task.CompletedTask,
            chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()), showHeader: false);
        owner.Content = view; owner.Show(); Layout(owner);
        try
        {
            Press(owner, "CustomPetFile_idle"); await Until(() => !view.IsBusy);
            Press(owner, "CustomPetFile_stretch"); await Until(() => !view.IsBusy);
            var hint = Find<TextBlock>(owner, "CustomPetPreviewHint");
            Assert.False(hint.IsVisible);
            Assert.Contains("스트레칭", Find<TextBlock>(owner, "CustomPetPreviewAction").Text);
            Press(owner, "CustomPetRemove_idle"); Assert.False(hint.IsVisible);
            Assert.Contains("스트레칭", Find<TextBlock>(owner, "CustomPetPreviewAction").Text);
            Press(owner, "CustomPetRemove_stretch"); Assert.True(hint.IsVisible);
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public void MinimumBuilderShowsFirstFileButtonWithoutHorizontalScroll()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        window.Width = 860; window.Height = 680;
        Press(window, "SettingsNavPacks"); Layout(window); Find<TabControl>(window, "PetManagementTabs").SelectedIndex = 1; Layout(window);
        var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
        var add = Find<Button>(window, "CustomPetFile_idle");
        var point = add.TranslatePoint(default, scroll)!.Value;
        Assert.InRange(point.Y, 0, scroll.Viewport.Height - add.Bounds.Height);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        var disabled = Find<Button>(window, "CustomPetRemove_idle");
        Assert.False(disabled.IsEnabled);
        Assert.Equal(DesignSystem.DisabledText, Assert.IsType<PathIcon>(disabled.Content).Foreground);
    }

    [AvaloniaFact]
    public void HomeDistinguishesInvitationAndRestingAndEditsClearSavedFeedback()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
        scope.Runtime.TogglePause();
        Assert.StartsWith("휴식 대기 중", Find<TextBlock>(window, "TimerStateText").Text);
        scope.Runtime.StartBreak(); Assert.StartsWith("휴식 중", Find<TextBlock>(window, "TimerStateText").Text);
        scope.Runtime.Stop();
        var interval = Find<NumericUpDown>(window, "ReminderInterval"); interval.Value = 61;
        Assert.Contains("변경사항", Find<TextBlock>(window, "HomeTimingStatus").Text);
        Press(window, "ApplyHomeTimingSettings"); Assert.Contains("저장했어요", Find<TextBlock>(window, "HomeTimingStatus").Text);
        interval.Value = 62; Assert.Contains("변경사항", Find<TextBlock>(window, "HomeTimingStatus").Text);
        Press(window, "TimerToggle"); Assert.Equal(61, interval.Value);
        Assert.Contains("되돌렸어요", Find<TextBlock>(window, "HomeTimingStatus").Text);
    }

    [AvaloniaFact]
    public async Task SoundsArePendingUntilApplyAndPreviewCancelsPreviousPlayback()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "내 알림.wav"); File.WriteAllBytes(path, ReminderSounds.Default(ReminderSound.Due));
        window.ChooseSoundFile = _ => Task.FromResult<string?>(path);
        var tokens = new List<CancellationToken>();
        window.PlaySoundPreview = (_, _, token) => { tokens.Add(token); return Task.Delay(Timeout.Infinite, token); };
        Press(window, "SettingsNavSettings"); Layout(window);
        Press(window, "ImportDueSound"); await Until(() => Find<Button>(window, "ImportDueSound").IsEnabled);
        Assert.Null(scope.Runtime.Settings.ReminderSoundId);
        Assert.Equal("내 알림.wav", Find<TextBlock>(window, "DueSoundName").Text);
        Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        Press(window, "PreviewDueSound"); Assert.Equal("정지", Find<Button>(window, "PreviewDueSound").Content);
        Press(window, "PreviewCompletionSound"); Assert.True(tokens[0].IsCancellationRequested); Assert.False(tokens[1].IsCancellationRequested);
        Press(window, "SavePreferences"); Assert.NotNull(scope.Runtime.Settings.ReminderSoundId);
        Assert.Equal("내 알림.wav", AppSettings.Load(Path.Combine(scope.Root, "settings.json")).ReminderSoundName);
        Press(window, "ResetDueSound"); Assert.NotNull(scope.Runtime.Settings.ReminderSoundId); Assert.True(tokens[1].IsCancellationRequested);
        Press(window, "SavePreferences"); Assert.Null(scope.Runtime.Settings.ReminderSoundId);
        window.PlaySoundPreview = (_, _, _) => throw new IOException("device unavailable");
        Press(window, "PreviewDueSound"); Assert.Equal(DesignSystem.Error, Find<TextBlock>(window, "PreferencesStatus").Foreground);
        window.PlaySoundPreview = (_, _, token) => { tokens.Add(token); return Task.Delay(Timeout.Infinite, token); };
        Press(window, "PreviewDueSound"); Press(window, "SettingsNavTimer"); Assert.True(tokens[^1].IsCancellationRequested);
    }

    [AvaloniaFact]
    public void KeyboardFocusUsesFieldLabelAndLeavesInputBorderUnchanged()
    {
        var number = new NumericUpDown { Value = 5 }; var combo = new ComboBox { ItemsSource = new[] { "하나" }, SelectedIndex = 0 };
        var numericField = Ui.Field("분", number); var choiceField = Ui.Field("동작", combo);
        var window = new Window { Width = 360, Height = 300, Content = Ui.Column(numericField, choiceField) };
        try
        {
            window.Show(); Layout(window);
            var border = number.BorderBrush;
            number.GetVisualDescendants().OfType<TextBox>().Single().Focus(NavigationMethod.Tab);
            Assert.Equal("분 · 선택", ((TextBlock)numericField.Children[0]).Text); Assert.Equal(border, number.BorderBrush);
            combo.Focus(NavigationMethod.Tab); Assert.Equal("분", ((TextBlock)numericField.Children[0]).Text);
            Assert.Equal("동작 · 선택", ((TextBlock)choiceField.Children[0]).Text);
            number.Focus(NavigationMethod.Pointer); Assert.Equal("동작", ((TextBlock)choiceField.Children[0]).Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task SettingsExitReturnsToTheDraftAndCancelPreservesIt()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        Press(window, "SettingsNavPacks"); Layout(window);
        var tabs = Find<TabControl>(window, "PetManagementTabs"); tabs.SelectedIndex = 1; Layout(window);
        Find<TextBox>(window, "CustomPetName").Text = "작성 중인 친구";
        Press(window, "SettingsNavTimer"); Layout(window);
        var close = window.CanCloseDraft(); Layout(window);
        Assert.Equal(1, tabs.SelectedIndex);
        Choice(Assert.Single(window.OwnedWindows), "취소"); Assert.False(await close);
        Assert.Equal("작성 중인 친구", Find<TextBox>(window, "CustomPetName").Text);
    }

    [AvaloniaFact]
    public void TabNavigationExposesSelectionAndMovesFocusToPageContent()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        var settingsNav = Find<Button>(window, "SettingsNavSettings"); settingsNav.Focus(NavigationMethod.Tab);
        Press(window, "SettingsNavSettings"); Layout(window);
        Assert.Contains("선택됨", AutomationProperties.GetName(settingsNav));
        Assert.True(Find<ComboBox>(window, "BubbleDirection").IsFocused);
        Press(window, "SettingsNavTimer"); Layout(window);
        Assert.DoesNotContain("선택됨", AutomationProperties.GetName(settingsNav));
        Assert.True(Find<Button>(window, "TimerToggle").IsFocused);
    }

    [Fact]
    public void CompactBubblesKeepPetAndBubbleInsideEveryDirection()
    {
        foreach (var direction in Enum.GetValues<BubbleDirection>())
        foreach (var height in new[] { 170d, 268d })
        {
            var layout = PetBubbleLayout.Create(direction, true, height);
            Assert.True(layout.Pet.X >= 0 && layout.Pet.Y >= 0 && layout.Pet.X + 192 <= layout.Size.Width && layout.Pet.Y + 192 <= layout.Size.Height);
            Assert.True(layout.Bubble.X >= 0 && layout.Bubble.Y >= 0 && layout.Bubble.X + 320 <= layout.Size.Width && layout.Bubble.Y + height <= layout.Size.Height);
        }
    }

    [Fact]
    public void BundledStretchUsesTransparentFramesAtTheSameCanvasSizeAsIdle()
    {
        var pet = CharacterLibrary.LoadPackage(Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "default-cat"));
        var idle = pet.LoadAnimation("idle"); var stretch = pet.LoadAnimation("stretch");
        Assert.All(stretch, frame =>
        {
            Assert.Equal(idle[0].Image.Width, frame.Image.Width); Assert.Equal(idle[0].Image.Height, frame.Image.Height);
            Assert.Equal(0u, frame.Image.Pixels[0] >> 24);
            Assert.InRange(frame.Image.Pixels.Count(p => p >> 24 != 0) / (double)frame.Image.Pixels.Length, .1, .85);
        });
    }

    [Fact]
    public void SoundPreviewRequiresAnExistingValidImportAndHasBoundedDuration()
    {
        using var temp = new TempDirectory(); var sounds = new ReminderSounds(temp.Path);
        Assert.Throws<FileNotFoundException>(() => sounds.Resolve(ReminderSound.Due, new string('a', 64), strict: true));
        Assert.InRange(ReminderSounds.Duration(ReminderSounds.Default(ReminderSound.Due)).TotalSeconds, .3, .4);
    }

    private sealed class DataScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public DataScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }

    private sealed class SettingsScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public SettingsScope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window);
        }
        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
