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
    public BreakHistory BreakHistory { get; private set; }
    public IReadOnlyList<BreakRoutine> Routines { get; private set; }
    public string? BreakHistoryError { get; private set; }
    internal EditorWindow? ActiveEditor => editor;
    internal Window? ActiveReminder => reminder;
    internal BreakReminderWindow? ActiveBreakReminder => reminder;
    internal PetWindow? ActivePet => pet;
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly string settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
    private readonly string historyFile = Path.Combine(AppPaths.DataRoot, "break-history.json");
    private readonly Stopwatch monotonic = Stopwatch.StartNew();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> clips = [];
    private readonly List<CharacterPackage> builtIns = [];
    private TrayIcon? tray;
    private NativeMenuItem? trayStatus, trayPause, trayPet;
    private SettingsWindow? settingsWindow;
    private EditorWindow? editor;
    private PetWindow? pet;
    private BreakReminderWindow? reminder;
    private bool quitting;
    private bool quitPending, openingEditor;
    private bool openingReminder, historyWritable = true;
    private int reminderGeneration;
    private SingleInstance? instanceActivation;
    public AppRuntime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        this.desktop = desktop;
        Library = new(Path.Combine(AppPaths.DataRoot, "Characters"), Directory.Exists(AppPaths.BuiltInRoot)
            ? Directory.EnumerateDirectories(AppPaths.BuiltInRoot).Select(path => Path.GetFileName(path)) : ["default-cat"]);
        Library.Warning += message => AppPaths.Log(new IOException(message));
        try { Settings = AppSettings.Load(settingsFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            AppPaths.Log(ex); Settings = new();
            // Preserve the invalid file for inspection, but a failure to copy it (locked,
            // read-only, out of disk space) must not stop startup from recovering.
            if (File.Exists(settingsFile))
            {
                try { File.Copy(settingsFile, settingsFile + $".invalid-{DateTime.UtcNow:yyyyMMddHHmmss}", true); }
                catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException) { AppPaths.Log(copyError); }
            }
        }
        Routines = BreakRoutines.ForSettings(Settings);
        Clock = new(TimeSpan.FromMinutes(Settings.IntervalMinutes));
        try { BreakHistory = BreakHistory.Load(historyFile); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            AppPaths.Log(error); BreakHistory = new(); historyWritable = false;
            BreakHistoryError = "History could not be opened. New breaks are kept for this session only.";
        }
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
            await Reload(); BuildTray(); timer.Start(); Clock.Start(monotonic.Elapsed);
            await UpdatePet();
            if (!background) ShowSettings();
        }
        catch (Exception error) { if (diagnostic) throw; ShowSettings(); await Ui.Error(settingsWindow, error); }
    }
    public async Task Reload()
    {
        var users = await Task.Run(() => Library.List()); Characters = builtIns.Concat(users).ToArray(); clips.Clear(); Changed?.Invoke();
    }
    public async Task SelectInstalledCharacter(CharacterPackage character)
    {
        await Reload(); await UpdateSettings(Settings with { SelectedCharacterId = character.Manifest.Id });
    }
    private void Tick()
    {
        TimeSpan idle;
        try { idle = PlatformServices.IdleTime(); ActivityError = null; }
        catch (Exception error) { idle = TimeSpan.FromDays(1); if (ActivityError != error.Message) AppPaths.Log(error); ActivityError = error.Message; }
        if (Clock.Tick(monotonic.Elapsed, idle, TimeSpan.FromMinutes(Settings.IdleMinutes), reminder is not null || openingReminder)) _ = ShowReminder();
        var remaining = $"{(int)Clock.Remaining.TotalMinutes:00}:{Clock.Remaining.Seconds:00}";
        if (tray is not null) tray.ToolTipText = $"Unfold · {remaining}{(Clock.Stopped ? " · stopped" : Clock.Paused ? " · paused" : Clock.IdlePaused ? " · away" : "")}";
        if (trayStatus is not null) trayStatus.Header = $"Next stretch: {remaining}";
        if (trayPause is not null) trayPause.Header = Clock.Stopped ? "Start" : Clock.Paused ? "Resume" : "Pause";
        if (trayPet is not null) trayPet.Header = Settings.ShowPet ? "Hide Pet" : "Show Pet";
        Changed?.Invoke();
    }
    public void TogglePause() { Clock.TogglePause(monotonic.Elapsed); Changed?.Invoke(); }
    public void Stop() { CancelReminder(); Clock.Stop(monotonic.Elapsed); Changed?.Invoke(); }
    public void Reset() { CancelReminder(); Clock.Reset(monotonic.Elapsed); Changed?.Invoke(); }
    private void CancelReminder() { reminderGeneration++; reminder?.Close(); }
    public void ShowSettings() { if (settingsWindow is null) return; settingsWindow.Show(); settingsWindow.ResumePreview(); settingsWindow.WindowState = WindowState.Normal; if (!DiagnosticMode) settingsWindow.Activate(); }
    internal void HideSettingsForDiagnostics() => settingsWindow?.HideToTray();
    public async Task UpdateSettings(AppSettings value)
    {
        value = value.ValidatePersonalization();
        value.Save(settingsFile); var changedCharacter = value.SelectedCharacterId != Settings.SelectedCharacterId;
        if (value.IntervalMinutes != Settings.IntervalMinutes || (value.ActiveProfileId is not null && value.ActiveProfileId != Settings.ActiveProfileId))
        {
            Clock.SetInterval(TimeSpan.FromMinutes(value.IntervalMinutes)); Clock.ScheduleAfterBreak(monotonic.Elapsed);
        }
        Routines = BreakRoutines.ForSettings(value);
        Settings = value;
        if (changedCharacter) clips.Clear();
        await UpdatePet(); Changed?.Invoke();
    }
    public void SavePosition(Avalonia.PixelPoint position)
    {
        Settings = Settings with { PetX = position.X, PetY = position.Y };
        try { Settings.Save(settingsFile); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException) { AppPaths.Log(error); }
    }
    public Task<IReadOnlyList<AnimationFrame>> Clip(string key) => Clip(Selected, key);
    private Task<IReadOnlyList<AnimationFrame>> Clip(CharacterPackage? selected, string key)
    {
        if (selected is null) return Task.FromResult<IReadOnlyList<AnimationFrame>>([]);
        var cacheKey = $"{selected.DirectoryPath}:{key}";
        if (clips.TryGetValue(cacheKey, out var cached)) return cached;
        // Sharing the in-flight task prevents the pet and settings preview
        // from decoding/uploading the same source independently on startup.
        return clips[cacheKey] = Task.Run(() => selected.LoadAnimation(key));
    }
    private async Task UpdatePet()
    {
        if (!Settings.ShowPet || Selected is null) { pet?.HidePet(); return; }
        var current = pet ??= new PetWindow(this);
        if (DiagnosticMode) PrepareDiagnosticWindow(current);
        await current.SetCharacter();
        // A concurrent hide or quit can complete while SetCharacter() is in flight
        // (Dispose() nulls pet, another UpdatePet() call flips ShowPet off): re-check
        // both before resurrecting a pet nobody asked for anymore.
        if (pet == current && Settings.ShowPet) current.ShowPet();
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
    public async Task ShowReminder()
    {
        if (reminder is not null) { if (!DiagnosticMode) reminder.Activate(); return; }
        if (openingReminder || quitting) return;
        openingReminder = true;
        var generation = reminderGeneration;
        try
        {
            var selected = Selected;
            if (selected is null) return;
            var routine = Routines.FirstOrDefault(item => item.Id == Settings.BreakRoutineId) ?? BreakRoutines.All[0];
            var profile = Settings.WorkProfiles.FirstOrDefault(item => item.Id == Settings.ActiveProfileId);
            var frames = await Clip(selected, "idle");
            if (quitting || generation != reminderGeneration) return;
            var session = new BreakSession(routine, selected.Manifest.Id, profile);
            var window = new BreakReminderWindow(session, selected.Manifest.Name, frames, selected.Manifest.RenderStyle == "pixel");
            if (DiagnosticMode) PrepareDiagnosticWindow(window);
            reminder = window;
            window.Started += () => ReactToBreak(session, "stretch");
            window.Finished += result =>
            {
                if (reminder == window) reminder = null;
                if (!quitting) FinishBreak(result);
            };
            window.Show(); if (!DiagnosticMode) { window.Activate(); NativeReminder.Show(window); }
            ReactToBreak(session, "attention");
            Changed?.Invoke();
        }
        catch (Exception error) { if (quitting || generation != reminderGeneration) return; if (DiagnosticMode) throw; ShowSettings(); await Ui.Error(settingsWindow!, error); }
        finally { openingReminder = false; }
    }
    private void ReactToBreak(BreakSession session, string animation)
    {
        if (Selected?.Manifest.Id == session.CharacterId && Settings.ShowPet && pet is { IsVisible: true }) _ = pet.React(animation);
    }
    private void FinishBreak(BreakSession session)
    {
        if (session.State == BreakSessionState.Completed)
        {
            Clock.ScheduleAfterBreak(monotonic.Elapsed);
            if (BreakHistory.Add(session, DateTimeOffset.Now) && historyWritable)
            {
                try { BreakHistory.Save(historyFile); BreakHistoryError = null; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    AppPaths.Log(error); BreakHistoryError = "Your break is counted, but history could not be saved.";
                }
            }
            ReactToBreak(session, "celebrate");
        }
        else if (session.State == BreakSessionState.Snoozed) Clock.ScheduleAfterBreak(monotonic.Elapsed, TimeSpan.FromMinutes(5));
        else Clock.Tick(monotonic.Elapsed, TimeSpan.Zero, TimeSpan.FromMinutes(Settings.IdleMinutes), true);
        Changed?.Invoke();
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
        trayPet = new NativeMenuItem("Hide Pet"); menu.Items.Add(trayPet);
        trayPet.Click += async (_, _) => { try { await UpdateSettings(Settings with { ShowPet = !Settings.ShowPet }); } catch (Exception error) { AppPaths.Log(error); } };
        trayPause = new NativeMenuItem("Pause"); trayPause.Click += (_, _) => TogglePause(); menu.Items.Add(trayPause);
        Item("Stop timer", Stop); Item("Reset timer", Reset);
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
        // macOS can constrain an off-screen window down to 1x1 without explicit minimums.
        if (double.IsFinite(window.Width)) window.MinWidth = window.Width;
        if (double.IsFinite(window.Height)) window.MinHeight = window.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Position = new(-32000, -32000);
        window.ShowInTaskbar = false; window.ShowActivated = false;
    }
}
