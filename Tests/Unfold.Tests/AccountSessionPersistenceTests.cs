using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class AccountSessionPersistenceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static AccountSession Session => new(UserId, "private-access", "refresh-0", DateTimeOffset.UtcNow.AddMinutes(-1), "person@example.test");
    private static AccountConnection Connection => new(new("https://session-test.supabase.co"), "sb_publishable_test", 43821, AccountEnvironment.Live, ["KR"]);
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static HttpResponseMessage Refreshed(string refreshToken, Guid? user = null) => Json(new
    {
        access_token = "new-access", refresh_token = refreshToken, expires_in = 3600,
        user = new { id = user ?? UserId, email = "person@example.test" }
    });
    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => handle(request, token);
    }
    private static DesktopAccountService Service(AccountSessionVault vault, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) =>
        new(AccountScreenContent.Load(), Connection, new(Connection.ProjectUrl, "sb_publishable_test", AccountEnvironment.Live, new Transport(handle)), vault);
    private sealed class MemoryStore : IAccountCredentialStore
    {
        public byte[]? Value { get; set; }
        public bool FailRead { get; set; }
        public bool FailWrite { get; set; }
        public bool FailDelete { get; set; }
        public int Deletes { get; private set; }
        public byte[]? Read() => FailRead ? throw new IOException("Credential store unavailable.") : Value?.ToArray();
        public void Write(byte[] value) { if (FailWrite) throw new IOException("Credential store unavailable."); Value = value.ToArray(); }
        public void Delete() { if (FailDelete) throw new IOException("Credential store unavailable."); Value = null; Deletes++; }
    }

    [Fact]
    public async Task RestartRestoresIdentityFromServerAndImmediatelySavesRotatedCredential()
    {
        var store = new MemoryStore();
        await new AccountSessionVault(store).SaveAsync(Session);
        var stored = Encoding.UTF8.GetString(store.Value!);
        Assert.DoesNotContain(Session.AccessToken, stored); Assert.DoesNotContain(Session.Email!, stored);
        Assert.DoesNotContain("admin", stored); Assert.DoesNotContain("active", stored);
        // New vault and HTTP client: no original in-memory session is available.
        var vault = new AccountSessionVault(store);
        using var service = Service(vault, async (request, token) =>
        {
            Assert.Equal("?grant_type=refresh_token", request.RequestUri!.Query);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.Equal("refresh-0", body.GetProperty("refresh_token").GetString());
            return Refreshed("refresh-1");
        });
        var restored = await service.RestoreSessionAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserId, restored!.UserId); Assert.Equal(Session.Email, restored.Email);
        Assert.True(restored.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal("refresh-1", (await new AccountSessionVault(store).LoadAsync())!.RefreshToken);
    }

    [Fact]
    public async Task PurchaseFailureAfterRefreshDoesNotLoseTheRotatedToken()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        using (var service = Service(vault, (request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/auth/v1/token"
            ? Refreshed("refresh-1") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
        {
            var restored = await service.RestoreSessionAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<AccountException>(() => service.CheckPurchaseAsync(restored!, TestContext.Current.CancellationToken));
        }
        using var restarted = Service(new(store), async (request, token) =>
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.Equal("refresh-1", body.GetProperty("refresh_token").GetString()); return Refreshed("refresh-2");
        });
        Assert.NotNull(await restarted.RestoreSessionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TemporaryNetworkFailureKeepsCredentialAndAllowsRetry()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        var offline = true;
        using var service = Service(vault, (_, _) => Task.FromResult(offline ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Refreshed("refresh-1")));
        var error = await Assert.ThrowsAsync<AccountException>(() => service.RestoreSessionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.Unavailable, error.Failure);
        Assert.Equal("refresh-0", (await vault.LoadAsync())!.RefreshToken);
        offline = false; Assert.NotNull(await service.RestoreSessionAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task InvalidServerSessionIsDeletedAndDoesNotRestore(HttpStatusCode status)
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        using var service = Service(vault, (_, _) => Task.FromResult(new HttpResponseMessage(status)));
        Assert.Null(await service.RestoreSessionAsync(TestContext.Current.CancellationToken)); Assert.Null(store.Value);
    }

    [Fact]
    public async Task WrongServerIdentityCannotOverwriteTheStoredAccount()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        using var service = Service(vault, (_, _) => Task.FromResult(Refreshed("other-refresh", Guid.NewGuid())));
        var error = await Assert.ThrowsAsync<AccountException>(() => service.RestoreSessionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.InvalidResponse, error.Failure);
        Assert.Equal("refresh-0", (await vault.LoadAsync())!.RefreshToken);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"SchemaVersion\":99}")]
    [InlineData("{\"SchemaVersion\":1,\"UserId\":\"11111111-1111-4111-8111-111111111111\",\"RefreshToken\":\"token\\nline\"}")]
    public async Task CorruptCredentialsAreRemovedWithoutCrashing(string data)
    {
        var store = new MemoryStore { Value = Encoding.UTF8.GetBytes(data) };
        Assert.Null(await new AccountSessionVault(store).LoadAsync()); Assert.Null(store.Value);
    }

    [Fact]
    public async Task SecureStoreUnavailableNeverFallsBackToPlainText()
    {
        var store = new MemoryStore { FailRead = true };
        var error = await Assert.ThrowsAsync<AccountException>(() => new AccountSessionVault(store).LoadAsync());
        Assert.Equal(AccountFailure.SessionStorageUnavailable, error.Failure); Assert.Null(store.Value);
        store.FailRead = false; store.FailWrite = true;
        error = await Assert.ThrowsAsync<AccountException>(() => new AccountSessionVault(store).SaveAsync(Session));
        Assert.Equal(AccountFailure.SessionStorageUnavailable, error.Failure); Assert.Null(store.Value);
    }

    [Fact]
    public async Task LogoutStillAttemptsDeletionIfReadingTheStoredCredentialFails()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        store.FailRead = true;
        using var service = Service(vault, (_, _) => throw new InvalidOperationException("Read failure must not send HTTP."));
        var error = await Assert.ThrowsAsync<AccountException>(() => service.SignOutAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.SessionStorageUnavailable, error.Failure); Assert.Null(store.Value); Assert.Equal(1, store.Deletes);
    }

    [Fact]
    public void ProjectEnvironmentAndDataProfileHaveIndependentCredentialScopes()
    {
        var root = Path.Combine(Path.GetTempPath(), "unfold-profile");
        var original = AccountSessionVault.Scope(Connection, root);
        Assert.Equal(original, AccountSessionVault.Scope(Connection, root));
        Assert.NotEqual(original, AccountSessionVault.Scope(Connection with { ProjectUrl = new("https://other.supabase.co") }, root));
        Assert.NotEqual(original, AccountSessionVault.Scope(Connection with { Environment = AccountEnvironment.Test }, root));
        Assert.NotEqual(original, AccountSessionVault.Scope(Connection, root + "-isolated"));
        Assert.DoesNotContain("supabase", original);
    }

    [Fact]
    public async Task LogoutDeletesBeforeNetworkRevocationAndStaleRefreshCannotResurrect()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        using var service = Service(vault, (_, _) =>
        {
            Assert.Null(store.Value); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        await Assert.ThrowsAsync<AccountException>(() => service.SignOutAsync(Session, TestContext.Current.CancellationToken));
        Assert.Null(await service.RestoreSessionAsync(TestContext.Current.CancellationToken));
        var error = await Assert.ThrowsAsync<AccountException>(() => service.RefreshSessionAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.AuthenticationRequired, error.Failure); Assert.Null(store.Value);
    }

    [Fact]
    public async Task LateRefreshCompletesBeforeLogoutClearsAndCannotSaveAfterLogout()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var refreshing = Service(vault, (_, _) => { arrived.SetResult(); return reply.Task; });
        using var logout = Service(vault, (_, _) => throw new InvalidOperationException("Clear must not send HTTP."));
        var refresh = refreshing.RestoreSessionAsync(TestContext.Current.CancellationToken);
        await arrived.Task; var clear = logout.ClearSavedSessionAsync();
        Assert.False(clear.IsCompleted); reply.SetResult(Refreshed("late-refresh"));
        await refresh; await clear; Assert.Null(store.Value);
        Assert.Null(await logout.RestoreSessionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentRefreshesUseTheNewestStoredTokenRatherThanReusingAnOldOne()
    {
        var store = new MemoryStore(); var vault = new AccountSessionVault(store); await vault.SaveAsync(Session);
        var count = 0;
        using var service = Service(vault, async (request, token) =>
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.Equal("refresh-" + count, body.GetProperty("refresh_token").GetString());
            return Refreshed("refresh-" + ++count);
        });
        await Task.WhenAll(service.RefreshSessionAsync(Session, TestContext.Current.CancellationToken),
            service.RefreshSessionAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(2, count); Assert.Equal("refresh-2", (await vault.LoadAsync())!.RefreshToken);
    }

    [Fact]
    public async Task AutomaticRestoreChecksAccessAndNetworkRetryDoesNotOpenGoogleAgain()
    {
        var available = false;
        var service = new FakeAccountService { Purchase = PurchaseAccess.Active,
            RestoreSession = _ => available ? Task.FromResult<AccountSession?>(FakeAccountService.Session)
                : Task.FromException<AccountSession?>(new AccountException(AccountFailure.Unavailable)) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        await model.RestoreAsync(); Assert.True(model.IsLogin); Assert.False(model.PurchaseReady);
        Assert.Equal(model.Copy.RetryButton, model.PrimaryText);
        available = true; await model.PrimaryAsync();
        Assert.True(model.PurchaseReady); Assert.True(model.RestoredAutomatically);
        Assert.Equal(2, service.RestoreCalls); Assert.Equal(0, service.SignInCalls); Assert.Equal(1, service.PurchaseCalls);
    }

    [Fact]
    public async Task RestoreDoesNotEraseTheUnavailableConnectionMessage()
    {
        using var model = new AccountScreenModel(AccountScreenContent.Load(), new FakeAccountService { CanSignIn = false });
        await model.RestoreAsync();
        Assert.False(model.CanPrimary); Assert.Equal(model.Copy.SignInUnavailable, model.Status);
    }

    [Fact]
    public async Task ChangeAccountFailureKeepsIdentityAndDoesNotLeaveTheButtonsBusy()
    {
        var service = new FakeAccountService { ClearSavedSession = () => Task.FromException(new AccountException(AccountFailure.SessionStorageUnavailable)) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, FakeAccountService.Session);
        await model.SecondaryAsync();
        Assert.True(model.IsPurchase); Assert.False(model.IsBusy); Assert.Equal(model.Copy.SessionStorageUnavailable, model.Status);
    }

    [Fact]
    public async Task InvalidRestoreAndFailedDeletionRemainLockedAndReportTheStorageProblem()
    {
        var service = new FakeAccountService
        {
            RestoreSession = _ => Task.FromResult<AccountSession?>(FakeAccountService.Session),
            PurchaseError = new(AccountFailure.AuthenticationRequired),
            ClearSavedSession = () => Task.FromException(new AccountException(AccountFailure.SessionStorageUnavailable))
        };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        await model.RestoreAsync();
        Assert.True(model.IsLogin); Assert.False(model.IsBusy); Assert.False(model.PurchaseReady);
        Assert.Equal(model.Copy.SessionStorageUnavailable, model.Status);
    }

    [Fact]
    public async Task DisposingWhileRestoringCannotPublishOrUnlockALateSession()
    {
        var reply = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeAccountService { RestoreSession = _ => reply.Task, Purchase = PurchaseAccess.Active };
        var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        var events = 0; model.SessionChanged += _ => events++;
        var restore = model.RestoreAsync(); Assert.True(model.IsBusy); model.Dispose();
        reply.SetResult(FakeAccountService.Session); await restore;
        Assert.Equal(0, events); Assert.Equal(0, service.PurchaseCalls); Assert.False(model.PurchaseReady);
    }

    [AvaloniaFact]
    public async Task NewAppRuntimesRestoreWithoutGoogleOrCodeAndLogoutRemainsDeletedOnTheNextStart()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        var store = new MemoryStore(); await new AccountSessionVault(store).SaveAsync(Session);
        var accessChecks = 0; var refreshes = 0;
        try
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            new AppSettings { ShowPet = false }.Save(Path.Combine(temp.Path, "settings.json"));
            Task<HttpResponseMessage> Handle(HttpRequestMessage request, CancellationToken token)
            {
                if (request.RequestUri!.AbsolutePath == "/auth/v1/token") return Task.FromResult(Refreshed("restart-refresh-" + ++refreshes));
                if (request.RequestUri.AbsolutePath == "/auth/v1/logout") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                Assert.Equal("/functions/v1/get-app-access", request.RequestUri.AbsolutePath); accessChecks++;
                return Task.FromResult(Json(new { schema_version = 1, user_id = UserId, product_id = "unfold", environment = "live", status = "active" }));
            }
            for (var launch = 0; launch < 3; launch++)
            {
                using var lifetime = new ClassicDesktopStyleApplicationLifetime();
                var vault = new AccountSessionVault(store);
                using var runtime = new AppRuntime(lifetime) { AccountServiceFactory = () => Service(vault, Handle) };
                await runtime.Start(false); Dispatcher.UIThread.RunJobs();
                if (launch < 2)
                {
                    Assert.True(runtime.AccessAllowed); Assert.Null(runtime.ActiveAccount);
                    Assert.Equal(UserId, runtime.AccountSession!.UserId); Assert.True(lifetime.MainWindow!.IsVisible);
                    if (launch == 1) { await runtime.SignOut(); Assert.Null(store.Value); }
                }
                else
                {
                    Assert.False(runtime.AccessAllowed); Assert.Null(runtime.AccountSession);
                    Assert.True(runtime.ActiveAccount!.Model.IsLogin); Assert.False(lifetime.MainWindow!.IsVisible);
                }
            }
            Assert.Equal(2, accessChecks); Assert.Equal(2, refreshes);
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
}

[Collection("Timer settings")]
public class AccountRestoreRuntimeTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedSessionAndPurchaseChecksNeverOpenTheLoginWindow(bool background)
    {
        using var scope = new Scope();
        var loginWindows = 0;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<AccountWindow>((_, _) => loginWindows++);
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var purchase = new TaskCompletionSource<PurchaseAccess>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        scope.Service.PurchaseCheck = (_, _) => purchase.Task;
        var start = scope.Runtime.Start(background);
        await Wait(() => scope.Service.RestoreCalls == 1);
        Assert.Null(scope.Runtime.ActiveAccount); Assert.False(scope.Runtime.AccessAllowed);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(scope.Lifetime.MainWindow!.IsVisible);
        scope.Runtime.ShowAccount();
        Assert.Null(scope.Runtime.ActiveAccount);
        session.SetResult(FakeAccountService.Session);
        await Wait(() => scope.Service.PurchaseCalls == 1);
        Assert.Null(scope.Runtime.ActiveAccount); Assert.False(scope.Runtime.AccessAllowed);
        purchase.SetResult(PurchaseAccess.Active); await start;
        Assert.Equal(0, loginWindows); Assert.Null(scope.Runtime.ActiveAccount);
        Assert.True(scope.Runtime.AccessAllowed); Assert.Equal(!background, scope.Lifetime.MainWindow.IsVisible);
        Assert.Equal(0, scope.Service.SignInCalls); Assert.True(scope.Service.Disposed);
    }

    [AvaloniaFact]
    public async Task SettingsRequestedDuringBackgroundRestoreOpensOnlyAfterVerification()
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        var start = scope.Runtime.Start(true);
        await Wait(() => scope.Service.RestoreCalls == 1);
        scope.Runtime.ShowSettings();
        Assert.Null(scope.Runtime.ActiveAccount); Assert.False(scope.Lifetime.MainWindow!.IsVisible);
        session.SetResult(FakeAccountService.Session); await start;
        Assert.True(scope.Runtime.AccessAllowed); Assert.True(scope.Lifetime.MainWindow.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInvalidSessionShowsLoginAfterRestorationFinishes(bool invalid)
    {
        using var scope = new Scope();
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        var start = scope.Runtime.Start(false);
        await Wait(() => scope.Service.RestoreCalls == 1);
        Assert.Null(scope.Runtime.ActiveAccount);
        if (invalid) session.SetException(new AccountException(AccountFailure.AuthenticationRequired));
        else session.SetResult(null);
        await start;
        Assert.False(scope.Runtime.AccessAllowed); Assert.Null(scope.Runtime.AccountSession);
        Assert.True(scope.Runtime.ActiveAccount!.IsVisible); Assert.True(scope.Runtime.ActiveAccount.Model.IsLogin);
        Assert.False(scope.Lifetime.MainWindow!.IsVisible); Assert.False(scope.Service.Disposed);
    }

    [AvaloniaFact]
    public async Task StartupNetworkFailurePreservesTheRestoreRetryWithoutGoogleSignIn()
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        scope.Service.RestoreSession = _ => Task.FromException<AccountSession?>(new AccountException(AccountFailure.Unavailable));
        await scope.Runtime.Start(false);
        var account = scope.Runtime.ActiveAccount!;
        Assert.True(account.IsVisible); Assert.False(scope.Runtime.AccessAllowed);
        Assert.Equal(account.Model.Copy.RestoreUnavailable, account.Model.Status);
        Assert.Equal(account.Model.Copy.RetryButton, account.Model.PrimaryText);
        Assert.False(scope.Service.Disposed);
        scope.Service.RestoreSession = _ => Task.FromResult<AccountSession?>(FakeAccountService.Session);
        account.FindControl<Button>("AccountPrimary")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(() => scope.Runtime.AccessAllowed && scope.Lifetime.MainWindow!.IsVisible);
        Assert.Null(scope.Runtime.ActiveAccount); Assert.Equal(0, scope.Service.SignInCalls);
        Assert.Equal(2, scope.Service.RestoreCalls);
    }

    [AvaloniaFact]
    public async Task DisposingDuringStartupDoesNotPublishALateSessionOrOpenWindows()
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        var start = scope.Runtime.Start(false);
        await Wait(() => scope.Service.RestoreCalls == 1);
        scope.Runtime.Dispose(); Assert.True(scope.Service.Disposed);
        session.SetResult(FakeAccountService.Session); await start;
        Assert.Null(scope.Runtime.AccountSession); Assert.Null(scope.Runtime.ActiveAccount);
        Assert.False(scope.Runtime.AccessAllowed); Assert.Equal(0, scope.Service.PurchaseCalls);
    }

    [AvaloniaFact]
    public async Task SignOutDuringStartupCannotBeUndoneByALateRestore()
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        var start = scope.Runtime.Start(false);
        await Wait(() => scope.Service.RestoreCalls == 1);
        await scope.Runtime.SignOut();
        session.SetResult(FakeAccountService.Session); await start;
        Assert.Null(scope.Runtime.AccountSession); Assert.False(scope.Runtime.AccessAllowed);
        Assert.True(scope.Runtime.ActiveAccount!.IsVisible); Assert.True(scope.Runtime.ActiveAccount.Model.IsLogin);
        Assert.Equal(1, scope.Service.ClearCalls); Assert.Equal(0, scope.Service.PurchaseCalls);
    }

    [AvaloniaFact]
    public async Task QuitDuringStartupCancelsRestoreWithoutShowingLoginOrHome()
    {
        using var scope = new Scope();
        var session = new TaskCompletionSource<AccountSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.RestoreSession = _ => session.Task;
        var start = scope.Runtime.Start(false);
        await Wait(() => scope.Service.RestoreCalls == 1);
        scope.Runtime.ConfirmActionOverride = (_, _, _) => throw new InvalidOperationException("Startup has no visible window or running timer to confirm.");
        await scope.Runtime.Quit();
        Assert.True(scope.Service.Disposed);
        session.SetResult(FakeAccountService.Session); await start;
        Assert.Null(scope.Runtime.ActiveAccount); Assert.Null(scope.Runtime.AccountSession);
        Assert.False(scope.Runtime.AccessAllowed);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifiedRestoredSessionStartsTheAppAndBackgroundStartKeepsHomeHidden(bool background)
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        await scope.Runtime.Start(background); await Wait(() => scope.Runtime.AccessAllowed && scope.Runtime.ActiveAccount is null);
        Assert.NotNull(scope.Runtime.AccountSession); Assert.Equal(0, scope.Service.SignInCalls);
        Assert.Equal(1, scope.Service.RestoreCalls); Assert.Equal(1, scope.Service.PurchaseCalls);
        Assert.Equal(!background, scope.Lifetime.MainWindow!.IsVisible);
        Assert.False(scope.Runtime.Clock.Stopped);
    }
    [AvaloniaTheory]
    [InlineData(PurchaseAccess.Unowned)]
    [InlineData(PurchaseAccess.Revoked)]
    public async Task StoredLoginDoesNotBypassCurrentServerAccess(PurchaseAccess access)
    {
        using var scope = new Scope(); scope.Service.Purchase = access;
        await scope.Runtime.Start(false);
        Assert.NotNull(scope.Runtime.AccountSession); Assert.False(scope.Runtime.AccessAllowed);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(scope.Lifetime.MainWindow!.IsVisible);
        Assert.True(scope.Runtime.ActiveAccount!.Model.IsPurchase);
    }
    [AvaloniaFact]
    public async Task FailedStartupVerificationRemainsLockedAndKeepsVerifiedLoginForRetry()
    {
        using var scope = new Scope(); scope.Service.PurchaseError = new(AccountFailure.Unavailable);
        await scope.Runtime.Start(false);
        Assert.False(scope.Runtime.AccessAllowed); Assert.NotNull(scope.Runtime.AccountSession);
        Assert.True(scope.Runtime.ActiveAccount!.Model.PurchaseUnknown);
        Assert.Equal(scope.Runtime.AccountContent.Copy.RetryButton, scope.Runtime.ActiveAccount.Model.PrimaryText);
    }
    [AvaloniaFact]
    public async Task TokenRenewalIsNotLostWhenTheSubsequentAccessCheckFails()
    {
        using var scope = new Scope(); scope.Service.Purchase = PurchaseAccess.Active;
        scope.Service.RestoreSession = _ => Task.FromResult<AccountSession?>(new(FakeAccountService.Session.UserId,
            "short-access", "short-refresh", DateTimeOffset.UtcNow.AddSeconds(10), "person@example.test"));
        await scope.Runtime.Start(false); await Wait(() => scope.Runtime.AccessAllowed);
        scope.Service.PurchaseError = new(AccountFailure.Unavailable);
        await scope.Runtime.RecheckPurchaseAccess();
        Assert.False(scope.Runtime.AccessAllowed); Assert.Equal("refreshed-access", scope.Runtime.AccountSession!.AccessToken);
        Assert.True(scope.Runtime.ActiveAccount!.Model.IsPurchase);
    }
    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++)
        { Dispatcher.UIThread.RunJobs(); if (predicate()) return; await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(predicate());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public ClassicDesktopStyleApplicationLifetime Lifetime { get; } = new();
        public FakeAccountService Service { get; } = new() { RestoreSession = _ => Task.FromResult<AccountSession?>(FakeAccountService.Session) };
        public AppRuntime Runtime { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            new AppSettings { ShowPet = false }.Save(Path.Combine(temp.Path, "settings.json"));
            Runtime = new(Lifetime) { AccountServiceFactory = () => Service };
        }
        public void Dispose()
        {
            Runtime.Dispose(); Lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
