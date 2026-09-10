using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class AppRuntime : IDisposable
{
    public AppSettings Settings { get; private set; }
    public CharacterLibrary Library { get; }
    public StretchClock Clock { get; }
    public IReadOnlyList<CharacterPackage> Characters { get; private set; } = [];
    public CharacterPackage? Selected => Characters.FirstOrDefault(c => c.Manifest.Id == Settings.SelectedCharacterId) ?? Characters.FirstOrDefault();
    public event Action? Changed;
    public string? ActivityError { get; private set; }
    public bool DiagnosticMode { get; private set; }
    internal EditorWindow? ActiveEditor => editor;
    internal Window? ActiveReminder => reminder;
    internal PetWindow? ActivePet => pet;
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly string settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
    private readonly Stopwatch monotonic = Stopwatch.StartNew();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> clips = [];
    private readonly List<CharacterPackage> builtIns = [];
    private TrayIcon? tray;
    private NativeMenuItem? trayStatus, trayPause;
    private SettingsWindow? settingsWindow;
    private EditorWindow? editor;
    private PetWindow? pet;
    private Window? reminder;
    private bool quitting;
    private bool quitPending, openingEditor;
    private SingleInstance? instanceActivation;
    public AppRuntime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        this.desktop = desktop;
        Library = new(Path.Combine(AppPaths.DataRoot, "Characters"));
        Library.Warning += message => AppPaths.Log(new IOException(message));
        try { Settings = AppSettings.Load(settingsFile); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            AppPaths.Log(ex); Settings = new();
            if (File.Exists(settingsFile)) File.Copy(settingsFile, settingsFile + $".invalid-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
        }
        Clock = new(TimeSpan.FromMinutes(Settings.IntervalMinutes));
        timer.Tick += (_, _) => Tick();
        desktop.ShutdownRequested += async (_, e) =>
        {
            if (quitting) return;
            e.Cancel = true; await Quit();
        };
    }
    public async Task Start(bool background, bool diagnostic = false)
    {
        DiagnosticMode = diagnostic;
        settingsWindow = new(this); desktop.MainWindow = settingsWindow;
        instanceActivation = new SingleInstance(ShowSettings);
        if (diagnostic) PrepareDiagnosticWindow(settingsWindow);
        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(AppPaths.BuiltInRoot)) throw new DirectoryNotFoundException("Built-in character assets are missing.");
                foreach (var directory in Directory.EnumerateDirectories(AppPaths.BuiltInRoot)) builtIns.Add(CharacterLibrary.LoadPackage(directory, true));
                if (builtIns.Count == 0) throw new InvalidDataException("No built-in characters found.");
            });
            await Reload(); BuildTray(); timer.Start(); Clock.Reset(monotonic.Elapsed);
            await UpdatePet();
            if (!background) ShowSettings();
        }
        catch (Exception error) { if (diagnostic) throw; ShowSettings(); await Ui.Error(settingsWindow, error); }
    }
    public async Task Reload()
    {
        var users = await Task.Run(() => Library.List()); Characters = builtIns.Concat(users).ToArray(); clips.Clear(); Changed?.Invoke();
    }
    private void Tick()
    {
        TimeSpan idle;
        try { idle = PlatformServices.IdleTime(); ActivityError = null; }
        catch (Exception error) { idle = TimeSpan.FromDays(1); if (ActivityError != error.Message) AppPaths.Log(error); ActivityError = error.Message; }
        if (Clock.Tick(monotonic.Elapsed, idle, TimeSpan.FromMinutes(Settings.IdleMinutes))) _ = ShowReminder();
        var remaining = $"{(int)Clock.Remaining.TotalMinutes:00}:{Clock.Remaining.Seconds:00}";
        if (tray is not null) tray.ToolTipText = $"Unfold · {remaining}{(Clock.Paused ? " · paused" : Clock.IdlePaused ? " · away" : "")}";
        if (trayStatus is not null) trayStatus.Header = $"Next stretch: {remaining}";
        if (trayPause is not null) trayPause.Header = Clock.Paused ? "Resume" : "Pause";
        Changed?.Invoke();
    }
    public void TogglePause() { Clock.TogglePause(monotonic.Elapsed); Changed?.Invoke(); }
    public void Reset() { Clock.Reset(monotonic.Elapsed); Changed?.Invoke(); }
    public void ShowSettings() { if (settingsWindow is null) return; settingsWindow.Show(); settingsWindow.ResumePreview(); settingsWindow.WindowState = WindowState.Normal; if (!DiagnosticMode) settingsWindow.Activate(); }
    internal void HideSettingsForDiagnostics() => settingsWindow?.HideToTray();
    public async Task UpdateSettings(AppSettings value)
    {
        value.Save(settingsFile); var changedCharacter = value.SelectedCharacterId != Settings.SelectedCharacterId;
        if (value.IntervalMinutes != Settings.IntervalMinutes) Clock.SetInterval(TimeSpan.FromMinutes(value.IntervalMinutes));
        Settings = value;
        if (changedCharacter) clips.Clear();
        await UpdatePet(); Changed?.Invoke();
    }
    public void SavePosition(Avalonia.PixelPoint position)
    {
        Settings = Settings with { PetX = position.X, PetY = position.Y };
        try { Settings.Save(settingsFile); } catch (IOException error) { AppPaths.Log(error); }
    }
    public Task<IReadOnlyList<AnimationFrame>> Clip(string key)
    {
        if (Selected is not { } selected) return Task.FromResult<IReadOnlyList<AnimationFrame>>([]);
        var cacheKey = $"{selected.Manifest.Id}:{key}";
        if (clips.TryGetValue(cacheKey, out var cached)) return cached;
        // Sharing the in-flight task prevents the pet and settings preview
        // from decoding/uploading the same source independently on startup.
        return clips[cacheKey] = Task.Run(() => selected.LoadAnimation(key));
    }
    private async Task UpdatePet()
    {
        if (!Settings.ShowPet || Selected is null) { pet?.HidePet(); return; }
        pet ??= new PetWindow(this);
        await pet.SetCharacter(); pet.ShowPet();
    }
    public async Task OpenEditor(CharacterPackage? character = null)
    {
        if (openingEditor || quitting) return;
        openingEditor = true;
        try
        {
            if (editor is not null)
            {
                editor.Show(); editor.Activate();
                if (character?.Manifest.Id == editor.Session.CharacterId && character is not null) return;
                if (!await editor.CanCloseDocument()) return;
            }
            EditorSession session;
            if (character is not null)
            {
                if (character.IsBuiltIn) throw new InvalidOperationException("Create a new character to edit your own pixel art.");
                var opened = await Task.Run(() => Library.OpenForEditing(character.Manifest.Id));
                session = new(opened.Document) { CharacterId = character.Manifest.Id, Revision = opened.Revision };
            }
            else session = new(new PixelDocument());
            editor?.CloseAfterApproval();
            editor = new EditorWindow(Library, session, SavedCharacter);
            if (DiagnosticMode) PrepareDiagnosticWindow(editor);
            var openedEditor = editor;
            openedEditor.Closed += (_, _) => { if (editor == openedEditor) editor = null; };
            openedEditor.Show(); if (!DiagnosticMode) openedEditor.Activate();
        }
        catch (Exception error) { ShowSettings(); await Ui.Error(settingsWindow!, error); }
        finally { openingEditor = false; }
    }
    private async void SavedCharacter(CharacterPackage character)
    {
        try { await Reload(); await UpdateSettings(Settings with { SelectedCharacterId = character.Manifest.Id }); }
        catch (Exception error) { if (editor is not null) await Ui.Error(editor, error); else AppPaths.Log(error); }
    }
    public async Task DeleteCharacter(CharacterPackage character)
    {
        if (character.IsBuiltIn || settingsWindow is null) return;
        if (await Ui.Confirm(settingsWindow, "Delete character?", $"Delete {character.Manifest.Name} from your library?", "Delete", "Cancel") != 0) return;
        try { await Task.Run(() => Library.Delete(character.Manifest.Id)); await Reload(); await UpdateSettings(Settings with { SelectedCharacterId = Characters[0].Manifest.Id }); }
        catch (Exception error) { await Ui.Error(settingsWindow, error); }
    }
    public async Task ShowReminder()
    {
        if (reminder is not null) { reminder.Activate(); return; }
        try
        {
            var animation = new AnimationView { Width = 240, Height = 240 };
            var selected = Selected; var clip = await Clip("stretch");
            if (reminder is not null) { animation.Dispose(); return; }
            animation.SetFrames(clip, selected?.Manifest.Animations.GetValueOrDefault("stretch")?.Loop ?? true, selected?.Manifest.RenderStyle == "pixel");
            var window = new Window { Title = "Time to stretch · Unfold", Width = 390, Height = 435, CanResize = false, Topmost = true, Background = Ui.Background, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            if (DiagnosticMode) PrepareDiagnosticWindow(window);
            window.Content = new Border { Padding = new Thickness(24), Child = Ui.Column(Ui.Text("A little room to breathe.", 22, Ui.Accent), animation,
                Ui.Text("Stand up, stretch, and rest your eyes.", 14), Ui.Button("I'm refreshed", () => window.Close())) };
            reminder = window; window.Closed += (_, _) => { animation.Dispose(); if (reminder == window) reminder = null; };
            window.Show(); if (!DiagnosticMode) { window.Activate(); NativeReminder.Show(window); }
        }
        catch (Exception error) { ShowSettings(); await Ui.Error(settingsWindow!, error); }
    }
    private void BuildTray()
    {
        var pixels = new uint[32 * 32];
        for (var y = 3; y < 29; y++) for (var x = 3; x < 29; x++)
            if (Math.Pow(x - 15.5, 2) + Math.Pow(y - 15.5, 2) < 160) pixels[y * 32 + x] = 0xFFF4B860;
        for (var y = 8; y < 23; y++) for (var x = 10; x < 22; x++) if (x < 13 || x > 18 || y > 19) pixels[y * 32 + x] = 0xFF141820;
        using var icon = Ui.Bitmap(new(32, 32, pixels));
        tray = new TrayIcon { Icon = new WindowIcon(icon), ToolTipText = "Unfold", IsVisible = true };
        var menu = new NativeMenu();
        trayStatus = new NativeMenuItem("Next stretch") { IsEnabled = false }; menu.Items.Add(trayStatus);
        void Item(string text, Action action) { var item = new NativeMenuItem(text); item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("Settings", ShowSettings);
        trayPause = new NativeMenuItem("Pause"); trayPause.Click += (_, _) => TogglePause(); menu.Items.Add(trayPause);
        Item("Reset timer", Reset); Item("Stretch now", () => _ = ShowReminder());
        menu.Items.Add(new NativeMenuItemSeparator()); Item("Quit Unfold", () => _ = Quit());
        tray.Menu = menu; tray.Clicked += (_, _) => ShowSettings();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
    }
    public async Task Quit()
    {
        if (quitting || quitPending) return;
        quitPending = true;
        try
        {
            if (editor is not null && !await editor.CanCloseDocument()) return;
            quitting = true; editor?.CloseAfterApproval(); Dispose(); desktop.Shutdown();
        }
        finally { quitPending = false; }
    }
    public void Dispose() { timer.Stop(); instanceActivation?.Dispose(); instanceActivation = null; tray?.Dispose(); tray = null; pet?.ClosePet(); pet = null; reminder?.Close(); }
    internal static void PrepareDiagnosticWindow(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Position = new(-32000, -32000);
        window.ShowInTaskbar = false; window.ShowActivated = false;
    }
}
