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
        var session = new BreakSession(BreakRoutines.All[0], "default-cat", durationSeconds: 180); model.Invite(session);
        var started = 0; var finished = 0; model.Started += _ => started++; model.Finished += _ => finished++;
        var bubble = new PetSpeechBubble(() => model.Start(now), () => model.Snooze(), () => model.Complete(now));
        var window = new Window { Width = 320, Height = 268, Content = bubble };
        try
        {
            bubble.Refresh(model, 7); window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("7분 뒤에", Button(window, "PetBreakSnooze").Content);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("3분") == true);
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

    // UI-05: a folded bubble used to leave the tray saying "다음 휴식: 12:34 · 진행 중" while the
    // work timer was actually held for a break. Tooltip, status row, recovery action and pet
    // badge now come from one contract, so this pins the wording and the enablement together.
    [Fact]
    public void TrayStatusNamesWaitingAndRestingAndOffersRecoveryOnlyWhenFolded()
    {
        var idle = TrayReminderStatus.Create(PetNotice.None, true, "12:34", "진행 중");
        Assert.Equal(ReminderBadge.None, idle.Badge); Assert.False(idle.CanExpand);
        Assert.Contains("다음 휴식", idle.Status); Assert.Contains("진행 중", idle.ToolTip);

        var waiting = TrayReminderStatus.Create(PetNotice.Invitation, true, "12:34", "진행 중");
        Assert.Equal(ReminderBadge.Waiting, waiting.Badge); Assert.True(waiting.CanExpand);
        Assert.StartsWith("휴식 대기 중", waiting.Status); Assert.Contains("휴식 대기 중", waiting.ToolTip);
        Assert.DoesNotContain("다음 휴식", waiting.Status); Assert.DoesNotContain("진행 중", waiting.ToolTip);

        var resting = TrayReminderStatus.Create(PetNotice.Resting, true, "12:34", "진행 중");
        Assert.Equal(ReminderBadge.Resting, resting.Badge); Assert.True(resting.CanExpand);
        Assert.StartsWith("휴식 중", resting.Status); Assert.Contains("휴식 중", resting.ToolTip);

        // Expanded: the tray still names the break, but there is nothing to recover.
        var expanded = TrayReminderStatus.Create(PetNotice.Resting, false, "12:34", "진행 중");
        Assert.Equal(ReminderBadge.None, expanded.Badge); Assert.False(expanded.CanExpand);
        Assert.Equal(resting.Status, expanded.Status); Assert.Equal(resting.ToolTip, expanded.ToolTip);

        // The advance heads-up and the completion notice do not hold the timer, so they must not
        // claim a break is waiting.
        foreach (var notice in new[] { PetNotice.Advance, PetNotice.Completed })
        {
            var transient = TrayReminderStatus.Create(notice, true, "12:34", "진행 중");
            Assert.Equal(ReminderBadge.None, transient.Badge); Assert.False(transient.CanExpand);
            Assert.Equal(idle.Status, transient.Status);
        }
    }

    [AvaloniaFact]
    public async Task FoldedReminderKeepsTrayAndBadgeInStepThroughRecoveryAndCompletion()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        Border Badge() => pet.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PetReminderBadge");
        Avalonia.Controls.Shapes.Ellipse Mark() => pet.GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Ellipse>().Single(shape => shape.Name == "PetReminderBadgeMark");
        void Draw() { pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout(); }
        try
        {
            await runtime.UpdateSettings(runtime.Settings with { BubbleCollapsed = true });
            pet.Show(); Draw();
            Assert.False(Badge().IsVisible); Assert.False(runtime.CanExpandReminder);
            Assert.Contains("다음 휴식", runtime.TrayStatus.Status);

            var session = new BreakSession(BreakRoutines.All[0], "default-cat");
            Assert.True(runtime.Reminder.Invite(session));
            await runtime.ShowReminder();
            // No Tick has run: the tray and the badge must already describe the waiting break.
            Assert.Equal(ReminderBadge.Waiting, runtime.TrayStatus.Badge);
            Assert.StartsWith("휴식 대기 중", runtime.TrayStatus.Status);
            Assert.True(runtime.CanExpandReminder);
            Draw();
            Assert.True(Badge().IsVisible); Assert.Equal(ReminderBadge.Waiting, pet.BadgeState);
            Assert.Equal(new Size(192, 192), pet.ClientSize);
            var waitingFill = Mark().Fill; var waitingStroke = Mark().StrokeThickness;

            runtime.StartBreak();
            Assert.Equal(ReminderBadge.Resting, runtime.TrayStatus.Badge);
            Assert.StartsWith("휴식 중", runtime.TrayStatus.Status);
            Assert.Contains("휴식 중", runtime.TrayStatus.ToolTip);
            Draw();
            Assert.True(Badge().IsVisible); Assert.Equal(ReminderBadge.Resting, pet.BadgeState);
            // Waiting and resting differ by fill and outline, not only by colour.
            Assert.NotEqual(waitingFill, Mark().Fill); Assert.NotEqual(waitingStroke, Mark().StrokeThickness);
            Assert.Equal(new Size(192, 192), pet.ClientSize);

            runtime.Reminder.Tick(TimeSpan.FromSeconds(6)); var elapsed = session.Elapsed;
            Assert.True(elapsed > TimeSpan.Zero);

            await runtime.ExpandReminder();
            Assert.False(runtime.Settings.BubbleCollapsed);
            Assert.False(AppSettings.Load(Path.Combine(temp.Path, "settings.json")).BubbleCollapsed);
            Assert.Same(session, runtime.Reminder.Session); Assert.Equal(elapsed, session.Elapsed);
            Assert.Equal(ReminderBadge.None, runtime.TrayStatus.Badge); Assert.False(runtime.CanExpandReminder);
            Assert.StartsWith("휴식 중", runtime.TrayStatus.Status);
            Draw();
            Assert.False(Badge().IsVisible);
            Assert.Equal(PetBubbleLayout.Create(runtime.Settings.BubbleDirection, true).Size, pet.ClientSize);
            Assert.Empty(pet.OwnedWindows);

            runtime.CompleteBreak();
            Assert.Equal(BreakSessionState.Completed, session.State);
            Assert.Equal(ReminderBadge.None, runtime.TrayStatus.Badge);
            Assert.Contains("다음 휴식", runtime.TrayStatus.Status); Assert.False(runtime.CanExpandReminder);
            Draw(); Assert.False(Badge().IsVisible);

            // Stopping the timer cancels a folded reminder, and the badge has to go with it.
            await runtime.UpdateSettings(runtime.Settings with { BubbleCollapsed = true });
            Assert.True(runtime.Reminder.Invite(new BreakSession(BreakRoutines.All[0], "default-cat")));
            await runtime.ShowReminder(); Draw();
            Assert.True(Badge().IsVisible); Assert.True(runtime.CanExpandReminder);
            runtime.Stop();
            Assert.Equal(ReminderBadge.None, runtime.TrayStatus.Badge); Assert.False(runtime.CanExpandReminder);
            Draw(); Assert.False(Badge().IsVisible);
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
