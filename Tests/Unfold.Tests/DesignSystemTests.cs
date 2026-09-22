using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class DesignSystemTests
{
    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static Button Button(Window window, string text) =>
        window.GetVisualDescendants().OfType<Button>().Single(control => Equals(control.Content, text) || AutomationProperties.GetName(control) == text);
    private static void Press(Window window, string text) => Button(window, text).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Fits(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0,
            $"{window.Title}: {control.Name ?? control.GetType().Name} ({(control as ContentControl)?.Content}) has no size.");
        Assert.True(point.X >= 0 && point.Y >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 &&
            point.Y + control.Bounds.Height <= window.ClientSize.Height + 1, $"{window.Title}: {control.Name ?? control.GetType().Name} is clipped.");
    }
    private static void IsAppModal(Window window)
    {
        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.Equal(Brushes.Transparent, window.Background);
        Assert.Contains(WindowTransparencyLevel.Transparent, window.TransparencyLevelHint);
        Assert.False(window.ShowInTaskbar);
        Assert.Contains("unfold-modal", window.Classes);
        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
        Assert.Equal(window.Height, window.MinHeight);
        Assert.Equal(window.Height, window.MaxHeight);
        Assert.True(window.Transitions is null || window.Transitions.Count == 0);
        Assert.Null(window.RenderTransform);
        foreach (var control in window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("unfold-action")))
        {
            Assert.True(control.Transitions is null || control.Transitions.Count == 0, $"{control.GetType().Name} still has motion transitions.");
            Assert.Null(control.RenderTransform);
        }
    }

    [AvaloniaFact]
    public void PageActionsRemainVisibleAtMinimumSizeAndWhileTheBodyScrolls()
    {
        using var temp = new TempDirectory();
        Window[] windows = [
            new RoutineEditorWindow(null, _ => Task.CompletedTask),
            new ProfileEditorWindow(new(), null, _ => Task.CompletedTask),
            new PersonalizationWindow(() => new(), _ => Task.CompletedTask),
            new BreakReviewWindow(new BreakHistory().Review),
            new PetPackWindow(new(temp.Path), _ => Task.CompletedTask)
        ];
        foreach (var window in windows)
        {
            try
            {
                if (window.CanResize) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
                window.Show(); Layout(window);
                Assert.Contains("unfold-page", window.Classes);
                Assert.Equal(DesignSystem.Shell, Find<Border>(window, "PageFrame").Background);
                var actions = Find<Border>(window, "PageActions"); Fits(window, actions);
                var original = actions.TranslatePoint(default, window);
                var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, window.Title);
                scroll.ScrollToEnd(); Layout(window);
                Assert.Equal(original, actions.TranslatePoint(default, window));
                foreach (var button in actions.GetVisualDescendants().OfType<Button>())
                { Fits(window, button); Assert.Contains("unfold-action", button.Classes); }
                if (window is PersonalizationWindow)
                {
                    var tabs = Find<TabControl>(window, "PersonalizationTabs");
                    foreach (var tab in new[] { 0, 1 })
                    {
                        tabs.SelectedIndex = tab; Layout(window);
                        foreach (var label in tab == 0 ? new[] { "새 루틴", "루틴 편집", "루틴 사용", "루틴 삭제" } :
                            new[] { "새 프로필", "프로필 편집", "프로필 적용", "프로필 삭제" })
                        {
                            var button = Button(window, label); Fits(window, button);
                            Assert.Contains(actions, button.GetVisualAncestors());
                        }
                    }
                }
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void TheBodyScrollBarReservesItsTrackInsteadOfCoveringPageControls()
    {
        using var temp = new TempDirectory();
        Window[] windows = [
            new RoutineEditorWindow(null, _ => Task.CompletedTask),
            new ProfileEditorWindow(new(), null, _ => Task.CompletedTask),
            new PersonalizationWindow(() => new(), _ => Task.CompletedTask),
            new BreakReviewWindow(new BreakHistory().Review),
            new PetPackWindow(new(temp.Path), _ => Task.CompletedTask)
        ];
        foreach (var window in windows)
        {
            try
            {
                if (window.CanResize) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
                window.Show(); Layout(window);
                PageBodyScrollGeometry.NeverOverlapsScrollBar(window);
                var scroll = PageBodyScrollGeometry.Scroll(window);
                scroll.ScrollToEnd(); Layout(window);
                PageBodyScrollGeometry.NeverOverlapsScrollBar(window);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void SettingsPetBuilderKeepsItsInputsAndButtonsClearOfTheBodyScrollBar()
    {
        using var scope = new SettingsScope(); var window = scope.Window;
        Find<Button>(window, "SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Layout(window);
        Find<TabControl>(window, "PetManagementTabs").SelectedIndex = 1; Layout(window);
        string[] controls = ["CustomPetName", "CustomPetFile_idle", "CustomPetRemove_idle",
            "CustomPetPreview_idle", "CustomPetFile_click"];
        foreach (var size in new[] { new Size(1120, 800), new Size(990, 740), new Size(860, 680) })
        {
            window.Width = size.Width; window.Height = size.Height; Layout(window);
            Assert.Equal(size.Width, window.ClientSize.Width, 0); Assert.Equal(size.Height, window.ClientSize.Height, 0);
            if (size.Width == 1120)
            {
                Assert.Equal(320, Find<TextBox>(window, "CustomPetName").Bounds.Width, 0);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "ImportPetMedia");
                var slotOrigins = CustomPetDraft.Actions.Select(key =>
                    Find<Border>(window, "CustomPetSlot_" + key).TranslatePoint(default, window)!.Value).ToArray();
                Assert.All(slotOrigins, origin => Assert.InRange(Math.Abs(origin.Y - slotOrigins[0].Y), 0, .5));
                for (var index = 1; index < slotOrigins.Length; index++) Assert.True(slotOrigins[index].X > slotOrigins[index - 1].X);
                var preview = Find<Border>(window, "CustomPetPreviewSurface");
                var previewOrigin = preview.TranslatePoint(default, window)!.Value;
                Assert.True(previewOrigin.Y + preview.Bounds.Height < slotOrigins[0].Y);
                var bodyScroll = Find<ScrollViewer>(window, "PageBodyScroll");
                var bodyOrigin = bodyScroll.TranslatePoint(default, window)!.Value;
                foreach (var key in CustomPetDraft.Actions)
                {
                    var remove = Find<Button>(window, "CustomPetRemove_" + key);
                    var removeOrigin = remove.TranslatePoint(default, window)!.Value;
                    Assert.True(removeOrigin.Y + remove.Bounds.Height <= bodyOrigin.Y + bodyScroll.Viewport.Height + .5,
                        $"{key} action controls are clipped in the default settings layout.");
                }
            }
            var pageScroll = PageBodyScrollGeometry.Scroll(window);
            if (pageScroll.Extent.Height > pageScroll.Viewport.Height + 1)
                PageBodyScrollGeometry.ClearsScrollBar(window, controls);
            else
                PageBodyScrollGeometry.KeepsFullWidthWithoutScrollBar(window);
            pageScroll.ScrollToEnd(); Layout(window);
            if (pageScroll.Extent.Height > pageScroll.Viewport.Height + 1)
                PageBodyScrollGeometry.ClearsScrollBar(window, controls);
            else
                PageBodyScrollGeometry.KeepsFullWidthWithoutScrollBar(window);
            pageScroll.ScrollToHome(); Layout(window);
        }
    }

    [AvaloniaFact]
    public async Task NestedEditorsAndDeleteConfirmationShareTheThemeAndCancelSafely()
    {
        using var profile = new ProfileScope();
        var settings = new AppSettings().SaveRoutine(new(BreakRoutines.CustomId, "나의 루틴", [new("쉬어 가요.", 20)]));
        var window = new PersonalizationWindow(() => settings, value => { settings = value; return Task.CompletedTask; });
        window.Show(); Layout(window);
        try
        {
            Press(window, "루틴 편집"); Layout(window);
            var editor = Assert.IsType<RoutineEditorWindow>(Assert.Single(window.OwnedWindows));
            Assert.Equal(DesignSystem.Shell, Find<Border>(editor, "PageFrame").Background);
            Assert.Equal(DesignSystem.Accent, Button(editor, "내 루틴 저장").Background);
            editor.Close(); Layout(window);
            Press(window, "루틴 삭제"); Layout(window);
            var confirm = Assert.Single(window.OwnedWindows);
            IsAppModal(confirm);
            Assert.Equal(DesignSystem.Shell, Find<Border>(confirm, "PageFrame").Background);
            Assert.Equal(Brushes.Transparent, Find<Border>(confirm, "ModalBody").Background);
            Assert.DoesNotContain(confirm.GetVisualDescendants().OfType<ScrollViewer>(), scroll => scroll.Name == "PageBodyScroll");
            Assert.Contains("danger", Button(confirm, "삭제").Classes);
            Assert.True(Button(confirm, "취소").IsFocused);
            Assert.False(Button(confirm, "삭제").IsDefault);
            confirm.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            await Task.Yield(); Layout(window);
            Assert.Empty(window.OwnedWindows); Assert.NotNull(settings.CustomRoutine);
            var prompt = Ui.Prompt(window, "이름 바꾸기", "내 이름"); Layout(window);
            var name = Assert.Single(window.OwnedWindows);
            IsAppModal(name);
            Assert.Equal(DesignSystem.Shell, Find<Border>(name, "PageFrame").Background);
            Fits(name, Find<Border>(name, "PageActions"));
            name.Close(); Assert.Null(await prompt);
            var error = Ui.Error(window, new IOException("diagnostic")); Layout(window);
            var alert = Assert.Single(window.OwnedWindows);
            IsAppModal(alert);
            Assert.Equal(Brushes.Transparent, Find<Border>(alert, "ModalBody").Background);
            Fits(alert, Find<Border>(alert, "PageActions"));
            Assert.Equal(DesignSystem.Accent, Button(alert, "확인").Background);
            Press(alert, "확인"); await error;
        }
        finally { foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); window.Close(); }
    }

    /// <summary>A drop shadow is painted outside the frame it belongs to, so a modal window has to keep
    /// transparent room for it. A blur that reached the window edge would be cut into a hard line
    /// instead of fading out, which is exactly the flat look the shadow is there to replace.</summary>
    private static void ShadowClearsTheWindowEdge(Window modal)
    {
        var frame = Find<Border>(modal, "PageFrame");
        Assert.True(frame.BoxShadow.Count > 0, $"{modal.Title}: the modal frame has no drop shadow.");
        var origin = frame.TranslatePoint(default, modal)!.Value;
        for (var index = 0; index < frame.BoxShadow.Count; index++)
        {
            var shadow = frame.BoxShadow[index];
            Assert.True(shadow.Color.A > 0, $"{modal.Title}: shadow layer {index} is fully transparent.");
            // A blur radius reaches half its length beyond the edge it is drawn from.
            var reach = shadow.Blur / 2 + shadow.Spread;
            Assert.True(origin.X + shadow.OffsetX - reach >= 0 && origin.Y + shadow.OffsetY - reach >= 0 &&
                origin.X + frame.Bounds.Width + shadow.OffsetX + reach <= modal.ClientSize.Width + .5 &&
                origin.Y + frame.Bounds.Height + shadow.OffsetY + reach <= modal.ClientSize.Height + .5,
                $"{modal.Title} {modal.ClientSize}: shadow layer {index} is clipped by the window edge.");
        }
    }

    [AvaloniaFact]
    public async Task SharedModalsAreRaisedByAShadowTheirWindowDoesNotClip()
    {
        using var profile = new ProfileScope();
        var owner = new Window { Width = 900, Height = 700, Title = "소유 창" };
        owner.Content = Ui.PageFrame(owner, Ui.Text("소유 창 본문"));
        owner.Show(); Layout(owner);
        try
        {
            // A page fills its own window, so it stays flat. Only the floating modal is raised.
            Assert.Equal(0, Find<Border>(owner, "PageFrame").BoxShadow.Count);
            var confirmTask = Ui.Confirm(owner, "삭제할까요?", "이 항목을 삭제할까요?", "삭제", "취소");
            Layout(owner);
            var confirm = Assert.Single(owner.OwnedWindows); Layout(confirm);
            ShadowClearsTheWindowEdge(confirm);
            // The shadow room is added to the window; the card keeps the width it was designed at.
            Assert.Equal(confirm.ClientSize.Width - 2 * DesignSystem.ModalShadowRoom,
                Find<Border>(confirm, "PageFrame").Bounds.Width, 1);
            Press(confirm, "취소"); Assert.Equal(1, await confirmTask);
            var promptTask = Ui.Prompt(owner, "이름 바꾸기", "내 이름");
            Layout(owner);
            var prompt = Assert.Single(owner.OwnedWindows); Layout(prompt);
            ShadowClearsTheWindowEdge(prompt); Fits(prompt, Find<Border>(prompt, "PageActions"));
            prompt.Close(); Assert.Null(await promptTask);
            var errorTask = Ui.Error(owner, new IOException("diagnostic"));
            Layout(owner);
            var alert = Assert.Single(owner.OwnedWindows); Layout(alert);
            ShadowClearsTheWindowEdge(alert);
            Press(alert, "확인"); await errorTask;
        }
        finally { foreach (var owned in owner.OwnedWindows.ToArray()) owned.Close(); owner.Close(); }
    }

    [AvaloniaFact]
    public void EveryPaletteKeepsTheModalShadowVisibleInsideItsRoom()
    {
        try
        {
            foreach (var palette in DesignSystem.Themes)
            {
                DesignSystem.ApplyTheme(palette.Id);
                var shadow = DesignSystem.ModalShadow;
                Assert.True(shadow.Count > 0, palette.Name);
                for (var index = 0; index < shadow.Count; index++)
                {
                    var layer = shadow[index];
                    // A shadow the eye cannot separate from the desktop leaves the modal looking flat.
                    Assert.InRange(layer.Color.A, (byte)1, (byte)255);
                    var reach = layer.OffsetY + layer.Blur / 2 + layer.Spread;
                    Assert.True(reach <= DesignSystem.ModalShadowRoom,
                        $"{palette.Name}: shadow layer {index} reaches {reach} past a {DesignSystem.ModalShadowRoom} gutter.");
                }
            }
        }
        finally { DesignSystem.ApplyTheme(AppTheme.OatLatte); }
    }

    [AvaloniaFact]
    public void DefaultOatTokensKeepSharedGeometry()
    {
        static Color ColorOf(IBrush brush) => ((ISolidColorBrush)brush).Color;
        DesignSystem.ApplyTheme(AppTheme.OatLatte);
        (IBrush Brush, string Hex)[] tokens =
        [
            (DesignSystem.Cream, "#342D28"), (DesignSystem.Muted, "#706154"),
            (DesignSystem.Surface, "#FFFCF7"), (DesignSystem.Raised, "#F0E5D8"),
            (DesignSystem.Accent, "#985139"), (DesignSystem.Ink, "#FFFFFF")
        ];
        foreach (var (brush, hex) in tokens) Assert.Equal(Color.Parse(hex), ColorOf(brush));
        Assert.Same(DesignSystem.OutlineSubtle, DesignSystem.Outline);
        Assert.Equal(new Thickness(1), DesignSystem.BorderSubtle);
        Assert.Equal(new Thickness(1), DesignSystem.BorderStrong);
        Assert.Equal(2, DesignSystem.FocusRingWidth);
        Assert.Equal(2, DesignSystem.FocusRingOffset);
    }

    [AvaloniaFact]
    public void ButtonStatesKeepPrimaryTextLegibleAndExposeKeyboardFocus()
    {
        var primary = Ui.Primary(Ui.Button("저장", () => { }));
        var secondary = Ui.Button("취소", () => { });
        var window = new Window { Width = 440, Height = 300 };
        window.Content = Ui.Page(window, "버튼 상태", "", Ui.Text("확인"), Ui.Actions(secondary, primary));
        window.Show(); Layout(window);
        try
        {
            Assert.Equal(DesignSystem.Accent, primary.Background); Assert.Equal(DesignSystem.Ink, primary.Foreground);
            var point = primary.TranslatePoint(new(10, 10), window)!.Value;
            window.MouseMove(point); Layout(window);
            var presenter = primary.GetVisualDescendants().OfType<ContentPresenter>().Single(item => item.Name == "PART_ContentPresenter");
            Assert.Equal(DesignSystem.AccentHover, presenter.Background); Assert.Equal(DesignSystem.Ink, presenter.Foreground);
            window.MouseDown(point, MouseButton.Left); Layout(window);
            Assert.Equal(DesignSystem.Accent, presenter.Background);
            window.MouseUp(point, MouseButton.Left); window.MouseMove(new(1, 1));
            secondary.Focus(NavigationMethod.Tab); primary.Focus(NavigationMethod.Tab); Layout(window);
            Assert.Equal(DesignSystem.Ink, primary.BorderBrush);
            primary.IsEnabled = false; Layout(window);
            Assert.Equal(DesignSystem.DisabledFill, presenter.Background); Assert.Equal(DesignSystem.DisabledText, presenter.Foreground);
            Assert.Equal(1, primary.Opacity);
            Assert.True(Contrast(DesignSystem.Accent, DesignSystem.Ink) >= 4.5);
            Assert.True(Contrast(DesignSystem.DisabledText, DesignSystem.DisabledFill) >= 3);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void SelectedInputsListsAndCheckmarksUseContrastingInk()
    {
        var input = new TextBox { Text = "선택한 이름" };
        var list = new ListBox { ItemsSource = new[] { "나의 휴식", "집중하는 시간" }, SelectedIndex = 0 };
        var check = new CheckBox { Content = "바탕화면에 펫 표시", IsChecked = true };
        var window = new Window { Width = 440, Height = 400 };
        window.Content = Ui.Page(window, "선택 상태", "", Ui.Column(input, list, check), Ui.Actions(Ui.Button("닫기", () => { })));
        window.Show(); Layout(window); input.Focus(); input.SelectAll();
        try
        {
            Assert.Equal(DesignSystem.Accent, input.SelectionBrush);
            Assert.Equal(DesignSystem.Ink, input.SelectionForegroundBrush);
            var item = list.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.IsSelected);
            Assert.Equal(DesignSystem.Ink, item.Foreground);
            var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().Single(item => item.Name == "PART_ContentPresenter");
            Assert.Equal(DesignSystem.Accent, presenter.Background); Assert.Equal(DesignSystem.Ink, presenter.Foreground);
            var glyph = check.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(item => item.Name == "CheckGlyph");
            Assert.Equal(DesignSystem.Ink, glyph.Fill);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void ValidationAndExportFeedbackRemainVisibleAbovePinnedActions()
    {
        var routine = new RoutineEditorWindow(null, _ => Task.CompletedTask);
        var profile = new ProfileEditorWindow(new(), null, _ => Task.CompletedTask);
        var review = new BreakReviewWindow(new BreakHistory().Review, exportReview: _ => throw new IOException("No space"));
        try
        {
            routine.Show(); Layout(routine); Find<TextBox>(routine, "RoutineName").Text = "";
            Press(routine, "내 루틴 저장"); Layout(routine); Fits(routine, Find<TextBlock>(routine, "RoutineError"));
            profile.Show(); Layout(profile); Find<TextBox>(profile, "ProfileName").Text = "";
            Press(profile, "프로필 저장"); Layout(profile); Fits(profile, Find<TextBlock>(profile, "ProfileError"));
            review.Width = review.MinWidth; review.Height = review.MinHeight; review.Show(); Layout(review);
            Press(review, "CSV 내보내기"); Layout(review);
            var message = Find<TextBlock>(review, "ReviewStatus"); Assert.Contains("내보내지 못했어요", message.Text); Fits(review, message);
        }
        finally { routine.Close(); profile.Close(); review.Close(); }
    }
    [AvaloniaFact]
    public void InputsKeepTheirAppearanceWhileHoveredAndEdited()
    {
        static string? ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color.ToString() : brush?.ToString();
        var number = new NumericUpDown { Value = 5, Minimum = 1, Maximum = 60, FormatString = "0", Width = 150 };
        var name = new TextBox { Text = "나의 휴식" };
        var choice = new ComboBox { ItemsSource = new[] { "기본 루틴", "나의 루틴" }, SelectedIndex = 0 };
        var window = new Window { Width = 340, Height = 400 };
        window.Content = Ui.Page(window, "알림 설정", "", Ui.Column(number, name, choice), Ui.Actions(Ui.Button("닫기", window.Close)));
        window.Show(); Layout(window);
        try
        {
            var editor = number.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.Equal(TextAlignment.Left, number.TextAlignment);
            Assert.Equal(TextAlignment.Left, editor.TextAlignment);
            Assert.Equal(VerticalAlignment.Center, number.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, editor.VerticalContentAlignment);
            var text = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
            var textPosition = text.TranslatePoint(default, number)!.Value;
            Assert.InRange(Math.Abs(textPosition.Y + text.Bounds.Height / 2 - number.Bounds.Height / 2), 0, 2);
            var border = editor.GetVisualDescendants().OfType<Border>().First(item => item.Name is "PART_BorderElement" or "PART_Border");
            var original = (editor.Background, editor.BorderBrush, editor.BorderThickness, border.Background, border.BorderBrush, border.BorderThickness);
            var numberAppearance = (ColorOf(number.Background), ColorOf(number.BorderBrush), number.BorderThickness);
            Assert.Equal(ColorOf(DesignSystem.OutlineStrong), ColorOf(number.BorderBrush));
            Assert.Equal(DesignSystem.BorderStrong, number.BorderThickness);
            window.MouseMove(editor.TranslatePoint(new Point(12, 12), window)!.Value); Layout(window);
            Assert.Equal(original, (editor.Background, editor.BorderBrush, editor.BorderThickness, border.Background, border.BorderBrush, border.BorderThickness));
            Assert.Equal(numberAppearance, (ColorOf(number.Background), ColorOf(number.BorderBrush), number.BorderThickness));
            editor.Focus(NavigationMethod.Tab); Layout(window);
            Assert.True(editor.IsFocused);
            Assert.Equal(original, (editor.Background, editor.BorderBrush, editor.BorderThickness, border.Background, border.BorderBrush, border.BorderThickness));
            Assert.Equal(numberAppearance, (ColorOf(number.Background), ColorOf(number.BorderBrush), number.BorderThickness));
            var spinnerButtons = number.GetVisualDescendants().OfType<RepeatButton>()
                .Where(item => item.Name is "PART_IncreaseButton" or "PART_DecreaseButton").ToDictionary(item => item.Name!);
            var increase = spinnerButtons["PART_IncreaseButton"];
            var decrease = spinnerButtons["PART_DecreaseButton"];
            var increasePosition = increase.TranslatePoint(default, number)!.Value;
            var decreasePosition = decrease.TranslatePoint(default, number)!.Value;
            Assert.True(decreasePosition.X > increasePosition.X);
            Assert.Equal(increasePosition.Y, decreasePosition.Y);
            Assert.InRange(decreasePosition.X + decrease.Bounds.Width, number.Bounds.Width - 1.5, number.Bounds.Width);
            Assert.Equal(new CornerRadius(0), increase.CornerRadius);
            Assert.Equal(new CornerRadius(0, DesignSystem.ControlRadius.TopRight, DesignSystem.ControlRadius.BottomRight, 0), decrease.CornerRadius);
            Assert.Equal(increase.CornerRadius, increase.GetVisualDescendants().OfType<ContentPresenter>().First().CornerRadius);
            var buttonFill = decrease.Background;
            var buttonSurface = decrease.GetVisualDescendants().OfType<ContentPresenter>().First();
            Assert.Equal(decrease.CornerRadius, buttonSurface.CornerRadius);
            var surfaceColor = ColorOf(buttonSurface.Background);
            window.MouseMove(decrease.TranslatePoint(new Point(8, 8), window)!.Value); Layout(window);
            Assert.Equal(buttonFill, decrease.Background);
            Assert.Equal(surfaceColor, ColorOf(buttonSurface.Background));
            Assert.Equal(decrease.CornerRadius, buttonSurface.CornerRadius);
            var nameBorder = name.GetVisualDescendants().OfType<Border>().First(item => item.Name is "PART_BorderElement" or "PART_Border");
            var nameAppearance = (ColorOf(name.Background), ColorOf(name.BorderBrush), name.BorderThickness, ColorOf(nameBorder.Background), ColorOf(nameBorder.BorderBrush));
            Assert.Equal(ColorOf(DesignSystem.OutlineStrong), ColorOf(name.BorderBrush));
            Assert.Equal(DesignSystem.BorderStrong, name.BorderThickness);
            window.MouseMove(name.TranslatePoint(new Point(12, 12), window)!.Value); Layout(window);
            Assert.Equal(nameAppearance, (ColorOf(name.Background), ColorOf(name.BorderBrush), name.BorderThickness, ColorOf(nameBorder.Background), ColorOf(nameBorder.BorderBrush)));
            name.Focus(NavigationMethod.Tab); Layout(window);
            Assert.True(name.IsFocused);
            Assert.Equal(nameAppearance, (ColorOf(name.Background), ColorOf(name.BorderBrush), name.BorderThickness, ColorOf(nameBorder.Background), ColorOf(nameBorder.BorderBrush)));
            var choiceBorder = choice.GetVisualDescendants().OfType<Border>().First();
            var choiceAppearance = (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush));
            Assert.Equal(ColorOf(DesignSystem.OutlineStrong), ColorOf(choice.BorderBrush));
            Assert.Equal(DesignSystem.BorderStrong, choice.BorderThickness);
            window.MouseMove(choice.TranslatePoint(new Point(12, 12), window)!.Value); Layout(window);
            Assert.Equal(choiceAppearance, (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush)));
            choice.Focus(NavigationMethod.Tab); Layout(window);
            Assert.True(choice.IsFocused);
            Assert.Equal(choiceAppearance, (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush)));
            number.IsEnabled = false; Layout(window);
            Assert.True(number.ClipToBounds);
            Assert.Equal(DesignSystem.ControlRadius, number.CornerRadius);
            Assert.Equal(DesignSystem.BorderStrong, number.BorderThickness);
            Assert.Equal(ColorOf(DesignSystem.Shell), ColorOf(number.Background));
            Assert.Equal(ColorOf(DesignSystem.OutlineStrong), ColorOf(number.BorderBrush));
            Assert.Equal(new CornerRadius(0), editor.CornerRadius);
            Assert.Equal(new CornerRadius(0), border.CornerRadius);
            Assert.Equal(ColorOf(Brushes.Transparent), ColorOf(editor.Background));
            Assert.Equal(ColorOf(Brushes.Transparent), ColorOf(editor.BorderBrush));
            Assert.Equal(ColorOf(Brushes.Transparent), ColorOf(border.Background));
            Assert.Equal(ColorOf(Brushes.Transparent), ColorOf(border.BorderBrush));
            foreach (var button in spinnerButtons.Values)
            {
                var surface = button.GetVisualDescendants().OfType<ContentPresenter>().First();
                Assert.Equal(ColorOf(DesignSystem.Shell), ColorOf(button.Background));
                Assert.Equal(ColorOf(DesignSystem.Shell), ColorOf(surface.Background));
                Assert.Equal(button.CornerRadius, surface.CornerRadius);
            }
        }
        finally { window.Close(); }
    }
    private sealed class SettingsScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private readonly AppRuntime runtime;
        public SettingsWindow Window { get; }
        public SettingsScope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            runtime = new(lifetime); Window = new(runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Window.HideToTray(); Window.Dispose(); runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
    private sealed class ProfileScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public ProfileScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
    private static double Contrast(IBrush first, IBrush second)
    {
        static double Luminance(IBrush brush)
        {
            var c = ((ISolidColorBrush)brush).Color;
            static double Linear(byte channel) { var s = channel / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}


/// <summary>
/// UI-04: the shared page body reserves the vertical scrollbar's track instead of letting
/// Fluent paint it over the content. These checks compare real coordinates — the right edge
/// of each control against the left edge of the scrollbar — not just visibility.
/// </summary>
internal static class PageBodyScrollGeometry
{
    internal static ScrollViewer Scroll(Window window) =>
        window.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PageBodyScroll");
    private static ScrollBar VerticalBar(ScrollViewer scroll) =>
        scroll.GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Vertical &&
            bar.GetVisualAncestors().OfType<ScrollViewer>().First() == scroll);
    private static double Right(Control control, Window window) =>
        control.TranslatePoint(default, window)!.Value.X + control.Bounds.Width;

    /// <summary>Right edge of the page's pinned actions: where the body ends when nothing scrolls.</summary>
    private static double PageRight(Window window)
    {
        var actions = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PageActions");
        return Right(actions, window);
    }

    /// <summary>The scrollbar is shown, it stays inside the page frame, and every named control stops a full gutter before its track.</summary>
    internal static void ClearsScrollBar(Window window, params string[] names)
    {
        var scroll = Scroll(window); var bar = VerticalBar(scroll); var body = (Control)scroll.Content!;
        Assert.True(bar.IsVisible && bar.Bounds.Width > 0, $"{window.Title} {window.ClientSize}: the body scrollbar is not laid out.");
        var barLeft = bar.TranslatePoint(default, window)!.Value.X;
        // The body pays for the native track and the gutter while the page actions stay pinned.
        Assert.Equal(bar.Bounds.Width + Ui.ScrollGutter, PageRight(window) - Right(body, window), 1);
        Assert.Equal(Ui.ScrollGutter, barLeft - Right(body, window), 1);
        // A track arranged outside the viewer would be clipped away instead of drawn.
        Assert.True(barLeft + bar.Bounds.Width <= Right(scroll, window) + .5,
            $"{window.Title} {window.ClientSize}: the track ends at {barLeft + bar.Bounds.Width} outside the viewer at {Right(scroll, window)}.");
        // A page hosted inside settings has no frame of its own; only check one when it is there.
        if (window.GetVisualDescendants().OfType<Border>().FirstOrDefault(control => control.Name == "PageFrame") is { } frame)
            Assert.True(barLeft + bar.Bounds.Width <= Right(frame, window) - frame.BorderThickness.Right + .5,
                $"{window.Title} {window.ClientSize}: the track reaches {barLeft + bar.Bounds.Width} past the page frame.");
        foreach (var name in names)
        {
            var control = window.GetVisualDescendants().OfType<Control>().Single(item => item.Name == name);
            Assert.True(control.IsVisible && control.Bounds.Width > 0, $"{window.Title}: {name} is not laid out.");
            Assert.True(Right(control, window) <= barLeft - Ui.ScrollGutter + .5,
                $"{window.Title} {window.ClientSize}: {name} ends at {Right(control, window)} but the scrollbar starts at {barLeft}.");
        }
    }

    /// <summary>Without a scrollbar the body ends with the pinned actions; the gutter is not a permanent inset.</summary>
    internal static void KeepsFullWidthWithoutScrollBar(Window window)
    {
        var scroll = Scroll(window); var body = (Control)scroll.Content!;
        Assert.False(VerticalBar(scroll).IsVisible, $"{window.Title} {window.ClientSize}: the body still scrolls.");
        Assert.Equal(PageRight(window), Right(body, window), 1);
    }

    /// <summary>Whichever way the page is sized, the body and the scrollbar never share pixels.</summary>
    internal static void NeverOverlapsScrollBar(Window window, params string[] names)
    {
        if (VerticalBar(Scroll(window)).IsVisible) ClearsScrollBar(window, names);
        else KeepsFullWidthWithoutScrollBar(window);
    }
}
