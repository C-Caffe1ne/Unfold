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

    [AvaloniaTheory]
    [InlineData(PetNotice.Advance)]
    [InlineData(PetNotice.Invitation)]
    [InlineData(PetNotice.Resting)]
    [InlineData(PetNotice.Completed)]
    public void SpeechTextIsCenteredAndPassiveNoticesUseTheBubbleCenter(PetNotice notice)
    {
        var model = new PetReminder();
        if (notice == PetNotice.Advance) model.ShowAdvance(TimeSpan.Zero);
        else
        {
            model.Invite(new(BreakRoutines.All[0], "default-cat", durationSeconds: 60));
            if (notice is PetNotice.Resting or PetNotice.Completed) model.Start(TimeSpan.Zero);
            if (notice == PetNotice.Completed) model.Complete(TimeSpan.Zero);
        }
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => { }); bubble.Refresh(model, 60);
        var window = new Window { Width = bubble.Width, Height = bubble.Height, Content = bubble };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var text in bubble.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)))
                Assert.Equal(Avalonia.Media.TextAlignment.Center, text.TextAlignment);
            var title = bubble.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTitle");
            var origin = title.TranslatePoint(default, bubble)!.Value;
            Assert.InRange(Math.Abs(origin.X + title.Bounds.Width / 2 - bubble.Bounds.Width / 2), 0, 1);
            if (notice is PetNotice.Advance or PetNotice.Completed)
                Assert.InRange(Math.Abs(origin.Y + title.Bounds.Height / 2 - bubble.Bounds.Height / 2), 0, 1);
            if (notice == PetNotice.Resting)
            {
                var timer = bubble.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTimer");
                var timerOrigin = timer.TranslatePoint(default, bubble)!.Value;
                Assert.InRange(Math.Abs(timerOrigin.X + timer.Bounds.Width / 2 - bubble.Bounds.Width / 2), 0, 1);
                Assert.True(Button(window, "PetBreakComplete").IsVisible);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CompletionNoticeExpiresWithoutWaitingForTheWorkClockTick()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        var runtime = new AppRuntime(lifetime);
        try
        {
            await runtime.UpdateSettings(runtime.Settings with { ReminderSoundsEnabled = false });
            // Do not start the runtime: its recurring work timer never ticks in this test.
            runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
            runtime.StartBreak(); runtime.CompleteBreak();
            Assert.Equal(PetNotice.Completed, runtime.Reminder.Notice);
            var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            runtime.Changed += () => { if (!runtime.Reminder.HasNotice) expired.TrySetResult(); };
            await expired.Task.WaitAsync(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);
            Assert.False(runtime.Reminder.HasNotice);
            Assert.Single(runtime.BreakHistory.Completions);
        }
        finally { runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaFact]
    public void BubbleButtonsAllowEarlyCompletionWithoutOpeningAnotherWindow()
    {
        var now = TimeSpan.Zero; var model = new PetReminder();
        var session = new BreakSession(BreakRoutines.All[0], "default-cat", durationSeconds: 180); model.Invite(session);
        var started = 0; var finished = 0; model.Started += _ => started++; model.Finished += _ => finished++;
        var bubble = new PetSpeechBubble(() => model.Start(now), () => model.Snooze(), () => model.Complete(now));
        var window = new Window { Width = DesignSystem.SpeechBubbleWidth, Height = DesignSystem.SpeechRestingHeight, Content = bubble };
        try
        {
            bubble.Refresh(model, 7); window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("7분 뒤에", Button(window, "PetBreakSnooze").Content);
            Assert.Equal(DesignSystem.SpeechInvitationHeight, bubble.Height);
            var title = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTitle");
            Assert.Equal("스트레칭할 시간이에요", title.Text); Assert.Equal(Avalonia.Media.TextAlignment.Center, title.TextAlignment);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text?.Contains(session.Routine.Name, StringComparison.Ordinal) == true ||
                text.Text?.Contains(session.CurrentStep.Instruction, StringComparison.Ordinal) == true);
            Assert.False(Button(window, "PetBreakComplete").IsVisible);
            Click(Button(window, "PetBreakStart")); bubble.Refresh(model, 7);
            Assert.Equal(1, started); Assert.True(Button(window, "PetBreakComplete").IsVisible);
            Assert.Equal(DesignSystem.SpeechRestingHeight, bubble.Height); Assert.Equal("함께 쉬어 가요", title.Text);
            now = TimeSpan.FromSeconds(3); model.Tick(now); bubble.Refresh(model, 7);
            Click(Button(window, "PetBreakComplete")); bubble.Refresh(model, 7);
            Assert.Equal(1, finished); Assert.Equal(BreakSessionState.Completed, session.State);
            Assert.Equal(PetNotice.Completed, model.Notice); Assert.Equal(3, model.CompletedSeconds);
            Assert.Equal(DesignSystem.SpeechCompletedHeight, bubble.Height); Assert.Equal("스트레칭을 마쳤어요!", title.Text);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text?.Contains("쉬었어요", StringComparison.Ordinal) == true ||
                text.Text?.Contains("다음 휴식 때", StringComparison.Ordinal) == true);
            var advance = new PetReminder(); advance.ShowAdvance(TimeSpan.Zero); bubble.Refresh(advance, 7);
            Assert.Equal(DesignSystem.SpeechAdvanceHeight, bubble.Height); Assert.Equal("5분 뒤에 스트레칭해요", title.Text);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text?.Contains("하던 일을", StringComparison.Ordinal) == true);
            Assert.Empty(window.OwnedWindows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task PetScaleAndAllDirectionsKeepSessionAndButtonsInsideOneWindow()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        try
        {
            var session = new BreakSession(BreakRoutines.All[0], "default-cat"); runtime.Reminder.Invite(session);
            pet.Show();
            foreach (var scale in new[] { 50, 100, 150 })
            foreach (var direction in Enum.GetValues<BubbleDirection>())
            {
                await runtime.UpdateSettings(runtime.Settings with { BubbleDirection = direction, PetScalePercent = scale });
                pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
                var petSize = DesignSystem.PetBaseSize * scale / 100d;
                Assert.Equal(PetBubbleLayout.Create(direction, true, DesignSystem.SpeechInvitationHeight, petSize).Size, pet.ClientSize);
                Assert.Equal(new Size(petSize, petSize), pet.PetView.Bounds.Size);
                var tail = pet.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Polygon>().Single(shape => shape.Name == "PetSpeechTail");
                Assert.Null(tail.Stroke); Assert.Equal(0, tail.StrokeThickness); Assert.Equal(DesignSystem.Shell, tail.Fill);
                var outline = pet.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Polyline>().Single(shape => shape.Name == "PetSpeechTailOutline");
                Assert.Null(outline.Fill); Assert.Equal(DesignSystem.Outline, outline.Stroke); Assert.Equal(1, outline.StrokeThickness);
                Assert.Equal([tail.Points[0], tail.Points[2], tail.Points[1]], outline.Points);
                foreach (var button in pet.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible))
                {
                    var point = button.TranslatePoint(default, pet)!.Value;
                    Assert.True(point.X >= 0 && point.X + button.Bounds.Width <= pet.ClientSize.Width);
                    Assert.True(point.Y >= 0 && point.Y + button.Bounds.Height <= pet.ClientSize.Height);
                }
            }
            var menu = pet.ContextMenu!.Items.OfType<MenuItem>().ToArray();
            Assert.Equal(new object?[] { "설정", "펫 숨기기" }, menu.Select(item => item.Header));
            runtime.Reminder.Start(TimeSpan.Zero); runtime.Reminder.Tick(TimeSpan.FromSeconds(7));
            pet.RefreshSpeech(); Assert.Same(session, runtime.Reminder.Session);
            Assert.Equal(TimeSpan.FromSeconds(7), session.Elapsed); Assert.Empty(pet.OwnedWindows);
            Assert.Equal(150, AppSettings.Load(Path.Combine(temp.Path, "settings.json")).PetScalePercent);
        }
        finally { pet.ClosePet(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [Fact]
    public void TrayStatusNamesWaitingAndRestingWithoutAHiddenBubbleRecoveryState()
    {
        var idle = TrayReminderStatus.Create(PetNotice.None, "12:34", "진행 중");
        Assert.Contains("다음 휴식", idle.Status); Assert.Contains("진행 중", idle.ToolTip);

        var waiting = TrayReminderStatus.Create(PetNotice.Invitation, "12:34", "진행 중");
        Assert.StartsWith("휴식 대기 중", waiting.Status); Assert.Contains("휴식 대기 중", waiting.ToolTip);
        Assert.DoesNotContain("다음 휴식", waiting.Status); Assert.DoesNotContain("진행 중", waiting.ToolTip);

        var resting = TrayReminderStatus.Create(PetNotice.Resting, "12:34", "진행 중");
        Assert.StartsWith("휴식 중", resting.Status); Assert.Contains("휴식 중", resting.ToolTip);

        foreach (var notice in new[] { PetNotice.Advance, PetNotice.Completed })
        {
            var transient = TrayReminderStatus.Create(notice, "12:34", "진행 중");
            Assert.Equal(idle.Status, transient.Status);
        }
    }

    [AvaloniaFact]
    public async Task DebugPreviewLeavesLiveTimerReminderHistoryAndSoundsUntouched()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        try
        {
            pet.Show();
            var remaining = runtime.Clock.Remaining;
            var history = runtime.BreakHistory.Completions.Count;
            var dueSounds = runtime.DueSoundRequests;
            var completionSounds = runtime.CompletionSoundRequests;
            foreach (var notice in new[] { PetNotice.Advance, PetNotice.Invitation, PetNotice.Resting, PetNotice.Completed })
            {
                await runtime.ShowReminderPreview(notice);
                pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
                Assert.Equal(notice, runtime.PreviewNotice);
                Assert.Equal(notice, runtime.PresentedReminder.Notice);
                Assert.False(runtime.Reminder.HasNotice);
                Assert.Equal(remaining, runtime.Clock.Remaining);
                Assert.Equal(history, runtime.BreakHistory.Completions.Count);
                Assert.Equal(dueSounds, runtime.DueSoundRequests);
                Assert.Equal(completionSounds, runtime.CompletionSoundRequests);
                Assert.True(pet.GetVisualDescendants().OfType<PetSpeechBubble>().Single().IsVisible);
            }
            runtime.CloseReminderPreview(); pet.RefreshSpeech(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
            Assert.Null(runtime.PreviewNotice); Assert.Equal(new Size(DesignSystem.PetBaseSize, DesignSystem.PetBaseSize), pet.ClientSize);

            var live = new BreakSession(BreakRoutines.All[0], "default-cat");
            Assert.True(runtime.Reminder.Invite(live));
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.ShowReminderPreview(PetNotice.Advance));
            Assert.Same(live, runtime.Reminder.Session); Assert.Equal(PetNotice.Invitation, runtime.PresentedReminder.Notice);
            Assert.Empty(pet.OwnedWindows);
        }
        finally { pet.ClosePet(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaFact]
    public void OvertimeTimerAndCompletionButtonFitAfterInstructionBodyIsRemoved()
    {
        var model = new PetReminder(); var session = new BreakSession(new("long", "긴 안내", [new(new string('쉼', 180), 1)]), "default-cat");
        model.Invite(session); model.Start(TimeSpan.Zero); model.Tick(TimeSpan.FromSeconds(2));
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => model.Complete(TimeSpan.FromSeconds(2)));
        bubble.Refresh(model, 60);
        var window = new Window { Width = DesignSystem.SpeechBubbleWidth, Height = DesignSystem.SpeechRestingHeight, Content = bubble };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var image = window.CaptureRenderedFrame(); Assert.NotNull(image);
            var timer = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PetBreakTimer");
            var complete = Button(window, "PetBreakComplete"); Assert.Equal("+00:01", timer.Text);
            Assert.Equal(DesignSystem.SpeechRestingHeight, bubble.Height);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == session.CurrentStep.Instruction);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text is "준비되면 언제든 완료할 수 있어요." or "+60분에 도달했어요. 완료를 눌러 주세요.");
            Assert.Equal(DesignSystem.SpeechTimerSize, timer.FontSize); Assert.Equal(DesignSystem.SpeechControlHeight, complete.Bounds.Height);
            var timerOrigin = timer.TranslatePoint(default, window)!.Value; var buttonOrigin = complete.TranslatePoint(default, window)!.Value;
            Assert.True(buttonOrigin.Y >= timerOrigin.Y + timer.Bounds.Height);
            Assert.True(buttonOrigin.Y + complete.Bounds.Height <= window.ClientSize.Height);
        }
        finally { window.Close(); }
    }
}
