using Avalonia;
using PixelPoint = Avalonia.PixelPoint;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class NotificationLayoutTests
{
    [AvaloniaTheory]
    [InlineData(760, false)]
    [InlineData(468, true)]
    [InlineData(340, true)]
    public void NotificationEditorsAlignRightAndCompactVolumeFitsNarrowCards(double cardWidth, bool stacked)
    {
        using var scope = new Scope(); var window = scope.Window;
        var card = Find<Border>(window, "SettingsNotificationCard"); card.Width = cardWidth; Layout(window);
        var direction = Find<Slider>(window, "BubbleOpacityPercent");
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        var volumeControls = Find<Grid>(window, "ReminderVolumeControls");
        AssertRightAligned(Find<Grid>(window, "BubbleOpacityRow"), (Control)direction.Parent!);
        AssertRightAligned(Find<Grid>(window, "ReminderVolumeRow"), volumeControls);
        Assert.Equal(stacked ? 2 : 0, Grid.GetRow((Control)direction.Parent!));
        Assert.Equal(stacked ? 2 : 0, Grid.GetRow(volumeControls));
        Assert.InRange(volume.Bounds.Width, 200, 280);
        foreach (var name in new[] { "ReminderSoundVolumePercent", "CompletionSoundVolumePercent" })
            Assert.Equal(volume.Bounds.Width, Find<Slider>(window, name).Bounds.Width, 1);
        var icon = Find<SoundVolumeIcon>(window, "ReminderVolumeIcon");
        var sliderOrigin = volume.TranslatePoint(default, volumeControls)!.Value;
        var iconOrigin = icon.TranslatePoint(default, volumeControls)!.Value;
        Assert.True(iconOrigin.X >= 0);
        Assert.True(iconOrigin.X + icon.Bounds.Width <= sliderOrigin.X);
        foreach (var (rowName, buttonName) in new[] { ("DueSoundRow", "ResetDueSound"), ("CompletionSoundRow", "ResetCompletionSound") })
            AssertRightAligned(Find<Grid>(window, rowName), Find<Button>(window, buttonName));
        foreach (var control in card.GetVisualDescendants().OfType<Control>().Where(item =>
            item.IsEffectivelyVisible && item is Button or ComboBox or Slider))
        {
            var origin = control.TranslatePoint(default, card)!.Value;
            Assert.True(origin.X >= 0);
            Assert.True(origin.X + control.Bounds.Width <= card.Bounds.Width + 1);
        }
    }

    [AvaloniaFact]
    public void ResizingNotificationRowsPreservesAppliedValuesAndReturnsToTheWideLayout()
    {
        using var scope = new Scope(); var window = scope.Window;
        var card = Find<Border>(window, "SettingsNotificationCard");
        var direction = Find<Slider>(window, "BubbleOpacityPercent");
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        direction.Value = 70; volume.Value = 42;
        foreach (var width in new[] { 468, 760, 340, 760 })
        {
            card.Width = width; Layout(window);
            Assert.Equal(70, direction.Value); Assert.Equal(42, volume.Value);
            Assert.Equal(SoundVolumeGlyph.Low, Find<SoundVolumeIcon>(window, "ReminderVolumeIcon").Glyph);
            Assert.Equal(42, scope.Runtime.Settings.ReminderVolumePercent);
            Assert.Equal(width < 528 ? 2 : 0, Grid.GetRow((Control)direction.Parent!));
            AssertRightAligned(Find<Grid>(window, "BubbleOpacityRow"), (Control)direction.Parent!);
        }
        Assert.Equal(70, scope.Runtime.Settings.BubbleOpacityPercent);
        Assert.Equal(42, scope.Runtime.Settings.ReminderVolumePercent);
    }

    [AvaloniaFact]
    public void BubbleOpacityAndDialoguePersistAndResetWithoutPositionSelector()
    {
        using var scope = new Scope(); var window = scope.Window;
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "BubbleDirection");
        var opacity = Find<Slider>(window, "BubbleOpacityPercent");
        foreach (var value in new[] { 0, 37, 100 })
        {
            opacity.Value = value;
            Assert.Equal(value, scope.Runtime.Settings.BubbleOpacityPercent);
        }
        var input = Find<TextBox>(window, "InvitationDialogue");
        input.Text = "잠깐 같이 쉬어요"; Layout(window);
        Assert.Equal(input.Text, scope.Runtime.Settings.InvitationDialogue);
        input.Text = ""; Layout(window);
        Assert.Equal("잠깐 같이 쉬어요", scope.Runtime.Settings.InvitationDialogue);
        Find<Button>(window, "ResetPetDialogue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window);
        Assert.Equal(new AppSettings().InvitationDialogue, scope.Runtime.Settings.InvitationDialogue);
    }

    [Theory]
    [InlineData(-1920, -1800, BubbleDirection.Right)]
    [InlineData(-1920, -400, BubbleDirection.Left)]
    [InlineData(0, 20, BubbleDirection.Right)]
    [InlineData(0, 1700, BubbleDirection.Left)]
    public void AutomaticSideAndSurfaceRemainStableAcrossAllBubbleStates(int screenX, int petX, BubbleDirection expected)
    {
        var area = new PixelRect(screenX, 0, 1920, 1080); var anchor = new PixelPoint(petX, 700);
        var direction = PetBubbleLayout.AutomaticDirection(anchor, 1, area, 192);
        Assert.Equal(expected, direction);
        var sizes = new[] { (DesignSystem.SpeechHoverHeight, DesignSystem.SpeechHoverWidth),
            (DesignSystem.SpeechInvitationHeight, DesignSystem.SpeechBubbleWidth),
            (DesignSystem.SpeechRestingHeight, DesignSystem.SpeechBubbleWidth),
            (DesignSystem.SpeechCompletedHeight, DesignSystem.SpeechBubbleWidth) };
        var layouts = sizes.Select(size => PetBubbleLayout.CreateSurface(direction, anchor, 1, area, 192, size.Item1, size.Item2)).ToArray();
        foreach (var layout in layouts)
        {
            Assert.Equal(layouts[0].Size, layout.Size); Assert.Equal(layouts[0].Pet, layout.Pet);
            Assert.Equal(layouts[0].Position(anchor, 1, area), layout.Position(anchor, 1, area));
            Assert.Equal(anchor, layout.Position(anchor, 1, area) + new PixelPoint((int)layout.Pet.X, (int)layout.Pet.Y));
        }
    }

    [Fact]
    public void BubbleSettingsRoundTripAndCorruptFieldsRecoverIndependently()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { BubbleOpacityPercent = 0, InvitationDialogue = "함께 쉬어요", RestingDialogue = "잘 쉬고 있나요?" };
        settings.Save(path); var loaded = AppSettings.Load(path);
        Assert.Equal(0, loaded.BubbleOpacityPercent); Assert.Equal(settings.InvitationDialogue, loaded.InvitationDialogue);
        Assert.Equal(settings.RestingDialogue, loaded.RestingDialogue);
        File.WriteAllText(path, "{\"BubbleOpacityPercent\":101,\"InvitationDialogue\":null,\"IntervalMinutes\":33}");
        loaded = AppSettings.Load(path); Assert.Equal(100, loaded.BubbleOpacityPercent);
        Assert.Equal(new AppSettings().InvitationDialogue, loaded.InvitationDialogue); Assert.Equal(33, loaded.IntervalMinutes);
        Assert.Throws<InvalidDataException>(() => (settings with { BubbleOpacityPercent = -1 }).Save(path));
    }

    private static void AssertRightAligned(Control row, Control editor)
    {
        var origin = editor.TranslatePoint(default, row)!.Value;
        Assert.InRange(Math.Abs(row.Bounds.Width - origin.X - editor.Bounds.Width), 0, 1);
    }
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window);
            Find<Button>(Window, "SettingsNavSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(Window);
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
