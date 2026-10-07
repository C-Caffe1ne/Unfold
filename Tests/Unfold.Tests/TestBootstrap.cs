using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Unfold.Desktop;

[assembly: AvaloniaTestApplication(typeof(Unfold.Tests.TestBootstrap))]

namespace Unfold.Tests;

public static class TestBootstrap
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseSkia().UseHeadless(new() { UseHeadlessDrawing = false });
}
public sealed class TestApplication : Application
{
    public override void Initialize() { DesignSystem.Install(this); }
}
