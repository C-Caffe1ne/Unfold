using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
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
public class AccountAccessCodeTests
{
    [Fact]
    public void OlderPresentationFilesKeepTheCodeEntryDefaultsAndCanEditTheNewCopy()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(AccountScreenContent.Load(), AccountScreenContent.JsonOptions))!;
        var copy = json["copy"]!.AsObject();
        foreach (var key in new[] { "codeButton", "codeDialogTitle", "codeLabel", "codeConfirmButton", "invalidCodeMessage" })
            copy.Remove(key);
        Assert.Equal("코드 입력", AccountScreenContent.Parse(json.ToJsonString()).Copy.CodeButton);
        copy["codeButton"] = "다른 코드 버튼";
        Assert.Equal("다른 코드 버튼", AccountScreenContent.Parse(json.ToJsonString()).Copy.CodeButton);
    }

    [AvaloniaFact]
    public void LoginIsRequiredBeforeCodeEntryAndCannotDismissBeforeAccess()
    {
        var allowed = false;
        var service = new FakeAccountService();
        var window = new AccountWindow(new(AccountScreenContent.Load(), service), requiresPurchase: true, allowClose: () => allowed);
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var code = window.FindControl<Button>("AccountCode")!;
            Assert.False(code.IsVisible);
            code.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(window.OwnedWindows);
            window.Close(); Assert.True(window.IsVisible); Assert.False(service.Disposed);
            Assert.False(window.Model.PurchaseReady); Assert.Equal(0, service.SignInCalls);
        }
        finally { allowed = true; window.Close(); }
        Assert.True(service.Disposed);
    }
    [Fact]
    public async Task RedemptionRequiresLoginAndRechecksStoredGrantBeforeUnlocking()
    {
        var service = new FakeAccountService();
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service);
        Assert.False(model.CanRedeemCode);
        Assert.NotNull(await model.RedeemCodeAsync("admin", TestContext.Current.CancellationToken));
        Assert.Equal(0, service.RedeemCalls);
        await model.PrimaryAsync();
        service.Redeem = (session, code, _) => {
            Assert.Equal("admin", code); Assert.NotEqual(Guid.Empty, session.UserId);
            service.Purchase = PurchaseAccess.Active;
            return Task.FromResult(AccessCodeResult.Redeemed);
        };
        Assert.True(model.CanRedeemCode);
        Assert.Null(await model.RedeemCodeAsync(" admin ", TestContext.Current.CancellationToken));
        Assert.True(model.PurchaseReady); Assert.False(model.CanRedeemCode);
        Assert.Equal(2, service.PurchaseCalls); Assert.Equal(0, service.CheckoutCalls);
    }

    [Theory]
    [InlineData(AccessCodeResult.InvalidCode)]
    [InlineData(AccessCodeResult.RateLimited)]
    public async Task RejectedCodesKeepAccessLockedAndAllowRetry(AccessCodeResult result)
    {
        var service = new FakeAccountService { Redeem = (_, _, _) => Task.FromResult(result) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, FakeAccountService.Session);
        Assert.Equal(result == AccessCodeResult.RateLimited ? model.Copy.CodeRateLimited : model.Copy.InvalidCodeMessage,
            await model.RedeemCodeAsync("admin", TestContext.Current.CancellationToken));
        Assert.False(model.PurchaseReady); Assert.True(model.CanRedeemCode); Assert.Equal(0, service.PurchaseCalls);
    }

    [Fact]
    public async Task SavedCodeAloneCannotUnlockWhenVerificationFails()
    {
        var service = new FakeAccountService { Redeem = (_, _, _) => Task.FromResult(AccessCodeResult.Redeemed),
            PurchaseError = new(AccountFailure.Unavailable) };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, FakeAccountService.Session);
        Assert.NotNull(await model.RedeemCodeAsync("admin", TestContext.Current.CancellationToken));
        Assert.False(model.PurchaseReady); Assert.True(model.CanRedeemCode);
    }

    [Fact]
    public async Task CancelledOrDisposedRedemptionCannotApplyALateReplyAndDuplicateSubmissionIsIgnored()
    {
        var pending = new TaskCompletionSource<AccessCodeResult>();
        var service = new FakeAccountService { Redeem = (_, _, _) => pending.Task, Purchase = PurchaseAccess.Active };
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, FakeAccountService.Session);
        using var cancel = new CancellationTokenSource();
        var first = model.RedeemCodeAsync("admin", cancel.Token);
        Assert.False(model.CanRedeemCode);
        Assert.NotNull(await model.RedeemCodeAsync("admin", cancel.Token));
        cancel.Cancel(); model.Dispose(); pending.SetResult(AccessCodeResult.Redeemed);
        Assert.NotNull(await first); Assert.False(model.PurchaseReady);
        Assert.Equal(1, service.RedeemCalls); Assert.Equal(0, service.PurchaseCalls);
    }

    [Fact]
    public async Task ExpiredSessionIsRefreshedBeforeRedeemingAndAuthenticationFailureRequiresLogin()
    {
        var service = new FakeAccountService { Redeem = (_, _, _) => throw new AccountException(AccountFailure.AuthenticationRequired) };
        var expired = new AccountSession(Guid.NewGuid(), "expired", "refresh", DateTimeOffset.UtcNow.AddMinutes(-1));
        using var model = new AccountScreenModel(AccountScreenContent.Load(), service, expired);
        Assert.NotNull(await model.RedeemCodeAsync("admin", TestContext.Current.CancellationToken));
        Assert.Equal(1, service.RefreshCalls); Assert.True(model.IsLogin); Assert.False(model.PurchaseReady);
    }

    [AvaloniaFact]
    public async Task AuthenticatedCodeModalUsesServerResultThenClosesAccountWindow()
    {
        var service = new FakeAccountService();
        var window = new AccountWindow(new(AccountScreenContent.Load(), service), requiresPurchase: true);
        try
        {
            window.Show(); await window.Model.PrimaryAsync(); Dispatcher.UIThread.RunJobs();
            var button = window.FindControl<Button>("AccountCode")!;
            Assert.True(button.IsVisible); Assert.True(button.IsEnabled);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => window.OwnedWindows.Any());
            var dialog = Assert.Single(window.OwnedWindows);
            var input = Find<TextBox>(dialog, "AccessCodeInput");
            var confirm = Find<Button>(dialog, "AccessCodeConfirm");
            input.Text = "invalid"; confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => Find<TextBlock>(dialog, "AccessCodeError").IsVisible);
            Assert.True(window.IsVisible); Assert.False(window.Model.PurchaseReady);
            service.Redeem = (_, _, _) => { service.Purchase = PurchaseAccess.Active; return Task.FromResult(AccessCodeResult.Redeemed); };
            input.Text = "admin"; confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => !window.IsVisible);
            Assert.True(window.Model.PurchaseReady); Assert.Equal(2, service.RedeemCalls);
        }
        finally { window.Model.Dispose(); foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); }
    }

    private static T Find<T>(Window owner, string name) where T : Control =>
        owner.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static async Task Wait(Func<bool> completed)
    {
        for (var i = 0; i < 100 && !completed(); i++)
        {
            Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.True(completed());
    }
}
