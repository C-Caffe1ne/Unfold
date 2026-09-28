using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AccountPresentationTests
{
    [Fact]
    public void UpdatedWelcomeAppearsOnceEvenWhenLegacyDismissalExists()
    {
        using var dir = new TempDirectory();
        var marker = Path.Combine(dir.Path, "account-welcome-seen");
        Assert.False(AppRuntime.HasSeenCurrentAccountWelcome(marker));
        File.WriteAllText(marker, "1");
        Assert.False(AppRuntime.HasSeenCurrentAccountWelcome(marker));
        File.WriteAllText(marker, AppRuntime.AccountWelcomeRevision);
        Assert.True(AppRuntime.HasSeenCurrentAccountWelcome(marker));
    }

    [Fact]
    public void EditedCopyAndIndependentCurrencyPricesLoadWithoutCodeChanges()
    {
        using var dir = new TempDirectory();
        var content = AccountScreenContent.Load();
        content = content with { Brand = "Changed", Copy = content.Copy with { WelcomeTitle = "Changed title" },
            Markets = [content.Markets[0] with { AmountMinor = 5900 }, content.Markets[1]] };
        var path = Path.Combine(dir.Path, "entry-screen.json");
        File.WriteAllText(path, JsonSerializer.Serialize(content, AccountScreenContent.JsonOptions));
        var edited = AccountScreenContent.Load(path);
        Assert.Equal("Changed title", edited.Copy.WelcomeTitle);
        Assert.Equal("5,900원", edited.Markets[0].DisplayPrice);
        Assert.Equal("US$3.99", edited.Markets[1].DisplayPrice);
        File.WriteAllText(path, "{invalid json");
        Assert.Equal("unfold", AccountScreenContent.Load(path).Brand);
        Assert.Equal("4,900원", AccountScreenContent.Load(path).Markets[0].DisplayPrice);
    }

    [Fact]
    public void InvalidPresentationFallsBackAndImageCannotEscapeAssetDirectory()
    {
        var content = AccountScreenContent.Load();
        foreach (var invalid in new[] { content with { CompanionImage = "../../other.png" }, content with { Markets = [] },
            content with { DefaultMarket = "missing" }, content with { Copy = content.Copy with { WelcomeTitle = null! } } })
            Assert.Throws<InvalidDataException>(() => AccountScreenContent.Parse(JsonSerializer.Serialize(invalid, AccountScreenContent.JsonOptions)));
    }

    [Fact]
    public async Task UnknownPurchaseIsRetryableAndNeverTreatedAsUnowned()
    {
        var service = new FakeAccountService { PurchaseError = new(AccountFailure.Unavailable) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        await model.PrimaryAsync();
        Assert.True(model.IsPurchase); Assert.True(model.PurchaseUnknown); Assert.True(model.CanPrimary);
        Assert.Equal(model.Copy.RetryButton, model.PrimaryText);
        Assert.Equal("person@example.test", model.Email);
        service.PurchaseError = null;
        await model.PrimaryAsync();
        Assert.Equal(1, service.SignInCalls); Assert.Equal(2, service.PurchaseCalls);
        Assert.False(model.PurchaseUnknown); Assert.False(model.PurchaseReady); Assert.True(model.CanPrimary);
        Assert.Equal(model.Copy.PurchaseButton, model.PrimaryText);
        Assert.Equal("", model.Status);
        model.Secondary();
        Assert.True(model.IsLogin); Assert.False(model.PurchaseUnknown); Assert.False(model.PurchaseReady);
    }

    [Fact]
    public async Task KoreanCheckoutOpensOnceThenPurchaseIsCheckedExplicitly()
    {
        var service = new FakeAccountService();
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        await model.PrimaryAsync();
        Assert.True(model.IsPurchase); Assert.Equal(model.Copy.PurchaseButton, model.PrimaryText);
        await model.PrimaryAsync();
        Assert.Equal(1, service.CheckoutCalls); Assert.True(model.CheckoutStarted);
        Assert.Equal(model.Copy.CheckPurchaseButton, model.PrimaryText);
        Assert.Equal(model.Copy.CheckoutWaiting, model.Status);
        await model.PrimaryAsync();
        Assert.Equal(2, service.PurchaseCalls); Assert.Equal(model.Copy.PurchaseNotFound, model.Status);
        Assert.Equal(1, service.CheckoutCalls);
        service.Purchase = PurchaseAccess.Active;
        await model.PrimaryAsync();
        Assert.True(model.PurchaseReady); Assert.Equal(model.Copy.ReadyStatus, model.Status);
    }

    [Fact]
    public async Task SignInErrorsStayOnLoginAndActiveStatusOnlyComesFromServer()
    {
        var service = new FakeAccountService { SignInError = new(AccountFailure.Unavailable) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        await model.PrimaryAsync();
        Assert.True(model.IsLogin); Assert.Equal(0, service.PurchaseCalls); Assert.True(model.CanPrimary);
        service.SignInError = null; service.Purchase = PurchaseAccess.Active;
        await model.PrimaryAsync();
        Assert.True(model.PurchaseReady); Assert.Equal(model.Copy.ReadyStatus, model.Status);
        model.Secondary(); Assert.False(model.PurchaseReady);
    }

    [Fact]
    public async Task CancelAndRepeatedClicksCannotStartExtraLoginsOrApplyLateResults()
    {
        var pending = new TaskCompletionSource<AccountSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeAccountService { SignIn = _ => pending.Task };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        var task = model.PrimaryAsync();
        await model.PrimaryAsync();
        Assert.Equal(1, service.SignInCalls); Assert.True(model.IsBusy);
        model.Secondary(); pending.SetResult(FakeAccountService.Session);
        await task;
        Assert.True(model.IsLogin); Assert.False(model.IsBusy); Assert.Equal(0, service.PurchaseCalls);
        Assert.Equal(model.Copy.SignInCancelled, model.Status);
    }

    [Fact]
    public async Task DisposingScreenCancelsWorkAndForgetsSession()
    {
        var service = new FakeAccountService { SignIn = async token => { await Task.Delay(Timeout.Infinite, token); return FakeAccountService.Session; } };
        var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        var task = model.PrimaryAsync(); model.Dispose(); await task;
        Assert.True(service.Disposed); Assert.Equal(0, service.PurchaseCalls); Assert.True(model.IsLogin);
    }
}

internal sealed class FakeAccountService : IAccountScreenService
{
    public static AccountSession Session => new(Guid.NewGuid(), "private-access", "private-refresh", DateTimeOffset.UtcNow.AddHours(1), "person@example.test");
    public bool CanSignIn { get; set; } = true;
    public int SignInCalls { get; private set; }
    public int PurchaseCalls { get; private set; }
    public int CheckoutCalls { get; private set; }
    public bool Disposed { get; private set; }
    public PurchaseAccess Purchase { get; set; } = PurchaseAccess.Unowned;
    public AccountException? SignInError { get; set; }
    public AccountException? PurchaseError { get; set; }
    public AccountException? CheckoutError { get; set; }
    public Func<CancellationToken, Task<AccountSession>>? SignIn { get; set; }
    public HashSet<string> EnabledMarkets { get; } = ["KR", "GLOBAL"];
    public bool CanCheckout(string market) => EnabledMarkets.Contains(market);
    public Task<AccountSession> SignInAsync(CancellationToken token)
    {
        SignInCalls++;
        return SignInError is not null ? Task.FromException<AccountSession>(SignInError) : SignIn?.Invoke(token) ?? Task.FromResult(Session);
    }
    public Task<PurchaseAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token)
    {
        PurchaseCalls++;
        return PurchaseError is not null ? Task.FromException<PurchaseAccess>(PurchaseError) : Task.FromResult(Purchase);
    }
    public Task StartCheckoutAsync(AccountSession session, string market, Guid requestId, CancellationToken token)
    {
        CheckoutCalls++;
        Assert.NotEqual(Guid.Empty, requestId);
        Assert.Contains(market, EnabledMarkets);
        return CheckoutError is not null ? Task.FromException(CheckoutError) : Task.CompletedTask;
    }
    public void Dispose() => Disposed = true;
}

[Collection("Timer settings")]
public class AccountWindowTests
{
    [AvaloniaFact]
    public void InvalidConnectionOverrideDisablesSignInWithoutBreakingTheApp()
    {
        const string name = "UNFOLD_SUPABASE_URL";
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "not an absolute URI");
            Assert.Null(AccountConnection.Load());
            using var service = new DesktopAccountService(AccountScreenContent.Load());
            Assert.False(service.CanSignIn);
        }
        finally { Environment.SetEnvironmentVariable(name, original); }
    }

    [AvaloniaFact]
    public async Task ALayoutRendersLoginAndPurchaseWithSharedThemesAndFitsMinimumSize()
    {
        var previous = DesignSystem.CurrentTheme;
        try
        {
            foreach (var theme in DesignSystem.Themes)
            {
                DesignSystem.ApplyTheme(theme.Id);
                using var model = new AccountScreenModel(AccountScreenContent.Load(), new FakeAccountService());
                var window = new AccountWindow(model); window.Show(); Layout();
                Assert.Equal(model.Copy.WelcomeTitle, window.FindControl<TextBlock>("AccountHeading")!.Text);
                Assert.Same(DesignSystem.Shell, window.FindControl<Border>("AccountFrame")!.Background);
                Assert.NotNull(window.FindControl<Image>("AccountCompanion")!.Source);
                Capture(window, "login-" + theme.Id);
                await model.PrimaryAsync(); Layout();
                Assert.Equal(model.Copy.PurchaseTitle, window.FindControl<TextBlock>("AccountHeading")!.Text);
                Assert.True(window.FindControl<Button>("AccountPrimary")!.IsEnabled);
                Assert.Equal("4,900원", window.FindControl<TextBlock>("AccountPrice")!.Text);
                Capture(window, "purchase-" + theme.Id);
                window.FindControl<ComboBox>("AccountMarket")!.SelectedItem = model.Markets[1]; Layout();
                Assert.Equal("US$3.99", model.Price);
                Assert.Equal("US$3.99", window.FindControl<TextBlock>("AccountPrice")!.Text);
                window.Width = 640; window.Height = 560; Layout();
                AssertInside(window, window.FindControl<Button>("AccountOpenApp")!);
                var scroll = window.FindControl<ScrollViewer>("AccountFormScroll")!;
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
                scroll.ScrollToEnd(); Layout();
                AssertInside(window, window.FindControl<Button>("AccountPrimary")!);
                AssertInside(window, window.FindControl<Button>("AccountSecondary")!);
                Capture(window, "minimum-" + theme.Id);
                window.FindControl<Button>("AccountOpenApp")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(window.IsVisible);
            }
        }
        finally { DesignSystem.ApplyTheme(previous); }
    }

    [AvaloniaFact]
    public void UnconfiguredSignInHasAnErrorAndStillAllowsOpeningTheLocalApp()
    {
        var service = new FakeAccountService { CanSignIn = false };
        var window = new AccountWindow(new(AccountScreenContent.Load(), service)); window.Show(); Layout();
        Assert.False(window.FindControl<Button>("AccountPrimary")!.IsEnabled);
        Assert.True(window.FindControl<Button>("AccountOpenApp")!.IsEnabled);
        Assert.Equal(window.Model.Copy.SignInUnavailable, window.FindControl<TextBlock>("AccountStatus")!.Text);
        window.Close(); Assert.True(service.Disposed);
    }
    private static void Layout() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    private static void AssertInside(Window window, Control control)
    {
        var origin = control.TranslatePoint(default, window)!.Value;
        Assert.True(origin.X >= 0 && origin.Y >= 0 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1
            && origin.Y + control.Bounds.Height <= window.ClientSize.Height + 1, control.Name);
    }
    private static void Capture(Window window, string name)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/verification/account-entry"));
        Directory.CreateDirectory(path);
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
        frame.Save(Path.Combine(path, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}

public class AccountLoopbackTests
{
    [Fact]
    public async Task OnlyExactLocalCallbackIsAcceptedAndNothingSecretIsEchoed()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var callback = new AccountLoopbackCallback(0, "Return to app");
        var receiving = callback.ReceiveAsync(deadline.Token);
        using var http = new HttpClient();
        var wrong = await http.GetAsync(new Uri(callback.RedirectUri, "/favicon.ico"), deadline.Token);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.False(receiving.IsCompleted);
        var response = await http.GetAsync(callback.RedirectUri + "?code=private-code", deadline.Token);
        Assert.Equal("Return to app", await response.Content.ReadAsStringAsync(deadline.Token));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("?code=private-code", (await receiving).Query);
    }

    [Fact]
    public async Task CancelledBrowserCallbackReturnsToLoginAndDisposalReleasesPort()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var callback = new AccountLoopbackCallback(0, "Return to app");
        var port = callback.RedirectUri.Port;
        try
        {
            var receiving = callback.ReceiveAsync(deadline.Token);
            using var http = new HttpClient();
            using var response = await http.GetAsync(callback.RedirectUri, deadline.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receiving);
        }
        finally { callback.Dispose(); }
        using var next = new AccountLoopbackCallback(port, "Return to app");
        using var cancel = new CancellationTokenSource(); var pending = next.ReceiveAsync(cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
