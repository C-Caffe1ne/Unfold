using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using static Unfold.Desktop.DesignSystem;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private readonly TextBlock companionName = Label("", Section, Cream);
    private readonly TextBlock todayCount = Label("0", 40, Cream);
    private readonly TextBlock intervalHint = Label("", 12, Muted);
    private readonly ContentControl settingsPageHost = new() { Name = "SettingsPageHost",
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Dictionary<string, Button> navigationItems = [];
    private Control? dashboardPage;
    private Control? preferencesPage;
    private BreakReviewView? reviewPage;
    private PetManagementView? petPage;

    private Control BuildDashboard()
    {
        Background = DesignSystem.Canvas;
        Classes.Add("unfold-page");
        var main = new Grid { Name = "SettingsMain", RowDefinitions = new($"{HomeTimerHeight},{Inset},*") };
        main.Children.Add(BuildTimerCard());
        var companion = BuildCompanionCard(); Grid.SetRow(companion, 2); main.Children.Add(companion);

        var details = Ui.Column(BuildHomeTimingCard(), BuildReviewCard()); details.Spacing = Inset;
        var detailsScroll = new ScrollViewer { Name = "SettingsDetailsScroll", Content = details,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dashboard = new Grid { Name = "SettingsDashboard", ColumnDefinitions = new("*,20,300") };
        dashboard.Children.Add(main); Grid.SetColumn(detailsScroll, 2); dashboard.Children.Add(detailsScroll);
        var dashboardScroll = new ScrollViewer { Name = "SettingsDashboardScroll", Content = dashboard,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        bool? compactLayout = null;
        void UpdateDashboardLayout()
        {
            var compact = (ClientSize.Width > 0 ? ClientSize.Width : Width) < 860;
            if (compactLayout == compact) return;
            compactLayout = compact;
            dashboardScroll.Offset = default;
            dashboardScroll.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            dashboard.ColumnDefinitions = new(compact ? "*" : "*,20,300");
            dashboard.RowDefinitions = new(compact ? "Auto,20,Auto" : "*");
            main.RowDefinitions[2].Height = compact ? new GridLength(420) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(detailsScroll, compact ? 0 : 2); Grid.SetRow(detailsScroll, compact ? 2 : 0);
            detailsScroll.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            if (compact && FocusManager?.GetFocusedElement() is Control focused && focused.GetVisualAncestors().Contains(dashboard))
                Dispatcher.UIThread.Post(() =>
                {
                    if (compactLayout == true && ReferenceEquals(FocusManager?.GetFocusedElement(), focused)) focused.BringIntoView();
                }, DispatcherPriority.Loaded);
        }
        SizeChanged += (_, _) => UpdateDashboardLayout();
        UpdateDashboardLayout();
        dashboardPage = dashboardScroll; settingsPageHost.Content = dashboardScroll;
        var frame = new Grid { ColumnDefinitions = new("64,16,*") };
        frame.Children.Add(BuildNavigation()); Grid.SetColumn(settingsPageHost, 2); frame.Children.Add(settingsPageHost);
        return new Border { Name = "SettingsFrame", Margin = new(16), Padding = new(14), CornerRadius = new(32),
            Background = Shell, BorderBrush = Outline, BorderThickness = new(1), Child = frame };
    }

    private Border BuildNavigation()
    {
        var rail = new Grid { Name = "SettingsNavigationRail", RowDefinitions = new("*,Auto"), Margin = new(7, 12) };
        var timer = Nav("SettingsNavTimer", "타이머 탭", "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M11,6 H13 V11 H17 V13 H11 Z", OpenDashboard);
        var settings = Nav("SettingsNavSettings", "설정 탭", "M19.4,13 A7.8,7.8 0 0 0 19.45,11 L21.1,9.7 L19.1,6.3 L17.05,7.1 A8,8 0 0 0 15.35,6.1 L15,3.9 H11 L10.65,6.1 A8,8 0 0 0 8.95,7.1 L6.9,6.3 L4.9,9.7 L6.55,11 A7.8,7.8 0 0 0 6.6,13 L4.9,14.3 L6.9,17.7 L8.95,16.9 A8,8 0 0 0 10.65,17.9 L11,20.1 H15 L15.35,17.9 A8,8 0 0 0 17.05,16.9 L19.1,17.7 L21.1,14.3 Z M13,10 A3,3 0 1 1 13,16 A3,3 0 1 1 13,10 Z", OpenPreferences);
        var review = Nav("SettingsNavReview", "기록 · 내보내기 탭", "M3,14 H7 V21 H3 Z M10,8 H14 V21 H10 Z M17,3 H21 V21 H17 Z", OpenReview);
        var pets = Nav("SettingsNavPacks", "펫 추가 탭", "M10,3 H14 V10 H21 V14 H14 V21 H10 V14 H3 V10 H10 Z", OpenPetPacks);
        navigationItems["pets"] = pets;
        navigationItems["dashboard"] = timer; navigationItems["settings"] = settings;
        navigationItems["review"] = review;
        SelectNavigation("dashboard");
        var links = Ui.Column(timer, pets, review, settings); links.Name = "SettingsNavigationLinks";
        links.Spacing = 12; rail.Children.Add(links);
        var quit = Nav("SettingsQuit", "Unfold 종료", "M11,2 H13 V12 H11 Z M7,4 L8,6 A8,8 0 1 0 16,6 L17,4 A10,10 0 1 1 7,4 Z", runtime.Quit);
        var bottom = Ui.Column(BuildThemeButton(), quit); bottom.Spacing = 12;
        Grid.SetRow(bottom, 1); rail.Children.Add(bottom);
        return new Border { Background = Surface, CornerRadius = new(28), Child = rail };
    }

    private Border BuildCompanionCard()
    {
        var content = new Grid { RowDefinitions = new("*,Auto"), Margin = new(Inset) };
        companionName.Name = "CompanionName"; companionName.FontWeight = FontWeight.SemiBold;
        companionName.MaxLines = 2; companionName.TextTrimming = TextTrimming.CharacterEllipsis;
        var petLabel = Label("함께하는 펫", Body, Muted);
        var intro = Ui.Column(petLabel, companionName); intro.Spacing = 4; intro.IsHitTestVisible = false;
        companionPreviewStage = new Grid { Name = "CompanionPreviewStage", Width = PetBaseSize, Height = PetBaseSize };
        companionPreviewStage.Children.Add(new Ellipse { MaxWidth = 220, MaxHeight = 140, Fill = PetHalo,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch });
        companionPreviewStage.Children.Add(preview);
        var previewScroll = new ScrollViewer { Name = "CompanionPreviewScroll", Content = companionPreviewStage,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        var previewArea = new Grid { MinHeight = 100, ClipToBounds = true, Margin = new(0, 0, 0, Space) };
        previewArea.Children.Add(previewScroll); previewArea.Children.Add(intro); content.Children.Add(previewArea);
        characters.Height = HomeControlHeight; characters.Width = HomeChoiceWidth; characters.HorizontalAlignment = HorizontalAlignment.Left;
        characters.Classes.Add("home-choice");
        characters.ContainerPrepared += (_, e) => e.Container.Classes.Add("home-choice-item");
        var scaleLabel = Label("펫 크기", Body, Cream); scaleLabel.Name = "PetScaleLabel";
        Ui.KeyboardFocusLabel(petScale, scaleLabel);
        AutomationProperties.SetLabeledBy(petScale, scaleLabel);
        petScale.Width = HomePetScaleWidth; petScale.MinWidth = 0;
        petScale.HorizontalAlignment = HorizontalAlignment.Left; petScale.VerticalAlignment = VerticalAlignment.Center;
        var scaleValueRow = new Grid { Width = HomePetScaleWidth, HorizontalAlignment = HorizontalAlignment.Left };
        petScaleValue.HorizontalAlignment = HorizontalAlignment.Right;
        scaleValueRow.Children.Add(petScaleValue);
        var scaleField = Ui.Column(scaleLabel, petScale, scaleValueRow); scaleField.Spacing = 4;
        var characterLabel = Label("펫 선택", Body, Cream); Ui.KeyboardFocusLabel(characters, characterLabel);
        AutomationProperties.SetLabeledBy(characters, characterLabel);
        var characterField = new Grid { ColumnDefinitions = new("Auto,12,*"), MinHeight = HomeControlHeight };
        characterLabel.VerticalAlignment = VerticalAlignment.Center; characterField.Children.Add(characterLabel);
        Grid.SetColumn(characters, 2); characterField.Children.Add(characters);
        var footer = Ui.Column(scaleField, characterField);
        footer.Spacing = Space; Grid.SetRow(footer, 1); content.Children.Add(footer);
        return Card("SettingsCompanionCard", content, Raised, CardRadius);
    }

    private Border BuildTimerCard()
    {
        countdown.FontSize = 64; countdown.Foreground = Cream; countdown.FontWeight = FontWeight.Light;
        countdown.LineHeight = 70;
        state.Foreground = Muted; state.FontSize = Caption;
        foreach (var button in timerControls.Children.OfType<Button>())
        {
            button.Classes.Add("timer-control");
        }
        var toggle = timerControls.Children.OfType<Button>().First(); toggle.Classes.Add("primary");
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("다음 휴식까지", Body, Cream)); Grid.SetColumn(intervalHint, 1); header.Children.Add(intervalHint);
        var footer = new Grid { ColumnDefinitions = new("*,12,Auto") };
        var statusContent = new Grid { ColumnDefinitions = new("Auto,7,*") };
        statusContent.Children.Add(timerStateDot); Grid.SetColumn(state, 2); statusContent.Children.Add(state);
        timerStateBadge.Child = statusContent; timerStateBadge.HorizontalAlignment = HorizontalAlignment.Left;
        timerStateBadge.VerticalAlignment = VerticalAlignment.Bottom; footer.Children.Add(timerStateBadge);
        timerControls.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(timerControls, 2); footer.Children.Add(timerControls);
        var body = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new(Inset) };
        body.Children.Add(header); Grid.SetRow(countdown, 1); body.Children.Add(countdown);
        Grid.SetRow(footer, 2); body.Children.Add(footer);
        return Card("SettingsTimerCard", body, Surface, CardRadius);
    }

    private Border BuildTimerSettingsCard()
    {
        Control TimingRow(string name, string label, NumericUpDown input)
        {
            var caption = SettingsLabel(label);
            Ui.KeyboardFocusLabel(input, caption); AutomationProperties.SetLabeledBy(input, caption);
            input.Width = SettingsNumberWidth; input.Height = SettingsControlHeight;
            var row = new Grid { Name = name, ColumnDefinitions = new("*,24,Auto") };
            row.Children.Add(caption); Grid.SetColumn(input, 2); row.Children.Add(input);
            return row;
        }
        var fields = Ui.Column(TimingRow("IdleSettingsRow", "자리 비움 시간 (분)", idle),
            TimingRow("SnoozeSettingsRow", "다시 알림 시간 (분)", snooze));
        fields.Spacing = SettingsRowGap;
        ToolTip.SetTip(idle, "이 시간 동안 입력이 없으면 작업 타이머를 일시정지해요.");
        ToolTip.SetTip(snooze, "알림을 미룬 뒤 다시 알릴 때까지의 작업 시간이에요.");
        var body = Ui.Column(SettingsHeading("타이머 설정"), fields);
        body.Spacing = Inset; body.Margin = new(Inset);
        return Card("SettingsTimerSettingsCard", body, Surface, new(28));
    }

    private Border BuildHomeTimingCard()
    {
        homeTimingApply.Classes.Add("primary");
        homeTimingApply.Width = HomeActionWidth; homeTimingApply.Height = HomeControlHeight;
        homeTimingApply.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(HomeHeading("시간 설정")); Grid.SetColumn(homeTimingApply, 1); header.Children.Add(homeTimingApply);
        Control Field(string text, NumericUpDown input)
        {
            var caption = Label(text, Body, Cream);
            Ui.KeyboardFocusLabel(input, caption); AutomationProperties.SetLabeledBy(input, caption);
            input.Width = HomeNumberWidth; input.Height = HomeControlHeight;
            input.HorizontalAlignment = HorizontalAlignment.Left;
            var field = Ui.Column(caption, input); field.Spacing = Space; return field;
        }
        var stretch = Field("스트레칭 알림 간격 (분)", interval);
        var rest = Field("휴식 시간 (분)", breakDuration);
        ToolTip.SetTip(breakDuration, "다음 휴식의 목표 시간을 1~10분으로 설정해요.");
        homeTimingStatus.FontSize = DesignSystem.Caption;
        var body = Ui.Column(header, stretch, rest, homeTimingStatus);
        body.Spacing = 16; body.Margin = new(Inset);
        return Card("SettingsHomeTimingCard", body, Surface, CardRadius);
    }

    private Border BuildReviewCard()
    {
        today.FontSize = Body; today.Foreground = Cream; historyStatus.FontSize = Caption; historyStatus.Foreground = Muted;
        var metric = Ui.Row(todayCount, Label("회", 12, Muted));
        var review = Ui.Quiet(Ui.AsyncButton("기록 · 내보내기", OpenReview)); review.Name = "SettingsOpenReview";
        review.HorizontalAlignment = HorizontalAlignment.Left; review.Height = HomeControlHeight;
        var body = Ui.Column(HomeHeading("오늘의 작은 쉼"), metric, today, historyStatus, review);
        body.Spacing = Space; body.Margin = new(Inset);
        return Card("SettingsReviewCard", body, Raised, CardRadius);
    }

    private static TextBlock HomeHeading(string text) => new()
    { Text = text, FontSize = Section, FontWeight = FontWeight.SemiBold, Foreground = Cream,
        VerticalAlignment = VerticalAlignment.Center };

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
        preferencesSections = Ui.Column(BuildNotificationSettingsCard(), BuildTimerSettingsCard(), BuildAppBehaviorCard(), BuildDebugSettingsCard());
        preferencesSections.Name = "SettingsPreferencesSections"; preferencesSections.Spacing = Inset;
        // An inner capped grid preserves the left edge instead of centering a MaxWidth-limited stack.
        var body = new Grid { ColumnDefinitions = new("*") };
        body.ColumnDefinitions[0].MaxWidth = SettingsContentWidth;
        body.Children.Add(preferencesSections);
        var scroll = Ui.PageBodyScroll(body); scroll.Name = "SettingsPreferencesScroll";
        var page = new Grid { Name = "SettingsPreferencesPage", RowDefinitions = new("*,Auto") };
        page.Children.Add(scroll);
        var footer = BuildPreferencesFooter(); Grid.SetRow(footer, 1); page.Children.Add(footer);
        RestorePreferences(PreferencesValues.From(runtime.Settings));
        return page;
    }

    private Border BuildAppBehaviorCard()
    {
        showPet.FontSize = launchAtLogin.FontSize = Body;
        showPet.Foreground = launchAtLogin.Foreground = Cream;
        showPet.MinHeight = launchAtLogin.MinHeight = 24;
        ToolTip.SetTip(showPet, "바탕화면에 펫을 표시하거나 숨겨요.");
        ToolTip.SetTip(launchAtLogin, "컴퓨터에 로그인하면 Unfold를 자동으로 실행해요.");
        var options = Ui.Column(showPet, launchAtLogin); options.Spacing = Space;
        var body = Ui.Column(SettingsHeading("앱 동작"), options);
        body.Spacing = Inset; body.Margin = new(Inset);
        return Card("SettingsAppBehaviorCard", body, Surface, new(28));
    }

    private void ShowPage(string key, Control page)
    {
        var keyboard = FocusManager?.GetFocusedElement() is Control focused && focused.Classes.Contains(":focus-visible");
        if (key != "settings") { stopSoundPreview?.Invoke(); runtime.CloseReminderPreview(); }
        settingsPageHost.Content = page; SelectNavigation(key);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible || settingsPageHost.Content != page) return;
            var controls = page.GetVisualDescendants().OfType<Control>();
            Control? target = key switch
            {
                "dashboard" => timerControls.Children.OfType<Button>().First(),
                "settings" => controls.FirstOrDefault(control => control.Name == "BubbleDirection"),
                "pets" => controls.OfType<TabItem>().FirstOrDefault(tab => tab.IsSelected),
                _ => controls.OfType<Button>().FirstOrDefault(button => button.IsEnabled && button.IsEffectivelyVisible)
            };
            target?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }, DispatcherPriority.Loaded);
    }
    private void SelectNavigation(string key)
    {
        foreach (var item in navigationItems)
        {
            item.Value.Classes.Remove("primary");
            AutomationProperties.SetName(item.Value, item.Value.Tag + (item.Key == key ? " · 선택됨" : ""));
            AutomationProperties.SetHelpText(item.Value, item.Key == key ? "현재 보고 있는 페이지예요." : "이 페이지로 이동해요.");
        }
        if (!navigationItems.TryGetValue(key, out var selected)) return;
        selected.Classes.Add("primary");
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
        button.Tag = label;
        button.Content = new PathIcon { Data = Geometry.Parse(path), Width = 20, Height = 20 };
        button.Width = 46; button.Height = 46; button.Padding = new(10);
        button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        return button;
    }

}
