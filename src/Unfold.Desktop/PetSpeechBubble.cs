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
    private readonly TextBlock title = Ui.Text("", DesignSystem.Section);
    private readonly AnimatedTimeText timer = new() { FontSize = DesignSystem.SpeechTimerSize };
    private readonly Button start, snooze, complete;
    private readonly Grid invitation = new() { ColumnDefinitions = new("*,8,*") };
    private readonly Grid reminderBody;

    public PetSpeechBubble(Action startBreak, Action snoozeBreak, Action completeBreak)
    {
        Name = "PetSpeechBubble"; Width = DesignSystem.SpeechBubbleWidth; Height = DesignSystem.SpeechInvitationHeight;
        Background = DesignSystem.Shell; BorderBrush = DesignSystem.Outline; BorderThickness = new(1);
        CornerRadius = DesignSystem.CardRadius; Padding = new(20, 14);
        title.Name = "PetBreakTitle"; title.FontWeight = FontWeight.SemiBold;
        title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextAlignment = TextAlignment.Center;
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
        reminderBody = new Grid { RowDefinitions = new("*,Auto") };
        reminderBody.Children.Add(title); Grid.SetRow(footer, 1); reminderBody.Children.Add(footer);
        timer.FontFamily = DesignSystem.AppFont;
        timer.FontWeight = FontWeight.SemiBold;
        Child = reminderBody; AutomationProperties.SetName(this, "펫의 스트레칭 알림");
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && !IsVisible)
            { timer.StopMotion(); }
        };
    }
    public void Refresh(PetReminder reminder, int snoozeMinutes, AppSettings? settings = null)
    {
        Width = DesignSystem.SpeechBubbleWidth;
        IsHitTestVisible = true; AutomationProperties.SetName(this, "펫의 스트레칭 알림");
        Height = reminder.Notice switch
        {
            PetNotice.Advance => DesignSystem.SpeechAdvanceHeight,
            PetNotice.Invitation => DesignSystem.SpeechInvitationHeight,
            PetNotice.Resting => DesignSystem.SpeechRestingHeight,
            PetNotice.Completed => DesignSystem.SpeechCompletedHeight,
            _ => DesignSystem.SpeechInvitationHeight
        };
        settings ??= new AppSettings();
        var session = reminder.Session;
        title.Text = reminder.Notice switch
        {
            PetNotice.Advance => settings.AdvanceDialogue,
            PetNotice.Invitation => settings.InvitationDialogue,
            PetNotice.Resting => session?.Remaining < TimeSpan.Zero ? settings.OvertimeDialogue : settings.RestingDialogue,
            PetNotice.Completed => settings.CompletedDialogue, _ => ""
        };
        ToolTip.SetTip(title, title.Text);
        invitation.IsVisible = reminder.Notice == PetNotice.Invitation;
        complete.IsVisible = timer.IsVisible = reminder.Notice == PetNotice.Resting;
        complete.IsEnabled = session?.State is BreakSessionState.InProgress or BreakSessionState.AwaitingConfirmation;
        snooze.Content = $"{snoozeMinutes}분 뒤에";
        timer.UpdateValue(session is null ? "" : PetReminder.TimerText(session), reminder.Notice == PetNotice.Resting);
        timer.Foreground = session?.Remaining < TimeSpan.Zero ? DesignSystem.Warning : DesignSystem.Cream;
        AutomationProperties.SetName(timer, "휴식 타이머 " + timer.Text);
    }
    internal void FocusAction() => (complete.IsVisible ? complete : start).Focus(Avalonia.Input.NavigationMethod.Tab);
}

public sealed record PetBubbleLayout(Size Size, Point Pet, Point Bubble, IReadOnlyList<Point> Tail)
{
    public static PetBubbleLayout Create(BubbleDirection direction, bool expanded,
        double bubbleHeight = DesignSystem.SpeechInvitationHeight, double petSize = DesignSystem.PetBaseSize,
        double bubbleWidth = DesignSystem.SpeechBubbleWidth)
    {
        if (petSize <= 0 || !double.IsFinite(petSize)) throw new ArgumentOutOfRangeException(nameof(petSize));
        if (!expanded) return new(new(petSize, petSize), default, default, []);
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
    internal static PetBubbleLayout CreateExpanded(BubbleDirection preferred, PixelPoint anchor, double scale,
        PixelRect work, double petSize, double bubbleHeight, double bubbleWidth)
    {
        var opposite = preferred switch
        {
            BubbleDirection.Top => BubbleDirection.Bottom, BubbleDirection.Bottom => BubbleDirection.Top,
            BubbleDirection.Left => BubbleDirection.Right, _ => BubbleDirection.Left
        };
        // Keep the pet at its screen anchor. Flip the bubble when its preferred
        // side has no room, then slide its cross-axis alignment at the edges.
        foreach (var direction in (preferred is BubbleDirection.Top or BubbleDirection.Bottom
            ? new[] { preferred, opposite, BubbleDirection.Left, BubbleDirection.Right } : new[] { preferred, opposite }).Distinct())
        {
            var layout = Create(direction, true, bubbleHeight, petSize, bubbleWidth);
            if (layout.Size.Width * scale > work.Width || layout.Size.Height * scale > work.Height) continue;
            var position = layout.Position(anchor, scale, work);
            var pet = new Point((anchor.X - position.X) / scale, (anchor.Y - position.Y) / scale);
            var vertical = direction is BubbleDirection.Top or BubbleDirection.Bottom;
            if (Math.Abs(vertical ? pet.Y - layout.Pet.Y : pet.X - layout.Pet.X) > 1 / scale) continue;
            if (pet.X < 0 || pet.Y < 0 || pet.X + petSize > layout.Size.Width || pet.Y + petSize > layout.Size.Height) continue;
            var shift = pet - layout.Pet;
            return layout with { Pet = pet, Tail = layout.Tail.Select(point => point + shift).ToArray() };
        }
        return Create(preferred, true, bubbleHeight, petSize, bubbleWidth);
    }
    internal static BubbleDirection AutomaticDirection(PixelPoint anchor, double scale, PixelRect area, double petSize) =>
        anchor.X + petSize * scale / 2 < area.X + area.Width / 2d ? BubbleDirection.Right : BubbleDirection.Left;

    internal static PetBubbleLayout CreateSurface(BubbleDirection direction, PixelPoint anchor, double scale,
        PixelRect? area, double petSize, double bubbleHeight, double bubbleWidth)
    {
        var surfaceHeight = new[] { DesignSystem.SpeechAdvanceHeight,
            DesignSystem.SpeechInvitationHeight, DesignSystem.SpeechRestingHeight, DesignSystem.SpeechCompletedHeight }.Max();
        var surfaceWidth = DesignSystem.SpeechBubbleWidth;
        var surface = area is { } work ? CreateExpanded(direction, anchor, scale, work, petSize, surfaceHeight, surfaceWidth)
            : Create(direction, true, surfaceHeight, petSize, surfaceWidth);
        var actual = Create(direction, true, bubbleHeight, petSize, bubbleWidth);
        var shift = surface.Pet - actual.Pet;
        return surface with { Bubble = actual.Bubble + shift, Tail = actual.Tail.Select(point => point + shift).ToArray() };
    }
    public PixelPoint Position(PixelPoint petAnchor, double scale, PixelRect work)
    {
        var x = petAnchor.X - (int)Math.Round(Pet.X * scale); var y = petAnchor.Y - (int)Math.Round(Pet.Y * scale);
        return new(Math.Clamp(x, work.X, Math.Max(work.X, work.Right - (int)Math.Ceiling(Size.Width * scale))),
            Math.Clamp(y, work.Y, Math.Max(work.Y, work.Bottom - (int)Math.Ceiling(Size.Height * scale))));
    }
}
