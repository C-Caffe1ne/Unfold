using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class PersonalizationWindowTests
{
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static Button Button(Window window, string label) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label));
    private static void Press(Window window, string label) => Button(window, label).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    [AvaloniaFact]
    public void ProfileEditorSavesTheSelectedRoutineAndInterval()
    {
        WorkProfile? saved = null; var settings = new AppSettings();
        var window = new ProfileEditorWindow(settings, null, profile => { saved = profile; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs();
        Find<TextBox>(window, "ProfileName").Text = "집중 작업"; Find<NumericUpDown>(window, "ProfileInterval").Value = 45;
        Find<NumericUpDown>(window, "ProfileIdle").Value = 3; Find<ComboBox>(window, "ProfileRoutine").SelectedIndex = 1;
        Press(window, "Save profile");
        Assert.NotNull(saved); Assert.False(window.IsVisible); Assert.Equal("집중 작업", saved.Name);
        Assert.Equal("look-away", saved.RoutineId); Assert.Equal(45, saved.IntervalMinutes); Assert.Equal(3, saved.IdleMinutes);
    }
    [AvaloniaFact]
    public void ProfileEditorKeepsInvalidOrUnsavedDataOpen()
    {
        var window = new ProfileEditorWindow(new(), null, _ => throw new IOException("Read only"));
        window.Show(); Dispatcher.UIThread.RunJobs(); Find<TextBox>(window, "ProfileName").Text = ""; Press(window, "Save profile");
        Assert.True(window.IsVisible); Assert.Contains("Use a name", Find<TextBlock>(window, "ProfileError").Text);
        Find<TextBox>(window, "ProfileName").Text = "Work"; Press(window, "Save profile");
        Assert.True(window.IsVisible); Assert.Contains("Could not save", Find<TextBlock>(window, "ProfileError").Text); window.Close();
    }
    [AvaloniaFact]
    public void LibraryProtectsBuiltInsAndAppliesProfileThroughTheSaveCallback()
    {
        var settings = new AppSettings().SaveProfile(new("focus", "Focus", 45, 3, "look-away"));
        var window = new PersonalizationWindow(() => settings, value => { settings = value; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs();
        Assert.False(Button(window, "Delete routine").IsEnabled); Assert.False(Button(window, "Edit routine").IsEnabled);
        Find<TabControl>(window, "PersonalizationTabs").SelectedIndex = 1; Dispatcher.UIThread.RunJobs(); Press(window, "Apply profile");
        Assert.Equal("focus", settings.ActiveProfileId); Assert.Equal("look-away", settings.BreakRoutineId); Assert.Equal(45, settings.IntervalMinutes);
        window.Close();
    }
    [AvaloniaFact]
    public void EditingAnAdditionalRoutineKeepsItsId()
    {
        BreakRoutine? saved = null;
        var routine = new BreakRoutine("writing", "Writing", [new("Pause", 20)]);
        var window = new RoutineEditorWindow(routine, value => { saved = value; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs(); Find<TextBox>(window, "RoutineName").Text = "Writing pause"; Press(window, "Save my routine");
        Assert.Equal("writing", saved?.Id); Assert.Equal("Writing pause", saved?.Name);
    }
    [AvaloniaFact]
    public void ReviewNavigationAndExportUseTheVisibleSnapshot()
    {
        var today = new DateOnly(2026, 9, 13); BreakReview? exported = null;
        var window = new BreakReviewWindow(new BreakHistory().Review, exportReview: value => { exported = value; return Task.FromResult<string?>("review.csv"); }, currentDay: today);
        window.Show(); Dispatcher.UIThread.RunJobs(); Assert.False(Button(window, "Next 7 days").IsEnabled);
        Press(window, "Previous 7 days"); Press(window, "Export CSV");
        Assert.NotNull(exported); Assert.Equal(today.AddDays(-7), exported.EndDay); Assert.Equal(7, exported.Days.Count);
        Assert.Contains("Exported 0", Find<TextBlock>(window, "ReviewStatus").Text);
        Press(window, "Next 7 days"); Assert.Equal(today, window.Review.EndDay); window.Close();
    }
}
