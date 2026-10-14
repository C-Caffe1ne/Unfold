using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Unfold.Core;

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
            // Diagnostics compare against the original Korean copy, whatever the host language is.
            var diagnostic = desktop.Args is ["--review-glb", ..] or ["--review-original-pets", ..] or ["--review-pet-pack", ..] ||
                desktop.Args?.Contains("--smoke-test") == true;
            Runtime = new AppRuntime(desktop, diagnostic ? null : L.Preferred(PlatformServices.PreferredLanguages()));
            desktop.Exit += (_, _) => Runtime.Dispose();
            var ready = Runtime;
            Dispatcher.UIThread.Post(async () =>
            {
                if (desktop.Args is ["--review-glb", var glbPath]) await GlbDiagnostics.Run(ready, desktop, glbPath);
                else if (desktop.Args is ["--review-original-pets"]) await OriginalCompanionDiagnostics.Run(ready, desktop);
                else if (desktop.Args is ["--review-pet-pack", var packPath]) await PetPackDiagnostics.Run(ready, desktop, packPath);
                else if (desktop.Args?.Contains("--smoke-test") == true) await SmokeDiagnostics.Run(ready, desktop);
                else await ready.Start(desktop.Args?.Contains("--background") == true);
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
