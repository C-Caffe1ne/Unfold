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
        // The minimum-width 200% preview needs a few pixels reclaimed from the frame after the
        // shared page body reserves a visible scrollbar track and gutter.
        Content = Ui.PageFrame(this, page, inset: 10);
        Closing += (_, e) => { if (page.IsBusy) e.Cancel = true; };
        Closed += (_, _) => page.Dispose();
    }
}

internal sealed class PetPackView : UserControl, IDisposable
{
    private static readonly Geometry PauseIcon = Geometry.Parse("M 6,4 H 10 V 20 H 6 Z M 14,4 H 18 V 20 H 14 Z");
    private static readonly Geometry PlayIcon = Geometry.Parse("M 7,4 L 21,12 L 7,20 Z");
    private static readonly Geometry ReplayIcon = Geometry.Parse("M 12,5 V 1 L 7,6 L 12,11 V 7 C 15.3,7 18,9.7 18,13 C 18,16.3 15.3,19 12,19 C 8.7,19 6,16.3 6,13 H 4 C 4,17.4 7.6,21 12,21 C 16.4,21 20,17.4 20,13 C 20,8.6 16.4,5 12,5 Z");
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>> chooseFile;
    private readonly Window owner;
    private readonly AnimationView preview = new() { Name = "PackPreview", Width = 192, Height = 192 };
    private readonly ComboBox clips = new() { Name = "PackClip", Width = DesignSystem.PetChoiceWidth, PlaceholderText = "파일 선택 후 확인",
        HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = false };
    private readonly ComboBox size = new() { Name = "PackSize", ItemsSource = new[] { "100%", "150%", "200%" }, SelectedIndex = 0, Width = DesignSystem.PetPreviewOptionWidth };
    private readonly Border previewSurface;
    private readonly TextBlock playbackStatus = Text("", DesignSystem.Caption);
    private readonly TextBlock title = Text("", DesignSystem.Section), version = Text("", DesignSystem.Caption),
        status = Text("", DesignSystem.Caption);
    private readonly TextBlock previewHint = Ui.Caption("");
    private readonly WrapPanel packInfo = new() { Name = "PackInfo", ItemSpacing = 12, LineSpacing = 4, IsVisible = false };
    private readonly Grid previewStage = new() { MinHeight = 192 };
    private readonly Button open, install, pause, replay;
    private CharacterPack? pack;
    private CharacterPackInstallInfo? target;
    private bool closed, installing, reading, paused, loadingPreview, previewReady;
    private int generation;
    public bool IsBusy => installing || reading;
    public event Action? BusyChanged;
    public PetPackView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseFile = null, bool showHeader = true)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        open = Ui.AsyncButton("펫 팩 열기…", OpenPack); open.Name = "OpenPetPack";
        install = Ui.Button("저장", () => _ = InstallPack()); install.Name = "InstallPetPack"; install.IsEnabled = false;
        pause = Ui.Button("", () => { paused = !paused; UpdatePlayback(); }); pause.Name = "PausePackPreview";
        replay = Ui.Button("", () => { paused = false; _ = PlayClip(); }); replay.Name = "ReplayPackPreview";
        ConfigurePreviewButton(pause, PauseIcon, "미리보기 일시정지");
        ConfigurePreviewButton(replay, ReplayIcon, "선택한 동작 다시 재생");
        ToolTip.SetTip(replay, "선택한 동작을 처음부터 다시 재생");
        AutomationProperties.SetName(clips, "미리 볼 동작");
        AutomationProperties.SetName(size, "미리보기 크기");
        PetManagementView.StyleChoice(clips);
        PetManagementView.StyleChoice(size, previewOption: true);
        clips.ItemTemplate = new FuncDataTemplate<string>((key, _) => new TextBlock { Text = ClipName(key), FontSize = DesignSystem.Body });
        preview.HorizontalAlignment = HorizontalAlignment.Center;
        previewHint.Name = "PackPreviewHint";
        previewHint.HorizontalAlignment = HorizontalAlignment.Center; previewHint.VerticalAlignment = VerticalAlignment.Center;
        previewHint.TextAlignment = TextAlignment.Center; previewHint.MaxWidth = 300;
        previewStage.Children.Add(preview); previewStage.Children.Add(previewHint);
        var playbackControls = new StackPanel { Name = "PackPlaybackControls", Orientation = Orientation.Horizontal,
            Spacing = DesignSystem.Space, HorizontalAlignment = HorizontalAlignment.Center };
        playbackControls.Children.Add(pause); playbackControls.Children.Add(replay);
        var previewContent = Ui.Column(previewStage, playbackControls); previewContent.Spacing = DesignSystem.Gap;
        previewSurface = new Border { Name = "PackPreviewSurface", Background = Ui.Panel,
            CornerRadius = DesignSystem.CardRadius, Padding = new Thickness(12), MaxWidth = DesignSystem.PetPreviewWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch, Child = previewContent };
        size.SelectionChanged += (_, _) => previewStage.MinHeight = preview.Width = preview.Height = size.SelectedIndex switch { 1 => 288, 2 => 384, _ => 192 };
        clips.SelectionChanged += async (_, _) => await PlayClip();
        preview.Completed += () => { if (!closed && !loadingPreview) _ = PlayClip("idle"); };
        Ui.Primary(install); status.Name = "PackStatus"; version.Foreground = playbackStatus.Foreground = status.Foreground = DesignSystem.Muted;
        title.FontWeight = FontWeight.SemiBold; title.MaxWidth = 320;
        packInfo.Children.Add(title); packInfo.Children.Add(version);
        playbackStatus.Name = "PackPlaybackStatus"; playbackStatus.TextAlignment = TextAlignment.Center;
        playbackStatus.Margin = new Thickness(0, -8, 0, 0);
        install.Width = 80; install.Height = open.Height = DesignSystem.PetControlHeight; open.Width = 120;
        open.HorizontalAlignment = HorizontalAlignment.Left;
        var previewOptions = new WrapPanel
        {
            Name = "PackPreviewOptions", Orientation = Orientation.Horizontal,
            ItemSpacing = DesignSystem.Gap, LineSpacing = DesignSystem.Gap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        previewOptions.Children.Add(PetManagementView.Field("미리 볼 동작", clips));
        previewOptions.Children.Add(PetManagementView.Field("미리보기 크기", size));
        var body = Ui.Column(open, packInfo, previewSurface, previewOptions, playbackStatus);
        body.Spacing = DesignSystem.Inset;
        var footer = new Grid { ColumnDefinitions = new("*,20,Auto") };
        status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(status); Grid.SetColumn(install, 2); footer.Children.Add(install);
        install.VerticalAlignment = VerticalAlignment.Center;
        Content = PetManagementView.Page("새로운 친구를 만나 보세요.", "", body,
            footer, showHeader);
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
    private void UpdatePlayback()
    {
        previewHint.IsVisible = false;
        pause.IsEnabled = replay.IsEnabled = previewReady && !loadingPreview && !closed;
        ((PathIcon)pause.Content!).Data = paused ? PlayIcon : PauseIcon;
        var label = paused ? "미리보기 계속" : "미리보기 일시정지";
        AutomationProperties.SetName(pause, label); ToolTip.SetTip(pause, label);
        preview.SetRunning(TopLevel.GetTopLevel(this) is not null && owner.IsVisible && !closed && previewReady && !loadingPreview && !paused);
    }
    private static void ConfigurePreviewButton(Button button, Geometry icon, string label)
    {
        button.Content = new PathIcon { Data = icon, Width = 20, Height = 20 };
        button.Width = 44; button.Height = 44; button.MinHeight = 44; button.Padding = new Thickness(10);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
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
            install.IsEnabled = clips.IsEnabled = false; status.Foreground = DesignSystem.Muted; status.Text = "펫 팩을 확인하고 있어요…";
            playbackStatus.Text = "";
            DisposePack(); target = null; clips.ItemsSource = null; preview.SetFrames([], true);
            title.Text = ""; version.Text = ""; packInfo.IsVisible = false;
            candidate = await Task.Run(() => CharacterPack.Open(path));
            var info = await Task.Run(() => library.InspectInstall(candidate));
            if (closed) return;
            pack = candidate; candidate = null; target = info;
            title.Text = pack.Character.Manifest.Name;
            packInfo.IsVisible = true;
            version.Text = $"버전 {pack.ContentVersion}" + (info.InstalledVersion is { } current ? $" · 설치된 버전 {current}" : " · 새로운 펫");
            clips.ItemsSource = pack.Character.Manifest.Animations.Keys.Order().ToArray(); clips.SelectedItem = "idle"; clips.IsEnabled = true;
            status.Text = info.Action switch
            {
                "Update" => "업데이트 준비 완료", "Reinstall" => "재설치 준비 완료", _ => "저장 준비 완료"
            };
            await PlayClip();
            if (!closed) install.IsEnabled = previewReady;
        }
        catch (Exception error) { if (!closed) { install.IsEnabled = false; status.Foreground = DesignSystem.Error; status.Text = "팩을 열지 못했어요. " + Ui.ErrorText(error); } AppPaths.Log(error); }
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
            status.Foreground = DesignSystem.Muted;
            playbackStatus.Text = returnTo is not null ? "쉬는 모습" :
                $"{ClipName(key)} · {frames.Sum(frame => frame.Duration.TotalSeconds):0.###}초 · {(current.Character.Manifest.Animations[key].Loop ? "반복" : "1회")}";
        }
        catch (Exception error) { AppPaths.Log(error); if (!closed && request == generation) { previewReady = false; status.Foreground = DesignSystem.Error; status.Text = "미리보기를 재생하지 못했어요. " + Ui.ErrorText(error); install.IsEnabled = false; } }
        finally { if (!closed && request == generation) { loadingPreview = false; UpdatePlayback(); } }
    }
    private async Task InstallPack()
    {
        if (IsBusy || closed || pack is null || target is null) return;
        installing = true; install.IsEnabled = open.IsEnabled = false; BusyChanged?.Invoke(); status.Foreground = DesignSystem.Muted; status.Text = "펫을 저장하고 있어요…";
        CharacterPackage? result = null;
        try
        {
            result = await Task.Run(() => library.Install(pack, target.Revision));
            if (closed) return;
            await installed(result);
            ResetSavedPack();
            status.Text = "저장했어요.";
        }
        catch (Exception error)
        {
            status.Foreground = DesignSystem.Error;
            status.Text = result is null ? "저장하지 못했어요. " + Ui.ErrorText(error) + " 팩을 다시 열어 시도해 주세요." :
                "팩을 저장했지만 선택하지 못했어요. 설정 창을 다시 열어 펫을 선택해 주세요. " + Ui.ErrorText(error);
            AppPaths.Log(error);
        }
        finally { installing = false; if (!closed) { open.IsEnabled = true; BusyChanged?.Invoke(); } }
    }
    private void ResetSavedPack()
    {
        generation++; previewReady = loadingPreview = paused = false;
        preview.SetFrames([], true); DisposePack(); target = null;
        clips.ItemsSource = null; clips.SelectedIndex = -1; clips.IsEnabled = install.IsEnabled = false;
        title.Text = version.Text = playbackStatus.Text = ""; packInfo.IsVisible = false;
        size.SelectedIndex = 0;
        UpdatePlayback();
    }
    private void DisposePack()
    {
        try { pack?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppPaths.Log(error); }
        pack = null;
    }
}
