using System.ComponentModel;
using Unfold.Core;

namespace Unfold.Desktop;

internal interface IAccountScreenService : IDisposable
{
    bool CanSignIn { get; }
    bool CanCheckout(string market);
    Task<AccountSession> SignInAsync(CancellationToken token);
    Task<AccountSession> RefreshSessionAsync(AccountSession session, CancellationToken token);
    Task<AccountSession?> RestoreSessionAsync(CancellationToken token) => Task.FromResult<AccountSession?>(null);
    Task ClearSavedSessionAsync() => Task.CompletedTask;
    Task SignOutAsync(AccountSession session, CancellationToken token);
    Task<PurchaseAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token);
    Task<AccessCodeResult> RedeemCodeAsync(AccountSession session, string code, CancellationToken token);
    Task StartCheckoutAsync(AccountSession session, string market, Guid requestId, CancellationToken token);
}

/// <summary>Account presentation is independent of AppRuntime. No state here unlocks the app.</summary>
public sealed class AccountScreenModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAccountScreenService service;
    private AccountSession? session;
    private CancellationTokenSource? operation;
    private AccountMarket market;
    private readonly AccountMarket[] markets;
    private Guid checkoutRequestId = Guid.NewGuid();
    private AccountOperation operationKind;
    private bool checkoutStarted;
    private bool disposed;
    private bool restorePending;
    internal bool RestoredAutomatically { get; private set; }
    public AccountScreenContent Content { get; }
    public AccountScreenCopy Copy => Content.Copy;
    public AccountMarket[] Markets => markets;
    public AccountMarket SelectedMarket
    {
        get => market;
        set
        {
            if (value is null || !Markets.Contains(value) || checkoutStarted || IsBusy || value == market) return;
            market = value; checkoutRequestId = Guid.NewGuid(); Status = ""; Notify();
        }
    }
    public string Price => market.DisplayPrice;
    public bool IsBusy => operation is not null;
    public bool IsPurchase => session is not null;
    public bool IsLogin => !IsPurchase;
    public bool PurchaseUnknown { get; private set; }
    public bool PurchaseReady { get; private set; }
    public bool ShowCode => IsPurchase && !PurchaseReady;
    public bool CanRedeemCode => !disposed && ShowCode && !IsBusy;
    public bool CheckoutStarted => checkoutStarted;
    public bool CanChangeMarket => !IsBusy && !checkoutStarted && Markets.Length > 1;
    public string Email => session?.Email ?? Copy.SignedInLabel;
    public string Step => IsPurchase ? Copy.PurchaseStep : Copy.LoginStep;
    public string Heading => PurchaseReady ? Copy.ReadyTitle : IsPurchase ? Copy.PurchaseTitle : Copy.WelcomeTitle;
    public string Status { get; private set; } = "";
    public bool HasStatus => Status.Length != 0;
    public bool CanPrimary => !IsBusy && (IsLogin ? service.CanSignIn
        : !PurchaseReady && (PurchaseUnknown || checkoutStarted || service.CanCheckout(market.Id)));
    public string PrimaryText => IsBusy ? operationKind switch {
        AccountOperation.SignIn => Copy.SigningInButton,
        AccountOperation.CheckPurchase => Copy.CheckingButton,
        AccountOperation.Checkout => Copy.OpeningCheckoutButton,
        _ => Copy.CheckingButton,
    } : IsLogin ? restorePending ? Copy.RetryButton : Copy.GoogleButton
        : PurchaseUnknown ? Copy.RetryButton : checkoutStarted ? Copy.CheckPurchaseButton
        : PurchaseReady ? Copy.ReadyStatus : Copy.PurchaseButton;
    public bool ShowSecondary => IsBusy || IsPurchase;
    public string SecondaryText => IsBusy ? Copy.CancelButton : Copy.ChangeAccountButton;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal event Action<AccountSession?>? SessionChanged;

    internal AccountScreenModel(AccountScreenContent content, IAccountScreenService service, AccountSession? initialSession = null)
    {
        Content = content; this.service = service;
        session = initialSession; PurchaseUnknown = initialSession is not null;
        markets = content.Markets.Where(item => service.CanCheckout(item.Id)).ToArray();
        if (markets.Length == 0) markets = [content.Markets.Single(p => p.Id == content.DefaultMarket)];
        market = markets.FirstOrDefault(p => p.Id == content.DefaultMarket) ?? markets[0];
        if (!service.CanSignIn) Status = Copy.SignInUnavailable;
    }
    public async Task PrimaryAsync()
    {
        if (disposed || !CanPrimary) return;
        if (restorePending) { await RestoreAsync(); return; }
        RestoredAutomatically = false;
        using var pending = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        operation = pending;
        var kind = session is null ? AccountOperation.SignIn
            : PurchaseUnknown || checkoutStarted ? AccountOperation.CheckPurchase : AccountOperation.Checkout;
        operationKind = kind;
        Status = kind == AccountOperation.SignIn ? Copy.BrowserWaiting : ""; Notify();
        try
        {
            if (kind == AccountOperation.SignIn)
            {
                var signedIn = await service.SignInAsync(pending.Token);
                pending.Token.ThrowIfCancellationRequested();
                if (disposed) return;
                SetSession(signedIn); Status = ""; Notify();
                operationKind = AccountOperation.CheckPurchase; Notify();
                await CheckPurchase(pending.Token, false);
                return;
            }
            if (session!.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            {
                var refreshed = await service.RefreshSessionAsync(session, pending.Token);
                pending.Token.ThrowIfCancellationRequested();
                if (disposed) return;
                SetSession(refreshed);
            }
            if (kind == AccountOperation.CheckPurchase)
            {
                await CheckPurchase(pending.Token, checkoutStarted);
                return;
            }
            await service.StartCheckoutAsync(session!, market.Id, checkoutRequestId, pending.Token);
            pending.Token.ThrowIfCancellationRequested();
            if (disposed) return;
            checkoutStarted = true; PurchaseUnknown = false; Status = Copy.CheckoutWaiting;
        }
        catch (OperationCanceledException)
        {
            if (!disposed)
            {
                Status = session is null ? Copy.SignInCancelled : Copy.PurchaseUnavailable;
                PurchaseUnknown = session is not null && operationKind == AccountOperation.CheckPurchase;
            }
        }
        catch (AccountException error)
        {
            if (!disposed)
            {
                var storageFailed = false;
                if (error.Failure == AccountFailure.AuthenticationRequired)
                {
                    storageFailed = !await ForgetInvalidSessionAsync();
                    if (disposed) return;
                    SetSession(null); checkoutStarted = PurchaseUnknown = PurchaseReady = false;
                }
                else PurchaseUnknown = session is not null && operationKind == AccountOperation.CheckPurchase;
                Status = storageFailed || error.Failure == AccountFailure.SessionStorageUnavailable ? Copy.SessionStorageUnavailable : session is null ? Copy.SignInFailed
                    : operationKind == AccountOperation.Checkout ? Copy.CheckoutUnavailable : Copy.PurchaseUnavailable;
            }
        }
        finally { operation = null; operationKind = AccountOperation.None; if (!disposed) Notify(); }
    }
    internal async Task RestoreAsync()
    {
        if (disposed || IsBusy || session is not null) return;
        using var pending = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        operation = pending; operationKind = AccountOperation.Restore; Notify();
        try
        {
            var restored = await service.RestoreSessionAsync(pending.Token);
            pending.Token.ThrowIfCancellationRequested();
            if (disposed) return;
            restorePending = false;
            if (restored is null) { Status = service.CanSignIn ? "" : Copy.SignInUnavailable; return; }
            SetSession(restored);
            operationKind = AccountOperation.CheckPurchase; Notify();
            await CheckPurchase(pending.Token, false);
            RestoredAutomatically = PurchaseReady;
        }
        catch (OperationCanceledException) { if (!disposed) RestoreFailed(Copy.RestoreUnavailable); }
        catch (AccountException error)
        {
            if (disposed) return;
            if (error.Failure == AccountFailure.AuthenticationRequired)
            {
                var cleared = await ForgetInvalidSessionAsync();
                if (disposed) return;
                SetSession(null);
                restorePending = false; PurchaseUnknown = PurchaseReady = false;
                Status = cleared ? Copy.SignInFailed : Copy.SessionStorageUnavailable;
            }
            else RestoreFailed(error.Failure == AccountFailure.SessionStorageUnavailable ? Copy.SessionStorageUnavailable : Copy.RestoreUnavailable);
        }
        finally { operation = null; operationKind = AccountOperation.None; if (!disposed) Notify(); }
    }
    private void RestoreFailed(string message)
    {
        restorePending = session is null; PurchaseUnknown = session is not null; PurchaseReady = false; Status = message;
    }
    // Empty error text means the server grant was saved AND access was rechecked.
    public async Task<string?> RedeemCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (!CanRedeemCode) return Copy.CodeUnavailable;
        if (string.IsNullOrWhiteSpace(code)) return Copy.InvalidCodeMessage;
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pending.CancelAfter(TimeSpan.FromSeconds(40));
        operation = pending; operationKind = AccountOperation.RedeemCode; Notify();
        try
        {
            if (session!.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            {
                var refreshed = await service.RefreshSessionAsync(session, pending.Token);
                pending.Token.ThrowIfCancellationRequested();
                if (disposed) return Copy.CodeUnavailable;
                SetSession(refreshed);
            }
            var result = await service.RedeemCodeAsync(session!, code.Trim(), pending.Token);
            pending.Token.ThrowIfCancellationRequested();
            if (disposed) return Copy.CodeUnavailable;
            if (result == AccessCodeResult.InvalidCode) return Copy.InvalidCodeMessage;
            if (result == AccessCodeResult.RateLimited) return Copy.CodeRateLimited;
            await CheckPurchase(pending.Token, false);
            return PurchaseReady ? null : Copy.CodeUnavailable;
        }
        catch (OperationCanceledException) { return Copy.CodeUnavailable; }
        catch (AccountException error)
        {
            if (!disposed && error.Failure == AccountFailure.AuthenticationRequired)
            {
                var cleared = await ForgetInvalidSessionAsync();
                if (disposed) return Copy.CodeUnavailable;
                SetSession(null); PurchaseUnknown = PurchaseReady = checkoutStarted = false;
                Status = cleared ? Copy.SignInFailed : Copy.SessionStorageUnavailable;
            }
            return Copy.CodeUnavailable;
        }
        finally { operation = null; operationKind = AccountOperation.None; if (!disposed) Notify(); }
    }
    private async Task CheckPurchase(CancellationToken token, bool afterCheckout)
    {
        var access = await service.CheckPurchaseAsync(session!, token);
        token.ThrowIfCancellationRequested();
        if (disposed) return;
        PurchaseUnknown = false; PurchaseReady = access == PurchaseAccess.Active;
        Status = PurchaseReady ? Copy.ReadyStatus : afterCheckout ? Copy.PurchaseNotFound : "";
    }
    public void Secondary() => _ = SecondaryAsync();
    public async Task SecondaryAsync()
    {
        if (IsBusy) { operation?.Cancel(); return; }
        if (disposed) return;
        using var pending = new CancellationTokenSource();
        operation = pending; operationKind = AccountOperation.ChangeAccount; Notify();
        try { await service.ClearSavedSessionAsync(); }
        catch (AccountException) { if (!disposed) Status = Copy.SessionStorageUnavailable; return; }
        finally { operation = null; operationKind = AccountOperation.None; if (!disposed) Notify(); }
        if (disposed) return;
        SetSession(null); PurchaseUnknown = PurchaseReady = checkoutStarted = false; checkoutRequestId = Guid.NewGuid();
        restorePending = RestoredAutomatically = false;
        Status = service.CanSignIn ? "" : Copy.SignInUnavailable; Notify();
    }
    private async Task<bool> ForgetInvalidSessionAsync()
    {
        try { await service.ClearSavedSessionAsync(); return true; }
        catch (AccountException) { return false; }
    }
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private void SetSession(AccountSession? value) { session = value; SessionChanged?.Invoke(value); }
    internal void ReportSignOutFailure(bool storageFailed = false)
    { Status = storageFailed ? Copy.SessionStorageUnavailable : Copy.SignOutUnavailable; Notify(); }
    public void Dispose() { if (disposed) return; disposed = true; operation?.Cancel(); service.Dispose(); session = null; }

    private enum AccountOperation { None, SignIn, CheckPurchase, Checkout, RedeemCode, Restore, ChangeAccount }
}
