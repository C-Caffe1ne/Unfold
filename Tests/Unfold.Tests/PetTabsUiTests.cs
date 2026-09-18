using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetTabsUiTests
{
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Control root, string name) => Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(Window window, Control control, Point? point = null)
    {
        Layout(window);
        var position = control.TranslatePoint(point ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(position); window.MouseDown(position, MouseButton.Left); window.MouseUp(position, MouseButton.Left);
        Layout(window);
    }
    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 300 && !predicate(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(predicate());
    }

    [AvaloniaFact]
    public async Task WholeCardPreviewUsesPointerAndKeyboardWithoutInterceptingFileActions()
    {
        using var data = new DataScope(); var picks = 0;
        var owner = new Window { Width = 860, Height = 680 };
        using var view = new CustomPetView(owner, _ => Task.CompletedTask, chooseMedia: () =>
        { picks++; return Task.FromResult<string?>(CustomPetDraftTests.Fixture()); }, showHeader: false);
        owner.Content = Ui.PageFrame(owner, view); owner.Show(); Layout(owner);
        var idlePreview = Find<Button>(owner, "CustomPetPreview_idle");
        var stretchPreview = Find<Button>(owner, "CustomPetPreview_stretch");
        var idleClicks = 0; idlePreview.Click += (_, _) => idleClicks++;
        string Selected() => Find<TextBlock>(owner, "CustomPetPreviewAction").Text ?? "";
        try
        {
            foreach (var key in CustomPetDraft.Actions)
            {
                var preview = Find<Button>(owner, "CustomPetPreview_" + key);
                Assert.IsNotType<PathIcon>(preview.Content);
                Assert.Equal(2, Find<StackPanel>(owner, "CustomPetActions_" + key).Children.Count);
            }
            Click(owner, Find<Button>(owner, "CustomPetFile_idle"));
            await Until(() => !view.IsBusy && Selected().Contains("쉬는 모습"));
            Assert.Equal(1, picks); Assert.Equal(0, idleClicks);
            Click(owner, Find<Button>(owner, "CustomPetFile_stretch"));
            await Until(() => !view.IsBusy && Selected().Contains("스트레칭"));
            Assert.Equal(2, picks);
            Click(owner, Find<Border>(owner, "CustomPetSlot_idle"), new Point(14, 14));
            await Until(() => Selected().Contains("쉬는 모습"));
            Assert.Equal(1, idleClicks); Assert.Equal(2, picks);
            Assert.Equal(DesignSystem.Cream, Find<Border>(owner, "CustomPetSlot_idle").BorderBrush);
            Click(owner, Find<Border>(owner, "CustomPetSlot_attention"), new Point(14, 14));
            Assert.Contains("쉬는 모습", Selected()); Assert.Equal(2, picks);
            stretchPreview.Focus();
            owner.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            owner.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await Until(() => Selected().Contains("스트레칭"));
            idlePreview.Focus();
            owner.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            owner.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            await Until(() => Selected().Contains("쉬는 모습"));
            var beforeRemove = idleClicks;
            Click(owner, Find<Button>(owner, "CustomPetRemove_stretch"));
            Assert.Contains("쉬는 모습", Selected()); Assert.False(stretchPreview.IsEnabled);
            Assert.Equal(beforeRemove, idleClicks); Assert.Equal(2, picks);
            Click(owner, Find<Button>(owner, "CustomPetRemove_idle"));
            Assert.True(Find<TextBlock>(owner, "CustomPetPreviewHint").IsVisible);
            Assert.False(idlePreview.IsEnabled); Assert.Equal(beforeRemove, idleClicks);
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public void MinimumSettingsWindowShowsAllFiveCardsAndKeepsTheCreateActionPinned()
    {
        using var data = new DataScope(); using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        var window = new SettingsWindow(runtime) { Width = 860, Height = 680 };
        window.Show(); Layout(window); Press(window, "SettingsNavPacks"); Layout(window);
        var tabs = Find<TabControl>(window, "PetManagementTabs"); tabs.SelectedIndex = 1; Layout(window);
        try
        {
            foreach (var dimensions in new[] { new Size(860, 680), new Size(1120, 800) })
            {
                window.Width = dimensions.Width; window.Height = dimensions.Height; Layout(window);
                var slots = Find<WrapPanel>(window, "CustomPetActionSlots");
                var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
                Assert.Single(slots.Children.Select(card => Math.Round(card.Bounds.Y, 1)).Distinct());
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
                foreach (var key in CustomPetDraft.Actions)
                {
                    var add = Find<Button>(window, "CustomPetFile_" + key);
                    var location = add.TranslatePoint(default, scroll)!.Value;
                    Assert.InRange(location.Y, 0, scroll.Viewport.Height - add.Bounds.Height);
                    Assert.True(location.X + add.Bounds.Width <= scroll.Viewport.Width);
                }
                var geometry = slots.Children.Select(card => card.Bounds).ToArray();
                foreach (var key in CustomPetDraft.Actions)
                    Find<TextBlock>(window, "CustomPetLabel_" + key).Text = "2048 × 2048px\n512프레임 · 999.99초";
                Layout(window); Assert.Equal(geometry, slots.Children.Select(card => card.Bounds).ToArray());
                var create = Find<Button>(window, "CreateCustomPetPack");
                var before = create.TranslatePoint(default, window)!.Value;
                Assert.True(before.Y + create.Bounds.Height <= window.ClientSize.Height);
                scroll.ScrollToEnd(); Layout(window);
                Assert.Equal(before, create.TranslatePoint(default, window)!.Value);
                scroll.ScrollToHome();
            }
        }
        finally { window.HideToTray(); window.Dispose(); }
    }

    [AvaloniaFact]
    public async Task ImportedMetadataDoesNotResizeCardsAndSelectionSurvivesTabChanges()
    {
        using var data = new DataScope(); var owner = new Window { Width = 860, Height = 680 };
        using var view = new PetManagementView(owner, new CharacterLibrary(Path.Combine(data.Root, "library")),
            _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()), showPageHeaders: false);
        owner.Content = Ui.PageFrame(owner, view); owner.Show(); Layout(owner);
        var tabs = Find<TabControl>(owner, "PetManagementTabs"); tabs.SelectedIndex = 1; Layout(owner);
        try
        {
            Find<TextBox>(owner, "CustomPetName").Text = "카드 선택 유지";
            var card = Find<Border>(owner, "CustomPetSlot_idle"); var before = card.Bounds;
            Click(owner, Find<Button>(owner, "CustomPetFile_idle"));
            await Until(() => Find<Button>(owner, "CreateCustomPetPack").IsEnabled); Layout(owner);
            Assert.Equal(before, card.Bounds);
            var label = Find<TextBlock>(owner, "CustomPetLabel_idle").Text;
            Assert.Contains("프레임", label); Assert.Contains("초", label); Assert.Contains("px", label);
            Assert.DoesNotContain(".gif", label);
            tabs.SelectedIndex = 0; Layout(owner); tabs.SelectedIndex = 1; Layout(owner);
            Assert.Equal("카드 선택 유지", Find<TextBox>(owner, "CustomPetName").Text);
            Assert.Equal(DesignSystem.Cream, card.BorderBrush);
            Assert.Contains("쉬는 모습", Find<TextBlock>(owner, "CustomPetPreviewAction").Text);
        }
        finally { owner.Close(); }
    }

    private sealed class DataScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temp.Path;
        public DataScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
