using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class EditorLifecycleTests
{
    private static async Task<Window> Dialog(Window owner)
    {
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (owner.OwnedWindows.FirstOrDefault() is { } dialog) return dialog;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Expected an editor dialog.");
    }
    [AvaloniaFact]
    public async Task ClosingConfirmationTitlebarCancelsWithoutLosingDrawing()
    {
        using var temp = new TempDirectory(); var session = new EditorSession(new(8, 8));
        var window = new EditorWindow(new(temp.Path), session, _ => { }); window.Show();
        session.BeginStroke(new(1, 1)); session.EndStroke();
        var pending = window.CanCloseDocument(); (await Dialog(window)).Close();
        Assert.False(await pending); Assert.True(session.IsDirty); Assert.True(window.IsVisible); window.CloseAfterApproval();
    }
    [AvaloniaFact]
    public async Task CancelledNamePromptReleasesSaveGateAndKeepsDrawing()
    {
        using var temp = new TempDirectory(); var session = new EditorSession(new(8, 8));
        var window = new EditorWindow(new(temp.Path), session, _ => { }); window.Show();
        session.BeginStroke(new(1, 1)); session.EndStroke();
        var pending = window.SaveDocument(); (await Dialog(window)).Close();
        Assert.False(await pending); Assert.True(session.IsDirty); Assert.Null(session.CharacterId);
        var again = window.SaveDocument(); (await Dialog(window)).Close(); Assert.False(await again);
        window.CloseAfterApproval();
    }
    [AvaloniaFact]
    public async Task FailedSaveDoesNotOverwriteOutsideChangesOrDiscardEdits()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path); var doc = new PixelDocument(8, 8);
        var saved = library.Save(doc); var opened = library.OpenForEditing(saved.Manifest.Id);
        var session = new EditorSession(opened.Document) { CharacterId = saved.Manifest.Id, Revision = opened.Revision };
        var window = new EditorWindow(library, session, _ => { }); window.Show();
        session.BeginStroke(new(1, 1)); session.EndStroke();
        doc.Name = "Outside change"; library.Save(doc, saved.Manifest.Id, opened.Revision);
        var pending = window.SaveDocument(); var dialog = await Dialog(window);
        dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(await pending); Assert.True(session.IsDirty); Assert.True(window.IsEnabled);
        Assert.Equal("Outside change", library.OpenForEditing(saved.Manifest.Id).Document.Name); window.CloseAfterApproval();
    }
}
