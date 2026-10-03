using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Svg;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class SettingsAccountTests
{
    [AvaloniaFact]
    public async Task VerifiedEmailSurvivesClosingTheAccountWindowAndLogoutClearsIt()
    {
        using var scope = new Scope(); await scope.Start();
        var login = new FakeAccountService { Purchase = PurchaseAccess.Active };
        var reopened = new FakeAccountService { Purchase = PurchaseAccess.Active };
        var signOut = new FakeAccountService(); var nextLogin = new FakeAccountService();
        var services = new Queue<IAccountScreenService>([login, reopened, signOut, nextLogin]);
        scope.Runtime.AccountServiceFactory = services.Dequeue;
        await scope.SignIn();
        var session = Assert.IsType<AccountSession>(scope.Runtime.AccountSession);
        var email = Find<TextBlock>(scope.Window, "SettingsAccountEmail");
        var icon = Find<Image>(scope.Window, "SettingsAccountGoogleIcon");
        var button = Find<Button>(scope.Window, "SignOutAccount");
        Assert.Equal("person@example.test", email.Text); Assert.True(icon.IsVisible); Assert.IsType<SvgImage>(icon.Source);
        Assert.Equal("로그아웃", button.Content); Assert.True(button.IsEnabled);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<Button>(), item => item.Name == "OpenAccount");
        foreach (var file in Directory.GetFiles(scope.Root))
        {
            var contents = File.ReadAllText(file);
            Assert.DoesNotContain("private-access", contents); Assert.DoesNotContain("private-refresh", contents);
            Assert.DoesNotContain("person@example.test", contents);
        }
        scope.Runtime.ShowAccount();
        var account = Assert.IsType<AccountWindow>(scope.Runtime.ActiveAccount);
        Assert.True(account.Model.IsPurchase); Assert.True(account.Model.PurchaseUnknown); Assert.False(account.Model.PurchaseReady);
        Press(account, "AccountPrimary"); await Until(() => scope.Runtime.ActiveAccount is null);
        Assert.Equal(0, reopened.SignInCalls); Assert.Equal(1, reopened.PurchaseCalls);
        Assert.Same(session, scope.Runtime.AccountSession);
        Press(scope.Window, "SignOutAccount"); await Until(() => scope.Runtime.ActiveAccount is not null);
        Assert.Null(scope.Runtime.AccountSession); Assert.Equal(1, signOut.SignOutCalls); Assert.True(signOut.Disposed);
        Assert.Equal(scope.Runtime.AccountContent.Copy.SignedOutLabel, email.Text); Assert.False(icon.IsVisible);
        Assert.True(scope.Runtime.ActiveAccount!.Model.IsLogin); Assert.False(scope.Runtime.ActiveAccount.Model.PurchaseReady);
        Assert.False(scope.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task OfflineLogoutStillClearsLocalIdentityAndReportsTheServerFailure()
    {
        using var scope = new Scope(); await scope.Start();
        var services = new Queue<IAccountScreenService>([
            new FakeAccountService { Purchase = PurchaseAccess.Active },
            new FakeAccountService { SignOut = (_, _) => Task.FromException(new AccountException(AccountFailure.Unavailable)) },
            new FakeAccountService()]);
        scope.Runtime.AccountServiceFactory = services.Dequeue; await scope.SignIn();
        await scope.Runtime.SignOut();
        Assert.Null(scope.Runtime.AccountSession);
        var account = Assert.IsType<AccountWindow>(scope.Runtime.ActiveAccount);
        Assert.Equal(scope.Runtime.AccountContent.Copy.SignOutUnavailable, account.Model.Status);
        Assert.True(account.Model.IsLogin);
    }

    [AvaloniaFact]
    public async Task LogoutIsSingleFlightAndDisposalCancelsItWithoutReopeningAWindow()
    {
        using var scope = new Scope(); await scope.Start();
        CancellationToken signingOut = default;
        var signOut = new FakeAccountService { SignOut = (_, token) => { signingOut = token; return Task.Delay(Timeout.Infinite, token); } };
        var services = new Queue<IAccountScreenService>([new FakeAccountService { Purchase = PurchaseAccess.Active }, signOut]);
        scope.Runtime.AccountServiceFactory = services.Dequeue; await scope.SignIn();
        var pending = scope.Runtime.SignOut(); await scope.Runtime.SignOut();
        Assert.True(scope.Runtime.AccountSignOutPending); Assert.Equal(1, signOut.SignOutCalls); Assert.Null(scope.Runtime.AccountSession);
        scope.Runtime.Dispose(); await pending;
        Assert.True(signingOut.IsCancellationRequested); Assert.True(signOut.Disposed); Assert.Null(scope.Runtime.ActiveAccount);
    }

    [AvaloniaFact]
    public async Task DiagnosticDismissalDoesNotInventAUserEmailAndCanReturnToLoginWithoutServerLogout()
    {
        using var scope = new Scope(); await scope.Start();
        var service = new FakeAccountService(); scope.Runtime.AccountServiceFactory = () => service;
        scope.Runtime.ShowAccount(); var account = scope.Runtime.ActiveAccount!;
        Assert.False(Find<Button>(account, "AccountCode").IsVisible);
        account.Close(); await Until(() => scope.Runtime.ActiveAccount is null);
        Assert.Null(scope.Runtime.AccountSession);
        Assert.Equal(0, service.SignInCalls);
        Assert.Equal(scope.Runtime.AccountContent.Copy.SignedOutLabel, Find<TextBlock>(scope.Window, "SettingsAccountEmail").Text);
        Assert.False(Find<Image>(scope.Window, "SettingsAccountGoogleIcon").IsVisible);
        Assert.True(Find<Button>(scope.Window, "SignOutAccount").IsEnabled);
        Press(scope.Window, "SignOutAccount");
        await Until(() => scope.Runtime.ActiveAccount is not null);
        Assert.True(scope.Runtime.ActiveAccount!.Model.IsLogin); Assert.Equal(0, service.SignOutCalls);
    }

    [Fact]
    public async Task ReopenedExpiredSessionsRefreshBeforeCheckingPurchaseAndDoNotSignInAgain()
    {
        var expired = new AccountSession(Guid.NewGuid(), "old-access", "old-refresh", DateTimeOffset.UtcNow.AddMinutes(-1), "person@example.test");
        var service = new FakeAccountService();
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, expired);
        AccountSession? refreshed = null; model.SessionChanged += value => refreshed = value;
        await model.PrimaryAsync();
        Assert.Equal(1, service.RefreshCalls); Assert.Equal(0, service.SignInCalls); Assert.Equal(1, service.PurchaseCalls);
        Assert.Equal(expired.UserId, refreshed!.UserId); Assert.True(refreshed.ExpiresAt > DateTimeOffset.UtcNow);
        model.Secondary(); Assert.Null(refreshed); Assert.True(model.IsLogin);
    }

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 200; i++) { Layout(); if (ready()) return; await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window => (SettingsWindow)lifetime.MainWindow!;
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            new AppSettings { ShowPet = false }.Save(Path.Combine(Root, "settings.json"));
            Runtime = new(lifetime);
        }
        public async Task Start() { await Runtime.Start(false, true); Press(Window, "SettingsNavSettings"); }
        public async Task SignIn()
        {
            Runtime.ShowAccount(); Press(Runtime.ActiveAccount!, "AccountPrimary");
            await Until(() => Runtime.ActiveAccount is null); Layout();
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
