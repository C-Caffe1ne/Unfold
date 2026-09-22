using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>One contract behind the tray tooltip and status row.</summary>
public readonly record struct TrayReminderStatus(string Status, string ToolTip)
{
    public static TrayReminderStatus Create(PetNotice notice, string remaining, string clockState)
    {
        // Invitation and Resting both hold the work timer, so the countdown alone would read as
        // a stuck clock. Tooltip and status therefore lead with the same label.
        var label = notice switch { PetNotice.Invitation => "휴식 대기 중", PetNotice.Resting => "휴식 중", _ => null };
        return label is null
            ? new($"다음 휴식: {remaining} · {clockState}", $"Unfold · {remaining} · {clockState}")
            : new($"{label} · 작업 타이머 {remaining} 멈춤", $"Unfold · {label} · 타이머 {remaining} 멈춤");
    }
}

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
    public bool CanEditTimerInterval => Clock.Paused || Clock.Stopped;
    internal EditorWindow? ActiveEditor => editor;
    public PetReminder Reminder { get; } = new();
    internal PetReminder PresentedReminder => Reminder.HasNotice ? Reminder : reminderPreview ?? Reminder;
    public PetNotice? PreviewNotice => reminderPreview?.Notice;
    /// <summary>Last state pushed to the tray. Kept as a value so callers can read the tray
    /// contract without touching the native menu.</summary>
    public TrayReminderStatus TrayStatus { get; private set; }
    internal BreakSession? ActiveReminder => Reminder.Session;
    internal PetWindow? ActivePet => pet;
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly string settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
    private readonly string historyFile = Path.Combine(AppPaths.DataRoot, "break-history.json");
    private readonly Stopwatch monotonic = Stopwatch.StartNew();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer noticeExpiryTimer = new();
    private TimeSpan? scheduledNoticeExpiry;
    private readonly Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> clips = [];
    private readonly List<CharacterPackage> builtIns = [];
    private TrayIcon? tray;
    private NativeMenuItem? trayStatus, trayPause, trayPet, trayFocusReminder;
    private SettingsWindow? settingsWindow;
    private EditorWindow? editor;
    private PetWindow? pet;
    private readonly ReminderSoundPlayer soundPlayer = new();
    internal int DueSoundRequests { get; private set; }
    internal int CompletionSoundRequests { get; private set; }
    private bool quitting;
    private bool disposed;
    private bool quitPending, stopPending, openingEditor;
    private bool petNoticeSuppressed;
    internal Func<string, string, string[], Task<int>>? ConfirmActionOverride { get; set; }
    private bool openingReminder, historyWritable = true;
    private int reminderGeneration;
    private PetReminder? reminderPreview;
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
        DesignSystem.ApplyTheme(Settings.Theme);
        Routines = BreakRoutines.ForSettings(Settings);
        Clock = new(TimeSpan.FromMinutes(Settings.IntervalMinutes));
        try { BreakHistory = BreakHistory.Load(historyFile); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            AppPaths.Log(error); BreakHistory = new(); historyWritable = false;
            BreakHistoryError = "기록을 열지 못했어요. 새 휴식은 앱을 종료할 때까지만 보관돼요.";
        }
        Reminder.Started += session => ReactToBreak(session, "stretch");
        Reminder.Finished += session => { if (!quitting) FinishBreak(session); };
        RefreshTray();
        timer.Tick += (_, _) => Tick();
        noticeExpiryTimer.Tick += (_, _) =>
        {
            if (disposed) return;
            noticeExpiryTimer.Stop(); scheduledNoticeExpiry = null;
            Reminder.Tick(monotonic.Elapsed);
            RefreshPetNotice(); Changed?.Invoke();
        };
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
                if (!Directory.Exists(AppPaths.BuiltInRoot)) throw new DirectoryNotFoundException("기본 펫 파일이 없어요. Unfold를 다시 설치해 주세요.");
                foreach (var directory in Directory.EnumerateDirectories(AppPaths.BuiltInRoot)) builtIns.Add(CharacterLibrary.LoadPackage(directory, true));
                if (builtIns.Count == 0) throw new InvalidDataException("기본 펫을 찾지 못했어요. Unfold를 다시 설치해 주세요.");
            });
            await Reload(); BuildTray(); timer.Start(); Clock.Start(monotonic.Elapsed);
            await UpdatePet();
            if (!background) ShowSettings();
        }
        catch (Exception error) { if (diagnostic) throw; ShowSettings(); await Ui.Error(settingsWindow, error); }
    }
    public async Task Reload()
    {
        var users = await Task.Run(() => Library.List());
        var bundledIds = builtIns.Select(item => item.Manifest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Characters = builtIns.Concat(users.Where(item => !bundledIds.Contains(item.Manifest.Id))).ToArray();
        clips.Clear(); Changed?.Invoke();
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
        var now = monotonic.Elapsed;
        Reminder.Tick(now);
        if (Clock.Tick(now, idle, TimeSpan.FromMinutes(Settings.IdleMinutes), Reminder.Session is not null || openingReminder)) _ = ShowReminder();
        else if (Clock.AdvanceWarningDue)
        {
            ClearReminderPreview(); petNoticeSuppressed = false; Reminder.ShowAdvance(now); _ = EnsurePetNotice();
        }
        RefreshPetNotice();
        Changed?.Invoke();
    }
    /// <summary>Recomputes every tray surface from <see cref="TrayReminderStatus"/>.</summary>
    private void RefreshTray()
    {
        var remaining = $"{(int)Clock.Remaining.TotalMinutes:00}:{Clock.Remaining.Seconds:00}";
        var clockState = Clock.Stopped ? "중지" : Clock.Paused ? "일시정지" : Clock.IdlePaused ? "자리 비움" : "진행 중";
        TrayStatus = TrayReminderStatus.Create(Reminder.Notice, remaining, clockState);
        if (tray is not null) tray.ToolTipText = TrayStatus.ToolTip;
        if (trayStatus is not null) trayStatus.Header = TrayStatus.Status;
        if (trayFocusReminder is not null) trayFocusReminder.IsEnabled = Reminder.Session is not null;
        if (trayPause is not null) trayPause.Header = Clock.Stopped ? "시작" : Clock.Paused ? "계속" : "일시정지";
        if (trayPet is not null) trayPet.Header = Settings.ShowPet ? "펫 숨기기" : "펫 표시";
    }
    public void TogglePause() { Clock.TogglePause(monotonic.Elapsed); Changed?.Invoke(); }
    public void Stop() { CancelReminder(); Clock.Stop(monotonic.Elapsed); Changed?.Invoke(); }
    public async Task RequestStop()
    {
        if (quitting || disposed || stopPending || quitPending) return;
        stopPending = true;
        try
        {
            if (await ConfirmAction("타이머를 중지할까요?", "타이머를 중지하면 남은 시간이 초기화되고 진행 중인 휴식이 취소돼요.", "중지", "취소") == 0 && !disposed)
                Stop();
        }
        finally { stopPending = false; }
    }
    public void Reset() { CancelReminder(); Clock.Reset(monotonic.Elapsed); Changed?.Invoke(); }
    private void CancelReminder() { reminderGeneration++; Reminder.Cancel(); RefreshPetNotice(); }
    public void ShowSettings() { if (settingsWindow is null) return; settingsWindow.Show(); settingsWindow.ResumePreview(); settingsWindow.WindowState = WindowState.Normal; if (!DiagnosticMode) settingsWindow.Activate(); }
    internal void HideSettingsForDiagnostics() => settingsWindow?.HideToTray();
    public Task UpdateSettings(AppSettings value) => UpdateSettings(value, false);
    public Task HidePet() => UpdateSettings(Settings with { ShowPet = false }, true);
    private async Task UpdateSettings(AppSettings value, bool hideCurrentNotice)
    {
        value = value.ValidatePersonalization();
        var reschedulesTimer = value.IntervalMinutes != Settings.IntervalMinutes ||
            (value.ActiveProfileId is not null && value.ActiveProfileId != Settings.ActiveProfileId);
        if (reschedulesTimer && !CanEditTimerInterval)
            throw new ArgumentException("타이머를 일시정지하거나 중지한 뒤 스트레칭 시간 또는 업무 프로필을 적용해 주세요.");
        value.Save(settingsFile); var changedCharacter = value.SelectedCharacterId != Settings.SelectedCharacterId;
        var hidingPet = hideCurrentNotice || (!value.ShowPet && Settings.ShowPet);
        if (reschedulesTimer)
        {
            Clock.SetInterval(TimeSpan.FromMinutes(value.IntervalMinutes)); Clock.ScheduleAfterBreak(monotonic.Elapsed);
        }
        Routines = BreakRoutines.ForSettings(value);
        Settings = value;
        if (hidingPet) { petNoticeSuppressed = true; ClearReminderPreview(); }
        else if (Settings.ShowPet) petNoticeSuppressed = false;
        if (DesignSystem.CurrentTheme != Settings.Theme) DesignSystem.ApplyTheme(Settings.Theme);
        if (!Settings.ReminderSoundsEnabled) soundPlayer.Stop();
        if (changedCharacter) clips.Clear();
        await UpdatePet(); RefreshPetNotice(); Changed?.Invoke();
    }
    public void SetTheme(AppTheme theme)
    {
        if (Settings.Theme == theme) return;
        var value = Settings with { Theme = theme };
        value.Save(settingsFile);
        Settings = value;
        DesignSystem.ApplyTheme(theme);
        Changed?.Invoke();
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
        if (!ShouldShowPet || Selected is null) { pet?.HidePet(); return; }
        var current = pet ??= new PetWindow(this);
        if (DiagnosticMode) PrepareDiagnosticWindow(current);
        await current.SetCharacter();
        // A concurrent hide or quit can complete while SetCharacter() is in flight
        // (Dispose() nulls pet, another UpdatePet() call flips ShowPet off): re-check
        // both before resurrecting a pet nobody asked for anymore.
        if (pet == current && ShouldShowPet) current.ShowPet();
    }
    private bool ShouldShowPet => Settings.ShowPet || (!petNoticeSuppressed && PresentedReminder.HasNotice);
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
        ClearReminderPreview();
        if (Reminder.Session is not null) { RefreshPetNotice(); return; }
        if (openingReminder || quitting) return;
        openingReminder = true;
        var generation = reminderGeneration;
        try
        {
            var selected = Selected;
            if (selected is null) return;
            var routine = Routines.FirstOrDefault(item => item.Id == Settings.BreakRoutineId) ?? BreakRoutines.All[0];
            var profile = Settings.WorkProfiles.FirstOrDefault(item => item.Id == Settings.ActiveProfileId);
            await Clip(selected, "idle");
            if (quitting || generation != reminderGeneration) return;
            var session = new BreakSession(routine, selected.Manifest.Id, profile, Settings.BreakDurationMinutes * 60);
            if (!Reminder.Invite(session)) return;
            petNoticeSuppressed = false;
            await UpdatePet();
            if (quitting || generation != reminderGeneration || Reminder.Session != session) return;
            PlayReminderSound(ReminderSound.Due);
            ReactToBreak(session, "attention"); RefreshPetNotice(); Changed?.Invoke();
        }
        catch (Exception error) { if (quitting || generation != reminderGeneration) return; if (DiagnosticMode) throw; AppPaths.Log(error); }
        finally { openingReminder = false; }
    }
    public void StartBreak() { Reminder.Start(monotonic.Elapsed); RefreshPetNotice(); Changed?.Invoke(); }
    public void SnoozeBreak() { Reminder.Snooze(); RefreshPetNotice(); Changed?.Invoke(); }
    public void CompleteBreak() { Reminder.Complete(monotonic.Elapsed); RefreshPetNotice(); Changed?.Invoke(); }
    public async Task ShowReminderPreview(PetNotice notice)
    {
        if (notice == PetNotice.None) { CloseReminderPreview(); return; }
        if (Reminder.HasNotice) throw new InvalidOperationException("진행 중인 알림을 먼저 완료하거나 미뤄 주세요.");
        var routine = Routines.FirstOrDefault(item => item.Id == Settings.BreakRoutineId) ?? BreakRoutines.All[0];
        var characterId = Selected?.Manifest.Id ?? Settings.SelectedCharacterId;
        var preview = new PetReminder();
        var session = new BreakSession(routine, characterId, durationSeconds: Settings.BreakDurationMinutes * 60);
        switch (notice)
        {
            case PetNotice.Advance: preview.ShowAdvance(TimeSpan.Zero); break;
            case PetNotice.Invitation: preview.Invite(session); break;
            case PetNotice.Resting: preview.Invite(session); preview.Start(TimeSpan.Zero); break;
            case PetNotice.Completed:
                preview.Invite(session); preview.Start(TimeSpan.Zero); preview.Complete(TimeSpan.Zero); break;
            default: throw new ArgumentOutOfRangeException(nameof(notice));
        }
        reminderPreview = preview;
        petNoticeSuppressed = false;
        await UpdatePet(); RefreshPetNotice(); Changed?.Invoke();
    }
    public void CloseReminderPreview()
    {
        if (reminderPreview is null) return;
        reminderPreview = null; RefreshPetNotice(); Changed?.Invoke();
    }
    private void ClearReminderPreview() => reminderPreview = null;
    public async Task FocusReminder()
    {
        if (Reminder.Session is null) return;
        try { petNoticeSuppressed = false; await UpdatePet(); pet?.FocusReminder(); }
        catch (Exception error) { AppPaths.Log(error); }
    }
    private async Task EnsurePetNotice()
    {
        try { await UpdatePet(); }
        catch (Exception error) { AppPaths.Log(error); }
    }
    private void PlayReminderSound(ReminderSound sound)
    {
        if (!Settings.ReminderSoundsEnabled) return;
        if (sound == ReminderSound.Due) DueSoundRequests++; else CompletionSoundRequests++;
        if (!DiagnosticMode) soundPlayer.Play(sound, Settings);
    }
    private void RefreshPetNotice()
    {
        ScheduleNoticeExpiry();
        RefreshTray();
        if (pet is not { } current) return;
        current.RefreshSpeech();
        if (ShouldShowPet)
        { if (!current.IsVisible) current.ShowPet(); }
        else if (current.IsVisible) current.HidePet();
    }
    private void ScheduleNoticeExpiry()
    {
        if (disposed) return;
        var expiry = Reminder.NoticeExpiresAt;
        if (expiry == scheduledNoticeExpiry) return;
        noticeExpiryTimer.Stop(); scheduledNoticeExpiry = expiry;
        if (expiry is not { } deadline) return;
        var remaining = deadline - monotonic.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            Reminder.Tick(monotonic.Elapsed); scheduledNoticeExpiry = null; return;
        }
        // Completion can happen between work-clock ticks. Expire at its own deadline,
        // without adding up to another second while waiting for the work timer.
        noticeExpiryTimer.Interval = remaining; noticeExpiryTimer.Start();
    }
    private void ReactToBreak(BreakSession session, string animation)
    {
        if (Selected?.Manifest.Id == session.CharacterId && pet is { IsVisible: true }) _ = pet.React(animation);
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
                    AppPaths.Log(error); BreakHistoryError = "이번 휴식을 집계했지만 기록 파일에 저장하지 못했어요.";
                }
            }
            ReactToBreak(session, "celebrate"); PlayReminderSound(ReminderSound.Completed);
        }
        else if (session.State == BreakSessionState.Snoozed)
        {
            Clock.ScheduleAfterBreak(monotonic.Elapsed, TimeSpan.FromMinutes(Settings.SnoozeMinutes));
            if (Reminder.ConsecutiveSnoozes == 3 && Selected?.HasOriginalBehavior == true)
                ReactToBreak(session, "sulk");
        }
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
        trayStatus = new NativeMenuItem("다음 휴식") { IsEnabled = false }; menu.Items.Add(trayStatus);
        trayFocusReminder = new NativeMenuItem("휴식 알림으로 이동") { IsEnabled = false }; menu.Items.Add(trayFocusReminder);
        trayFocusReminder.Click += async (_, _) => await FocusReminder();
        void Item(string text, Action action) { var item = new NativeMenuItem(text); item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("설정", ShowSettings);
        trayPet = new NativeMenuItem("펫 숨기기"); menu.Items.Add(trayPet);
        trayPet.Click += async (_, _) => { try { if (Settings.ShowPet) await HidePet(); else await UpdateSettings(Settings with { ShowPet = true }); } catch (Exception error) { AppPaths.Log(error); } };
        trayPause = new NativeMenuItem("일시정지"); trayPause.Click += (_, _) => TogglePause(); menu.Items.Add(trayPause);
        Item("타이머 중지", () => _ = RequestStop());
        menu.Items.Add(new NativeMenuItemSeparator()); Item("Unfold 종료", () => _ = Quit());
        tray.Menu = menu; tray.Clicked += (_, _) => ShowSettings();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
        RefreshTray();
    }
    public async Task Quit()
    {
        if (quitting || disposed || quitPending || stopPending) return;
        quitPending = true;
        try
        {
            if (await ConfirmAction("Unfold를 종료할까요?", "Unfold를 종료하면 타이머와 휴식 알림도 종료돼요.", "종료", "취소") != 0 || disposed) return;
            if (settingsWindow is not null && !await settingsWindow.CanCloseDraft()) return;
            if (editor is not null && !await editor.CanCloseDocument()) return;
            quitting = true; editor?.CloseAfterApproval(); Dispose(); desktop.Shutdown();
        }
        finally { quitPending = false; }
    }
    private Task<int> ConfirmAction(string title, string message, params string[] choices)
    {
        if (ConfirmActionOverride is { } confirm) return confirm(title, message, choices);
        var owner = (Window?)settingsWindow ?? desktop.MainWindow ?? pet;
        if (owner is null)
        {
            settingsWindow = new(this); desktop.MainWindow = settingsWindow; owner = settingsWindow;
            if (DiagnosticMode) PrepareDiagnosticWindow(owner);
        }
        if (!owner.IsVisible)
        {
            if (owner == settingsWindow) ShowSettings(); else owner.Show();
        }
        return Ui.Confirm(owner, title, message, choices);
    }
    public void Dispose() { if (disposed) return; disposed = true; timer.Stop(); noticeExpiryTimer.Stop(); scheduledNoticeExpiry = null; settingsWindow?.Dispose(); instanceActivation?.Dispose(); instanceActivation = null; tray?.Dispose(); tray = null; pet?.ClosePet(); pet = null; soundPlayer.Dispose(); }
    internal static void PrepareDiagnosticWindow(Window window)
    {
        // macOS can constrain an off-screen window down to 1x1 without explicit minimums.
        if (double.IsFinite(window.Width)) window.MinWidth = window.Width;
        if (double.IsFinite(window.Height)) window.MinHeight = window.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Position = new(-32000, -32000);
        window.ShowInTaskbar = false; window.ShowActivated = false;
    }
}
