using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class BreakReminderTests
{
    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void BubbleButtonsAllowEarlyCompletionWithoutOpeningAnotherWindow()
    {
        var now = TimeSpan.Zero; var model = new PetReminder();
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); model.Invite(session);
        var started = 0; var finished = 0; model.Started += _ => started++; model.Finished += _ => finished++;
        var bubble = new PetSpeechBubble(() => model.Start(now), () => model.Snooze(), () => model.Complete(now));
        var window = new Window { Width = 320, Height = 268, Content = bubble };
        try
        {
            bubble.Refresh(model, 7); window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("7분 뒤에", Button(window, "PetBreakSnooze").Content);
            Assert.False(Button(window, "PetBreakComplete").IsVisible);
            Click(Button(window, "PetBreakStart")); bubble.Refresh(model, 7);
            Assert.Equal(1, started); Assert.True(Button(window, "PetBreakComplete").IsVisible);
            now = TimeSpan.FromSeconds(3); model.Tick(now); bubble.Refresh(model, 7);
            Click(Button(window, "PetBreakComplete")); bubble.Refresh(model, 7);
            Assert.Equal(1, finished); Assert.Equal(BreakSessionState.Completed, session.State);
            Assert.Equal(PetNotice.Completed, model.Notice); Assert.Equal(3, model.CompletedSeconds);
            Assert.Empty(window.OwnedWindows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task PetFoldingAndAllDirectionsKeepSessionAndButtonsInsideOneWindow()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        try
        {
            var session = new BreakSession(BreakRoutines.All[0], "default-cat"); runtime.Reminder.Invite(session);
            pet.Show();
            foreach (var direction in Enum.GetValues<BubbleDirection>())
            {
                await runtime.UpdateSettings(runtime.Settings with { BubbleDirection = direction });
                pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
                Assert.Equal(PetBubbleLayout.Create(direction, true).Size, pet.ClientSize);
                foreach (var button in pet.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible))
                {
                    var point = button.TranslatePoint(default, pet)!.Value;
                    Assert.True(point.X >= 0 && point.X + button.Bounds.Width <= pet.ClientSize.Width);
                    Assert.True(point.Y >= 0 && point.Y + button.Bounds.Height <= pet.ClientSize.Height);
                }
            }
            var fold = pet.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Name == "PetToggleSpeech");
            fold.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Dispatcher.UIThread.RunJobs(); pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
            Assert.True(runtime.Settings.BubbleCollapsed); Assert.Equal(new Size(192, 192), pet.ClientSize);
            runtime.Reminder.Start(TimeSpan.Zero); runtime.Reminder.Tick(TimeSpan.FromSeconds(7));
            fold.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Dispatcher.UIThread.RunJobs(); pet.RefreshSpeech();
            Assert.False(runtime.Settings.BubbleCollapsed); Assert.Same(session, runtime.Reminder.Session);
            Assert.Equal(TimeSpan.FromSeconds(7), session.Elapsed); Assert.Empty(pet.OwnedWindows);
            Assert.False(AppSettings.Load(Path.Combine(temp.Path, "settings.json")).BubbleCollapsed);
        }
        finally { pet.ClosePet(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaFact]
    public void OvertimeTimerAndCompletionButtonFitWithLongInstructions()
    {
        var model = new PetReminder(); var session = new BreakSession(new("long", "긴 안내", [new(new string('쉼', 180), 1)]), "default-cat");
        model.Invite(session); model.Start(TimeSpan.Zero); model.Tick(TimeSpan.FromSeconds(2));
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => model.Complete(TimeSpan.FromSeconds(2)));
        bubble.Refresh(model, 60);
        var window = new Window { Width = 320, Height = 268, Content = bubble };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var image = window.CaptureRenderedFrame(); Assert.NotNull(image);
            var timer = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTimer");
            var complete = Button(window, "PetBreakComplete"); Assert.Equal("+00:01", timer.Text);
            var timerOrigin = timer.TranslatePoint(default, window)!.Value; var buttonOrigin = complete.TranslatePoint(default, window)!.Value;
            Assert.True(buttonOrigin.Y >= timerOrigin.Y + timer.Bounds.Height);
            Assert.True(buttonOrigin.Y + complete.Bounds.Height <= window.ClientSize.Height);
        }
        finally { window.Close(); }
    }
}
