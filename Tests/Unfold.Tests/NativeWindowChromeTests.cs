using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class NativeWindowChromeTests
{
    [AvaloniaTheory]
    [InlineData("account")]
    [InlineData("update")]
    [InlineData("review")]
    [InlineData("custom-pet")]
    public void RegularWindowsUseOsDrawnControlsAndAnOsDragTitleBar(string kind)
    {
        using var updates = new AppUpdates(new UpdateTestBackend());
        Window window = kind switch
        {
            "account" => new AccountWindow(new(AccountScreenContent.Load(), new FakeAccountService())),
            "update" => new UpdateWindow(updates, () => Task.FromResult(false)),
            "review" => new BreakReviewWindow(new BreakHistory().Review),
            _ => new CustomPetWindow(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"))
        };
        try
        {
            window.Show(); Layout();
            Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
            // AppKit keeps its traffic lights in an extended title bar; Windows keeps its OS title bar.
            Assert.Equal(OperatingSystem.IsMacOS(), window.ExtendClientAreaToDecorationsHint);
            Assert.Equal(-1, window.ExtendClientAreaTitleBarHeightHint);
            Assert.True(window.CanMinimize);
            Assert.DoesNotContain(WindowTransparencyLevel.Transparent, window.TransparencyLevelHint);
            var titleBar = window.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "NativeTitleBar");
            Assert.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(titleBar));
            var inset = window.IsExtendedIntoWindowDecorations ? window.WindowDecorationMargin : default;
            Assert.Equal(inset.Top, titleBar.Height);
            // The band under the OS buttons is the color the Windows title bar is painted with.
            Assert.Same(DesignSystem.Canvas, Assert.IsAssignableFrom<Panel>(window.Content).Background);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Plum, 0xF7EDF1u)]
    [InlineData(AppTheme.OatLatte, 0xE6EFF5u)]
    [InlineData(AppTheme.Sage, 0xE8F2F0u)]
    [InlineData(AppTheme.MidnightBlue, 0x332117u)]
    public void WindowsTitleBarIsPaintedWithTheThemeCanvasAndHidesOnlyItsTitleAndIcon(AppTheme theme, uint colorRef)
    {
        var previous = DesignSystem.CurrentTheme;
        try
        {
            DesignSystem.ApplyTheme(theme);
            // DWM expects COLORREF (0x00BBGGRR), not ARGB.
            Assert.Equal(colorRef, NativeWindowChrome.CaptionColor(DesignSystem.Canvas.Color));
        }
        finally { DesignSystem.ApplyTheme(previous); }
        // WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON; the system menu (WTNCA_NOSYSMENU) stays available.
        Assert.Equal(3u, NativeWindowChrome.HiddenTitleBarParts);
    }

    [Fact]
    public void MacAppHostDeclaresTheSdkThatAppKitLaysOutWindowButtonsFor()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Only macOS builds produce the Mach-O apphost.");
        // AppKit sizes the title bar and its buttons for the main executable's SDK. The stock .NET
        // apphost declares an older one, so macOS 26 would draw the previous 28 pt layout.
        var sdk = MachOSdk(Path.Combine(AppContext.BaseDirectory, "Unfold"));
        Assert.True(sdk >= new Version(26, 0), $"The apphost declares macOS SDK {sdk}.");
    }

    [AvaloniaFact]
    public async Task OsCloseOnThePurchaseGateOffersTheExistingQuitInsteadOfDoingNothing()
    {
        var quits = 0; var allowClose = false;
        var window = new AccountWindow(new(AccountScreenContent.Load(), new FakeAccountService()),
            () => { quits++; return Task.CompletedTask; }, requiresPurchase: true, allowClose: () => allowClose);
        try
        {
            window.Show(); Layout();
            Assert.True(OsClose(window));
            await Task.Yield(); Layout();
            Assert.True(window.IsVisible);
            Assert.Equal(1, quits);
            // A programmatic close of the gate stays a silent no-op, as before.
            window.Close(); await Task.Yield(); Layout();
            Assert.True(window.IsVisible);
            Assert.Equal(1, quits);
        }
        finally { allowClose = true; window.Close(); }
    }

    [AvaloniaFact]
    public async Task OsCloseOfTheWelcomeScreenClosesWithoutAskingToQuit()
    {
        var quits = 0;
        var window = new AccountWindow(new(AccountScreenContent.Load(), new FakeAccountService()),
            () => { quits++; return Task.CompletedTask; });
        window.Show(); Layout();
        Assert.False(OsClose(window));
        await Task.Yield(); Layout();
        Assert.False(window.IsVisible);
        Assert.Equal(0, quits);
    }

    private static Version MachOSdk(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        Assert.Equal(0xFEEDFACFu, reader.ReadUInt32()); // thin 64-bit Mach-O
        reader.BaseStream.Position = 16;
        var commands = reader.ReadUInt32();
        reader.BaseStream.Position = 32;
        for (var i = 0; i < commands; i++)
        {
            var start = reader.BaseStream.Position;
            var command = reader.ReadUInt32(); var size = reader.ReadUInt32();
            if (command == 0x32) // LC_BUILD_VERSION: platform, minos, sdk (xxxx.yy.zz)
            {
                reader.BaseStream.Position = start + 16;
                var sdk = reader.ReadUInt32();
                return new((int)(sdk >> 16), (int)(sdk >> 8 & 0xFF));
            }
            reader.BaseStream.Position = start + size;
        }
        throw new InvalidDataException("The apphost has no LC_BUILD_VERSION.");
    }

    /// <summary>The platform's close notification (title bar button, Cmd+W, Alt+F4), not <see cref="Window.Close()"/>.</summary>
    private static bool OsClose(Window window) => (bool)typeof(Window)
        .GetMethod("HandleClosing", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(window, [WindowCloseReason.WindowClosing])!;

    private static void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
