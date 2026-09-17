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
        Assert.Equal(1, Find<NumericUpDown>(window, "ProfileInterval").Increment);
        Find<TextBox>(window, "ProfileName").Text = "집중 작업"; Find<NumericUpDown>(window, "ProfileInterval").Value = 45;
        Find<NumericUpDown>(window, "ProfileIdle").Value = 3; Find<ComboBox>(window, "ProfileRoutine").SelectedIndex = 1;
        Press(window, "프로필 저장");
        Assert.NotNull(saved); Assert.False(window.IsVisible); Assert.Equal("집중 작업", saved.Name);
        Assert.Equal("look-away", saved.RoutineId); Assert.Equal(45, saved.IntervalMinutes); Assert.Equal(3, saved.IdleMinutes);
    }
    [AvaloniaFact]
    public void ProfileEditorKeepsInvalidOrUnsavedDataOpen()
    {
        var window = new ProfileEditorWindow(new(), null, _ => throw new IOException("Read only"));
        window.Show(); Dispatcher.UIThread.RunJobs(); Find<TextBox>(window, "ProfileName").Text = ""; Press(window, "프로필 저장");
        Assert.True(window.IsVisible); Assert.Contains("이름", Find<TextBlock>(window, "ProfileError").Text);
        Find<TextBox>(window, "ProfileName").Text = "Work"; Press(window, "프로필 저장");
        Assert.True(window.IsVisible); Assert.Contains("저장하지 못했어요", Find<TextBlock>(window, "ProfileError").Text); window.Close();
    }
    [AvaloniaFact]
    public void LibraryProtectsBuiltInsAndAppliesProfileThroughTheSaveCallback()
    {
        var settings = new AppSettings().SaveProfile(new("focus", "Focus", 45, 3, "look-away"));
        var window = new PersonalizationWindow(() => settings, value => { settings = value; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs();
        Assert.False(Button(window, "루틴 삭제").IsEnabled); Assert.False(Button(window, "루틴 편집").IsEnabled);
        Find<TabControl>(window, "PersonalizationTabs").SelectedIndex = 1; Dispatcher.UIThread.RunJobs(); Press(window, "프로필 적용");
        Assert.Equal("focus", settings.ActiveProfileId); Assert.Equal("look-away", settings.BreakRoutineId); Assert.Equal(45, settings.IntervalMinutes);
        window.Close();
    }
    [AvaloniaFact]
    public void EditingAnAdditionalRoutineKeepsItsId()
    {
        BreakRoutine? saved = null;
        var routine = new BreakRoutine("writing", "Writing", [new("Pause", 20)]);
        var window = new RoutineEditorWindow(routine, value => { saved = value; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs(); Find<TextBox>(window, "RoutineName").Text = "Writing pause"; Press(window, "내 루틴 저장");
        Assert.Equal("writing", saved?.Id); Assert.Equal("Writing pause", saved?.Name);
    }
    [AvaloniaFact]
    public void StandaloneLibraryKeepsProfileApplyAvailableWithoutARuntime()
    {
        var settings = new AppSettings().SaveProfile(new("focus", "Focus", 45, 3, "look-away"));
        var window = new PersonalizationWindow(() => settings, value => { settings = value; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs();
        Find<TabControl>(window, "PersonalizationTabs").SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
        Assert.True(Button(window, "프로필 적용").IsEnabled);
        Assert.DoesNotContain("타이머를 일시정지하거나 중지", Find<TextBlock>(window, "ProfileDetail").Text);
        Press(window, "프로필 적용"); Dispatcher.UIThread.RunJobs();
        Assert.Equal("focus", settings.ActiveProfileId);
        window.Close();
    }

    [AvaloniaFact]
    public void ReviewNavigationAndExportUseTheVisibleSnapshot()
    {
        var today = new DateOnly(2026, 9, 13); BreakReview? exported = null;
        var window = new BreakReviewWindow(new BreakHistory().Review, exportReview: value => { exported = value; return Task.FromResult<string?>("review.csv"); }, currentDay: today);
        window.Show(); Dispatcher.UIThread.RunJobs(); Assert.False(Button(window, "다음 7일").IsEnabled);
        Press(window, "이전 7일"); Press(window, "CSV 내보내기");
        Assert.NotNull(exported); Assert.Equal(today.AddDays(-7), exported.EndDay); Assert.Equal(7, exported.Days.Count);
        Assert.Contains("완료한 휴식 0회", Find<TextBlock>(window, "ReviewStatus").Text);
        Press(window, "다음 7일"); Assert.Equal(today, window.Review.EndDay); window.Close();
    }
}
