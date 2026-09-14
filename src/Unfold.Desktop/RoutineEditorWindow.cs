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
        Title = "내 휴식 만들기 · Unfold"; Width = 500; Height = 700; CanResize = false;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var routine = existing ?? BreakRoutines.All[0];
        var id = existing?.Id ?? routineId ?? BreakRoutines.CustomId;
        var name = new TextBox { Name = "RoutineName", Text = existing?.Name ?? "나만의 휴식", MaxLength = 60 };
        AutomationProperties.SetName(name, "루틴 이름");
        var instructions = new TextBox[3]; var durations = new NumericUpDown[3];
        var body = Ui.Column(Ui.Field("루틴 이름", name));
        for (var index = 0; index < 3; index++)
        {
            var step = routine.Steps.ElementAtOrDefault(index);
            instructions[index] = new TextBox { Name = $"Step{index + 1}", Text = step?.Instruction ?? "", MaxLength = 180, PlaceholderText = "비워 두면 이 단계는 제외돼요" };
            durations[index] = new NumericUpDown { Name = $"Seconds{index + 1}", Minimum = 1, Maximum = 300, Value = step?.Seconds ?? 20,
                Increment = 5, FormatString = "0", Width = 125 };
            AutomationProperties.SetName(instructions[index], $"{index + 1}단계 안내");
            AutomationProperties.SetName(durations[index], $"{index + 1}단계 시간, 초 단위");
            body.Children.Add(Ui.Card(Ui.Column(Ui.Row(Ui.Caption($"{index + 1}단계"), durations[index], Ui.Caption("초")), instructions[index])));
        }
        var error = new TextBlock { Name = "RoutineError", Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var cancel = Ui.Button("취소", Close); cancel.IsCancel = true;
        var saveButton = Ui.AsyncButton("내 루틴 저장", async () =>
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
                error.Text = exception is ArgumentException ? exception.Message : "루틴을 저장하지 못했어요. 다시 시도해 주세요.";
                error.IsVisible = true;
            }
        });
        saveButton.IsDefault = true; Ui.Primary(saveButton); Ui.Quiet(cancel);
        Content = Ui.Page(this, "나에게 맞는 휴식을 만들어요.", "간단한 안내를 최대 3단계로 적어 보세요.",
            body, Ui.Column(error, Ui.Actions(cancel, saveButton)), "내 루틴");
        Opened += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
