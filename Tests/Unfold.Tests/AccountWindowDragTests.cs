using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class AccountWindowDragTests
{
    [AvaloniaFact]
    public void HeaderDragIsLeftToTheOsWindowMoveFromTheTopEdge()
    {
        var window = new AccountWindow(new(AccountScreenContent.Load(), new FakeAccountService()));
        try
        {
            window.Show(); Layout();
            var region = window.FindControl<Border>("AccountDragRegion")!;
            // In the extended macOS title bar this role starts AppKit's own move and title bar double-click.
            Assert.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(region));
            var point = region.TranslatePoint(new Point(region.Bounds.Width / 2, 5), window)!.Value;
            Assert.Same(region, window.InputHitTest(point));
            window.Position = new PixelPoint(100, 200);
            var initial = window.Position;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(120, 60), RawInputModifiers.LeftMouseButton); Layout();
            window.MouseUp(point + new Vector(120, 60), MouseButton.Left); Layout();
            // The app no longer moves the window with its own pointer arithmetic.
            Assert.Equal(initial, window.Position);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EmptyHeaderReceivesPointerPressesAndDoesNotInterceptAccountButtons()
    {
        var service = new FakeAccountService();
        var quitRequests = 0;
        var window = new AccountWindow(new(AccountScreenContent.Load(), service), () =>
        {
            quitRequests++;
            return Task.CompletedTask;
        });
        try
        {
            window.Show(); Layout();
            var header = window.FindControl<Grid>("AccountDragHandle")!;
            var region = window.FindControl<Border>("AccountDragRegion")!;
            var headerPresses = 0;
            region.AddHandler(InputElement.PointerPressedEvent, (_, _) => headerPresses++, RoutingStrategies.Tunnel, true);
            foreach (var width in new[] { 940, 640 })
            {
                window.Width = width;
                window.Height = width == 640 ? 560 : 620;
                Layout();
                var headerPoint = Center(header);
                Assert.Same(region, window.InputHitTest(headerPoint));
                Click(headerPoint);
                Assert.Equal(0, service.SignInCalls);
            }
            Assert.Equal(2, headerPresses);

            Click(Center(window.FindControl<Button>("AccountPrimary")!));
            Assert.Equal(1, service.SignInCalls);
            Assert.True(window.Model.IsPurchase);
            Assert.Equal(2, headerPresses);

            Click(Center(header));
            Assert.Equal(3, headerPresses);
            Click(Center(window.FindControl<Button>("AccountQuit")!));
            Assert.Equal(1, quitRequests);
            Assert.Equal(3, headerPresses);
        }
        finally { window.Close(); }

        Point Center(Control control) => control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        void Click(Point point)
        {
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Layout();
        }
    }

    private static void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
