using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class RoutineEditorTests
{
    private static T Control<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Save(Window window) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save my routine"))
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [AvaloniaFact]
    public void EditorSavesNamedStepsThroughTheSettingsContract()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        var window = new RoutineEditorWindow(null, routine =>
        {
            (new AppSettings { CustomRoutine = routine, BreakRoutineId = routine.Id }).Save(file); return Task.CompletedTask;
        });
        window.Show(); Dispatcher.UIThread.RunJobs();
        Control<TextBox>(window, "RoutineName").Text = "Writing pause";
        Control<TextBox>(window, "Step1").Text = "Rest my hands";
        Control<NumericUpDown>(window, "Seconds1").Value = 35;
        Control<TextBox>(window, "Step2").Text = ""; Control<TextBox>(window, "Step3").Text = "";
        Save(window);
        var loaded = AppSettings.Load(file);
        Assert.False(window.IsVisible); Assert.Equal("Writing pause", loaded.CustomRoutine!.Name);
        Assert.Equal(new BreakStep("Rest my hands", 35), Assert.Single(loaded.CustomRoutine.Steps));
    }
    [AvaloniaFact]
    public void EmptyRoutineNameDoesNotSaveOrCloseTheEditor()
    {
        var saved = false; var window = new RoutineEditorWindow(null, _ => { saved = true; return Task.CompletedTask; });
        window.Show(); Dispatcher.UIThread.RunJobs(); Control<TextBox>(window, "RoutineName").Text = " ";
        Save(window);
        Assert.False(saved); Assert.True(window.IsVisible);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.IsVisible && text.Text?.StartsWith("Use a name") == true);
        window.Close();
    }
}
