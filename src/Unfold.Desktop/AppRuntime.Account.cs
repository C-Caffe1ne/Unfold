using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class AppRuntime
{
    private bool purchaseGateEnabled, accessGranted, accessCheckPending;
    private int accessGeneration;
    private DateTimeOffset nextAccessCheck;
    private bool accountStartupPending;
    private AccountScreenModel? restoringAccount;
    internal bool AccessAllowed => !purchaseGateEnabled || accessGranted;

    private AccountScreenModel CreateAccountModel()
    {
        var model = new AccountScreenModel(AccountContent, CreateAccountService(), accountSession);
        model.SessionChanged += AccountSessionChanged;
        return model;
    }

    private void AccountSessionChanged(AccountSession? value)
    {
        if (value is null || value.UserId != accountSession?.UserId) LockPurchaseAccess();
        accountSession = value; Changed?.Invoke();
    }

    private async Task RestoreAccountAtStartup()
    {
        var model = CreateAccountModel();
        restoringAccount = model;
        try
        {
            // Do not create a login window until saved-session and access checks finish.
            await model.RestoreAsync();
            if (disposed || quitting || restoringAccount != model) return;
            accountStartupPending = false;
            if (model.PurchaseReady && accountSession is not null)
                await GrantPurchaseAccess(!backgroundStart);
            else
            {
                // Keep the restored identity, error and retry action in the fallback screen.
                ShowAccount(model);
                restoringAccount = null; // The window now owns the model.
            }
        }
        finally
        {
            if (restoringAccount == model) CancelAccountRestore();
        }
    }

    private void CancelAccountRestore()
    {
        accountStartupPending = false;
        if (restoringAccount is not { } model) return;
        restoringAccount = null;
        model.SessionChanged -= AccountSessionChanged;
        model.Dispose();
    }

    private async Task GrantPurchaseAccess(bool showSettings = true)
    {
        if (disposed || quitting || AccountSignOutPending || accountSession is not { } session) return;
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            LockPurchaseAccess(); ShowAccount(); return;
        }
        var generation = ++accessGeneration;
        accessGranted = true;
        nextAccessCheck = DateTimeOffset.UtcNow.AddMinutes(5);
        Clock.Start(monotonic.Elapsed); timer.Start(); RefreshTray();
        try
        {
            await UpdatePet();
            if (!disposed && AccessAllowed && generation == accessGeneration && showSettings) ShowSettings();
        }
        catch (Exception error)
        {
            AppPaths.Log(error);
            if (!disposed && AccessAllowed && generation == accessGeneration)
            {
                ShowSettings();
                await Ui.Error(settingsWindow!, error);
            }
        }
    }

    private void LockPurchaseAccess()
    {
        if (!purchaseGateEnabled) return;
        accessGeneration++; accessGranted = false;
        timer.Stop(); noticeExpiryTimer.Stop(); scheduledNoticeExpiry = null;
        ClearReminderPreview(); CancelReminder(); Clock.Stop(monotonic.Elapsed);
        soundPlayer.Stop(); pet?.HidePet(); settingsWindow?.HideToTray();
        RefreshTray(); Changed?.Invoke();
    }

    internal async Task RecheckPurchaseAccess()
    {
        if (!purchaseGateEnabled || !AccessAllowed || disposed || quitting || accessCheckPending) return;
        accessCheckPending = true;
        var generation = accessGeneration;
        var session = accountSession;
        try
        {
            if (session is null) throw new AccountException(AccountFailure.AuthenticationRequired);
            using var service = CreateAccountService();
            if (session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
            {
                var refreshed = await service.RefreshSessionAsync(session, accountLifetime.Token);
                if (refreshed.UserId != session.UserId || refreshed.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new AccountException(AccountFailure.InvalidResponse);
                session = refreshed;
                if (disposed || quitting || generation != accessGeneration) return;
                accountSession = session;
            }
            var access = await service.CheckPurchaseAsync(session, accountLifetime.Token);
            if (disposed || quitting || generation != accessGeneration) return;
            accountSession = session;
            if (access != PurchaseAccess.Active) { LockPurchaseAccess(); ShowAccount(); }
            else { nextAccessCheck = DateTimeOffset.UtcNow.AddMinutes(5); Changed?.Invoke(); }
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error)
        {
            if (disposed || quitting || generation != accessGeneration) return;
            if (error is AccountException { Failure: AccountFailure.AuthenticationRequired })
            {
                using var service = CreateAccountService();
                try { await service.ClearSavedSessionAsync(); }
                catch (AccountException storageError) { AppPaths.Log(storageError); }
                if (disposed || quitting || generation != accessGeneration) return;
                accountSession = null;
            }
            AppPaths.Log(error); LockPurchaseAccess(); ShowAccount();
        }
        finally { accessCheckPending = false; }
    }
}
