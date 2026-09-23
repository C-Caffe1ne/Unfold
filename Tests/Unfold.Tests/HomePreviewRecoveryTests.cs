using System.Reflection;
using System.Text.Json;
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
public class HomePreviewRecoveryTests
{
    [AvaloniaFact]
    public async Task FailedSelectionShowsItsOwnStillAndRetriesAfterRepair()
    {
        using var scope = new Scope(); await scope.Load();
        Assert.Equal(0xFFFF0000u, scope.Frames[0].Image.Pixels[0]);
        await scope.SelectB();
        await Until(() => scope.IdleTask?.IsFaulted == true);
        await Until(() => scope.Frames.Count == 1 && scope.Frames[0].Image.Pixels[0] == 0xFF00FF00u);
        scope.Repair(); await Until(() => scope.Frames.Count == 4);
        Assert.Same(scope.Runtime.Selected, Field<CharacterPackage>(scope.Window, "previewCharacter"));
    }

    [AvaloniaFact]
    public async Task FailedRetriesAreBoundedAndReturningHomeRetriesAgain()
    {
        using var scope = new Scope(); await scope.Load(); await scope.SelectB();
        await Until(() => Field<int>(scope.Window, "previewAttempts") == 3 && scope.IdleTask!.IsFaulted);
        var last = scope.IdleTask;
        await Task.Delay(1300, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        Assert.Same(last, scope.IdleTask);
        scope.Repair(); scope.Click("SettingsNavSettings"); scope.Click("SettingsNavTimer");
        await Until(() => scope.Frames.Count == 4);
    }

    [AvaloniaFact]
    public async Task OlderLoadCannotReplaceANewerSelection()
    {
        using var scope = new Scope(); await scope.Load();
        var pending = scope.DeferB(); await scope.SelectB();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { SelectedCharacterId = scope.A });
        await Until(() => scope.Frames.Count == 1 && scope.Frames[0].Image.Pixels[0] == 0xFFFF0000u);
        pending.SetResult(scope.GoodFrames); await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(0xFFFF0000u, scope.Frames[0].Image.Pixels[0]);
    }

    [AvaloniaTheory]
    [InlineData("hide")] [InlineData("dispose")] [InlineData("tab")]
    public async Task LateLoadCannotRestartHiddenOrDisposedPreview(string action)
    {
        using var scope = new Scope(); await scope.Load();
        var pending = scope.DeferB(); await scope.SelectB();
        switch (action)
        {
            case "hide": scope.Window.HideToTray(); break;
            case "dispose": scope.Window.Dispose(); break;
            case "tab": scope.Click("SettingsNavSettings"); break;
        }
        pending.SetResult(scope.GoodFrames); await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(Field<DispatcherTimer>(scope.Preview, "timer").IsEnabled);
        if (action == "dispose") Assert.Empty(scope.Frames);
        else
        {
            Assert.Single(scope.Frames);
            if (action == "hide") { scope.Window.Show(); scope.Window.ResumePreview(); }
            else scope.Click("SettingsNavTimer");
            await Until(() => scope.Frames.Count == 4);
        }
    }

    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 250 && !ready(); i++)
        { await Task.Delay(20, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; private set; } = null!;
        public AnimationView Preview => Field<AnimationView>(Window, "preview");
        public IReadOnlyList<AnimationFrame> Frames => Field<IReadOnlyList<AnimationFrame>>(Preview, "frames");
        public IReadOnlyList<AnimationFrame> GoodFrames => ImageCodec.DecodeGif(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif")));
        public string A { get; private set; } = "";
        private string b = "", gif = "";
        private Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> Cache => Field<Dictionary<string, Task<IReadOnlyList<AnimationFrame>>>>(Runtime, "clips");
        private string Key => $"{Runtime.Characters.Single(p => p.Manifest.Id == b).DirectoryPath}:idle";
        public Task<IReadOnlyList<AnimationFrame>>? IdleTask => Cache.GetValueOrDefault(Key);
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            new AppSettings { ShowPet = false, ReminderSoundsEnabled = false }.Save(Path.Combine(temp.Path, "settings.json"));
            Runtime = new(lifetime);
        }
        public async Task Load()
        {
            var doc = new PixelDocument(4, 4) { Name = "Red" }; Array.Fill(doc.Layers[0].Frames[0], 0xFFFF0000u);
            A = Runtime.Library.Save(doc).Manifest.Id;
            doc.Name = "Green"; Array.Fill(doc.Layers[0].Frames[0], 0xFF00FF00u);
            var package = Runtime.Library.Save(doc); b = package.Manifest.Id;
            gif = Path.Combine(package.DirectoryPath, "idle.gif"); File.WriteAllText(gif, "damaged");
            var manifest = package.Manifest with { Animations = new() { ["idle"] = new(Gif: "idle.gif") } };
            File.WriteAllBytes(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload(); await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = A });
            Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
            await Until(() => Frames.Count == 1);
        }
        public async Task SelectB()
        {
            // UpdateSettings clears the cache on selection. Defer after that reset through Changed.
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = b });
        }
        public TaskCompletionSource<IReadOnlyList<AnimationFrame>> DeferB()
        {
            var pending = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Seed through a completed selection while hidden, then resume the actual UI request.
            Window.HideToTray();
            Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = b }).GetAwaiter().GetResult();
            Cache[Key] = pending.Task; Window.Show(); Window.ResumePreview();
            return pending;
        }
        public void Repair() => File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"), gif, true);
        public void Click(string name)
        { Window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
        public void Dispose()
        { Window?.HideToTray(); Window?.Dispose(); Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
