using System.ComponentModel;
using Unfold.Core;

namespace Unfold.Desktop;

internal interface IAccountScreenService : IDisposable
{
    bool CanSignIn { get; }
    Task<AccountSession> SignInAsync(CancellationToken token);
    Task<PurchaseAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token);
}

/// <summary>Account presentation is independent of AppRuntime. No state here unlocks the app.</summary>
public sealed class AccountScreenModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAccountScreenService service;
    private AccountSession? session;
    private CancellationTokenSource? operation;
    private AccountMarket market;
    private bool disposed;
    public AccountScreenContent Content { get; }
    public AccountScreenCopy Copy => Content.Copy;
    public AccountMarket[] Markets => Content.Markets;
    public AccountMarket SelectedMarket
    {
        get => market;
        set { if (value is null || !Markets.Contains(value)) return; market = value; Notify(); }
    }
    public string Price => market.DisplayPrice;
    public string FooterPrice => Price + " · " + Copy.PurchaseTerm;
    public bool IsBusy => operation is not null;
    public bool IsPurchase => session is not null;
    public bool IsLogin => !IsPurchase;
    public bool PurchaseUnknown { get; private set; }
    public bool PurchaseReady { get; private set; }
    public string Email => session?.Email ?? Copy.SignedInLabel;
    public string Step => IsPurchase ? Copy.PurchaseStep : Copy.LoginStep;
    public string Heading => PurchaseReady ? Copy.ReadyTitle : IsPurchase ? Copy.PurchaseTitle : Copy.WelcomeTitle;
    public string Status { get; private set; } = "";
    public bool HasStatus => Status.Length != 0;
    public bool CanPrimary => !IsBusy && (IsLogin ? service.CanSignIn : PurchaseUnknown);
    public string PrimaryText => IsBusy ? (IsPurchase ? Copy.CheckingButton : Copy.SigningInButton)
        : IsLogin ? Copy.GoogleButton : PurchaseUnknown ? Copy.RetryButton : PurchaseReady ? Copy.ReadyStatus : Copy.PurchaseButton;
    public bool ShowSecondary => IsBusy || IsPurchase;
    public string SecondaryText => IsBusy ? Copy.CancelButton : Copy.ChangeAccountButton;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal AccountScreenModel(AccountScreenContent content, IAccountScreenService service)
    {
        Content = content; this.service = service;
        market = content.Markets.Single(p => p.Id == content.DefaultMarket);
        if (!service.CanSignIn) Status = Copy.SignInUnavailable;
    }
    public async Task PrimaryAsync()
    {
        if (disposed || !CanPrimary) return;
        using var pending = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        operation = pending; Status = IsLogin ? Copy.BrowserWaiting : ""; Notify();
        try
        {
            if (session is null)
            {
                var signedIn = await service.SignInAsync(pending.Token);
                pending.Token.ThrowIfCancellationRequested();
                if (disposed) return;
                session = signedIn; Status = ""; Notify();
            }
            var access = await service.CheckPurchaseAsync(session, pending.Token);
            pending.Token.ThrowIfCancellationRequested();
            if (disposed) return;
            PurchaseUnknown = false; PurchaseReady = access == PurchaseAccess.Active;
            Status = PurchaseReady ? Copy.ReadyStatus : Copy.CheckoutUnavailable;
        }
        catch (OperationCanceledException) { if (!disposed) Status = session is null ? Copy.SignInCancelled : Copy.PurchaseUnavailable; PurchaseUnknown = session is not null; }
        catch (AccountException error)
        {
            if (!disposed)
            {
                if (error.Failure == AccountFailure.AuthenticationRequired) session = null;
                PurchaseUnknown = session is not null;
                Status = session is null ? Copy.SignInFailed : Copy.PurchaseUnavailable;
            }
        }
        finally { operation = null; if (!disposed) Notify(); }
    }
    public void Secondary()
    {
        if (IsBusy) { operation?.Cancel(); return; }
        session = null; PurchaseUnknown = PurchaseReady = false;
        Status = service.CanSignIn ? "" : Copy.SignInUnavailable; Notify();
    }
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    public void Dispose() { if (disposed) return; disposed = true; operation?.Cancel(); service.Dispose(); session = null; }
}
