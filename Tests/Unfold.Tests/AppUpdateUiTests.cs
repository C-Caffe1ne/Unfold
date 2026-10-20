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
        Assert.StartsWith("- 업데이트 안내", notes.Text); Assert.True(notes.Text!.Length <= 6 * 103);
        Assert.Equal(Avalonia.Media.TextWrapping.Wrap, notes.TextWrapping);
        Assert.Equal(220, notes.FindAncestorOfType<ScrollViewer>()!.MaxHeight);
        backend.CheckResult = new("1.1.3-beta", new object()); await updates.Check();
        Assert.Contains("변경 이력", notes.Text); window.Close();
    }

    [AvaloniaTheory]
    [InlineData("핵심 변경")]
    [InlineData("주요 변경")]
    [InlineData("변경내용")]
    [InlineData("개선 사항")]
    public async Task UpdateNotesPreferCoreChangesAndExcludeDetailsAndInternalSections(string heading)
    {
        using var scope = new Scope();
        var backend = new UpdateTestBackend { CheckResult = new("99.7.2-beta", new object(),
            $"# 새로운 릴리스\n## 개요\n- 이전 안내\n## {heading}\n" +
            "- **입력 개선**: [새로운 선택](https://example.test/notes)을 빠르게 저장해요. 추가 설명은 상세 페이지에 있어요.\n" +
            "- `펫 표시`를 선택할 수 있어요.\n## 상세 변경\n- 상세 구현 설명\n## 내부 정리\n- 내부 코드 정리") };
        using var updates = new AppUpdates(backend); await updates.Check();
        var window = new UpdateWindow(updates, () => Task.FromResult(false)); window.Show();
        try
        {
            var notes = Notes(window);
            Assert.Equal("- 입력 개선: 새로운 선택을 빠르게 저장해요.\n- 펫 표시를 선택할 수 있어요.", notes.Text);
            Assert.Contains("99.7.2-beta", window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "UpdateStatus").Text);
            foreach (var excluded in new[] { "이전 안내", "추가 설명", "상세 구현", "내부 코드", "https://", "**", "`" })
                Assert.DoesNotContain(excluded, notes.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UpdateNotesLimitBulletCountAndShortenLongDescriptions()
    {
        using var scope = new Scope();
        var markdown = "## 주요 변경\n- **짧은 제목**: " + new string('가', 300) + ".\n- " + new string('나', 300) +
            "\n" + string.Join("\n", Enumerable.Range(3, 6).Select(i => $"- 변경 {i}을 적용했어요. 다음 문장은 자세한 설명이에요."));
        var backend = new UpdateTestBackend { CheckResult = new("99.8.0-beta", new object(), markdown) };
        using var updates = new AppUpdates(backend); await updates.Check();
        var window = new UpdateWindow(updates, () => Task.FromResult(false)); window.Show();
        try
        {
            var lines = Notes(window).Text!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(6, lines.Length); Assert.Equal("- 짧은 제목", lines[0]);
            Assert.StartsWith("- 나", lines[1]);
            Assert.All(lines, line => { Assert.StartsWith("- ", line); Assert.True(line.Length <= 102); });
            Assert.Equal("- 변경 3을 적용했어요.", lines[2]);
            Assert.DoesNotContain("변경 7", Notes(window).Text); Assert.DoesNotContain("변경 8", Notes(window).Text);
            Assert.DoesNotContain("다음 문장", Notes(window).Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UpdateNotesKeepPlainTextShortWhenAReleaseHasNoBullets()
    {
        using var scope = new Scope();
        var backend = new UpdateTestBackend { CheckResult = new("99.9.0-beta", new object(),
            "미래 버전의 새로운 동작을 안내해요. " + new string('가', 15000)) };
        using var updates = new AppUpdates(backend); await updates.Check();
        var window = new UpdateWindow(updates, () => Task.FromResult(false)); window.Show();
        try
        {
            Assert.Contains("미래 버전의 새로운 동작을 안내해요.", Notes(window).Text);
            Assert.True(Notes(window).Text!.Length <= 102);
        }
        finally { window.Close(); }
    }

    private static TextBlock Notes(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "UpdateReleaseNotes");

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
        Dispatcher.UIThread.RunJobs(); PetLibraryTestNavigation.OpenBuilder(settings);
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
