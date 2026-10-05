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
public class PurchaseGateTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingWelcomeMarkerAndBackgroundStartDoNotBypassPurchase(bool background)
    {
        using var scope = new Scope();
        File.WriteAllText(Path.Combine(scope.Root, "account-welcome-seen"), AppRuntime.AccountWelcomeRevision);
        await scope.Runtime.Start(background);
        AssertLocked(scope);
        var account = scope.Runtime.ActiveAccount!;
        Assert.False(account.FindControl<Button>("AccountCode")!.IsVisible);
        account.Close(); Assert.True(account.IsVisible);
        scope.Runtime.ShowSettings(); scope.Runtime.TogglePause(); scope.Runtime.Reset();
        await scope.Runtime.ShowReminder(); await scope.Runtime.ShowReminderPreview(PetNotice.Invitation);
        await scope.Runtime.OpenEditor();
        AssertLocked(scope); Assert.Null(scope.Runtime.ActiveEditor);
        Assert.Null(scope.Runtime.ActiveReminder); Assert.Null(scope.Runtime.PreviewNotice);
        Assert.Equal(0, scope.Runtime.DueSoundRequests);
        await Assert.ThrowsAsync<AccountException>(() => scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = false }));
    }

    [AvaloniaTheory]
    [InlineData(PurchaseAccess.Unowned)]
    [InlineData(PurchaseAccess.Revoked)]
    public async Task SignInWithoutActiveEntitlementDoesNotUnlock(PurchaseAccess access)
    {
        using var scope = new Scope(); scope.Service.Purchase = access;
        await scope.Runtime.Start(false); await scope.Runtime.ActiveAccount!.Model.PrimaryAsync();
        Assert.NotNull(scope.Runtime.AccountSession); AssertLocked(scope);
    }

    [AvaloniaFact]
    public async Task VerifiedPurchaseUnlocksAndSignOutImmediatelyStopsPaidFeatures()
    {
        using var scope = new Scope(); await scope.Runtime.Start(false); await scope.Unlock();
        Assert.True(scope.Runtime.AccessAllowed); Assert.False(scope.Runtime.Clock.Stopped);
        Assert.True(scope.Home.IsVisible); Assert.NotNull(scope.Runtime.ActivePet);
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await scope.Runtime.SignOut();
        AssertLocked(scope); Assert.Null(scope.Runtime.AccountSession); Assert.Null(scope.Runtime.ActiveReminder);
        Assert.False(scope.Runtime.ActivePet!.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationOrUnavailableServerLocksAccessAndAllowsVerifiedRetry(bool unavailable)
    {
        using var scope = new Scope(); await scope.Runtime.Start(false); await scope.Unlock();
        if (unavailable) scope.Service.PurchaseError = new(AccountFailure.Unavailable);
        else scope.Service.Purchase = PurchaseAccess.Revoked;
        await scope.Runtime.RecheckPurchaseAccess(); AssertLocked(scope);
        scope.Service.PurchaseError = null; scope.Service.Purchase = PurchaseAccess.Active;
        await scope.Unlock(); Assert.True(scope.Runtime.AccessAllowed); Assert.True(scope.Home.IsVisible);
    }

    [AvaloniaFact]
    public void ProductionConnectionRequiresLiveEnvironment()
    {
        Assert.Equal(AccountEnvironment.Live, AccountConnection.Load()!.Environment);
    }

    [AvaloniaFact]
    public async Task LateActiveResponseCannotRestoreAccessAfterLogout()
    {
        using var scope = new Scope(); await scope.Runtime.Start(false); await scope.Unlock();
        var pending = new TaskCompletionSource<PurchaseAccess>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Service.PurchaseCheck = (_, _) => pending.Task;
        var recheck = scope.Runtime.RecheckPurchaseAccess();
        await scope.Runtime.SignOut();
        pending.SetResult(PurchaseAccess.Active); await recheck;
        AssertLocked(scope); Assert.Null(scope.Runtime.AccountSession);
    }

    [AvaloniaFact]
    public async Task LockedQuitDraftGuardUsesAccountModalAndCannotRevealHome()
    {
        using var scope = new Scope(); await scope.Runtime.Start(false); await scope.Unlock();
        scope.Home.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SettingsNavPacks")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Dispatcher.UIThread.RunJobs();
        scope.Home.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "CustomPetName").Text = "미저장 펫";
        scope.Service.Purchase = PurchaseAccess.Revoked; await scope.Runtime.RecheckPurchaseAccess();
        scope.Runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
        var pending = scope.Runtime.Quit();
        for (var i = 0; i < 100 && scope.Runtime.ActiveAccount!.OwnedWindows.Count == 0; i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        var dialog = Assert.Single(scope.Runtime.ActiveAccount!.OwnedWindows);
        Assert.False(scope.Home.IsVisible); Assert.Empty(scope.Home.OwnedWindows);
        dialog.Close(); await pending; AssertLocked(scope);
    }

    private static void AssertLocked(Scope scope)
    {
        Assert.False(scope.Runtime.AccessAllowed); Assert.True(scope.Runtime.Clock.Stopped);
        Assert.False(scope.Home.IsVisible); Assert.True(scope.Runtime.ActiveAccount!.IsVisible);
        Assert.True(scope.Runtime.ActivePet is null || !scope.Runtime.ActivePet.IsVisible);
        Assert.Equal("로그인·구매 확인 필요", scope.Runtime.TrayStatus.Status);
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public FakeAccountService Service { get; } = new();
        public AppRuntime Runtime { get; }
        public SettingsWindow Home => (SettingsWindow)lifetime.MainWindow!;
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime) { AccountServiceFactory = () => Service };
        }
        public async Task Unlock()
        {
            Service.Purchase = PurchaseAccess.Active;
            Runtime.ActiveAccount!.FindControl<Button>("AccountPrimary")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 200 && !Home.IsVisible; i++)
            { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
            Assert.True(Home.IsVisible);
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
