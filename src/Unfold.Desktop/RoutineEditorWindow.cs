using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class RoutineEditorWindow : Window
{
    public RoutineEditorWindow(BreakRoutine? existing, Func<BreakRoutine, Task> save, string? routineId = null)
    {
        Title = "Make your own break · Unfold"; Width = 500; Height = 640; CanResize = false;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var routine = existing ?? BreakRoutines.All[0];
        var id = existing?.Id ?? routineId ?? BreakRoutines.CustomId;
        var name = new TextBox { Name = "RoutineName", Text = existing?.Name ?? "My reset", MaxLength = 60 };
        AutomationProperties.SetName(name, "Routine name");
        var instructions = new TextBox[3]; var durations = new NumericUpDown[3];
        var body = Ui.Column(Ui.Text("A break that fits you.", 24, Ui.Accent),
            Ui.Text("Give yourself up to three simple prompts.", 14), Ui.Text("NAME", 12), name);
        for (var index = 0; index < 3; index++)
        {
            var step = routine.Steps.ElementAtOrDefault(index);
            instructions[index] = new TextBox { Name = $"Step{index + 1}", Text = step?.Instruction ?? "", MaxLength = 180, PlaceholderText = "Leave blank to omit this step" };
            durations[index] = new NumericUpDown { Name = $"Seconds{index + 1}", Minimum = 1, Maximum = 300, Value = step?.Seconds ?? 20,
                Increment = 5, FormatString = "0", Width = 125 };
            AutomationProperties.SetName(instructions[index], $"Step {index + 1} prompt");
            AutomationProperties.SetName(durations[index], $"Step {index + 1} seconds");
            body.Children.Add(Ui.Column(Ui.Row(Ui.Text($"STEP {index + 1}", 12), durations[index], Ui.Text("seconds", 12)), instructions[index]));
        }
        var error = new TextBlock { Foreground = Brushes.LightSalmon, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        body.Children.Add(error);
        var cancel = Ui.Button("Cancel", Close); cancel.IsCancel = true;
        var saveButton = Ui.AsyncButton("Save my routine", async () =>
        {
            try
            {
                var steps = instructions.Select((input, index) => new BreakStep(input.Text?.Trim() ?? "", (int)(durations[index].Value ?? 20)))
                    .Where(step => step.Instruction.Length > 0).ToArray();
                var edited = new BreakRoutine(id, name.Text?.Trim() ?? "", Array.AsReadOnly(steps));
                edited.Validate(); await save(edited); Close();
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                error.Text = exception is ArgumentException ? exception.Message : "Could not save your routine. Please try again.";
                error.IsVisible = true;
            }
        });
        saveButton.IsDefault = true;
        var actions = Ui.Row(cancel, saveButton);
        actions.HorizontalAlignment = HorizontalAlignment.Right; body.Children.Add(actions);
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(24), Child = body } };
        Opened += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
