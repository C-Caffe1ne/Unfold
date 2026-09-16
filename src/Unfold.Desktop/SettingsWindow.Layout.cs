using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using static Unfold.Desktop.DesignSystem;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private readonly TextBlock companionName = Label("", 20, Cream);
    private readonly TextBlock todayCount = Label("0", 48, Cream);
    private readonly TextBlock intervalHint = Label("", 12, Muted);
    private readonly ContentControl settingsPageHost = new() { Name = "SettingsPageHost",
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Dictionary<string, Button> navigationItems = [];
    private Control? dashboardPage;
    private PersonalizationView? personalizationPage;
    private BreakReviewView? reviewPage;
    private PetManagementView? petPage;

    private Control BuildDashboard(CheckBox login)
    {
        Background = DesignSystem.Canvas;
        Classes.Add("unfold-page");
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0, 0, 0, 18) };
        var heading = Ui.Column(Label("UNFOLD / 나의 휴식 공간", 11, Muted), Label("잠깐의 여유를 만들어 보세요.", 25, Cream));
        heading.Spacing = 6; header.Children.Add(heading);
        var badge = new Border { Background = Raised, CornerRadius = new(18), Padding = new(12, 8),
            VerticalAlignment = VerticalAlignment.Center, Child = Ui.Row(new Ellipse { Width = 6, Height = 6, Fill = Cream,
                VerticalAlignment = VerticalAlignment.Center }, Label("나의 페이스대로", 11, Cream)) };
        Grid.SetColumn(badge, 1); header.Children.Add(badge);

        var main = new Grid { Name = "SettingsMain", RowDefinitions = new("*,14,Auto") };
        main.Children.Add(BuildCompanionCard(login));
        var timer = BuildTimerCard(); Grid.SetRow(timer, 2); main.Children.Add(timer);

        var details = Ui.Column(BuildReminderCard(), BuildRoutineCard(), BuildSpeechSettingsCard(), BuildReviewCard()); details.Spacing = 14;
        var detailsScroll = new ScrollViewer { Name = "SettingsDetailsScroll", Content = details,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dashboard = new Grid { ColumnDefinitions = new("*,16,300") };
        dashboard.Children.Add(main); Grid.SetColumn(detailsScroll, 2); dashboard.Children.Add(detailsScroll);
        var content = new Grid { RowDefinitions = new("Auto,*") };
        content.Children.Add(header); Grid.SetRow(dashboard, 1); content.Children.Add(dashboard);
        dashboardPage = content; settingsPageHost.Content = content;
        var frame = new Grid { ColumnDefinitions = new("64,16,*") };
        frame.Children.Add(BuildNavigation()); Grid.SetColumn(settingsPageHost, 2); frame.Children.Add(settingsPageHost);
        return new Border { Name = "SettingsFrame", Margin = new(16), Padding = new(14), CornerRadius = new(32),
            Background = Shell, BorderBrush = Outline, BorderThickness = new(1), Child = frame };
    }

    private Border BuildNavigation()
    {
        var rail = new Grid { RowDefinitions = new("Auto,24,*,Auto"), Margin = new(7, 12) };
        var logo = new Border { Width = 44, Height = 44, Background = Cream, CornerRadius = new(22),
            Child = Glyph("M12,1 C13,8 16,11 23,12 C16,13 13,16 12,23 C11,16 8,13 1,12 C8,11 11,8 12,1 Z", Ink, 24) };
        rail.Children.Add(logo);
        var timer = Nav("SettingsNavTimer", "타이머 탭", "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M11,6 H13 V11 H17 V13 H11 Z", OpenDashboard);
        var personalization = Nav("SettingsNavRoutines", "내 루틴 · 업무 프로필 탭", "M4,3 H9 V8 H4 Z M12,4 H21 V6 H12 Z M4,10 H9 V15 H4 Z M12,11 H21 V13 H12 Z M4,17 H9 V22 H4 Z M12,18 H21 V20 H12 Z", OpenPersonalization);
        var review = Nav("SettingsNavReview", "기록 · 내보내기 탭", "M3,14 H7 V21 H3 Z M10,8 H14 V21 H10 Z M17,3 H21 V21 H17 Z", OpenReview);
        var pets = Nav("SettingsNavPacks", "펫 추가 탭", "M10,3 H14 V10 H21 V14 H14 V21 H10 V14 H3 V10 H10 Z", OpenPetPacks);
        navigationItems["pets"] = pets;
        navigationItems["dashboard"] = timer; navigationItems["personalization"] = personalization; navigationItems["review"] = review;
        SelectNavigation("dashboard");
        var links = Ui.Column(timer, personalization, review, pets);
        links.Spacing = 12; Grid.SetRow(links, 2); rail.Children.Add(links);
        var quit = Nav("SettingsQuit", "Unfold 종료", "M11,2 H13 V12 H11 Z M7,4 L8,6 A8,8 0 1 0 16,6 L17,4 A10,10 0 1 1 7,4 Z", runtime.Quit);
        Grid.SetRow(quit, 3); rail.Children.Add(quit);
        return new Border { Background = Surface, CornerRadius = new(28), Child = rail };
    }

    private Border BuildCompanionCard(CheckBox login)
    {
        var content = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new(24, 22) };
        companionName.MaxLines = 2; companionName.TextTrimming = TextTrimming.CharacterEllipsis;
        var intro = Ui.Column(Label("함께하는 펫", 11, Muted), companionName); intro.Spacing = 7; content.Children.Add(intro);
        var stage = new Grid { ClipToBounds = true, MinHeight = 100 };
        stage.Children.Add(new Ellipse { Width = 240, Height = 170, Fill = Brush.Parse("#424B3D"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        stage.Children.Add(new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Margin = new(0, 8) });
        Grid.SetRow(stage, 1); content.Children.Add(stage);
        characters.MinHeight = 38; characters.Width = 200; characters.HorizontalAlignment = HorizontalAlignment.Left;
        showPet.FontSize = 12; showPet.Foreground = Muted; showPet.MinHeight = 28;
        login.Name = "LaunchAtLogin"; login.FontSize = 12; login.Foreground = Muted;
        var footer = Ui.Column(characters, Ui.Row(showPet, login), Label("창을 닫아도 트레이에서 계속 실행돼요.", 11, Muted)); footer.Spacing = 6; Grid.SetRow(footer, 2); content.Children.Add(footer);
        return Card("SettingsCompanionCard", content, Raised, new(32, 32, 64, 32));
    }

    private Border BuildTimerCard()
    {
        countdown.FontSize = 64; countdown.Foreground = Cream; countdown.FontWeight = FontWeight.Light;
        state.Foreground = Muted; state.FontSize = 12;
        foreach (var button in timerControls.Children.OfType<Button>())
        {
            button.Classes.Add("timer-control");
        }
        var toggle = timerControls.Children.OfType<Button>().First(); toggle.Classes.Add("primary");
        ((PathIcon)toggle.Content!).Foreground = Ink;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("다음 휴식까지", 12, Cream)); Grid.SetColumn(intervalHint, 1); header.Children.Add(intervalHint);
        var footer = new Grid { ColumnDefinitions = new("*,12,Auto") };
        var statusContent = Ui.Row(timerStateDot, state); statusContent.Spacing = 7;
        timerStateBadge.Child = statusContent; footer.Children.Add(timerStateBadge);
        Grid.SetColumn(timerControls, 2); footer.Children.Add(timerControls);
        var body = Ui.Column(header, countdown, footer); body.Spacing = 7; body.Margin = new(24, 20);
        return Card("SettingsTimerCard", body, Surface, new(28));
    }

    private Border BuildReminderCard()
    {
        reminderApply.Classes.Add("compact"); reminderApply.Classes.Add("primary");
        reminderApply.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("알림 설정", 17, Cream)); Grid.SetColumn(reminderApply, 1); header.Children.Add(reminderApply);
        var fields = new Grid { ColumnDefinitions = new("*,12,*") };
        var minutes = Ui.Column(Label("알림 간격 (분)", 11, Muted), interval); minutes.Spacing = 6;
        var away = Ui.Column(Label("자리 비움 (분)", 11, Muted), idle); away.Spacing = 6;
        fields.Children.Add(minutes); Grid.SetColumn(away, 2); fields.Children.Add(away);
        ToolTip.SetTip(idle, "이 시간 동안 입력이 없으면 작업 타이머를 일시정지해요.");
        interval.MinHeight = idle.MinHeight = routines.MinHeight = 36;
        reminderSettingsStatus.FontSize = 11;
        var body = Ui.Column(header, fields, reminderSettingsStatus);
        body.Spacing = 10; body.Margin = new(20);
        return Card("SettingsReminderCard", body, Surface, new(28));
    }

    private Border BuildRoutineCard()
    {
        activeProfile.FontSize = 11; activeProfile.Foreground = Muted;
        var edit = ActionButton("내 루틴 편집", OpenRoutine); edit.Name = "SettingsEditRoutine";
        var library = ActionButton("루틴 · 프로필", OpenPersonalization); library.Name = "SettingsOpenLibrary";
        var actions = new Grid { ColumnDefinitions = new("*,8,*") };
        actions.Children.Add(edit); Grid.SetColumn(library, 2); actions.Children.Add(library);
        var body = Ui.Column(Label("나의 휴식", 17, Cream), routines, activeProfile, actions);
        body.Spacing = 10; body.Margin = new(20);
        return Card("SettingsRoutineCard", body, Surface, new(28));
    }

    private Border BuildReviewCard()
    {
        today.FontSize = 12; today.Foreground = Cream; historyStatus.FontSize = 11; historyStatus.Foreground = Muted;
        var metric = Ui.Row(todayCount, Label("회", 12, Muted));
        var review = Ui.Quiet(ActionButton("기록 · 내보내기", OpenReview)); review.Name = "SettingsOpenReview";
        var body = Ui.Column(Label("오늘의 작은 쉼", 17, Cream), metric, today, historyStatus, review);
        body.Spacing = 8; body.Margin = new(20);
        return Card("SettingsReviewCard", body, Raised, new(28));
    }

    private Task OpenRoutine() => new RoutineEditorWindow(runtime.Settings.CustomRoutine,
        routine => runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine))).ShowDialog(this);
    private Task OpenDashboard()
    {
        if (dashboardPage is not null) ShowPage("dashboard", dashboardPage);
        timerControls.Children.OfType<Button>().First().Focus();
        return Task.CompletedTask;
    }
    private Task OpenPersonalization()
    {
        personalizationPage ??= new PersonalizationView(this, () => runtime.Settings, runtime.UpdateSettings);
        personalizationPage.Refresh(); ShowPage("personalization", personalizationPage); return Task.CompletedTask;
    }
    private Task OpenReview()
    {
        reviewPage ??= new BreakReviewView(this, runtime.BreakHistory.Review, () => runtime.BreakHistoryError);
        reviewPage.Refresh(); ShowPage("review", reviewPage); return Task.CompletedTask;
    }
    private Task OpenPetPacks()
    {
        petPage ??= new PetManagementView(this, runtime.Library, runtime.SelectInstalledCharacter);
        ShowPage("pets", petPage); return Task.CompletedTask;
    }

    private void ShowPage(string key, Control page)
    {
        settingsPageHost.Content = page; SelectNavigation(key);
    }
    private void SelectNavigation(string key)
    {
        foreach (var item in navigationItems)
        {
            item.Value.Classes.Remove("primary");
            if (item.Value.Content is PathIcon icon) icon.Foreground = Cream;
        }
        if (!navigationItems.TryGetValue(key, out var selected)) return;
        selected.Classes.Add("primary");
        if (selected.Content is PathIcon selectedIcon) selectedIcon.Foreground = Ink;
    }

    private static TextBlock Label(string text, double size, IBrush brush) => new()
    { Text = text, FontSize = size, Foreground = brush, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private static Border Card(string name, Control child, IBrush brush, CornerRadius radius) => new()
    { Name = name, Child = child, Background = brush, CornerRadius = radius, ClipToBounds = true };
    private static PathIcon Glyph(string path, IBrush brush, double size = 20) => new()
    { Data = Geometry.Parse(path), Width = size, Height = size, Foreground = brush };
    private static Button ActionButton(string label, Func<Task> action)
    {
        var button = Ui.AsyncButton(label, action); button.Classes.Add("compact");
        button.HorizontalAlignment = HorizontalAlignment.Stretch; return button;
    }
    private static Button Nav(string name, string label, string path, Func<Task> action)
    {
        var button = ActionButton(label, action); button.Name = name;
        button.Classes.Add("navigation");
        button.Content = Glyph(path, Cream); button.Width = 46; button.Height = 46; button.Padding = new(10);
        button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        return button;
    }

}
