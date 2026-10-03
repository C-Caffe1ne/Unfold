using Avalonia.Controls;
using Avalonia.Threading;

namespace Unfold.Desktop;

public sealed partial class AppRuntime
{
    private UpdateWindow? updateWindow;
    private NativeMenuItem? trayUpdate;
    private bool updateRestartPending;
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
    }
    internal void ShowUpdates()
    {
        if (disposed || quitting) return;
        if (updateWindow is not null) { updateWindow.Activate(); return; }
        updateWindow = new(Updates, RestartForUpdate);
        var shown = updateWindow;
        shown.Closed += (_, _) => { if (updateWindow == shown) updateWindow = null; };
        if (DiagnosticMode) PrepareDiagnosticWindow(shown);
        var owner = accountWindow is { IsVisible: true } ? accountWindow : (Window?)settingsWindow ?? desktop.MainWindow;
        if (owner is { IsVisible: true }) shown.Show(owner); else shown.Show();
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
            if (editor is not null && !await editor.CanCloseDocument(updateWindow)) return false;
            if (disposed || quitting || Reminder.Session is not null || !Updates.PrepareApply()) return false;
            quitting = true; editor?.CloseAfterApproval(); Dispose(); desktop.Shutdown(); return true;
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
