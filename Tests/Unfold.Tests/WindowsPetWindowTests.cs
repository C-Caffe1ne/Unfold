using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Unfold.Desktop;

namespace Unfold.Tests;

public class WindowsPetWindowTests
{
    private const uint Transparent = WindowsPetWindow.Transparent;
    private const uint Layered = WindowsPetWindow.Layered;
    private const uint OtherStyles = 0x00200088; // No-redirection bitmap, tool window and topmost.

    [Theory]
    [InlineData("HWND", 1, true)]
    [InlineData("HWND", 0, false)]
    [InlineData("NSWindow", 1, false)]
    [InlineData("Headless", 1, false)]
    public void InputRoutingRequiresARealWindowsHandle(string descriptor, int value, bool expected)
    {
        Assert.Equal(expected, WindowsPetWindow.HasNativeHandle(new PlatformHandle((nint)value, descriptor)));
        Assert.False(WindowsPetWindow.HasNativeHandle(null));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(OtherStyles)]
    [InlineData(OtherStyles | Transparent)] // The old implementation only set this flag.
    public void CrossProcessClickThroughAddsAndInitializesALayer(uint initial)
    {
        var api = new FakeApi { Style = initial };
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(initial | Layered | Transparent, api.Style);
        Assert.Equal(["read", "write", "layer", "refresh", "read"], api.Calls);
    }

    [Fact]
    public void ExistingLayersAreNotReinitializedAndOtherStylesArePreserved()
    {
        var api = new FakeApi { Style = OtherStyles | Layered };
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(OtherStyles | Layered | Transparent, api.Style);
        Assert.DoesNotContain("layer", api.Calls);
        Assert.True(WindowsPetWindow.SetClickThrough(1, false, api));
        Assert.Equal(OtherStyles | Layered, api.Style);
    }

    [Fact]
    public void StableStateOnlyReadsButAvaloniaStyleResetIsRepaired()
    {
        var api = new FakeApi { Style = OtherStyles };
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        api.Calls.Clear();
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(["read"], api.Calls);
        api.Style = OtherStyles; // Avalonia recreates its extended style bits.
        api.Calls.Clear();
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(OtherStyles | Layered | Transparent, api.Style);
        Assert.Contains("layer", api.Calls);
    }

    [Fact]
    public void InteractiveStateClearsUnexpectedNativeTransparency()
    {
        var api = new FakeApi { Style = OtherStyles | Layered | Transparent };
        Assert.True(WindowsPetWindow.SetClickThrough(1, false, api));
        Assert.Equal(OtherStyles | Layered, api.Style);
        api.Calls.Clear();
        Assert.True(WindowsPetWindow.SetClickThrough(1, false, api));
        Assert.Equal(["read"], api.Calls);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("layer")]
    [InlineData("refresh")]
    [InlineData("verify")]
    public void FailedEnableIsNotReportedAsSuccessAndCanRetry(string failure)
    {
        var api = new FakeApi { Style = OtherStyles, FailOnce = failure };
        Assert.False(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(OtherStyles, api.Style);
        Assert.True(WindowsPetWindow.SetClickThrough(1, true, api));
        Assert.Equal(OtherStyles | Layered | Transparent, api.Style);
    }

    [Fact]
    public void FailedDisableCanRetryWithoutLosingTheLayer()
    {
        var api = new FakeApi { Style = OtherStyles | Layered | Transparent, FailOnce = "refresh" };
        Assert.False(WindowsPetWindow.SetClickThrough(1, false, api));
        Assert.Equal(OtherStyles | Layered | Transparent, api.Style);
        Assert.True(WindowsPetWindow.SetClickThrough(1, false, api));
        Assert.Equal(OtherStyles | Layered, api.Style);
        Assert.DoesNotContain("layer", api.Calls);
    }

    [Fact]
    public void ZeroHandleDoesNotReachWin32()
    {
        var api = new FakeApi();
        Assert.False(WindowsPetWindow.SetClickThrough(0, true, api));
        Assert.Empty(api.Calls);
    }

    [AvaloniaFact]
    public void HeadlessAndOtherPlatformsDoNotReachWin32OrShowTheWindow()
    {
        var window = new Window();
        try
        {
            Assert.False(WindowsPetWindow.SetClickThrough(window, true));
            Assert.False(window.IsVisible);
            window.Show();
            Assert.False(WindowsPetWindow.SetClickThrough(window, true));
            window.Hide();
            Assert.False(WindowsPetWindow.SetClickThrough(window, false));
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    }

    private sealed class FakeApi : WindowsPetWindow.IInputApi
    {
        public uint Style { get; set; }
        public string? FailOnce { get; set; }
        public List<string> Calls { get; } = [];
        private bool wrote;
        private bool Succeed(string step)
        {
            Calls.Add(step);
            if (FailOnce != step) return true;
            FailOnce = null;
            return false;
        }
        public bool TryGetStyle(nint hwnd, out uint style)
        {
            style = Style;
            if (wrote && FailOnce == "verify")
            {
                FailOnce = null;
                style &= ~Transparent;
            }
            return Succeed("read");
        }
        public bool TrySetStyle(nint hwnd, uint style)
        {
            if (!Succeed("write")) return false;
            Style = style; wrote = true;
            return true;
        }
        public bool InitializeLayer(nint hwnd) => Succeed("layer");
        public bool RefreshStyle(nint hwnd) => Succeed("refresh");
    }
}
