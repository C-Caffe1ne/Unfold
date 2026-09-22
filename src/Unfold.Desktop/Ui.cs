using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using Unfold.Core;

namespace Unfold.Desktop;

public static class Ui
{
    public static readonly IBrush Background = DesignSystem.Canvas, Panel = DesignSystem.Surface, Accent = DesignSystem.Cream;
    public static Button Button(string text, Action action)
    {
        var button = Action(text);
        button.Click += (_, _) => action(); return button;
    }
    public static Button AsyncButton(string text, Func<Task> action)
    {
        var button = Action(text);
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } finally { button.IsEnabled = true; } }; return button;
    }
    public static TextBlock Text(string text, double size = 14, IBrush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? DesignSystem.Cream, VerticalAlignment = VerticalAlignment.Center };
    public static StackPanel Row(params Control[] controls) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }.With(controls);
    public static StackPanel Column(params Control[] controls) => new StackPanel { Spacing = 12 }.With(controls);
    private static StackPanel With(this StackPanel panel, Control[] controls) { foreach (var control in controls) panel.Children.Add(control); return panel; }
    public static Button Action(string text) => new() { Content = text, Classes = { "unfold-action" } };
    public static Button Primary(Button button) { button.Classes.Add("primary"); return button; }
    public static Button Quiet(Button button) { button.Classes.Add("quiet"); return button; }
    public static Button Danger(Button button) { button.Classes.Add("danger"); return button; }
    public static TextBlock Caption(string text) => new() { Text = text, FontSize = DesignSystem.Caption,
        Foreground = DesignSystem.Muted, TextWrapping = TextWrapping.Wrap };
    public static Border Card(Control child, double padding = 16) => new() { Background = DesignSystem.Surface,
        CornerRadius = DesignSystem.CardRadius, Padding = new(padding), Child = child };
    public static StackPanel Field(string label, Control input)
    {
        var caption = Caption(label);
        Avalonia.Automation.AutomationProperties.SetLabeledBy(input, caption);
        KeyboardFocusLabel(input, caption);
        return new StackPanel { Spacing = 6 }.With([caption, input]);
    }
    public static void KeyboardFocusLabel(Control input, TextBlock caption)
    {
        var text = caption.Text; var brush = caption.Foreground;
        input.GotFocus += (_, args) =>
        {
            var keyboard = args.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
            caption.Text = keyboard ? text + " · 선택" : text;
            caption.Foreground = keyboard ? DesignSystem.FocusRing : brush;
        };
        input.LostFocus += (_, _) => { caption.Text = text; caption.Foreground = brush; };
    }
    public static WrapPanel Actions(params Control[] controls)
    {
        var panel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Orientation = Orientation.Horizontal,
            ItemSpacing = DesignSystem.Space, LineSpacing = DesignSystem.Space };
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }
    /// <summary>Clear space kept between the page body and the vertical scrollbar track.</summary>
    public const double ScrollGutter = DesignSystem.Space;
    // Fluent paints the vertical scrollbar on top of the scrolled content, so inputs and buttons
    // that stretch to the right edge end up underneath it. Turning auto-hide off reserves the
    // track in the template; the left margin then keeps a full gutter between that track and the
    // body. This keeps the native scrollbar drawable on every platform instead of moving it into
    // a clipped page-frame inset.
    public static ScrollViewer PageBodyScroll(Control body)
    {
        var scroll = new ScrollViewer { Name = "PageBodyScroll", Content = body, AllowAutoHide = false,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        scroll.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Avalonia.Controls.Primitives.ScrollBar>("PART_VerticalScrollBar") is { } bar)
                bar.Margin = new Thickness(ScrollGutter, 0, 0, 0);
        };
        return scroll;
    }
    public static Decorator CenteredBody(Control content, double maxWidth, string name)
    {
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new CenteredBodyDecorator(maxWidth) { Name = name, Child = content };
    }
    private sealed class CenteredBodyDecorator(double contentMaxWidth) : Decorator
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, contentMaxWidth) : contentMaxWidth;
            Child?.Measure(new Size(width, availableSize.Height));
            var desired = Child?.DesiredSize ?? default;
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : Math.Min(desired.Width, contentMaxWidth), desired.Height);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = Math.Min(finalSize.Width, contentMaxWidth);
            Child?.Arrange(new Rect((finalSize.Width - width) / 2, 0, width, finalSize.Height));
            return finalSize;
        }
    }
    public static Control PageContent(string title, string description, Control body, Control footer,
        string section = "나의 휴식", bool showHeader = true)
    {
        var scroll = PageBodyScroll(body);
        var layout = new Grid { RowDefinitions = showHeader ? new("Auto,16,*,16,Auto") : new("*,16,Auto") };
        if (showHeader)
        {
            var heading = Text(title, DesignSystem.Title); heading.FontWeight = FontWeight.SemiBold; heading.TextWrapping = TextWrapping.Wrap;
            var header = Column(Caption("UNFOLD / " + section), heading); header.Name = "PageHeader";
            header.Spacing = 6;
            if (description.Length > 0) header.Children.Add(Caption(description));
            layout.Children.Add(header); Grid.SetRow(scroll, 2);
        }
        layout.Children.Add(scroll);
        var actionBar = new Border { Name = "PageActions", BorderBrush = DesignSystem.Outline,
            BorderThickness = new(0, 1, 0, 0), Padding = new(0, 12, 0, 0), Child = footer };
        Grid.SetRow(actionBar, showHeader ? 4 : 2); layout.Children.Add(actionBar);
        return layout;
    }
    public static Border PageFrame(Window window, Control content, double inset = DesignSystem.Inset)
    {
        window.Classes.Add("unfold-page");
        return new Border { Name = "PageFrame", Margin = new(DesignSystem.FrameMargin), Padding = new(inset),
            Background = DesignSystem.Shell, BorderBrush = DesignSystem.Outline, BorderThickness = new(1),
            CornerRadius = DesignSystem.FrameRadius, Child = content };
    }
    public static Control Page(Window window, string title, string description, Control body, Control footer,
        string section = "나의 휴식", double inset = DesignSystem.Inset) =>
        PageFrame(window, PageContent(title, description, body, footer, section), inset);
    private static Window ModalWindow(string title, double width)
    {
        // The frame keeps its designed footprint and the window adds the shadow room on both sides,
        // so the drop shadow falls onto transparent pixels instead of being cut at the window edge.
        var outer = width + 2 * (DesignSystem.ModalShadowRoom - DesignSystem.FrameMargin);
        var dialog = new Window
        {
            Title = title,
            Width = outer,
            MinWidth = outer,
            MaxWidth = outer,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            ShowInTaskbar = false,
            Transitions = null,
            RenderTransform = null
        };
        dialog.Classes.Add("unfold-modal");
        DisableAutomaticWindowAnimation(dialog);
        return dialog;
    }
    private static void DisableAutomaticWindowAnimation(Window dialog)
    {
        if (!OperatingSystem.IsMacOS() || dialog.TryGetPlatformHandle()?.Handle is not nint handle || handle == 0) return;
        // AppKit otherwise infers an order-front animation for this borderless NSWindow. That
        // briefly scales the confirmation surface even though Avalonia transitions are disabled.
        ObjcMsgSend(handle, SelRegisterName("setAnimationBehavior:"), 2);
    }
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern nint SelRegisterName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void ObjcMsgSend(nint receiver, nint selector, nint value);
    private static Button ModalButton(Button button)
    {
        button.Transitions = null; button.RenderTransform = null;
        return button;
    }
    private static void LockModalSize(Window dialog)
    {
        if (dialog.Content is not Control content) return;
        content.Measure(new Size(dialog.Width, double.PositiveInfinity));
        dialog.Height = dialog.MinHeight = dialog.MaxHeight = Math.Ceiling(content.DesiredSize.Height);
        dialog.SizeToContent = SizeToContent.Manual;
    }
    private static Border ModalBody(Control child)
    {
        var body = Card(child); body.Name = "ModalBody"; body.Background = Brushes.Transparent;
        return body;
    }
    private static Control ModalPage(Window window, string title, string section, Control body, Control footer)
    {
        var heading = Text(title, DesignSystem.Title); heading.FontWeight = FontWeight.SemiBold; heading.TextWrapping = TextWrapping.Wrap;
        var header = Column(Caption("UNFOLD / " + section), heading); header.Name = "PageHeader"; header.Spacing = 6;
        var layout = new Grid { RowDefinitions = new("Auto,16,Auto,16,Auto") };
        layout.Children.Add(header); Grid.SetRow(body, 2); layout.Children.Add(body);
        var actionBar = new Border { Name = "PageActions", BorderBrush = DesignSystem.Outline,
            BorderThickness = new(0, 1, 0, 0), Padding = new(0, 12, 0, 0), Child = footer };
        Grid.SetRow(actionBar, 4); layout.Children.Add(actionBar);
        // A modal floats over the owner window rather than sitting in a page, so it carries an
        // elevation shadow. The wider margin is the transparent room that shadow is drawn into.
        var frame = PageFrame(window, layout);
        frame.Margin = new(DesignSystem.ModalShadowRoom);
        frame.BoxShadow = DesignSystem.ModalShadow;
        return frame;
    }
    public static unsafe Bitmap Bitmap(PixelImage image)
    {
        // Transfer raw pixels once; avoid PNG encoding/decoding on every
        // thumbnail edit and on every animation consumer's first load.
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var locked = bitmap.Lock();
        for (var y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(new Span<uint>((byte*)locked.Address + y * locked.RowBytes, image.Width));
        return bitmap;
    }

    public static async Task<int> Confirm(Window owner, string title, string message, params string[] choices)
    {
        var dialog = ModalWindow(title, 460);
        var result = -1;
        var buttons = choices.Select((choice, index) =>
        {
            var button = ModalButton(Button(choice, () => { result = index; dialog.Close(); }));
            if (choice is "취소" or "Cancel") { button.IsCancel = true; Quiet(button); }
            if (choice.Contains("삭제") || choice is "버리기" or "Delete" or "Discard" or "Crop") Danger(button);
            else if (index == 0) Primary(button);
            if (choices.Length == 1) button.IsDefault = true;
            return button;
        }).ToArray();
        var messageText = Text(message); messageText.TextWrapping = TextWrapping.Wrap;
        dialog.Content = ModalPage(dialog, title, "확인", ModalBody(messageText), Actions(buttons));
        dialog.Opened += (_, _) => (buttons.FirstOrDefault(button => button.IsCancel) ?? buttons.FirstOrDefault())?.Focus();
        LockModalSize(dialog);
        // Showing a modal while the originating pointer event is still unwinding can make macOS
        // activate the newly disabled owner and then the dialog again, which appears as a bounce.
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        // Closing the dialog without choosing an action must always cancel, never select Save/Delete.
        await dialog.ShowDialog(owner); return result;
    }
    public static Task Error(Window owner, Exception error)
    {
        AppPaths.Log(error); return Confirm(owner, "Unfold", ErrorText(error), "확인");
    }
    // Package errors also serve the command-line tools. Localize their known recovery
    // cases here; unexpected platform/decoder details stay in the diagnostic log.
    public static string ErrorText(Exception error) => error.Message switch
    {
        "A newer version is installed. Choose the same or a newer pack." => "더 최신 버전이 설치되어 있어요. 같은 버전이나 더 최신 팩을 선택해 주세요.",
        "This version contains different files. A changed pack needs a new content version." => "설치된 팩과 버전은 같지만 내용이 달라요. 제작자가 버전을 올린 팩을 선택해 주세요.",
        "This ID belongs to a built-in companion." => "기본 펫을 이 팩으로 교체할 수 없어요.",
        "This ID belongs to existing artwork. It cannot be replaced by a pet pack." => "이미 저장된 캐릭터와 충돌해요. 다른 펫 팩을 선택해 주세요.",
        "A companion already uses this ID with different casing." => "같은 식별자를 쓰는 펫이 이미 있어요. 다른 펫 팩을 선택해 주세요.",
        "The preview files changed. Reopen the original pack." or
        "The installed companion changed. Reopen the pack preview." or
        "The installed companion changed during validation." => "확인하는 동안 펫 파일이 바뀌었어요. 원본 팩을 다시 열어 주세요.",
        "Pet pack exceeds its file or total size limit." or
        "Pet pack exceeds 16 clips or its decoded sprite budget." or
        "Total decoded pet clips exceed 128 MiB." => "펫 팩이 허용된 크기를 초과했어요. 크기를 줄인 팩이 필요해요.",
        _ => error switch
        {
            UnauthorizedAccessException => "파일에 접근할 권한이 없어요. 파일과 저장 위치의 권한을 확인해 주세요.",
            FileNotFoundException or DirectoryNotFoundException => "파일을 찾지 못했어요. 파일의 위치를 확인해 주세요.",
            InvalidDataException or System.Text.Json.JsonException => "파일 형식이나 내용이 올바르지 않아요. 원본 파일을 확인해 주세요.",
            IOException => "파일을 읽거나 저장하지 못했어요. 저장 위치와 여유 공간을 확인해 주세요.",
            PlatformNotSupportedException => "이 환경에서는 해당 기능을 사용할 수 없어요.",
            _ => "작업을 완료하지 못했어요. 다시 시도해 주세요. 문제가 계속되면 앱을 다시 실행해 주세요."
        }
    };
    public static async Task<string?> Prompt(Window owner, string title, string value)
    {
        var input = new TextBox { Text = value, MinWidth = 280 };
        var dialog = ModalWindow(title, 420);
        var cancel = Quiet(ModalButton(Button("취소", () => dialog.Close()))); cancel.IsCancel = true;
        var save = Primary(ModalButton(Button("저장", () => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.Close(input.Text.Trim()); }))); save.IsDefault = true;
        dialog.Content = ModalPage(dialog, title, "이름 편집", Field("이름", input), Actions(cancel, save));
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        LockModalSize(dialog);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        return await dialog.ShowDialog<string?>(owner);
    }
}
