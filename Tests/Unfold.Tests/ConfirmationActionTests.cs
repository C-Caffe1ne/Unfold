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
public class ConfirmationActionTests
{
    [AvaloniaFact]
    public async Task StopRequiresApprovalAndDuplicateRequestsDoNotOpenMoreDialogs()
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        runtime.Clock.Start(TimeSpan.Zero);
        runtime.Clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        var remaining = runtime.Clock.Remaining;
        var session = new BreakSession(BreakRoutines.All[0], "default-cat");
        runtime.Reminder.Invite(session); runtime.StartBreak();

        var pending = runtime.RequestStop();
        Assert.Empty(scope.Owner.OwnedWindows);
        var dialog = await Dialog(scope.Owner);
        await runtime.RequestStop(); await runtime.Quit();
        Assert.Single(scope.Owner.OwnedWindows);
        Assert.False(runtime.Clock.Stopped); Assert.Same(session, runtime.Reminder.Session);
        Choice(dialog, "취소"); await pending;
        Assert.Equal(remaining, runtime.Clock.Remaining); Assert.Same(session, runtime.Reminder.Session);

        pending = runtime.RequestStop(); (await Dialog(scope.Owner)).Close(); await pending;
        Assert.False(runtime.Clock.Stopped); Assert.Same(session, runtime.Reminder.Session);

        pending = runtime.RequestStop(); Choice(await Dialog(scope.Owner), "중지"); await pending;
        Assert.True(runtime.Clock.Stopped); Assert.Equal(runtime.Clock.Interval, runtime.Clock.Remaining);
        Assert.Null(runtime.Reminder.Session); Assert.False(runtime.Reminder.HasNotice);
        Assert.Empty(runtime.BreakHistory.Completions);
    }

    [AvaloniaFact]
    public async Task QuitCancelAndTitlebarClosePreserveTimerAndReminder()
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        var session = new BreakSession(BreakRoutines.All[0], "default-cat");
        runtime.Reminder.Invite(session); runtime.StartBreak();
        var remaining = runtime.Clock.Remaining;
        var pending = runtime.Quit(); var dialog = await Dialog(scope.Owner);
        await runtime.Quit(); await runtime.RequestStop();
        Assert.Single(scope.Owner.OwnedWindows);
        Choice(dialog, "취소"); await pending;
        Assert.True(scope.Owner.IsVisible); Assert.Same(session, runtime.Reminder.Session);
        Assert.False(runtime.Clock.Stopped); Assert.Equal(remaining, runtime.Clock.Remaining);

        pending = runtime.Quit(); (await Dialog(scope.Owner)).Close(); await pending;
        Assert.True(scope.Owner.IsVisible); Assert.Same(session, runtime.Reminder.Session);
        Assert.False(runtime.Clock.Stopped); Assert.Equal(remaining, runtime.Clock.Remaining);
    }

    [AvaloniaFact]
    public async Task QuitApprovalStillRunsTheUnsavedEditorGuard()
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        await runtime.OpenEditor(); var editor = Assert.IsType<EditorWindow>(runtime.ActiveEditor);
        editor.Session.BeginStroke(new(1, 1)); editor.Session.EndStroke();
        Assert.True(editor.Session.IsDirty);
        var pending = runtime.Quit(); Choice(await Dialog(scope.Owner), "종료");
        var guard = await Dialog(editor);
        Assert.True(editor.Session.IsDirty); Assert.False(pending.IsCompleted);
        guard.Close(); await pending;
        Assert.True(editor.Session.IsDirty); Assert.True(editor.IsVisible);
        // A cancelled document guard releases the gate and does not cache exit approval.
        pending = runtime.Quit(); Choice(await Dialog(scope.Owner), "취소"); await pending;
        Assert.True(editor.Session.IsDirty); Assert.True(editor.IsVisible);
    }

    [AvaloniaFact]
    public async Task PetMenuHidesImmediatelyPersistsAndKeepsTheCurrentBreakRecoverable()
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        var character = runtime.Library.Save(new PixelDocument(4, 4) { Name = "숨기기 검사" });
        await runtime.Reload();
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = character.Manifest.Id, ReminderSoundsEnabled = false });
        var pet = Assert.IsType<PetWindow>(runtime.ActivePet);
        Assert.True(pet.IsVisible);
        await runtime.ShowReminder(); var session = Assert.IsType<BreakSession>(runtime.Reminder.Session);
        runtime.StartBreak();
        var menu = pet.ContextMenu!.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "펫 숨기기"));
        menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Dispatcher.UIThread.RunJobs();
        Assert.False(pet.IsVisible); Assert.False(runtime.Settings.ShowPet);
        Assert.False(AppSettings.Load(Path.Combine(scope.DataRoot, "settings.json")).ShowPet);
        Assert.Same(session, runtime.Reminder.Session); Assert.Empty(runtime.BreakHistory.Completions);

        // Routine refresh/settings saves must not undo an explicit hide of this notice.
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 110 });
        Assert.False(pet.IsVisible); Assert.Same(session, runtime.Reminder.Session);
        await runtime.FocusReminder(); Assert.True(pet.IsVisible); Assert.False(runtime.Settings.ShowPet);
        Assert.Same(session, runtime.Reminder.Session);
        await runtime.HidePet(); runtime.CompleteBreak();
        Assert.False(pet.IsVisible); Assert.Equal(PetNotice.Completed, runtime.Reminder.Notice);
        Assert.Single(runtime.BreakHistory.Completions);

        // Hiding the current notice does not silence the next scheduled invitation.
        await runtime.ShowReminder(); Assert.True(pet.IsVisible);
        Assert.Equal(PetNotice.Invitation, runtime.Reminder.Notice);
        Assert.False(runtime.Settings.ShowPet);
        // A temporarily surfaced pet can be hidden again even though ShowPet is already false.
        await runtime.HidePet(); Assert.False(pet.IsVisible); Assert.NotNull(runtime.Reminder.Session);
    }

    private static void Choice(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task<Window> Dialog(Window owner)
    {
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (owner.OwnedWindows.FirstOrDefault() is { } dialog) return dialog;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Expected an action confirmation dialog.");
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly TempDirectory temp = new();
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string DataRoot => temp.Path;
        public AppRuntime Runtime { get; }
        public Window Owner { get; } = new() { Width = 600, Height = 400 };
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime); lifetime.MainWindow = Owner; Owner.Show();
        }
        public void Dispose()
        {
            Runtime.ActiveEditor?.CloseAfterApproval(); Runtime.Dispose(); Owner.Close(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
