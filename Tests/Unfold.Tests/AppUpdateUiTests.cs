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
    public async Task ReleaseNotesWrapInABoundedScrollAreaAndMissingNotesHaveAFallback()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend
        { CheckResult = new("1.1.3-beta", new object(), "## 개선 사항\n- **업데이트 안내**\n" + new string('가', 20000)) };
        using var updates = new AppUpdates(backend); await updates.Check();
        var window = new UpdateWindow(updates, () => Task.FromResult(false)); window.Show();
        var notes = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "UpdateReleaseNotes");
        Assert.StartsWith("개선 사항\n- 업데이트 안내", notes.Text); Assert.True(notes.Text!.Length <= 12000);
        Assert.Equal(Avalonia.Media.TextWrapping.Wrap, notes.TextWrapping);
        Assert.Equal(220, notes.FindAncestorOfType<ScrollViewer>()!.MaxHeight);
        backend.CheckResult = new("1.1.3-beta", new object()); await updates.Check();
        Assert.Contains("변경 이력", notes.Text); window.Close();
    }

    [AvaloniaFact]
    public async Task StartupWaitsForLoginRestorationThenShowsOneModalWithoutDownloading()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend();
        var restore = new TaskCompletionSource<AccountSession?>();
        var service = new FakeAccountService { RestoreSession = _ => restore.Task, Purchase = PurchaseAccess.Active };
        scope.Runtime.AccountServiceFactory = () => service; scope.Runtime.SetUpdateBackend(backend);
        var start = scope.Runtime.Start(false); await Wait(() => service.RestoreCalls == 1);
        Assert.Null(scope.Runtime.ActiveUpdate); Assert.Null(scope.Runtime.ActiveAccount);
        restore.SetResult(FakeAccountService.Session); await start;
        var notice = Assert.IsType<UpdateWindow>(scope.Runtime.ActiveUpdate);
        Assert.Same(scope.MainWindow, notice.Owner); Assert.True(notice.IsDialog);
        Assert.Equal(0, backend.Downloads); Assert.Equal(0, backend.Applies);
        notice.Close(); Assert.True(scope.MainWindow!.IsEnabled);
        scope.Runtime.ShowSettings(); await scope.Runtime.Updates.Check(); Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Runtime.ActiveUpdate);
    }

    [AvaloniaFact]
    public async Task BackgroundStartupDefersALateUpdateUntilTheUserOpensTheApp()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { CheckGate = new() };
        scope.Runtime.AccountServiceFactory = () => new FakeAccountService
        { RestoreSession = _ => Task.FromResult<AccountSession?>(FakeAccountService.Session), Purchase = PurchaseAccess.Active };
        scope.Runtime.SetUpdateBackend(backend); await scope.Runtime.Start(true);
        backend.CheckGate.SetResult(); await Wait(() => scope.Runtime.Updates.State == AppUpdateState.Available);
        Assert.Null(scope.Runtime.ActiveUpdate); Assert.False(scope.MainWindow!.IsVisible);
        scope.Runtime.ShowSettings(); Assert.IsType<UpdateWindow>(scope.Runtime.ActiveUpdate);
        Assert.True(scope.Runtime.ActiveUpdate!.IsDialog);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentOrFailedChecksDoNotInterruptStartup(bool failed)
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { CheckResult = null,
            CheckFailure = failed ? new IOException("offline") : null };
        scope.Runtime.AccountServiceFactory = () => new FakeAccountService(); scope.Runtime.SetUpdateBackend(backend);
        await scope.Runtime.Start(false); Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Runtime.ActiveUpdate); Assert.True(scope.Runtime.ActiveAccount!.IsEnabled);
    }

    [AvaloniaFact]
    public async Task PendingUpdateIsAnnouncedOnTheLoginScreenWithoutRestarting()
    {
        using var scope = new Scope(); var backend = new UpdateTestBackend { Pending = new("1.0.4-beta", new object()) };
        scope.Runtime.AccountServiceFactory = () => new FakeAccountService(); scope.Runtime.SetUpdateBackend(backend);
        await scope.Runtime.Start(false);
        var notice = Assert.IsType<UpdateWindow>(scope.Runtime.ActiveUpdate);
        Assert.Same(scope.Runtime.ActiveAccount, notice.Owner); Assert.True(notice.IsDialog);
        Assert.Equal("재시작하여 적용", Action(notice).Content); Assert.Equal(0, backend.Applies);
    }

    private static async Task Wait(Func<bool> ready)
    {
        for (var i = 0; i < 100 && !ready(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Dispatcher.UIThread.RunJobs(); Assert.True(ready());
    }

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
