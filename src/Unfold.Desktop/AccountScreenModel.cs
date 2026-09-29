using System.ComponentModel;
using Unfold.Core;

namespace Unfold.Desktop;

internal interface IAccountScreenService : IDisposable
{
    bool CanSignIn { get; }
    bool CanCheckout(string market);
    Task<AccountSession> SignInAsync(CancellationToken token);
    Task<AccountAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token);
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
    public AccountIdentity? VerifiedAccount { get; private set; }
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
    } : IsLogin ? Copy.GoogleButton
        : PurchaseUnknown ? Copy.RetryButton : checkoutStarted ? Copy.CheckPurchaseButton
        : PurchaseReady ? Copy.ReadyStatus : Copy.PurchaseButton;
    public bool ShowSecondary => IsBusy || IsPurchase;
    public string SecondaryText => IsBusy ? Copy.CancelButton : Copy.ChangeAccountButton;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal AccountScreenModel(AccountScreenContent content, IAccountScreenService service)
    {
        Content = content; this.service = service;
        markets = content.Markets.Where(item => service.CanCheckout(item.Id)).ToArray();
        if (markets.Length == 0) markets = [content.Markets.Single(p => p.Id == content.DefaultMarket)];
        market = markets.FirstOrDefault(p => p.Id == content.DefaultMarket) ?? markets[0];
        if (!service.CanSignIn) Status = Copy.SignInUnavailable;
    }
    public async Task PrimaryAsync()
    {
        if (disposed || !CanPrimary) return;
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
                session = signedIn; Status = ""; Notify();
                operationKind = AccountOperation.CheckPurchase; Notify();
                await CheckPurchase(pending.Token, false);
                return;
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
                if (error.Failure == AccountFailure.AuthenticationRequired)
                {
                    session = null; checkoutStarted = PurchaseUnknown = PurchaseReady = false;
                }
                else PurchaseUnknown = session is not null && operationKind == AccountOperation.CheckPurchase;
                Status = session is null ? Copy.SignInFailed
                    : operationKind == AccountOperation.Checkout ? Copy.CheckoutUnavailable : Copy.PurchaseUnavailable;
            }
        }
        finally { operation = null; operationKind = AccountOperation.None; if (!disposed) Notify(); }
    }
    private async Task CheckPurchase(CancellationToken token, bool afterCheckout)
    {
        var access = await service.CheckPurchaseAsync(session!, token);
        token.ThrowIfCancellationRequested();
        if (disposed) return;
        PurchaseUnknown = false; PurchaseReady = access.Purchase == PurchaseAccess.Active;
        VerifiedAccount = PurchaseReady ? new(session!.UserId, session.Email, access.Role) : null;
        Status = PurchaseReady ? Copy.ReadyStatus : afterCheckout ? Copy.PurchaseNotFound : "";
    }
    public void Secondary()
    {
        if (IsBusy) { operation?.Cancel(); return; }
        session = null; VerifiedAccount = null; PurchaseUnknown = PurchaseReady = checkoutStarted = false; checkoutRequestId = Guid.NewGuid();
        Status = service.CanSignIn ? "" : Copy.SignInUnavailable; Notify();
    }
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    public void Dispose() { if (disposed) return; disposed = true; operation?.Cancel(); service.Dispose(); session = null; }

    private enum AccountOperation { None, SignIn, CheckPurchase, Checkout }
}
