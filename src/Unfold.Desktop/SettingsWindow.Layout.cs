using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;
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
    private Control? preferencesPage;
    private BreakReviewView? reviewPage;
    private PetManagementView? petPage;

    private Control BuildDashboard(CheckBox login)
    {
        Background = DesignSystem.Canvas;
        Classes.Add("unfold-page");
        var main = new Grid { Name = "SettingsMain", RowDefinitions = new("*,14,Auto") };
        main.Children.Add(BuildCompanionCard(login));
        var timer = BuildTimerCard(); Grid.SetRow(timer, 2); main.Children.Add(timer);

        var details = Ui.Column(BuildHomeTimingCard(), BuildReviewCard()); details.Spacing = 14;
        var detailsScroll = new ScrollViewer { Name = "SettingsDetailsScroll", Content = details,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dashboard = new Grid { ColumnDefinitions = new("*,16,300") };
        dashboard.Children.Add(main); Grid.SetColumn(detailsScroll, 2); dashboard.Children.Add(detailsScroll);
        dashboardPage = dashboard; settingsPageHost.Content = dashboard;
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
        var settings = Nav("SettingsNavSettings", "설정 탭", "M19.4,13 A7.8,7.8 0 0 0 19.45,11 L21.1,9.7 L19.1,6.3 L17.05,7.1 A8,8 0 0 0 15.35,6.1 L15,3.9 H11 L10.65,6.1 A8,8 0 0 0 8.95,7.1 L6.9,6.3 L4.9,9.7 L6.55,11 A7.8,7.8 0 0 0 6.6,13 L4.9,14.3 L6.9,17.7 L8.95,16.9 A8,8 0 0 0 10.65,17.9 L11,20.1 H15 L15.35,17.9 A8,8 0 0 0 17.05,16.9 L19.1,17.7 L21.1,14.3 Z M13,10 A3,3 0 1 1 13,16 A3,3 0 1 1 13,10 Z", OpenPreferences);
        var review = Nav("SettingsNavReview", "기록 · 내보내기 탭", "M3,14 H7 V21 H3 Z M10,8 H14 V21 H10 Z M17,3 H21 V21 H17 Z", OpenReview);
        var pets = Nav("SettingsNavPacks", "펫 추가 탭", "M10,3 H14 V10 H21 V14 H14 V21 H10 V14 H3 V10 H10 Z", OpenPetPacks);
        navigationItems["pets"] = pets;
        navigationItems["dashboard"] = timer; navigationItems["settings"] = settings;
        navigationItems["review"] = review;
        SelectNavigation("dashboard");
        var links = Ui.Column(timer, settings, review, pets);
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

    private Border BuildTimerSettingsCard()
    {
        reminderApply.Classes.Add("compact"); reminderApply.Classes.Add("primary");
        reminderApply.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("타이머 설정", 17, Cream)); Grid.SetColumn(reminderApply, 1); header.Children.Add(reminderApply);
        var fields = new Grid { ColumnDefinitions = new("*,12,*") };
        var away = Ui.Column(Label("자리 비움 시간 (분)", 11, Muted), idle,
            Label("입력이 없을 때 작업 타이머를 멈춰요.", 10, Muted)); away.Spacing = 6;
        var remindAgain = Ui.Column(Label("다시 알림 시간 (분)", 11, Muted), snooze,
            Label("알림을 미뤘을 때 다시 기다릴 시간이에요.", 10, Muted)); remindAgain.Spacing = 6;
        fields.Children.Add(away); Grid.SetColumn(remindAgain, 2); fields.Children.Add(remindAgain);
        ToolTip.SetTip(idle, "이 시간 동안 입력이 없으면 작업 타이머를 일시정지해요.");
        ToolTip.SetTip(snooze, "알림을 미룬 뒤 다시 알릴 때까지의 작업 시간이에요.");
        idle.MinHeight = snooze.MinHeight = 36;
        reminderSettingsStatus.FontSize = 11;
        var body = Ui.Column(header, fields, reminderSettingsStatus);
        body.Spacing = 10; body.Margin = new(20);
        return Card("SettingsTimerSettingsCard", body, Surface, new(28));
    }

    private Border BuildHomeTimingCard()
    {
        homeTimingApply.Classes.Add("compact"); homeTimingApply.Classes.Add("primary");
        homeTimingApply.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("시간 설정", 17, Cream)); Grid.SetColumn(homeTimingApply, 1); header.Children.Add(homeTimingApply);
        var stretch = Ui.Column(Label("스트레칭 시간 (분)", 11, Muted), interval,
            Label("이 시간 동안 작업하면 스트레칭을 알려요.", 10, Muted)); stretch.Spacing = 6;
        var rest = Ui.Column(Label("휴식 시간 (분)", 11, Muted), breakDuration,
            Label("휴식을 시작하면 이 시간부터 카운트해요.", 10, Muted)); rest.Spacing = 6;
        interval.MinHeight = breakDuration.MinHeight = 36;
        ToolTip.SetTip(breakDuration, "다음 휴식의 목표 시간을 1~10분으로 설정해요.");
        homeTimingStatus.FontSize = 11;
        var body = Ui.Column(header, stretch, rest, homeTimingStatus);
        body.Spacing = 10; body.Margin = new(20);
        return Card("SettingsHomeTimingCard", body, Surface, new(28));
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

    private Task OpenDashboard()
    {
        if (dashboardPage is not null) ShowPage("dashboard", dashboardPage);
        timerControls.Children.OfType<Button>().First().Focus();
        return Task.CompletedTask;
    }
    private Task OpenPreferences()
    {
        preferencesPage ??= BuildPreferencesPage();
        ShowPage("settings", preferencesPage);
        return Task.CompletedTask;
    }
    private Task OpenReview()
    {
        reviewPage ??= new BreakReviewView(this, runtime.BreakHistory.Review,
            () => runtime.BreakHistoryError, showHeader: false);
        reviewPage.Refresh(); ShowPage("review", reviewPage); return Task.CompletedTask;
    }
    private Task OpenPetPacks()
    {
        petPage ??= new PetManagementView(this, runtime.Library, runtime.SelectInstalledCharacter,
            showPageHeaders: false);
        ShowPage("pets", petPage); return Task.CompletedTask;
    }

    private Control BuildPreferencesPage()
    {
        var sections = Ui.Column(BuildNotificationSettingsCard(), BuildTimerSettingsCard());
        sections.Name = "SettingsPreferencesSections"; sections.Spacing = 14; sections.MaxWidth = 780;
        sections.HorizontalAlignment = HorizontalAlignment.Stretch;
        var scroll = Ui.PageBodyScroll(sections); scroll.Name = "SettingsPreferencesScroll";
        var page = new Grid { Name = "SettingsPreferencesPage" };
        page.Children.Add(scroll);
        return page;
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
