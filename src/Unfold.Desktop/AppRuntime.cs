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

public sealed partial class AppRuntime : IDisposable
{
    internal const string AccountWelcomeRevision = "account-checkout-kr-v1";
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
    internal AccountWindow? ActiveAccount => accountWindow;
    internal AccountSession? AccountSession => accountSession;
    internal bool AccountSignOutPending { get; private set; }
    internal Func<IAccountScreenService>? AccountServiceFactory { get; set; }
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
    internal TimeSpan DiagnosticTime => monotonic.Elapsed;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer noticeExpiryTimer = new();
    private TimeSpan? scheduledNoticeExpiry;
    private readonly Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> clips = [];
    private readonly List<CharacterPackage> builtIns = [];
    private TrayIcon? tray;
    private NativeMenuItem? trayStatus, trayPause, trayPet, trayFocusReminder;
    private SettingsWindow? settingsWindow;
    private AccountWindow? accountWindow;
    private AccountSession? accountSession;
    private readonly CancellationTokenSource accountLifetime = new();
    internal AccountScreenContent AccountContent { get; } = AccountScreenContent.Load();
    private readonly string accountWelcomeFile = Path.Combine(AppPaths.DataRoot, "account-welcome-seen");
    private PetWindow? pet;
    private readonly ReminderSoundPlayer soundPlayer = new();
    internal int DueSoundRequests { get; private set; }
    internal int CompletionSoundRequests { get; private set; }
    private bool quitting;
    private bool backgroundStart;
    private bool disposed;
    private bool quitPending, stopPending;
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
        var backupSettings = false;
        try { Settings = AppSettings.Load(settingsFile, out backupSettings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            AppPaths.Log(ex); Settings = new(); backupSettings = true;
        }
        if (backupSettings)
        {
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
        backgroundStart = background;
        purchaseGateEnabled = !diagnostic;
        accountStartupPending = purchaseGateEnabled;
        settingsWindow = new(this); desktop.MainWindow = settingsWindow;
        instanceActivation = new SingleInstance(() =>
        {
            if (accountWindow is { IsVisible: true }) { accountWindow.WindowState = WindowState.Normal; accountWindow.Activate(); }
            else ShowSettings();
        });
        if (diagnostic) PrepareDiagnosticWindow(settingsWindow);
        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(AppPaths.BuiltInRoot)) throw new DirectoryNotFoundException("기본 펫 파일이 없어요. Unfold를 다시 설치해 주세요.");
                foreach (var directory in Directory.EnumerateDirectories(AppPaths.BuiltInRoot)) builtIns.Add(CharacterLibrary.LoadPackage(directory, true));
                if (builtIns.Count == 0) throw new InvalidDataException("기본 펫을 찾지 못했어요. Unfold를 다시 설치해 주세요.");
            });
            await Reload();
            if (disposed || quitting) return;
            BuildTray();
            if (purchaseGateEnabled)
            {
                Clock.Stop(monotonic.Elapsed);
                if (accountStartupPending) await RestoreAccountAtStartup();
            }
            else
            {
                timer.Start(); Clock.Start(monotonic.Elapsed); await UpdatePet();
                if (!background) ShowSettings();
            }
        }
        catch (Exception error)
        {
            if (diagnostic) throw;
            CancelAccountRestore();
            if (disposed || quitting) return;
            ShowSettings();
            await Ui.Error((Window?)accountWindow ?? settingsWindow, error);
        }
    }
    public async Task Reload()
    {
        var selectedId = Settings.SelectedCharacterId;
        var users = await Task.Run(() =>
        {
            var listed = Library.List(loadModels: false).ToList();
            // Keep the existing startup fallback for a damaged selected pet,
            // while leaving all unselected GLB models undecoded.
            var selected = listed.FirstOrDefault(p => p.Manifest.Id == selectedId && p.IsGlb);
            if (selected is not null)
            {
                try { _ = selected.Model; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException)
                { AppPaths.Log(error); listed.Remove(selected); }
            }
            return listed;
        });
        var bundledIds = builtIns.Select(item => item.Manifest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Characters = builtIns.Concat(users.Where(item => !bundledIds.Contains(item.Manifest.Id))).ToArray();
        clips.Clear(); Changed?.Invoke();
    }
    public async Task SelectInstalledCharacter(CharacterPackage character)
    {
        await Reload(); await UpdateSettings(Settings with { SelectedCharacterId = character.Manifest.Id });
    }
    internal async Task RefreshAfterCharacterRemoval(string id)
    {
        await Reload();
        if (Settings.SelectedCharacterId == id)
            await UpdateSettings(Settings with { SelectedCharacterId = Selected?.Manifest.Id ?? "default-cat" });
    }
    private void Tick()
    {
        if (!AccessAllowed) return;
        if (purchaseGateEnabled && DateTimeOffset.UtcNow >= nextAccessCheck) _ = RecheckPurchaseAccess();
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
        if (trayFocusReminder is not null) trayFocusReminder.IsEnabled = AccessAllowed && Reminder.Session is not null;
        if (trayPause is not null) trayPause.IsEnabled = AccessAllowed;
        if (trayPet is not null) trayPet.IsEnabled = AccessAllowed;
        if (!AccessAllowed)
        {
            TrayStatus = new("로그인·구매 확인 필요", "Unfold · 로그인·구매 확인 필요");
            if (tray is not null) tray.ToolTipText = TrayStatus.ToolTip;
            if (trayStatus is not null) trayStatus.Header = TrayStatus.Status;
        }
        if (trayPause is not null) trayPause.Header = Clock.Stopped ? "시작" : Clock.Paused ? "계속" : "일시정지";
        if (trayPet is not null) trayPet.Header = Settings.ShowPet ? "펫 숨기기" : "펫 표시";
    }
    public void TogglePause() { if (!AccessAllowed) return; Clock.TogglePause(monotonic.Elapsed); Changed?.Invoke(); }
    public void Stop() { CancelReminder(); Clock.Stop(monotonic.Elapsed); Changed?.Invoke(); }
    public async Task RequestStop()
    {
        if (!AccessAllowed || quitting || disposed || stopPending || quitPending || updateRestartPending) return;
        stopPending = true;
        try
        {
            if (await ConfirmAction("타이머를 중지할까요?", "타이머를 중지하면 남은 시간이 초기화되고 진행 중인 휴식이 취소돼요.", "중지", "취소") == 0 && !disposed)
                Stop();
        }
        finally { stopPending = false; }
    }
    public void Reset() { if (!AccessAllowed) return; CancelReminder(); Clock.Reset(monotonic.Elapsed); Changed?.Invoke(); }
    private void CancelReminder() { reminderGeneration++; Reminder.Cancel(); RefreshPetNotice(); }
    public void ShowSettings() { if (accountStartupPending) { backgroundStart = false; return; } if (!AccessAllowed) { ShowAccount(); return; } if (settingsWindow is null) return; settingsWindow.Show(); settingsWindow.ResumePreview(); settingsWindow.WindowState = WindowState.Normal; if (!DiagnosticMode) settingsWindow.Activate(); }
    internal static bool HasSeenCurrentAccountWelcome(string path)
    {
        if (!File.Exists(path)) return false;
        try { return File.ReadAllText(path).Trim() == AccountWelcomeRevision; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AppPaths.Log(error);
            return false;
        }
    }
    public void ShowAccount()
    {
        if (disposed || quitting || AccountSignOutPending || accountStartupPending) return;
        if (accountWindow is not null) { accountWindow.WindowState = WindowState.Normal; accountWindow.Activate(); return; }
        ShowAccount(CreateAccountModel());
    }
    private void ShowAccount(AccountScreenModel model)
    {
        var window = new AccountWindow(model, Quit, purchaseGateEnabled, () => disposed || quitting || AccountSignOutPending);
        accountWindow = window;
        window.Closed += async (_, _) =>
        {
            model.SessionChanged -= AccountSessionChanged;
            accountWindow = null;
            if (disposed || quitting) return;
            if (purchaseGateEnabled)
            {
                if (AccountSignOutPending) return;
                if (model.PurchaseReady && accountSession is not null) await GrantPurchaseAccess(!model.RestoredAutomatically || !backgroundStart);
                else ShowAccount();
                return;
            }
            // This records only dismissal of a welcome screen, never authentication or purchase.
            try { File.WriteAllText(accountWelcomeFile, AccountWelcomeRevision); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppPaths.Log(error); }
            ShowSettings();
        };
        if (DiagnosticMode) PrepareDiagnosticWindow(window);
        window.Show();
    }
    private IAccountScreenService CreateAccountService() => AccountServiceFactory?.Invoke() ?? new DesktopAccountService(AccountContent);
    public async Task SignOut()
    {
        if (disposed || AccountSignOutPending) return;
        AccountSignOutPending = true;
        CancelAccountRestore();
        LockPurchaseAccess();
        var session = accountSession;
        accountSession = null;
        accountWindow?.Close();
        Changed?.Invoke();
        var failed = false;
        var storageFailed = false;
        try
        {
            using var service = CreateAccountService();
            if (session is not null)
            {
                await service.SignOutAsync(session, accountLifetime.Token);
            }
            else await service.ClearSavedSessionAsync();
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error)
        { AppPaths.Log(error); failed = true; storageFailed = error is AccountException { Failure: AccountFailure.SessionStorageUnavailable }; }
        finally
        {
            AccountSignOutPending = false;
            if (!disposed)
            {
                Changed?.Invoke(); ShowAccount();
                if (failed) accountWindow?.Model.ReportSignOutFailure(storageFailed);
                settingsWindow?.HideToTray();
            }
        }
    }
    internal void HideSettingsForDiagnostics() => settingsWindow?.HideToTray();
    public Task UpdateSettings(AppSettings value) => UpdateSettings(value, false);
    public Task HidePet() => UpdateSettings(Settings with { ShowPet = false }, true);
    private async Task UpdateSettings(AppSettings value, bool hideCurrentNotice)
    {
        if (!AccessAllowed) throw new AccountException(AccountFailure.AuthenticationRequired);
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
        if (!AccessAllowed) return;
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
        if (clips.TryGetValue(cacheKey, out var cached) && !cached.IsFaulted && !cached.IsCanceled) return cached;
        // Sharing the in-flight task prevents the pet and settings preview
        // from decoding/uploading the same source independently on startup.
        // Retry a failed decode on the next request. No completion callback may evict
        // a newer task after Reload/character changes have replaced this cache entry.
        return clips[cacheKey] = Task.Run(() => selected.LoadAnimation(key));
    }
    private async Task UpdatePet()
    {
        if (disposed || !ShouldShowPet || Selected is not { } selected) { pet?.HidePet(); return; }
        var current = pet ??= new PetWindow(this);
        if (DiagnosticMode) PrepareDiagnosticWindow(current);
        try { await current.SetCharacter(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (disposed || pet != current || Selected != selected || !ShouldShowPet) return;
            if (!PresentedReminder.HasNotice) throw;
            // A damaged animation must not suppress the existing reminder controls.
            // The package's already validated sheet provides a still image without IO.
            AppPaths.Log(error); current.ShowReminderFallback(selected);
        }
        // A concurrent hide or quit can complete while SetCharacter() is in flight
        // (Dispose() nulls pet, another UpdatePet() call flips ShowPet off): re-check
        // both before resurrecting a pet nobody asked for anymore.
        if (pet == current && ShouldShowPet) current.ShowPet();
    }
    private bool ShouldShowPet => AccessAllowed && (Settings.ShowPet || (!petNoticeSuppressed && PresentedReminder.HasNotice));
    public async Task ShowReminder()
    {
        if (!AccessAllowed || disposed || quitting) return;
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
            var session = new BreakSession(routine, selected.Manifest.Id, profile, Settings.BreakDurationMinutes * 60);
            if (!Reminder.Invite(session)) return;
            petNoticeSuppressed = false;
            // Deliver the notice exactly once, independently of image decoding.
            PlayReminderSound(ReminderSound.Due);
            RefreshPetNotice(); Changed?.Invoke();
            await UpdatePet();
            if (disposed || quitting || generation != reminderGeneration || Reminder.Session != session) return;
            if (Reminder.Notice == PetNotice.Invitation) ReactToBreak(session, "attention");
            RefreshPetNotice(); Changed?.Invoke();
        }
        catch (Exception error) { if (disposed || quitting || generation != reminderGeneration) return; if (DiagnosticMode) throw; AppPaths.Log(error); }
        finally { openingReminder = false; }
    }
    public void StartBreak() { if (!AccessAllowed) return; Reminder.Start(monotonic.Elapsed); RefreshPetNotice(); Changed?.Invoke(); }
    public void SnoozeBreak() { if (!AccessAllowed) return; Reminder.Snooze(); RefreshPetNotice(); Changed?.Invoke(); }
    public void CompleteBreak() { if (!AccessAllowed) return; Reminder.Complete(monotonic.Elapsed); RefreshPetNotice(); Changed?.Invoke(); }
    public async Task ShowReminderPreview(PetNotice notice)
    {
        if (!AccessAllowed) return;
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
        if (!AccessAllowed || Reminder.Session is null) return;
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
        if (!AccessAllowed || !Settings.ReminderSoundsEnabled) return;
        if (sound == ReminderSound.Due) DueSoundRequests++; else CompletionSoundRequests++;
        if (!DiagnosticMode) soundPlayer.Play(sound, Settings);
    }
    private void RefreshPetNotice()
    {
        RefreshTray();
        if (pet is { } current)
        {
            current.RefreshSpeech();
            if (ShouldShowPet)
            { if (!current.IsVisible) current.ShowPet(); }
            else if (current.IsVisible) current.HidePet();
            if (current.IsVisible && !petNoticeSuppressed && reminderPreview is null)
                Reminder.MarkInvitationPresented(monotonic.Elapsed);
        }
        ScheduleNoticeExpiry();
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
            Reminder.Tick(monotonic.Elapsed); scheduledNoticeExpiry = null;
            // Expiry happened after this refresh's visibility decision. Apply the new notice now.
            RefreshPetNotice(); return;
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
        tray = BrandTrayIcon.Create();
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
        menu.Items.Add(new NativeMenuItemSeparator()); InitializeUpdates(menu); Item("Unfold 종료", () => _ = Quit());
        tray.Menu = menu; tray.Clicked += (_, _) => ShowSettings();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
        RefreshTray();
    }
    public async Task Quit()
    {
        if (quitting || disposed || quitPending || stopPending || updateRestartPending) return;
        quitPending = true;
        try
        {
            // Startup has no visible owner or running paid features to confirm.
            if (!accountStartupPending && await ConfirmAction("Unfold를 종료할까요?", "Unfold를 종료하면 타이머와 휴식 알림도 종료돼요.", "종료", "취소") != 0) return;
            if (disposed) return;
            var lockedOwner = AccessAllowed ? null : accountWindow;
            if (settingsWindow is not null && !await settingsWindow.CanCloseDraft(lockedOwner)) return;
            quitting = true; Dispose(); desktop.Shutdown();
        }
        finally { quitPending = false; }
    }
    private Task<int> ConfirmAction(string title, string message, params string[] choices)
    {
        if (ConfirmActionOverride is { } confirm) return confirm(title, message, choices);
        var owner = accountWindow is { IsVisible: true } ? accountWindow : (Window?)settingsWindow ?? desktop.MainWindow ?? pet;
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
    public void Dispose() { if (disposed) return; disposed = true; DisposeUpdates(); CancelAccountRestore(); accountLifetime.Cancel(); accountLifetime.Dispose(); accountSession = null; accountWindow?.Close(); timer.Stop(); noticeExpiryTimer.Stop(); scheduledNoticeExpiry = null; settingsWindow?.Dispose(); instanceActivation?.Dispose(); instanceActivation = null; tray?.Dispose(); tray = null; pet?.ClosePet(); pet = null; soundPlayer.Dispose(); }
    internal static void PrepareDiagnosticWindow(Window window)
    {
        // macOS can constrain an off-screen window down to 1x1 without explicit minimums.
        if (double.IsFinite(window.Width)) window.MinWidth = window.Width;
        if (double.IsFinite(window.Height)) window.MinHeight = window.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Position = new(-32000, -32000);
        window.ShowInTaskbar = false; window.ShowActivated = false;
    }
}
