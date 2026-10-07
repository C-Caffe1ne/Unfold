using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class AppUpdateUiTests
{
    [AvaloniaFact]
    public async Task UpdateWindowShowsProgressAndClosingItKeepsTheDownload()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { DownloadGate = new() };
        using var updates = new AppUpdates(backend); await updates.Check();
        var window = new UpdateWindow(updates, () => Task.FromResult(false)); window.Show();
        Press(window); Assert.Equal(AppUpdateState.Downloading, updates.State);
        Assert.True(window.GetVisualDescendants().OfType<ProgressBar>().Single().IsVisible);
        window.Close(); backend.DownloadGate.SetResult();
        for (var i = 0; i < 50 && !updates.Downloaded; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(updates.Downloaded); Assert.Equal(0, backend.Applies);
        window = new(updates, () => Task.FromResult(false)); window.Show();
        Assert.Equal("재시작하여 적용", Action(window).Content); Press(window); Dispatcher.UIThread.RunJobs();
        Assert.Contains("진행 중인 휴식", window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "UpdateStatus").Text);
        Assert.True(Action(window).IsEnabled); window.Close();
    }

    [AvaloniaFact]
    public async Task RuntimeExposesUpdatesWithoutPurchaseAccessAndDoesNotDuplicateTheWindow()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend();
        scope.Runtime.AccountServiceFactory = () => new FakeAccountService();
        scope.Runtime.SetUpdateBackend(backend);
        await scope.Runtime.Start(false);
        Assert.False(scope.Runtime.AccessAllowed);
        scope.Runtime.ShowUpdates(); var first = scope.Runtime.ActiveUpdate;
        scope.Runtime.ShowUpdates(); Assert.Same(first, scope.Runtime.ActiveUpdate);
        first!.Close(); Assert.Null(scope.Runtime.ActiveUpdate);
        Assert.Equal(1, backend.Checks);
    }

    [AvaloniaFact]
    public async Task ActiveBreakBlocksApplyAndAFailedUpdaterDoesNotStopTheTimer()
    {
        using var scope = new Scope();
        var backend = new UpdateTestBackend { Pending = new("1.0.4-beta", new object()), ApplyFailure = new IOException("cannot launch") };
        scope.Runtime.SetUpdateBackend(backend); await scope.Runtime.Start(false, true);
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); scope.Runtime.Reminder.Invite(session);
        scope.Runtime.StartBreak(); Assert.False(await scope.Runtime.RestartForUpdate()); Assert.Equal(0, backend.Applies);
        Assert.Same(session, scope.Runtime.Reminder.Session); scope.Runtime.CompleteBreak();
        Assert.False(await scope.Runtime.RestartForUpdate()); Assert.Equal(1, backend.Applies);
        Assert.False(scope.Runtime.Clock.Stopped); Assert.True(scope.Runtime.ActiveUpdate is null);
        Assert.Equal(AppUpdateState.Error, scope.Runtime.Updates.State);
    }

    [AvaloniaFact]
    public async Task UnsavedPetDraftCancelStopsUpdateBeforeTheHelperIsLaunched()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { Pending = new("1.0.4-beta", new object()) };
        scope.Runtime.SetUpdateBackend(backend); await scope.Runtime.Start(false, true);
        var settings = Assert.IsType<SettingsWindow>(scope.MainWindow);
        settings.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        var name = settings.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "CustomPetName");
        name.Text = "저장하지 않은 펫";
        var restart = scope.Runtime.RestartForUpdate(); Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 50 && !settings.OwnedWindows.Any(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.Single(settings.OwnedWindows).Close(); Assert.False(await restart);
        Assert.Equal(0, backend.Applies); Assert.Equal("저장하지 않은 펫", name.Text); Assert.True(settings.IsVisible);
    }

    [AvaloniaFact]
    public async Task TrayOffersOneUpdateEntryAndRestartRequiresAnExplicitRequest()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { Pending = new("1.0.4-beta", new object()) };
        scope.Runtime.SetUpdateBackend(backend); await scope.Runtime.Start(false, true);
        var menu = Assert.Single(TrayIcon.GetIcons(Application.Current!)!).Menu!;
        Assert.Single(menu.Items.OfType<NativeMenuItem>(), i => (i.Header?.ToString() ?? "").StartsWith("업데이트 확인"));
        Assert.Equal(0, backend.Applies); Assert.True(await scope.Runtime.RestartForUpdate()); Assert.Equal(1, backend.Applies);
    }

    private static Button Action(Window window) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UpdateAction");
    private static void Press(Window window) => Action(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        internal AppRuntime Runtime { get; }
        internal Window? MainWindow => lifetime.MainWindow;
        internal Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
