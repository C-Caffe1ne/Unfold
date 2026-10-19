using System.Reflection;
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
public class OsShutdownTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OsShutdownClosesVisibleOrHiddenSettingsWithAnUnsavedDraft(bool hidden)
    {
        using var scope = new Scope();
        await scope.Runtime.Start(false, true);
        var settings = Assert.IsType<SettingsWindow>(scope.Lifetime.MainWindow);
        settings.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsNavPacks")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        settings.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "CustomPetName").Text = "저장하지 않은 펫";
        if (hidden) settings.HideToTray();
        var closed = false;
        settings.Closed += (_, _) => closed = true;

        var request = await scope.RequestShutdown(true);

        Assert.False(request.Cancel);
        Assert.Equal(0, scope.Confirmations);
        Assert.True(closed);
        Assert.Null(scope.Runtime.ActivePet);
        Assert.Empty(scope.Lifetime.Windows);
        Assert.Equal(1, scope.Exits);
    }

    [AvaloniaFact]
    public async Task OsShutdownClosesThePurchaseGateAndDisposesItsServiceWithoutSigningOut()
    {
        using var scope = new Scope();
        await scope.Runtime.Start(false);
        var account = Assert.IsType<AccountWindow>(scope.Runtime.ActiveAccount);
        Assert.True(account.IsVisible);
        Assert.False(scope.Runtime.AccessAllowed);

        var request = await scope.RequestShutdown(true);

        Assert.False(request.Cancel);
        Assert.Equal(0, scope.Confirmations);
        Assert.True(scope.Service.Disposed);
        Assert.Equal(0, scope.Service.SignOutCalls);
        Assert.Equal(0, scope.Service.ClearCalls);
        Assert.Null(scope.Runtime.ActiveAccount);
        Assert.Empty(scope.Lifetime.Windows);
        Assert.Equal(1, scope.Exits);
    }

    [AvaloniaFact]
    public async Task OsShutdownDoesNotWaitForAnAlreadyPendingManualQuit()
    {
        using var scope = new Scope();
        await scope.Runtime.Start(false, true);
        var confirmation = new TaskCompletionSource<int>();
        scope.Runtime.ConfirmActionOverride = (_, _, _) => { scope.Confirmations++; return confirmation.Task; };
        var quit = scope.Runtime.Quit();
        Assert.False(quit.IsCompleted);

        var request = await scope.RequestShutdown(true);

        Assert.False(request.Cancel);
        Assert.Equal(1, scope.Confirmations);
        Assert.Empty(scope.Lifetime.Windows);
        Assert.Equal(1, scope.Exits);
        confirmation.SetResult(0);
        await quit;
        Assert.Equal(1, scope.Exits);
    }

    [AvaloniaFact]
    public async Task NonWindowsShutdownRequestStillAsksForConfirmationAndCanBeCancelled()
    {
        using var scope = new Scope();
        await scope.Runtime.Start(false, true);
        var settings = Assert.IsType<SettingsWindow>(scope.Lifetime.MainWindow);

        var request = await scope.RequestShutdown(false);

        Assert.True(request.Cancel);
        Assert.Equal(1, scope.Confirmations);
        Assert.True(settings.IsVisible);
        Assert.Contains(settings, scope.Lifetime.Windows);
        Assert.Equal(0, scope.Exits);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly TempDirectory temp = new();
        public ClassicDesktopStyleApplicationLifetime Lifetime { get; } = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        public FakeAccountService Service { get; } = new();
        public AppRuntime Runtime { get; }
        public int Confirmations, Exits;
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            typeof(ClassicDesktopStyleApplicationLifetime).GetMethod("BeforeInit", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Lifetime, null);
            Runtime = new(Lifetime) { AccountServiceFactory = () => Service };
            Runtime.ConfirmActionOverride = (_, _, _) => { Confirmations++; return Task.FromResult(1); };
            Lifetime.Exit += (_, _) => Exits++;
        }
        public async Task<ShutdownRequestedEventArgs> RequestShutdown(bool windowsSessionEnding)
        {
            var request = new ShutdownRequestedEventArgs();
            var handling = Runtime.HandleShutdownRequest(request, windowsSessionEnding);
            Assert.True(handling.IsCompletedSuccessfully);
            await handling;
            if (request.Cancel) return request;
            // Avalonia keeps the platform flag internal. Reproduce Win32's request, then
            // exercise the real lifetime/window-closing pipeline without ending the host OS.
            typeof(ShutdownRequestedEventArgs).GetProperty("IsOSShutdown", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(request, windowsSessionEnding);
            typeof(ClassicDesktopStyleApplicationLifetime).GetMethod("OnShutdownRequested", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Lifetime, [null, request]);
            return request;
        }
        public void Dispose()
        {
            Runtime.Dispose();
            if (Lifetime.Windows.Count > 0) Lifetime.Shutdown();
            Lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous);
            temp.Dispose();
        }
    }
}
