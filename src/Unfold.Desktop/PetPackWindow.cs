using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PetPackWindow : Window
{
    public PetPackWindow(CharacterLibrary library, Func<CharacterPackage, Task> installed, Func<Task<string?>>? chooseFile = null,
        Func<Task<string?>>? chooseMedia = null, Func<Task<string?>>? chooseOutput = null)
    {
        Title = "Unfold · 펫 추가"; Width = 520; Height = 850; MinWidth = 480; MinHeight = 560;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var page = new PetManagementView(this, library, installed, chooseFile, chooseMedia, chooseOutput);
        Content = Ui.PageFrame(this, page, inset: 16);
        Closing += (_, e) => { if (page.IsBusy) e.Cancel = true; };
        Closed += (_, _) => page.Dispose();
    }
}

internal sealed class PetPackView : UserControl, IDisposable
{
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>> chooseFile;
    private readonly Window owner;
    private readonly AnimationView preview = new() { Name = "PackPreview", Width = 192, Height = 192 };
    private readonly ComboBox clips = new() { Name = "PackClip", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
    private readonly ComboBox background = new() { Name = "PackBackground", ItemsSource = new[] { "어두운 배경", "밝은 배경" }, SelectedIndex = 0, MinWidth = 130 };
    private readonly ComboBox size = new() { Name = "PackSize", ItemsSource = new[] { "100%", "150%", "200%" }, SelectedIndex = 0, MinWidth = 130 };
    private readonly Border previewSurface;
    private readonly TextBlock playbackStatus = Text("미리 볼 동작을 선택해 주세요.");
    private readonly TextBlock title = Text("펫 팩을 선택해 주세요", 22), version = Text(""), status = Text(".unfoldpet 파일을 열면 설치 전에 펫의 모습을 살펴볼 수 있어요."), warnings = Text("");
    private readonly Button open, install, pause, replay;
    private CharacterPack? pack;
    private CharacterPackInstallInfo? target;
    private bool closed, installing, reading, paused, loadingPreview, previewReady;
    private int generation;
    public bool IsBusy => installing || reading;
    public event Action? BusyChanged;
    public PetPackView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed, Func<Task<string?>>? chooseFile = null)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        warnings.IsVisible = false;
        open = Ui.AsyncButton("펫 팩 열기…", OpenPack); open.Name = "OpenPetPack";
        install = Ui.Button("설치", () => _ = InstallPack()); install.Name = "InstallPetPack"; install.IsEnabled = false;
        pause = Ui.Button("일시정지", () => { paused = !paused; UpdatePlayback(); }); pause.Name = "PausePackPreview";
        replay = Ui.Button("다시 재생", () => { paused = false; _ = PlayClip(); }); replay.Name = "ReplayPackPreview";
        AutomationProperties.SetName(replay, "선택한 동작 다시 재생");
        ToolTip.SetTip(replay, "선택한 동작을 처음부터 다시 재생");
        AutomationProperties.SetName(clips, "미리 볼 동작");
        AutomationProperties.SetName(background, "미리보기 배경");
        AutomationProperties.SetName(size, "미리보기 크기");
        clips.ItemTemplate = new FuncDataTemplate<string>((key, _) => new TextBlock { Text = ClipName(key), FontSize = DesignSystem.Body });
        previewSurface = new Border { Name = "PackPreviewSurface", Background = Ui.Panel, CornerRadius = DesignSystem.CardRadius, Padding = new Thickness(16), Child = preview };
        background.SelectionChanged += (_, _) => previewSurface.Background = background.SelectedIndex == 1 ? Brushes.WhiteSmoke : Ui.Panel;
        size.SelectionChanged += (_, _) => preview.Width = preview.Height = size.SelectedIndex switch { 1 => 288, 2 => 384, _ => 192 };
        clips.SelectionChanged += async (_, _) => await PlayClip();
        preview.Completed += () => { if (!closed && !loadingPreview) _ = PlayClip("idle"); };
        Ui.Primary(install); status.Name = "PackStatus"; version.Foreground = playbackStatus.Foreground = status.Foreground = DesignSystem.Muted;
        warnings.Foreground = DesignSystem.Warning;
        var body = Ui.Column(
            Ui.Actions(open), title, version, previewSurface,
            Ui.Row(Ui.Field("배경", background), Ui.Field("미리보기 크기", size)),
            Ui.Field("미리 볼 동작", clips), Ui.Actions(pause, replay), playbackStatus, warnings);
        body.Spacing = 10;
        Content = Ui.PageContent("새로운 친구를 만나 보세요.", ".unfoldpet 파일을 열어 미리보고 설치하세요.", body,
            Ui.Column(status, Ui.Actions(install)), "펫 추가");
        owner.PropertyChanged += OwnerPropertyChanged;
        AttachedToVisualTree += (_, _) => UpdatePlayback();
        DetachedFromVisualTree += (_, _) => UpdatePlayback();
        UpdatePlayback();
    }
    private static string ClipName(string? key) => key switch
    {
        "idle" => "쉬는 모습", "attention" => "휴식 안내", "stretch" => "스트레칭",
        "celebrate" => "휴식 완료", "click" => "클릭 반응", _ => key ?? ""
    };
    private static string InstallLabel(string action) => action switch
    {
        "Install" => "설치", "Update" => "업데이트", "Reinstall" => "재설치",
        _ => throw new InvalidOperationException("Unknown install action.")
    };
    private void UpdatePlayback()
    {
        pause.IsEnabled = replay.IsEnabled = previewReady && !loadingPreview && !closed;
        pause.Content = paused ? "계속" : "일시정지";
        var label = paused ? "미리보기 계속" : "미리보기 일시정지";
        AutomationProperties.SetName(pause, label); ToolTip.SetTip(pause, label);
        preview.SetRunning(TopLevel.GetTopLevel(this) is not null && owner.IsVisible && !closed && previewReady && !loadingPreview && !paused);
    }
    private static TextBlock Text(string value, double size = 14) => new() { Text = value, FontSize = size, Foreground = DesignSystem.Cream, TextWrapping = TextWrapping.Wrap };
    private async Task<string?> PickFile()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "펫 팩 열기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Unfold 펫 팩") { Patterns = ["*.unfoldpet"] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) UpdatePlayback();
    }
    public void Dispose()
    {
        if (closed) return;
        closed = true; generation++; owner.PropertyChanged -= OwnerPropertyChanged;
        preview.Dispose(); DisposePack();
    }
    public Task OpenPath(string path) => ReadPack(() => Task.FromResult<string?>(path));
    private Task OpenPack() => ReadPack(chooseFile);
    private async Task ReadPack(Func<Task<string?>> choose)
    {
        if (closed || IsBusy) return;
        CharacterPack? candidate = null;
        reading = true; open.IsEnabled = false; BusyChanged?.Invoke();
        try
        {
            var path = await choose(); if (closed || path is null) return;
            generation++; previewReady = false; loadingPreview = false; paused = false; UpdatePlayback();
            install.IsEnabled = clips.IsEnabled = false; install.Content = "설치"; status.Text = "펫 팩을 확인하고 있어요…";
            playbackStatus.Text = "미리 볼 동작을 선택해 주세요.";
            DisposePack(); target = null; clips.ItemsSource = null; preview.SetFrames([], true);
            title.Text = "펫 팩을 선택해 주세요"; version.Text = warnings.Text = ""; warnings.IsVisible = false;
            candidate = await Task.Run(() => CharacterPack.Open(path));
            var info = await Task.Run(() => library.InspectInstall(candidate));
            if (closed) return;
            pack = candidate; candidate = null; target = info;
            title.Text = pack.Character.Manifest.Name;
            version.Text = $"버전 {pack.ContentVersion}" + (info.InstalledVersion is { } current ? $" · 설치된 버전 {current}" : " · 새로운 펫");
            warnings.Text = pack.Audit.Warnings.Count == 0 ? "" : "미리보기 참고 사항\n" + string.Join("\n", pack.Audit.Warnings);
            warnings.IsVisible = pack.Audit.Warnings.Count > 0;
            clips.ItemsSource = pack.Character.Manifest.Animations.Keys.Order().ToArray(); clips.SelectedItem = "idle"; clips.IsEnabled = true;
            install.Content = InstallLabel(info.Action);
            status.Text = info.Action == "Install" ? "설치할 준비가 됐어요. 기존 펫은 그대로 유지돼요." :
                $"{InstallLabel(info.Action)}할 준비가 됐어요. 이 펫의 설치 파일을 교체해요.";
            await PlayClip();
            if (!closed) install.IsEnabled = previewReady;
        }
        catch (Exception error) { if (!closed) { install.IsEnabled = false; status.Text = "팩을 열지 못했어요. " + Ui.ErrorText(error); } AppPaths.Log(error); }
        finally { candidate?.Dispose(); reading = false; if (!closed) { open.IsEnabled = true; BusyChanged?.Invoke(); } }
    }
    private async Task PlayClip(string? returnTo = null)
    {
        if (closed || pack is not { } current || clips.SelectedItem is not string selected) return;
        var key = returnTo ?? selected;
        var request = ++generation;
        // Stop the previous clip before decoding so its completion cannot replace a newer selection.
        loadingPreview = true; UpdatePlayback();
        try
        {
            var frames = await Task.Run(() => current.Character.LoadAnimation(key));
            if (closed || request != generation || pack != current) return;
            preview.SetFrames(frames, current.Character.Manifest.Animations[key].Loop, current.Character.Manifest.RenderStyle == "pixel");
            previewReady = true;
            playbackStatus.Text = returnTo is not null ? "쉬는 모습으로 돌아왔어요. ‘다시 재생’을 누르면 선택한 반응을 다시 볼 수 있어요." :
                $"{ClipName(key)} · {frames.Sum(frame => frame.Duration.TotalSeconds):0.###}초 · {(current.Character.Manifest.Animations[key].Loop ? "반복 재생" : "한 번 재생 후 쉬는 모습으로 전환")}";
        }
        catch (Exception error) { AppPaths.Log(error); if (!closed && request == generation) { previewReady = false; status.Text = "미리보기를 재생하지 못했어요. " + Ui.ErrorText(error); install.IsEnabled = false; } }
        finally { if (!closed && request == generation) { loadingPreview = false; UpdatePlayback(); } }
    }
    private async Task InstallPack()
    {
        if (IsBusy || closed || pack is null || target is null) return;
        installing = true; install.IsEnabled = open.IsEnabled = false; BusyChanged?.Invoke(); status.Text = "펫을 설치하고 있어요…";
        CharacterPackage? result = null;
        try
        {
            result = await Task.Run(() => library.Install(pack, target.Revision));
            if (closed) return;
            await installed(result);
            status.Text = "펫을 설치하고 선택했어요. 타이머 탭에서 만나 보세요."; install.Content = "설치 완료";
        }
        catch (Exception error)
        {
            status.Text = result is null ? "설치하지 못했어요. " + Ui.ErrorText(error) + " 팩을 다시 열어 시도해 주세요." :
                "팩을 설치했지만 선택하지 못했어요. 설정 창을 다시 열어 펫을 선택해 주세요. " + Ui.ErrorText(error);
            AppPaths.Log(error);
        }
        finally { installing = false; if (!closed) { open.IsEnabled = true; BusyChanged?.Invoke(); } }
    }
    private void DisposePack()
    {
        try { pack?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppPaths.Log(error); }
        pack = null;
    }
}
