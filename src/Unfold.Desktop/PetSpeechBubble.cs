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
    private readonly TextBlock title = Ui.Text("", DesignSystem.Section), timer = Ui.Text("", DesignSystem.SpeechTimerSize);
    private readonly Button start, snooze, complete;
    private readonly Grid invitation = new() { ColumnDefinitions = new("*,8,*") };

    public PetSpeechBubble(Action startBreak, Action snoozeBreak, Action completeBreak)
    {
        Name = "PetSpeechBubble"; Width = DesignSystem.SpeechBubbleWidth; Height = DesignSystem.SpeechInvitationHeight;
        Background = DesignSystem.Shell; BorderBrush = DesignSystem.Outline; BorderThickness = new(1);
        CornerRadius = DesignSystem.CardRadius; Padding = new(20, 14);
        title.Name = "PetBreakTitle"; title.FontWeight = FontWeight.SemiBold;
        title.TextWrapping = TextWrapping.Wrap; title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Stretch;
        timer.Name = "PetBreakTimer"; timer.HorizontalAlignment = HorizontalAlignment.Stretch;
        timer.TextAlignment = TextAlignment.Center;
        start = Ui.Primary(Ui.Button("휴식 시작", startBreak)); start.Name = "PetBreakStart";
        snooze = Ui.Button("5분 뒤에", snoozeBreak); snooze.Name = "PetBreakSnooze";
        complete = Ui.Primary(Ui.Button("완료", completeBreak)); complete.Name = "PetBreakComplete";
        foreach (var button in new[] { start, snooze, complete })
        {
            button.Height = DesignSystem.SpeechControlHeight;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        }
        invitation.Children.Add(snooze); Grid.SetColumn(start, 2); invitation.Children.Add(start);
        var actions = new Grid(); actions.Children.Add(invitation); actions.Children.Add(complete);
        var footer = new StackPanel { Spacing = DesignSystem.SpeechGap };
        footer.Children.Add(timer); footer.Children.Add(actions);
        var body = new Grid { RowDefinitions = new("*,Auto") };
        body.Children.Add(title); Grid.SetRow(footer, 1); body.Children.Add(footer);
        Child = body; AutomationProperties.SetName(this, "펫의 스트레칭 알림");
    }
    public void Refresh(PetReminder reminder, int snoozeMinutes)
    {
        Height = reminder.Notice switch
        {
            PetNotice.Advance => DesignSystem.SpeechAdvanceHeight,
            PetNotice.Invitation => DesignSystem.SpeechInvitationHeight,
            PetNotice.Resting => DesignSystem.SpeechRestingHeight,
            PetNotice.Completed => DesignSystem.SpeechCompletedHeight,
            _ => DesignSystem.SpeechInvitationHeight
        };
        var session = reminder.Session;
        title.Text = reminder.Notice switch
        {
            PetNotice.Advance => "5분 뒤에 스트레칭해요",
            PetNotice.Invitation => "스트레칭할 시간이에요",
            PetNotice.Resting => session?.Remaining < TimeSpan.Zero ? "조금 더 쉬어도 좋아요" : "함께 쉬어 가요",
            PetNotice.Completed => "스트레칭을 마쳤어요!", _ => ""
        };
        invitation.IsVisible = reminder.Notice == PetNotice.Invitation;
        complete.IsVisible = timer.IsVisible = reminder.Notice == PetNotice.Resting;
        complete.IsEnabled = session?.State is BreakSessionState.InProgress or BreakSessionState.AwaitingConfirmation;
        snooze.Content = $"{snoozeMinutes}분 뒤에";
        timer.Text = session is null ? "" : PetReminder.TimerText(session);
        timer.Foreground = session?.Remaining < TimeSpan.Zero ? DesignSystem.Warning : DesignSystem.Cream;
        AutomationProperties.SetName(timer, "휴식 타이머 " + timer.Text);
    }
    internal void FocusAction() => (complete.IsVisible ? complete : start).Focus(Avalonia.Input.NavigationMethod.Tab);
}

public sealed record PetBubbleLayout(Size Size, Point Pet, Point Bubble, IReadOnlyList<Point> Tail)
{
    public static PetBubbleLayout Create(BubbleDirection direction, bool expanded,
        double bubbleHeight = DesignSystem.SpeechInvitationHeight, double petSize = DesignSystem.PetBaseSize)
    {
        if (petSize <= 0 || !double.IsFinite(petSize)) throw new ArgumentOutOfRangeException(nameof(petSize));
        if (!expanded) return new(new(petSize, petSize), default, default, []);
        var bubbleWidth = DesignSystem.SpeechBubbleWidth;
        var gap = DesignSystem.PetBubbleGap;
        var width = Math.Max(bubbleWidth, petSize);
        var horizontalCenter = width / 2;
        var height = Math.Max(petSize, bubbleHeight);
        var center = height / 2;
        var petY = (height - petSize) / 2;
        var bubbleY = (height - bubbleHeight) / 2;
        return direction switch
        {
            BubbleDirection.Top => new(new(width, bubbleHeight + gap + petSize),
                new((width - petSize) / 2, bubbleHeight + gap), new((width - bubbleWidth) / 2, 0),
                [new(horizontalCenter - 10, bubbleHeight - 1), new(horizontalCenter + 10, bubbleHeight - 1), new(horizontalCenter, bubbleHeight + gap)]),
            BubbleDirection.Bottom => new(new(width, bubbleHeight + gap + petSize),
                new((width - petSize) / 2, 0), new((width - bubbleWidth) / 2, petSize + gap),
                [new(horizontalCenter - 10, petSize + gap + 1), new(horizontalCenter + 10, petSize + gap + 1), new(horizontalCenter, petSize)]),
            BubbleDirection.Left => new(new(bubbleWidth + gap + petSize, height), new(bubbleWidth + gap, petY), new(0, bubbleY),
                [new(bubbleWidth - 1, center - 10), new(bubbleWidth - 1, center + 10), new(bubbleWidth + gap, center)]),
            BubbleDirection.Right => new(new(bubbleWidth + gap + petSize, height), new(0, petY), new(petSize + gap, bubbleY),
                [new(petSize + gap + 1, center - 10), new(petSize + gap + 1, center + 10), new(petSize, center)]),
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
