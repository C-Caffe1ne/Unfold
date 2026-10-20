using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetQuickMenuTests
{
    [AvaloniaFact]
    public async Task InvisibleMenuButtonPassesInputAndAnOpenMenuHoldsTheBarUntilClosed()
    {
        using var scope = new Scope(); await scope.Load();
        var timer = scope.Timer;
        var point = scope.MenuButton.TranslatePoint(new Point(14, 12), timer)!.Value;
        Assert.Equal(0, timer.HoverOpacity);
        Assert.False(timer.AcceptsPointerAt(point));
        Press(scope.MenuButton);
        Assert.False(timer.QuickMenu.IsOpen);

        scope.OpenMenu();
        Assert.True(timer.AcceptsPointerAt(point));
        var position = timer.Position;
        timer.UpdateHover(false, false, At(2000));
        timer.UpdateHover(false, false, At(2200));
        Assert.Equal(1, timer.HoverOpacity);
        Assert.True(timer.HoldsPlacement);
        Assert.True(scope.Pet.Overlay.HoldsPlacement);
        Assert.Equal(position, timer.Position);
        timer.QuickMenu.Close();
        timer.UpdateHover(false, false, At(3000));
        timer.UpdateHover(false, false, At(3090));
        Assert.InRange(timer.HoverOpacity, .1, .9);
        timer.UpdateHover(false, false, At(3180));
        Assert.Equal(0, timer.HoverOpacity);
        Assert.False(timer.HoldsPlacement);
        Assert.False(timer.AcceptsPointerAt(point));
    }

    [AvaloniaTheory]
    [InlineData("pet")]
    [InlineData("timer")]
    [InlineData("preference")]
    [InlineData("gesture")]
    public async Task HidingDisablingOrInteractingWithThePetClosesTheMenu(string action)
    {
        using var scope = new Scope(); await scope.Load(originalPet: action == "gesture");
        scope.OpenMenu();
        switch (action)
        {
            case "pet": await scope.Runtime.HidePet(); break;
            case "timer": scope.Timer.Hide(); break;
            case "preference":
                await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPetTimer = false });
                break;
            case "gesture":
                scope.Pet.BeginCompanionPress();
                Assert.True(scope.Pet.HasPointerInteraction);
                break;
        }
        Layout(scope.Timer);
        Assert.False(scope.Timer.QuickMenu.IsOpen);
        Assert.False(scope.Timer.AcceptsPointerAt(new Point(190, 12)));
        if (action != "timer") Assert.Equal(0, scope.Timer.HoverOpacity);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SoundTogglePersistsBothDirectionsWithoutChangingStoppedOrPausedClock(bool stopped)
    {
        using var scope = new Scope(); await scope.Load();
        if (!stopped) scope.Runtime.Clock.Reset(scope.Runtime.DiagnosticTime);
        var clock = ClockState(scope.Runtime.Clock);
        var selectedId = scope.Runtime.Settings.SelectedCharacterId;
        foreach (var enabled in new[] { true, false })
        {
            scope.OpenMenu();
            Assert.Equal(L.T(enabled ? "알림 소리 켜기" : "알림 소리 끄기"), scope.Item("PetQuickSound").Header);
            Click(scope.Item("PetQuickSound"));
            await Until(() => scope.Runtime.Settings.ReminderSoundsEnabled == enabled && scope.MenuButton.IsEnabled);

            Assert.False(scope.Timer.QuickMenu.IsOpen);
            Assert.Equal(enabled, AppSettings.Load(scope.SettingsPath).ReminderSoundsEnabled);
            Assert.Equal(clock, ClockState(scope.Runtime.Clock));
            Assert.Equal(selectedId, scope.Runtime.Settings.SelectedCharacterId);
            Assert.False(scope.Runtime.Reminder.HasNotice);
            Assert.Empty(scope.Dialogs);
        }
    }

    [AvaloniaFact]
    public async Task QuickSoundChangeMergesIntoAnInvalidPreferencesDraftWithoutBeingUndoneOnSave()
    {
        using var scope = new Scope(); await scope.Load();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ReminderSoundsEnabled = true });
        await scope.Runtime.OpenPetPreferences(); Layout(scope.Settings);
        var invitation = Find<TextBox>(scope.Settings, "InvitationDialogue");
        var sounds = Find<CheckBox>(scope.Settings, "ReminderSoundsEnabled");
        var savedInvitation = scope.Runtime.Settings.InvitationDialogue;
        var clock = ClockState(scope.Runtime.Clock);
        Assert.True(sounds.IsChecked);
        invitation.Text = "";
        Layout(scope.Settings);
        Assert.Equal(savedInvitation, scope.Runtime.Settings.InvitationDialogue);

        scope.OpenMenu();
        Click(scope.Item("PetQuickSound"));
        await Until(() => !scope.Runtime.Settings.ReminderSoundsEnabled && scope.MenuButton.IsEnabled);
        Layout(scope.Settings);
        Assert.Equal("", invitation.Text);
        Assert.False(sounds.IsChecked);
        Assert.Equal(savedInvitation, AppSettings.Load(scope.SettingsPath).InvitationDialogue);

        invitation.Text = "다시 함께 쉬어요";
        await Until(() => scope.Runtime.Settings.InvitationDialogue == "다시 함께 쉬어요");
        var saved = AppSettings.Load(scope.SettingsPath);
        Assert.Equal("다시 함께 쉬어요", saved.InvitationDialogue);
        Assert.False(scope.Runtime.Settings.ReminderSoundsEnabled);
        Assert.False(saved.ReminderSoundsEnabled);
        Assert.False(sounds.IsChecked);
        Assert.Equal(clock, ClockState(scope.Runtime.Clock));
        Assert.Empty(scope.Dialogs);
    }

    [AvaloniaFact]
    public async Task LibraryActionReusesTheSettingsWindowAndRetainsTheUnfinishedEditor()
    {
        using var scope = new Scope(); await scope.Load();
        scope.Runtime.ShowSettings(); Layout(scope.Settings);
        Press(Find<Button>(scope.Settings, "SettingsNavPacks"));
        PetLibraryTestNavigation.OpenBuilder(scope.Settings);
        var page = Assert.Single(scope.Settings.GetVisualDescendants().OfType<PetManagementView>());
        var name = Find<TextBox>(scope.Settings, "CustomPetName");
        name.Text = "빠른 메뉴에서 보존할 초안";
        Assert.True(page.HasUnsavedDraft);
        var originalWindow = scope.Settings;
        originalWindow.HideToTray();
        scope.OpenMenu();
        Click(scope.Item("PetQuickLibrary"));
        await Until(() => originalWindow.IsVisible && scope.MenuButton.IsEnabled);
        Layout(originalWindow);

        Assert.Same(originalWindow, scope.Settings);
        Assert.Same(originalWindow, Assert.Single(scope.SettingsWindows));
        Assert.Same(page, Assert.Single(originalWindow.GetVisualDescendants().OfType<PetManagementView>()));
        Assert.True(Find<TextBox>(originalWindow, "PetLibrarySearch").IsEffectivelyVisible);
        Assert.True(page.HasUnsavedDraft);
        Assert.Equal(L.T("계속 편집"), Find<Button>(originalWindow, "PetLibraryCreate").Content);
        Assert.Empty(originalWindow.OwnedWindows);
        Assert.False(scope.Timer.QuickMenu.IsOpen);
        PetLibraryTestNavigation.OpenBuilder(originalWindow);
        Assert.Same(name, Find<TextBox>(originalWindow, "CustomPetName"));
        Assert.Equal("빠른 메뉴에서 보존할 초안", name.Text);
    }

    [AvaloniaFact]
    public async Task SettingsActionOpensThePreferencesPageInTheExistingWindow()
    {
        using var scope = new Scope(); await scope.Load();
        var originalWindow = scope.Settings;
        Assert.False(originalWindow.IsVisible);
        var clock = ClockState(scope.Runtime.Clock);
        scope.OpenMenu();
        Click(scope.Item("PetQuickSettings"));
        await Until(() => originalWindow.IsVisible && scope.MenuButton.IsEnabled);
        Layout(originalWindow);

        Assert.Same(originalWindow, scope.Settings);
        Assert.Same(originalWindow, Assert.Single(scope.SettingsWindows));
        Assert.True(Find<Grid>(originalWindow, "SettingsPreferencesPage").IsEffectivelyVisible);
        Assert.Equal(clock, ClockState(scope.Runtime.Clock));
        Assert.False(scope.Timer.QuickMenu.IsOpen);
    }

    [AvaloniaFact]
    public async Task EscapeClosesTheFocusedPopupAndAllowsTheBarToFade()
    {
        using var scope = new Scope(); await scope.Load();
        scope.OpenMenu();
        Assert.True(scope.Item("PetQuickLibrary").Focus(NavigationMethod.Tab));
        var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(scope.Timer.QuickMenu));
        popup.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        popup.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        Layout(scope.Timer);

        Assert.False(scope.Timer.QuickMenu.IsOpen);
        scope.Timer.UpdateHover(false, false, At(2000));
        scope.Timer.UpdateHover(false, false, At(2180));
        Assert.Equal(0, scope.Timer.HoverOpacity);
        Assert.False(scope.Timer.HoldsPlacement);
    }

    [AvaloniaFact]
    public async Task FailedSoundSaveKeepsStateAndBlocksDuplicateActionsUntilTheErrorCloses()
    {
        using var scope = new Scope(); await scope.Load();
        var settings = scope.Runtime.Settings;
        var original = File.ReadAllText(scope.SettingsPath);
        var backup = scope.SettingsPath + ".saved";
        File.Move(scope.SettingsPath, backup);
        Directory.CreateDirectory(scope.SettingsPath);
        scope.OpenMenu();
        Click(scope.Item("PetQuickSound"));
        await Until(() => scope.Dialogs.Any());
        Assert.False(scope.MenuButton.IsEnabled);
        Assert.Same(settings, scope.Runtime.Settings);
        Assert.Equal(original, File.ReadAllText(backup));
        Click(scope.Item("PetQuickSound"));
        Click(scope.Item("PetQuickLibrary"));
        Press(scope.MenuButton);
        Layout(scope.Pet);
        var dialog = Assert.Single(scope.Dialogs);
        Assert.False(scope.Timer.QuickMenu.IsOpen);
        Assert.False(scope.Settings.IsVisible);
        Assert.Same(settings, scope.Runtime.Settings);
        Press(dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, L.T("확인"))));
        await Until(() => scope.MenuButton.IsEnabled && !scope.Dialogs.Any());

        Directory.Delete(scope.SettingsPath);
        File.Move(backup, scope.SettingsPath);
        scope.OpenMenu();
        Click(scope.Item("PetQuickSound"));
        await Until(() => scope.Runtime.Settings.ReminderSoundsEnabled && scope.MenuButton.IsEnabled);
        Assert.True(AppSettings.Load(scope.SettingsPath).ReminderSoundsEnabled);
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.Korean)]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Japanese)]
    [InlineData(AppLanguage.ChineseSimplified)]
    public async Task LocalizedMenuHasThreeNamedActionsAndCapturesItsRenderedPopup(AppLanguage language)
    {
        using var scope = new Scope(language); await scope.Load();
        scope.OpenMenu();
        Assert.Equal(L.T("빠른 메뉴"), AutomationProperties.GetName(scope.MenuButton));
        Assert.Equal(L.T("빠른 메뉴"), ToolTip.GetTip(scope.MenuButton));
        Assert.Equal(new[] { "PetQuickLibrary", "PetQuickSound", "PetQuickSettings" },
            scope.Timer.QuickMenu.Items.OfType<MenuItem>().Select(item => item.Name).ToArray());
        Assert.Equal(L.T("내 펫"), scope.Item("PetQuickLibrary").Header);
        Assert.Equal(L.T("알림 소리 켜기"), scope.Item("PetQuickSound").Header);
        Assert.Equal(L.T("설정"), scope.Item("PetQuickSettings").Header);
        var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(scope.Timer.QuickMenu));
        popup.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Assert.All(scope.Timer.QuickMenu.Items.OfType<MenuItem>(), item =>
        {
            Assert.True(item.Bounds.Width > 0); Assert.True(item.Bounds.Height > 0);
            Assert.True(item.IsEffectivelyVisible);
        });
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/verification/pet-quick-menu"));
        Directory.CreateDirectory(directory);
        using var frame = popup.CaptureRenderedFrame(); Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{language}-menu.png"), PngBitmapEncoderOptions.Default);
        using var bar = scope.Timer.CaptureRenderedFrame(); Assert.NotNull(bar);
        bar.Save(Path.Combine(directory, $"{language}-bar.png"), PngBitmapEncoderOptions.Default);
    }

    private static (TimeSpan Interval, TimeSpan Remaining, bool Paused, bool Stopped) ClockState(StretchClock clock) =>
        (clock.Interval, clock.Remaining, clock.Paused, clock.Stopped);
    private static TimeSpan At(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    private static void Layout(Control control)
    {
        Dispatcher.UIThread.RunJobs(); control.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    private static async Task Until(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 300 && !condition(); attempt++)
        { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(condition());
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previousData = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly AppLanguage previousLanguage = L.Current;
        private readonly AppTheme previousTheme = DesignSystem.CurrentTheme;
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private readonly HashSet<SettingsWindow> settingsWindows = [];
        private readonly IDisposable windowOpened;
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public PetTimerWindow Timer => Pet.Overlay.Timer;
        public SettingsWindow Settings => (SettingsWindow)lifetime.MainWindow!;
        public IEnumerable<SettingsWindow> SettingsWindows => settingsWindows;
        public IEnumerable<Window> Dialogs => Pet.OwnedWindows.Where(window => window.IsVisible && window.Title == "Unfold");
        public string SettingsPath => Path.Combine(temp.Path, "settings.json");
        public Button MenuButton => Find<Button>(Timer, "PetQuickMenu");
        public MenuItem Item(string name) => Timer.QuickMenu.Items.OfType<MenuItem>().Single(item => item.Name == name);
        public Scope(AppLanguage language = AppLanguage.Korean)
        {
            windowOpened = Window.WindowOpenedEvent.AddClassHandler<SettingsWindow>((window, _) => settingsWindows.Add(window));
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime, language);
        }
        public async Task Load(bool originalPet = false)
        {
            var fixture = new SpriteFixture(16, 16) { Name = "Quick menu fixture" };
            for (var y = 5; y < 11; y++) for (var x = 5; x < 11; x++) fixture.Pixels[y * 16 + x] = 0xffffffff;
            var id = Runtime.Library.InstallFixture(fixture).Manifest.Id;
            await Runtime.Start(true, true); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with
            {
                SelectedCharacterId = originalPet ? "bori-rabbit" : id,
                ShowPet = true, ShowPetTimer = true, ShowSystemResources = false, ReminderSoundsEnabled = false
            });
            Pet.Position = new(500, 300); Pet.RefreshSpeech();
            Layout(Pet); Layout(Pet.Overlay); Layout(Timer);
        }
        public void OpenMenu()
        {
            // Complete the reveal before the popup's own real-time hover update.
            Timer.UpdateHover(true, false, At(-180)); Timer.UpdateHover(true, false, At(0));
            Assert.Equal(1, Timer.HoverOpacity);
            Press(MenuButton); Layout(Timer);
            Assert.True(Timer.QuickMenu.IsOpen);
        }
        public void Dispose()
        {
            if (Runtime.ActivePet is { } pet)
                foreach (var dialog in pet.OwnedWindows.Where(window => window.Title == "Unfold").ToArray()) dialog.Close();
            Runtime.Dispose(); lifetime.Dispose(); windowOpened.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousData);
            L.Use(previousLanguage); DesignSystem.ApplyLanguage(previousLanguage); DesignSystem.ApplyTheme(previousTheme);
            temp.Dispose();
        }
    }
}
