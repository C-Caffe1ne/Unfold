using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Svg;
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
    private string selectedPage = "dashboard";
    private TextBlock? accountEmail;
    private Image? accountGoogleIcon;
    private Button? accountSignOut;

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
        var content = new Border { Name = "SettingsFrame", Padding = new(31, 8, 31, 31), Child = frame };
        // Fill the client area; the OS owns the outer frame and its corner shape.
        var surface = new Border { Name = "SettingsWindowSurface", Background = DesignSystem.Canvas, Child = BuildNativeWindowContent(content) };
        return surface;
    }

    private Border BuildNavigation()
    {
        var rail = new Grid { Name = "SettingsNavigationRail", RowDefinitions = new("*,Auto"), Margin = new(7, 12) };
        var timer = Nav("SettingsNavTimer", "홈", "home", OpenDashboard);
        var settings = Nav("SettingsNavSettings", "설정", "settings", OpenPreferences);
        var review = Nav("SettingsNavReview", "기록", "history", OpenReview);
        var pets = Nav("SettingsNavPacks", "펫 관리", "pet-add", OpenPetPacks);
        navigationItems["pets"] = pets;
        navigationItems["dashboard"] = timer; navigationItems["settings"] = settings;
        navigationItems["review"] = review;
        SelectNavigation("dashboard");
        var links = Ui.Column(timer, pets, review, BuildThemeButton()); links.Name = "SettingsNavigationLinks";
        links.Spacing = 12; rail.Children.Add(links);
        var quit = Ui.Danger(Nav("SettingsQuit", "종료", "exit", runtime.Quit));
        var version = new TextBlock { Name = "SidebarVersion", Text = AppRelease.DisplayVersion.Replace(" ", "\n"),
            FontSize = 10, Foreground = Muted, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch, TextTrimming = TextTrimming.CharacterEllipsis };
        AutomationProperties.SetName(version, AppRelease.DisplayVersion);
        ToolTip.SetTip(version, AppRelease.DisplayVersion);
        var bottom = Ui.Column(settings, quit, version); bottom.Spacing = 12;
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
        countdown.FontSize = 64; countdown.Foreground = Cream; countdown.FontWeight = FontWeight.Normal;
        countdown.LineHeight = 74;
        countdown.HorizontalAlignment = HorizontalAlignment.Center; countdown.VerticalAlignment = VerticalAlignment.Center;
        state.Foreground = Muted; state.FontSize = Caption;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(Label("다음 휴식까지", Body, Cream)); Grid.SetColumn(intervalHint, 1); header.Children.Add(intervalHint);
        var statusContent = new Grid { ColumnDefinitions = new("Auto,7,*") };
        statusContent.Children.Add(timerStateDot); Grid.SetColumn(state, 2); statusContent.Children.Add(state);
        timerStateBadge.Child = statusContent; timerStateBadge.HorizontalAlignment = HorizontalAlignment.Center;
        timerStateBadge.VerticalAlignment = VerticalAlignment.Center;
        timerControls.HorizontalAlignment = HorizontalAlignment.Center;
        var body = new Grid { RowDefinitions = new("Auto,8,Auto,*,Auto"), Margin = new(Inset) };
        body.Children.Add(header); Grid.SetRow(timerStateBadge, 2); body.Children.Add(timerStateBadge);
        Grid.SetRow(countdown, 3); body.Children.Add(countdown);
        Grid.SetRow(timerControls, 4); body.Children.Add(timerControls);
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
        PreferencesEdited();
        return Task.CompletedTask;
    }
    private Task OpenReview()
    {
        reviewPage ??= new BreakReviewView(this, runtime.BreakHistory.Review,
            () => runtime.BreakHistoryError, showHeader: false);
        reviewPage.Refresh(); ShowPage("review", reviewPage);
        return Task.CompletedTask;
    }
    private Task OpenPetPacks()
    {
        petPage ??= new PetManagementView(this, runtime.Library, runtime.SelectInstalledCharacter,
            showPageHeaders: false, removed: runtime.RefreshAfterCharacterRemoval);
        ShowPage("pets", petPage);
        return Task.CompletedTask;
    }

    private Control BuildPreferencesPage()
    {
        preferencesSections = Ui.Column(BuildNotificationSettingsCard(), BuildTimerSettingsCard(), BuildAppBehaviorCard(), BuildAppInfoCard(), BuildAccountCard(), BuildDebugSettingsCard());
        preferencesSections.Name = "SettingsPreferencesSections"; preferencesSections.Spacing = Inset;
        var body = Ui.CenteredBody(preferencesSections, SettingsContentWidth, "SettingsPreferencesBody");
        var scroll = Ui.PageBodyScroll(body); scroll.Name = "SettingsPreferencesScroll";
        var page = new Grid { Name = "SettingsPreferencesPage", RowDefinitions = new("*,Auto") };
        page.Children.Add(scroll);
        preferencesStatus.Margin = new(0, 24, 0, 0);
        Grid.SetRow(preferencesStatus, 1); page.Children.Add(preferencesStatus);
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

    private Border BuildAppInfoCard()
    {
        var version = SettingsLabel(AppRelease.DisplayVersion); version.Name = "SettingsAppVersion";
        var update = Ui.Button("업데이트 확인", runtime.ShowUpdates); update.Name = "SettingsCheckUpdates";
        update.Height = SettingsControlHeight;
        var row = new Grid { ColumnDefinitions = new("*,16,Auto") };
        row.Children.Add(version); Grid.SetColumn(update, 2); row.Children.Add(update);
        var body = Ui.Column(SettingsHeading("앱 정보"), row); body.Spacing = Inset; body.Margin = new(Inset);
        return Card("SettingsAppInfoCard", body, Surface, new(28));
    }

    private Border BuildAccountCard()
    {
        var copy = runtime.AccountContent.Copy;
        accountSignOut = Ui.Danger(Ui.Quiet(Ui.Action(copy.SignOutButton)));
        accountSignOut.Click += async (_, _) => { await runtime.SignOut(); RefreshAccount(); };
        accountSignOut.Name = "SignOutAccount"; accountSignOut.Width = SettingsActionWidth; accountSignOut.Height = SettingsControlHeight;
        accountEmail = SettingsLabel(""); accountEmail.Name = "SettingsAccountEmail";
        accountEmail.TextWrapping = TextWrapping.NoWrap; accountEmail.TextTrimming = TextTrimming.CharacterEllipsis;
        accountGoogleIcon = new Image { Name = "SettingsAccountGoogleIcon", Width = 24, Height = 24, Stretch = Stretch.Uniform,
            Source = new SvgImage { Source = SvgSource.Load("avares://Unfold/Assets/Icons/Google/google.svg", null) } };
        var identity = new Grid { Name = "SettingsAccountIdentity", ColumnDefinitions = new("Auto,10,*"), VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(accountGoogleIcon); Grid.SetColumn(accountEmail, 2); identity.Children.Add(accountEmail);
        var body = new Grid { ColumnDefinitions = new("*,16,Auto"), RowDefinitions = new("Auto,16,Auto"), Margin = new(Inset) };
        var heading = SettingsHeading(copy.AccountSection); body.Children.Add(heading); Grid.SetColumnSpan(heading, 3);
        Grid.SetRow(identity, 2); body.Children.Add(identity);
        Grid.SetRow(accountSignOut, 2); Grid.SetColumn(accountSignOut, 2); body.Children.Add(accountSignOut);
        RefreshAccount();
        return Card("SettingsAccountCard", body, Surface, new(28));
    }

    private void RefreshAccount()
    {
        if (accountEmail is null || accountSignOut is null || accountGoogleIcon is null) return;
        var session = runtime.AccountSession;
        accountEmail.Text = session is null ? runtime.AccountContent.Copy.SignedOutLabel : session.Email ?? runtime.AccountContent.Copy.SignedInLabel;
        ToolTip.SetTip(accountEmail, accountEmail.Text);
        accountGoogleIcon.IsVisible = session is not null;
        accountSignOut.IsEnabled = !runtime.AccountSignOutPending;
    }

    private void ShowPage(string key, Control page)
    {
        var keyboard = FocusManager?.GetFocusedElement() is Control focused && focused.Classes.Contains(":focus-visible");
        if (key != "settings") { stopSoundPreview?.Invoke(); runtime.CloseReminderPreview(); }
        settingsPageHost.Content = page; SelectNavigation(key);
        if (key == "dashboard") ResumePreview(); else SuspendPreview();
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible || settingsPageHost.Content != page) return;
            var controls = page.GetVisualDescendants().OfType<Control>();
            Control? target = key switch
            {
                "dashboard" => timerControls.Children.OfType<Button>().First(),
                "settings" => controls.FirstOrDefault(control => control.Name == "BubbleOpacityPercent"),
                "pets" => controls.OfType<TextBox>().FirstOrDefault(input =>
                    input.Name is "CustomPetName" or "GlbPetName" && input.IsEnabled && input.IsEffectivelyVisible),
                _ => controls.OfType<Button>().FirstOrDefault(button => button.IsEnabled && button.IsEffectivelyVisible)
            };
            if (target is not null && target.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified))
                target.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
    private void SelectNavigation(string key)
    {
        selectedPage = key;
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
    private static Button Nav(string name, string label, string icon, Func<Task> action)
    {
        var button = ActionButton(label, action); button.Name = name;
        button.Classes.Add("navigation");
        button.Tag = label;
        button.Content = new NavigationIcon(icon);
        button.Width = 46; button.Height = 46; button.Padding = new(9);
        button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        return button;
    }

}
