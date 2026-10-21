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
public sealed class PetActionFilesUiTests
{
    [AvaloniaFact]
    public async Task AnActionCardAddsOrdersCleansAndRemovesExtraFiles()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        var picks = new Queue<string>();
        string Still(string name, uint color)
        {
            var pixels = Enumerable.Repeat(0xFF00B140u, 40 * 40).ToArray();
            for (var y = 10; y < 30; y++) for (var x = 10; x < 30; x++) pixels[y * 40 + x] = color;
            var path = Path.Combine(temp.Path, name); File.WriteAllBytes(path, ImageCodec.EncodePng(new(40, 40, pixels))); return path;
        }
        foreach (var (name, color) in new[] { ("a.png", 0xFF111111u), ("b.png", 0xFF222222u), ("c.png", 0xFF333333u), ("d.png", 0xFF444444u) })
            picks.Enqueue(Still(name, color));
        var owner = new Window { Width = 860, Height = 900 };
        using var view = new CustomPetView(owner, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(picks.Dequeue()), showHeader: false);
        owner.Content = Ui.PageFrame(owner, view); owner.Show(); Layout(owner);
        try
        {
            var add = Find<Button>(owner, "CustomPetAddFile_idle");
            Assert.False(add.IsEnabled); Assert.False(Find<Control>(owner, "CustomPetOrder_idle").IsVisible);
            Click(owner, Find<Button>(owner, "CustomPetFile_idle"));
            await Until(() => !view.IsBusy && view.Draft.Files("idle").Count == 1);
            Assert.True(add.IsEnabled);

            Click(owner, add);
            await Until(() => !view.IsBusy && view.Draft.Files("idle").Count == 2);
            Assert.Equal("b.png", view.Draft.Files("idle")[1].FileName);
            Assert.True(Find<Control>(owner, "CustomPetOrder_idle").IsVisible);
            Assert.NotNull(Find<Button>(owner, "CustomPetRemoveFile_idle_1"));
            Find<RadioButton>(owner, "CustomPetOrderSequence_idle").IsChecked = true; Layout(owner);
            Assert.Equal("sequence", view.Draft.Order("idle"));

            Find<CheckBox>(owner, "CustomPetRemoveBackground_idle").IsChecked = true; Layout(owner);
            await Until(() => !view.IsBusy && view.Draft.Processing("idle").RemoveBackground);
            Assert.All(view.Draft.Files("idle"), file => Assert.Equal(0u, file.LoadFrames()[0].Image.Pixels[0] >> 24));
            Assert.Equal("b.png", view.Draft.ProcessingSource("idle", 1)!.FileName);

            Click(owner, add);
            await Until(() => !view.IsBusy && view.Draft.Files("idle").Count == 3);
            Assert.Equal(0u, view.Draft.Files("idle")[2].LoadFrames()[0].Image.Pixels[0] >> 24); // Joins the action's cleanup.
            Click(owner, add);
            await Until(() => !view.IsBusy && view.Draft.Files("idle").Count == 4);
            Assert.False(add.IsEnabled);

            Find<Button>(owner, "CustomPetRemoveFile_idle_1").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Layout(owner);
            Assert.Equal(["a.png", "c.png", "d.png"], Enumerable.Range(0, 3).Select(index => view.Draft.ProcessingSource("idle", index)!.FileName));
            Assert.True(add.IsEnabled);
        }
        finally { owner.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaFact]
    public async Task TheRunningPetPlaysEachFileOfAnActionInTurn()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime();
        var runtime = new AppRuntime(lifetime);
        try
        {
            ImportedPetClip Still(uint color) => ImportedPetClip.FromImage("still.png",
                ImageCodec.EncodePng(new(8, 8, Enumerable.Repeat(color, 64).ToArray())));
            var draft = new CustomPetDraft();
            draft.SetClip("idle", Still(0xFF101010));
            draft.SetClip("click", Still(0xFF111111)); draft.AddFile("click", Still(0xFF222222)); draft.AddFile("click", Still(0xFF333333));
            draft.SetOrder("click", "sequence");
            var package = draft.Save(runtime.Library, "순서");
            await runtime.Reload(); runtime.Stop();
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = false, ReminderSoundsEnabled = false });
            var colors = new List<uint>();
            for (var i = 0; i < 4; i++) colors.Add((await runtime.Clip("click"))[0].Image.Pixels[0]);
            Assert.Equal([0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF111111u], colors);
        }
        finally { runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Window window, Control control)
    {
        Layout(window);
        control.BringIntoView(); Layout(window);
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
