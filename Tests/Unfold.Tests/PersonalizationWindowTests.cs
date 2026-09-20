using Avalonia;
using Avalonia.Automation;
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
    private static Button Button(Window window, string label) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label) || AutomationProperties.GetName(button) == label);
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
    public void LibraryFooterGroupsActionsAndKeepsCloseOnThePrimaryRow()
    {
        var settings = new AppSettings().SaveProfile(new("focus", "Focus", 45, 3, "look-away"));
        var window = new PersonalizationWindow(() => settings, value => { settings = value; return Task.CompletedTask; });
        window.Show(); window.Width = window.MinWidth; window.Height = window.MinHeight;
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        var tabs = Find<TabControl>(window, "PersonalizationTabs");
        foreach (var index in new[] { 0, 1, 0 })
        {
            tabs.SelectedIndex = index; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var manage = index == 1 ? new[] { "새 프로필", "프로필 편집", "프로필 삭제" } : new[] { "새 루틴", "루틴 편집", "루틴 삭제" };
            var primary = index == 1 ? "프로필 적용" : "루틴 사용";
            var buttons = manage.Append(primary).Append("닫기").Select(label => Button(window, label)).ToArray();
            // 닫기 used to wrap onto its own line, leaving the footer visibly asymmetric.
            Assert.Single(buttons.Select(button => Math.Round(button.TranslatePoint(default, window)!.Value.Y)).Distinct());
            foreach (var button in buttons)
            {
                var origin = button.TranslatePoint(default, window)!.Value;
                Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0, $"{button.Content}");
                Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= window.ClientSize.Width + 1, $"{button.Content}");
                Assert.True(origin.Y + button.Bounds.Height <= window.ClientSize.Height + 1, $"{button.Content}");
            }
            var manageRight = manage.Max(label => Button(window, label).TranslatePoint(default, window)!.Value.X + Button(window, label).Bounds.Width);
            var primaryLeft = Button(window, primary).TranslatePoint(default, window)!.Value.X;
            Assert.True(primaryLeft > manageRight, "The primary group must stay right of the manage group.");
            Assert.True(Button(window, "닫기").TranslatePoint(default, window)!.Value.X > primaryLeft);
            Assert.Equal(3, Find<ContentControl>(window, "LibraryManageActions").GetVisualDescendants().OfType<Button>().Count());
        }
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
