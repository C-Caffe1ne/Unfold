using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
        window.GetVisualDescendants().OfType<Button>().Single(control => Equals(control.Content, text));
    private static void Press(Window window, string text) => Button(window, text).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Fits(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
        Assert.True(point.X >= 0 && point.Y >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 &&
            point.Y + control.Bounds.Height <= window.ClientSize.Height + 1, $"{window.Title}: {control.Name ?? control.GetType().Name} is clipped.");
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
            new PetPackWindow(new(temp.Path), _ => Task.CompletedTask),
            new BreakReminderWindow(new(BreakRoutines.All[0], "default-cat"), "모찌", [], true)
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
            Assert.Equal(DesignSystem.Cream, Button(editor, "내 루틴 저장").Background);
            editor.Close(); Layout(window);
            Press(window, "루틴 삭제"); Layout(window);
            var confirm = Assert.Single(window.OwnedWindows);
            Assert.Equal(DesignSystem.Shell, Find<Border>(confirm, "PageFrame").Background);
            Assert.Contains("danger", Button(confirm, "삭제").Classes);
            Assert.True(Button(confirm, "취소").IsFocused);
            Assert.False(Button(confirm, "삭제").IsDefault);
            confirm.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            await Task.Yield(); Layout(window);
            Assert.Empty(window.OwnedWindows); Assert.NotNull(settings.CustomRoutine);
            var prompt = Ui.Prompt(window, "이름 바꾸기", "내 이름"); Layout(window);
            var name = Assert.Single(window.OwnedWindows);
            Assert.Equal(DesignSystem.Shell, Find<Border>(name, "PageFrame").Background);
            Fits(name, Find<Border>(name, "PageActions"));
            name.Close(); Assert.Null(await prompt);
            var error = Ui.Error(window, new IOException("diagnostic")); Layout(window);
            var alert = Assert.Single(window.OwnedWindows);
            Fits(alert, Find<Border>(alert, "PageActions"));
            Assert.Equal(DesignSystem.Cream, Button(alert, "확인").Background);
            Press(alert, "확인"); await error;
        }
        finally { foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); window.Close(); }
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
            Assert.Equal(DesignSystem.Cream, primary.Background); Assert.Equal(DesignSystem.Ink, primary.Foreground);
            var point = primary.TranslatePoint(new(10, 10), window)!.Value;
            window.MouseMove(point); Layout(window);
            var presenter = primary.GetVisualDescendants().OfType<ContentPresenter>().Single(item => item.Name == "PART_ContentPresenter");
            Assert.Equal(DesignSystem.AccentHover, presenter.Background); Assert.Equal(DesignSystem.Ink, presenter.Foreground);
            window.MouseDown(point, MouseButton.Left); Layout(window);
            Assert.Equal(DesignSystem.Muted, presenter.Background);
            window.MouseUp(point, MouseButton.Left); window.MouseMove(new(1, 1));
            secondary.Focus(NavigationMethod.Tab); primary.Focus(NavigationMethod.Tab); Layout(window);
            Assert.Equal(DesignSystem.Ink, primary.BorderBrush);
            primary.IsEnabled = false; Layout(window);
            Assert.Equal(DesignSystem.Surface, presenter.Background); Assert.Equal(DesignSystem.Muted, presenter.Foreground);
            Assert.Equal(.55, primary.Opacity);
            Assert.True(Contrast(DesignSystem.Cream, DesignSystem.Ink) >= 7);
            Assert.True(Contrast(DesignSystem.Muted, DesignSystem.Surface) >= 4.5);
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
            Assert.Equal(DesignSystem.Cream, input.SelectionBrush);
            Assert.Equal(DesignSystem.Ink, input.SelectionForegroundBrush);
            var item = list.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.IsSelected);
            Assert.Equal(DesignSystem.Ink, item.Foreground);
            var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().Single(item => item.Name == "PART_ContentPresenter");
            Assert.Equal(DesignSystem.Cream, presenter.Background); Assert.Equal(DesignSystem.Ink, presenter.Foreground);
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
            window.MouseMove(name.TranslatePoint(new Point(12, 12), window)!.Value); Layout(window);
            Assert.Equal(nameAppearance, (ColorOf(name.Background), ColorOf(name.BorderBrush), name.BorderThickness, ColorOf(nameBorder.Background), ColorOf(nameBorder.BorderBrush)));
            name.Focus(NavigationMethod.Tab); Layout(window);
            Assert.True(name.IsFocused);
            Assert.Equal(nameAppearance, (ColorOf(name.Background), ColorOf(name.BorderBrush), name.BorderThickness, ColorOf(nameBorder.Background), ColorOf(nameBorder.BorderBrush)));
            var choiceBorder = choice.GetVisualDescendants().OfType<Border>().First();
            var choiceAppearance = (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush));
            window.MouseMove(choice.TranslatePoint(new Point(12, 12), window)!.Value); Layout(window);
            Assert.Equal(choiceAppearance, (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush)));
            choice.Focus(NavigationMethod.Tab); Layout(window);
            Assert.True(choice.IsFocused);
            Assert.Equal(choiceAppearance, (ColorOf(choice.Background), ColorOf(choice.BorderBrush), choice.BorderThickness, ColorOf(choiceBorder.Background), ColorOf(choiceBorder.BorderBrush)));
            number.IsEnabled = false; Layout(window);
            Assert.True(number.ClipToBounds);
            Assert.Equal(DesignSystem.ControlRadius, number.CornerRadius);
            Assert.Equal(new Thickness(1), number.BorderThickness);
            Assert.Equal(ColorOf(DesignSystem.Shell), ColorOf(number.Background));
            Assert.Equal(ColorOf(DesignSystem.Muted), ColorOf(number.BorderBrush));
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
