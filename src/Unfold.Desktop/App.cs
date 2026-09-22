using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Unfold.Desktop;

public sealed class App : Application
{
    public AppRuntime? Runtime { get; private set; }
    public override void Initialize() { DesignSystem.Install(this); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Runtime = new AppRuntime(desktop);
            desktop.Exit += (_, _) => Runtime.Dispose();
            var ready = Runtime;
            Dispatcher.UIThread.Post(async () =>
            {
                if (desktop.Args is ["--review-original-pets"]) await OriginalCompanionDiagnostics.Run(ready, desktop);
                else if (desktop.Args is ["--review-pet-pack", var packPath]) await PetPackDiagnostics.Run(ready, desktop, packPath);
                else if (desktop.Args?.Contains("--smoke-test") == true) await SmokeDiagnostics.Run(ready, desktop);
                else await ready.Start(desktop.Args?.Contains("--background") == true);
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
