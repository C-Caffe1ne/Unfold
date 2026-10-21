using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public sealed class PetMediaCleanupUiTests
{
    private const uint Green = 0xFF00B140, White = 0xFFFFFFFF;

    [AvaloniaFact]
    public async Task TheActionCardRemovesAndRestoresTheBackgroundOfItsFile()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        var pixels = Enumerable.Repeat(Green, 48 * 48).ToArray();
        for (var y = 12; y < 36; y++) for (var x = 12; x < 36; x++) pixels[y * 48 + x] = White;
        var file = Path.Combine(temp.Path, "screen.png"); File.WriteAllBytes(file, ImageCodec.EncodePng(new(48, 48, pixels)));
        var owner = new Window { Width = 860, Height = 800 };
        using var view = new CustomPetView(owner, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(file), showHeader: false);
        owner.Content = Ui.PageFrame(owner, view); owner.Show(); Layout(owner);
        try
        {
            var remove = Find<CheckBox>(owner, "CustomPetRemoveBackground_idle");
            Assert.False(remove.IsEnabled); // Nothing to clean before a file is added.
            Click(owner, Find<Button>(owner, "CustomPetFile_idle"));
            await Until(() => !view.IsBusy && view.Draft.Clips.ContainsKey("idle"));
            var original = view.Draft.Clips["idle"];
            Assert.True(remove.IsEnabled); Assert.False(Find<Control>(owner, "CustomPetTolerance_idle").IsEffectivelyVisible);

            remove.IsChecked = true; Layout(owner);
            await Until(() => !view.IsBusy && view.Draft.Processing("idle").RemoveBackground);
            Assert.Equal(Green, view.Draft.Processing("idle").KeyColor);
            Assert.Equal("#00B140", FindKeyText(owner));
            Assert.True(Find<Control>(owner, "CustomPetTolerance_idle").IsEffectivelyVisible);
            Assert.Equal(0u, view.Draft.Clips["idle"].LoadFrames()[0].Image.Pixels[0] >> 24);
            Assert.NotSame(original, view.Draft.Clips["idle"]);

            Find<CheckBox>(owner, "CustomPetAlignFeet_idle").IsChecked = true; Layout(owner);
            await Until(() => !view.IsBusy && view.Draft.Processing("idle").AlignFeet);
            Assert.True(view.Draft.Processing("idle").RemoveBackground);

            Find<Slider>(owner, "CustomPetTolerance_idle").Value = 10; await view.ApplyCleanup("idle");
            Assert.Equal(10, view.Draft.Processing("idle").Tolerance);

            remove.IsChecked = false; Layout(owner);
            await Until(() => !view.IsBusy && !view.Draft.Processing("idle").RemoveBackground);
            Find<CheckBox>(owner, "CustomPetAlignFeet_idle").IsChecked = false; Layout(owner);
            await Until(() => !view.IsBusy && !view.Draft.Processing("idle").IsActive);
            Assert.Same(original, view.Draft.Clips["idle"]);
        }
        finally { owner.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    private static string FindKeyText(Window owner) =>
        Find<Control>(owner, "CustomPetCleanup_idle").GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text?.StartsWith('#') == true).Text!;

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Window window, Control control)
    {
        Layout(window);
        var position = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(position); window.MouseDown(position, MouseButton.Left); window.MouseUp(position, MouseButton.Left);
        Layout(window);
    }
    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 500 && !predicate(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(predicate());
    }
}
