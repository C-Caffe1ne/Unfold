using Avalonia;
using PixelPoint = Avalonia.PixelPoint;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PetSpeechBubble : Border
{
    private readonly TextBlock title = Ui.Text("", 17), instruction = Ui.Text("", 14), timer = Ui.Text("", 34);
    private readonly TextBlock hint = Ui.Caption("");
    private readonly Button start, snooze, complete;
    private readonly Grid invitation = new() { ColumnDefinitions = new("*,8,*") };

    public PetSpeechBubble(Action startBreak, Action snoozeBreak, Action completeBreak)
    {
        Name = "PetSpeechBubble"; Width = 320; Height = 268;
        Background = DesignSystem.Shell; BorderBrush = DesignSystem.Outline; BorderThickness = new(1);
        CornerRadius = new(22); Padding = new(18);
        title.FontWeight = FontWeight.SemiBold; title.TextWrapping = TextWrapping.Wrap;
        instruction.TextWrapping = TextWrapping.Wrap; instruction.MaxLines = 4; instruction.TextTrimming = TextTrimming.CharacterEllipsis;
        timer.Name = "PetBreakTimer"; timer.HorizontalAlignment = HorizontalAlignment.Center;
        hint.TextAlignment = TextAlignment.Center;
        start = Ui.Primary(Ui.Button("휴식 시작", startBreak)); start.Name = "PetBreakStart";
        snooze = Ui.Button("5분 뒤에", snoozeBreak); snooze.Name = "PetBreakSnooze";
        complete = Ui.Primary(Ui.Button("완료", completeBreak)); complete.Name = "PetBreakComplete";
        foreach (var button in new[] { start, snooze, complete })
        { button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center; }
        invitation.Children.Add(snooze); Grid.SetColumn(start, 2); invitation.Children.Add(start);
        var body = new Grid { RowDefinitions = new("Auto,8,*,8,Auto,8,Auto,8,Auto") };
        body.Children.Add(title); Grid.SetRow(instruction, 2); body.Children.Add(instruction);
        Grid.SetRow(timer, 4); body.Children.Add(timer);
        var actions = new Grid(); actions.Children.Add(invitation); actions.Children.Add(complete);
        Grid.SetRow(actions, 6); body.Children.Add(actions); Grid.SetRow(hint, 8); body.Children.Add(hint);
        Child = body; AutomationProperties.SetName(this, "펫의 스트레칭 알림");
    }
    public void Refresh(PetReminder reminder, int snoozeMinutes)
    {
        var session = reminder.Session;
        title.Text = reminder.Notice switch
        {
            PetNotice.Advance => "5분 뒤에 스트레칭해요",
            PetNotice.Invitation => "스트레칭할 시간이에요",
            PetNotice.Resting => session?.Remaining < TimeSpan.Zero ? "조금 더 쉬어도 좋아요" : "함께 쉬어 가요",
            PetNotice.Completed => "스트레칭을 마쳤어요!", _ => ""
        };
        instruction.Text = reminder.Notice switch
        {
            PetNotice.Advance => "하던 일을 천천히 마무리해 주세요.",
            PetNotice.Invitation => session is null ? "준비되면 휴식을 시작해 주세요." :
                $"{session.Routine.Name} · {DurationText(session.DurationSeconds)}\n준비되면 휴식을 시작해 주세요.",
            PetNotice.Resting => session?.CurrentStep.Instruction,
            PetNotice.Completed => $"{reminder.CompletedSeconds / 60}분 {reminder.CompletedSeconds % 60}초 쉬었어요.\n다음 휴식 때 다시 만나요.", _ => ""
        };
        ToolTip.SetTip(instruction, instruction.Text);
        invitation.IsVisible = reminder.Notice == PetNotice.Invitation;
        complete.IsVisible = timer.IsVisible = reminder.Notice == PetNotice.Resting;
        complete.IsEnabled = session?.State is BreakSessionState.InProgress or BreakSessionState.AwaitingConfirmation;
        snooze.Content = $"{snoozeMinutes}분 뒤에";
        timer.Text = session is null ? "" : PetReminder.TimerText(session);
        timer.Foreground = session?.Remaining < TimeSpan.Zero ? DesignSystem.Warning : DesignSystem.Cream;
        hint.Text = reminder.Notice == PetNotice.Resting
            ? session?.Overtime >= BreakSession.MaximumOvertime ? "+60분에 도달했어요. 완료를 눌러 주세요." : "준비되면 언제든 완료할 수 있어요."
            : "펫 우클릭으로 말풍선을 접을 수 있어요.";
        AutomationProperties.SetName(timer, "휴식 타이머 " + timer.Text);
    }
    private static string DurationText(int seconds) => seconds % 60 == 0 ? $"{seconds / 60}분" : $"{seconds}초";
}

public sealed record PetBubbleLayout(Size Size, Point Pet, Point Bubble, IReadOnlyList<Point> Tail)
{
    public static PetBubbleLayout Create(BubbleDirection direction, bool expanded)
    {
        if (!expanded) return new(new(192, 192), default, default, []);
        return direction switch
        {
            BubbleDirection.Top => new(new(320, 472), new(64, 280), default, [new(150, 267), new(170, 267), new(160, 280)]),
            BubbleDirection.Bottom => new(new(320, 472), new(64, 0), new(0, 204), [new(150, 205), new(170, 205), new(160, 192)]),
            BubbleDirection.Left => new(new(524, 268), new(332, 38), default, [new(319, 124), new(319, 144), new(332, 134)]),
            BubbleDirection.Right => new(new(524, 268), new(0, 38), new(204, 0), [new(205, 124), new(205, 144), new(192, 134)]),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }
    public PixelPoint Position(PixelPoint petAnchor, double scale, PixelRect work)
    {
        var x = petAnchor.X - (int)Math.Round(Pet.X * scale); var y = petAnchor.Y - (int)Math.Round(Pet.Y * scale);
        return new(Math.Clamp(x, work.X, Math.Max(work.X, work.Right - (int)Math.Ceiling(Size.Width * scale))),
            Math.Clamp(y, work.Y, Math.Max(work.Y, work.Bottom - (int)Math.Ceiling(Size.Height * scale))));
    }
}
