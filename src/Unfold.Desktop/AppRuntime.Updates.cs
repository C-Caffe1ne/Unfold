using Avalonia.Controls;
using Avalonia.Threading;

namespace Unfold.Desktop;

public sealed partial class AppRuntime
{
    private UpdateWindow? updateWindow;
    private NativeMenuItem? trayUpdate;
    private bool updateRestartPending;
    private bool startupUpdatesReady, startupUpdateShown;
    private AppUpdates? updates;
    internal AppUpdates Updates => updates ??= new();
    internal UpdateWindow? ActiveUpdate => updateWindow;
    internal void SetUpdateBackend(IAppUpdateBackend backend)
    {
        if (updates is not null) throw new InvalidOperationException("Updates already initialized.");
        updates = new(backend);
    }
    private void InitializeUpdates(NativeMenu menu)
    {
        trayUpdate = new NativeMenuItem("업데이트 확인");
        trayUpdate.Click += (_, _) => ShowUpdates(); menu.Items.Add(trayUpdate);
        Updates.Changed += RefreshUpdateMenu;
        RefreshUpdateMenu();
        if (!DiagnosticMode && Updates.State != AppUpdateState.UnsupportedInstall) _ = Updates.Check();
    }
    private void RefreshUpdateMenu()
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(RefreshUpdateMenu); return; }
        if (disposed || trayUpdate is null) return;
        trayUpdate.Header = Updates.Release is not null ? "업데이트 확인 · 새 버전 있음" : "업데이트 확인";
        TryShowStartupUpdate();
    }
    private void TryShowStartupUpdate()
    {
        if (!startupUpdatesReady || startupUpdateShown || DiagnosticMode || backgroundStart || disposed || quitting ||
            Updates.State is not (AppUpdateState.Available or AppUpdateState.Ready)) return;
        Window? owner = accountWindow is { IsVisible: true } ? accountWindow : settingsWindow;
        if (owner is not { IsVisible: true, IsEnabled: true }) return;
        if (owner.OwnedWindows.Any(window => window.IsDialog)) return;
        startupUpdateShown = true;
        ShowUpdates(modal: true);
    }
    internal void ShowUpdates() => ShowUpdates(false);
    private void ShowUpdates(bool modal)
    {
        if (disposed || quitting) return;
        if (updateWindow is not null) { updateWindow.Activate(); return; }
        updateWindow = new(Updates, RestartForUpdate);
        var shown = updateWindow;
        shown.Closed += (_, _) => { if (updateWindow == shown) updateWindow = null; };
        if (DiagnosticMode) PrepareDiagnosticWindow(shown);
        var owner = accountWindow is { IsVisible: true } ? accountWindow : (Window?)settingsWindow ?? desktop.MainWindow;
        if (owner is { IsVisible: true })
        {
            if (modal) _ = shown.ShowDialog(owner); else shown.Show(owner);
        }
        else shown.Show();
        if (Updates.State is AppUpdateState.Idle or AppUpdateState.Current or AppUpdateState.Error && !Updates.Downloaded)
            _ = Updates.Check();
    }
    internal async Task<bool> RestartForUpdate()
    {
        if (disposed || quitting || quitPending || stopPending || updateRestartPending || !Updates.Downloaded ||
            Reminder.Session is not null) return false;
        updateRestartPending = true;
        try
        {
            if (settingsWindow is not null && !await settingsWindow.CanCloseDraft(updateWindow)) return false;
            if (disposed || quitting || Reminder.Session is not null || !Updates.PrepareApply()) return false;
            quitting = true; Dispose(); desktop.Shutdown(); return true;
        }
        catch (Exception error) { Updates.ReportApplyError(error); return false; }
        finally { updateRestartPending = false; }
    }
    private void DisposeUpdates()
    {
        if (updates is not null) { updates.Changed -= RefreshUpdateMenu; updates.Dispose(); }
        updateWindow?.Close(); updateWindow = null;
    }
}
