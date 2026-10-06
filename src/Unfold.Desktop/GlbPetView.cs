using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class GlbPetView : UserControl, IDisposable
{
    private readonly Window owner;
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>> chooseFile;
    private readonly AnimationView preview = new() { Name = "GlbPreview", Width = 240, Height = 240 };
    private readonly TextBlock status = Ui.Caption("GLB 파일을 열어 동작을 연결해 주세요."), info = Ui.Caption("");
    private readonly TextBox name = new() { Name = "GlbPetName", MaxLength = 80, PlaceholderText = "펫 이름" };
    private readonly ComboBox existing = new() { Name = "GlbExistingPets", PlaceholderText = "저장한 GLB 펫 편집" };
    private readonly ComboBox heading = new() { Name = "GlbHeading", ItemsSource = new[] { "정면", "오른쪽 90°", "뒤 180°", "왼쪽 90°" }, SelectedIndex = 0 };
    private readonly ComboBox root = new() { Name = "GlbRoot", PlaceholderText = "자동 선택" };
    private readonly ComboBox previewAction = new() { Name = "GlbPreviewAction", ItemsSource = GlbPetDraft.Actions.Select(GlbPetDraft.ActionName).ToArray(), SelectedIndex = 0 };
    private readonly StackPanel rows = new() { Name = "GlbMappings", Spacing = 8 };
    private readonly Button open, save, replay, pause;
    private GlbPetDraft? draft;
    private int generation;
    private bool closed, populating, paused, loading;
    public bool IsBusy { get; private set; }
    public bool HasUnsavedChanges { get; private set; }
    public event Action? BusyChanged;
    public GlbPetView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseFile = null, bool showHeader = true)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        status.Name = "GlbStatus"; info.Name = "GlbInfo";
        open = Ui.AsyncButton("GLB 파일 열기…", Open); open.Name = "OpenGlbPet";
        save = Ui.AsyncButton("저장하고 적용", Save); save.Name = "SaveGlbPet"; save.IsEnabled = false; Ui.Primary(save);
        replay = Ui.AsyncButton("다시 재생", Play); replay.Name = "ReplayGlbPet";
        pause = Ui.Button("일시정지", () => { paused = !paused; pause!.Content = paused ? "계속 재생" : "일시정지"; UpdatePlayback(); }); pause.Name = "PauseGlbPet";
        foreach (var control in new[] { existing, heading, root, previewAction }) PetManagementView.StyleChoice(control);
        AutomationProperties.SetName(existing, "저장한 GLB 펫 편집");
        existing.SelectionChanged += async (_, _) =>
        {
            if (populating || IsBusy || existing.SelectedItem is not CharacterPackage package) return;
            if (!await CanDiscard()) return;
            await Read(() => new GlbPetDraft(package));
        };
        heading.SelectionChanged += async (_, _) =>
        {
            if (populating || draft is null || heading.SelectedIndex < 0) return;
            draft.Heading = heading.SelectedIndex switch { 1 => 90, 2 => 180, 3 => -90, _ => 0 }; HasUnsavedChanges = true; await Play();
        };
        root.SelectionChanged += async (_, _) =>
        {
            if (populating || draft is null) return;
            draft.RootNode = root.SelectedIndex <= 0 ? null : root.SelectedItem as string; HasUnsavedChanges = true; await Play();
        };
        name.TextChanged += (_, _) => { if (!populating && draft is not null) { draft.Name = name.Text ?? ""; HasUnsavedChanges = true; } };
        previewAction.SelectionChanged += async (_, _) => { if (!populating) await Play(); };
        preview.PlaybackFailed += error => { if (!closed) { status.Text = "재생을 중단했어요. " + Ui.ErrorText(error); status.Foreground = DesignSystem.Error; } };
        preview.Completed += () => { if (!closed && !loading) _ = PlayIdle(); };
        var stage = new Border { Background = DesignSystem.Surface, CornerRadius = DesignSystem.CardRadius, Padding = new Thickness(12),
            Child = new Viewbox { Child = preview, Stretch = Avalonia.Media.Stretch.Uniform, MaxHeight = 260 }, Height = 270 };
        var direction = new Grid { ColumnDefinitions = new("*,12,*") };
        direction.Children.Add(PetManagementView.Field("고정 방향", heading));
        var rootField = PetManagementView.Field("몸 회전을 고정할 루트", root); Grid.SetColumn(rootField, 2); direction.Children.Add(rootField);
        var body = Ui.Column(Ui.Row(open), existing, info, stage, PetManagementView.Field("미리 볼 행동", previewAction),
            Ui.Row(pause, replay), PetManagementView.Field("펫 이름", name), direction,
            Ui.Caption("카메라와 전체 몸 방향은 고정돼요. 팔다리·표정은 움직이고, 클릭·드래그·말풍선은 그대로 사용할 수 있어요."), rows);
        var footer = new Grid { ColumnDefinitions = new("*,12,Auto") }; footer.Children.Add(status); Grid.SetColumn(save, 2); footer.Children.Add(save);
        Content = PetManagementView.Page("GLB로 친구를 만들어 보세요.", "", body, footer, showHeader);
        owner.PropertyChanged += OwnerChanged; AttachedToVisualTree += (_, _) => UpdatePlayback(); DetachedFromVisualTree += (_, _) => UpdatePlayback();
        RefreshExisting(); SetControls();
    }
    private void RefreshExisting()
    {
        populating = true;
        try { existing.ItemsSource = library.List().Where(p => p.IsGlb).ToArray(); existing.SelectedIndex = -1; }
        finally { populating = false; }
    }
    private async Task<string?> PickFile()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "GLB 펫 열기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GLB 3D 모델") { Patterns = ["*.glb"] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private async Task Open()
    {
        if (IsBusy || closed || !await CanDiscard()) return;
        try { var path = await chooseFile(); if (path is not null && !closed) await Read(() => new GlbPetDraft(path)); }
        catch (Exception e) { Error(e); }
    }
    private async Task Read(Func<GlbPetDraft> read)
    {
        SetBusy(true); loading = false; preview.SetRunning(false); generation++;
        try
        {
            var candidate = await Task.Run(read); if (closed) return;
            draft = candidate; Populate(); HasUnsavedChanges = true;
            status.Foreground = DesignSystem.Muted; status.Text = "동작을 선택하고 저장해 주세요.";
            await Play();
        }
        catch (Exception e) { Error(e); }
        finally { SetBusy(false); UpdatePlayback(); }
    }
    private void Populate()
    {
        if (draft is not { } current) return; populating = true;
        try
        {
            name.Text = current.Name;
            heading.SelectedIndex = current.Heading switch { 90 => 1, 180 => 2, -90 => 3, _ => 0 };
            root.ItemsSource = new[] { "자동 선택" }.Concat(current.Model.NodeNames.Distinct()).ToArray(); root.SelectedItem = current.RootNode ?? "자동 선택";
            info.Text = $"{current.FileName} · 삼각형 {current.Model.TriangleCount:N0}개 · 뼈대 관절 {current.Model.JointCount}개 · 동작 {current.Model.Animations.Count}개";
            rows.Children.Clear();
            foreach (var key in GlbPetDraft.Actions)
            {
                var mapping = current.Mappings.GetValueOrDefault(key);
                var options = (key == "idle" ? current.Model.Animations.Select(a => a.Name) : new[] { "기본 동작 유지" }.Concat(current.Model.Animations.Select(a => a.Name))).ToArray();
                var clip = new ComboBox { Name = "GlbClip_" + key, ItemsSource = options, SelectedItem = mapping?.ModelClip ?? options[0], MinWidth = 140 };
                var repeat = new ComboBox { Name = "GlbRepeat_" + key, ItemsSource = new[] { "한 번", "반복" }, SelectedIndex = mapping?.Loop == true || key is "idle" or "held" or "walk" ? 1 : 0,
                    IsEnabled = key is not ("idle" or "walk" or "held" or "pickup" or "land"), Width = 76 };
                var speeds = new[] { .25, .5, 1, 1.5, 2, 3 };
                var speed = new ComboBox { Name = "GlbSpeed_" + key, ItemsSource = new[] { "0.25배", "0.5배", "1배", "1.5배", "2배", "3배" },
                    SelectedIndex = System.Array.IndexOf(speeds, mapping?.Speed ?? 1), Width = 84 };
                foreach (var c in new[] { clip, repeat, speed }) PetManagementView.StyleChoice(c);
                AutomationProperties.SetName(clip, GlbPetDraft.ActionName(key) + " 애니메이션");
                AutomationProperties.SetName(repeat, GlbPetDraft.ActionName(key) + " 반복"); AutomationProperties.SetName(speed, GlbPetDraft.ActionName(key) + " 속도");
                async void Changed(object? sender, SelectionChangedEventArgs e)
                {
                    if (populating || closed || draft != current || speed.SelectedIndex < 0) return;
                    try
                    {
                        current.Set(key, key != "idle" && clip.SelectedIndex <= 0 ? null : clip.SelectedItem as string, repeat.SelectedIndex == 1, speeds[speed.SelectedIndex]);
                        HasUnsavedChanges = true; if (GlbPetDraft.Actions[previewAction.SelectedIndex] == key || key == "idle") await Play();
                        status.Text = "동작 설정을 바꿨어요. 저장하면 적용돼요."; status.Foreground = DesignSystem.Muted;
                    }
                    catch (Exception error) { Error(error); }
                }
                clip.SelectionChanged += Changed; repeat.SelectionChanged += Changed; speed.SelectionChanged += Changed;
                var grid = new Grid { ColumnDefinitions = new("*,8,76,8,84") }; grid.Children.Add(clip); Grid.SetColumn(repeat, 2); grid.Children.Add(repeat); Grid.SetColumn(speed, 4); grid.Children.Add(speed);
                rows.Children.Add(PetManagementView.Field(GlbPetDraft.ActionName(key), grid));
            }
        }
        finally { populating = false; SetControls(); }
    }
    private Task Play() => PlayAction(GlbPetDraft.Actions[Math.Max(0, previewAction.SelectedIndex)]);
    private Task PlayIdle() => PlayAction("idle");
    private async Task PlayAction(string action)
    {
        if (closed || draft is not { } current) return;
        var request = ++generation; loading = true; UpdatePlayback();
        try
        {
            // Snapshot mutable authoring settings before going off-thread.
            var mapping = current.Mappings.GetValueOrDefault(action) ?? current.Mappings["idle"];
            var definition = new GlbDefinition("model.glb", current.Heading, current.RootNode); var idle = current.Mappings["idle"].ModelClip!;
            var frames = await Task.Run(() => current.Model.CreateAnimation(mapping.ModelClip!, definition, idle, mapping.Speed));
            if (closed || request != generation) return;
            preview.SetFrames(frames, mapping.Loop, false); status.Foreground = DesignSystem.Muted;
        }
        catch (Exception e) { if (request == generation) Error(e); }
        finally { if (request == generation) { loading = false; UpdatePlayback(); } }
    }
    private async Task Save()
    {
        if (IsBusy || closed || draft is not { } current) return;
        SetBusy(true); loading = false; generation++; preview.SetRunning(false);
        try
        {
            var package = await Task.Run(() => current.Save(library)); if (closed) return;
            await installed(package); HasUnsavedChanges = false; RefreshExisting();
            status.Text = "GLB 펫을 저장하고 적용했어요."; status.Foreground = DesignSystem.Muted;
        }
        catch (Exception e) { Error(e); }
        finally { SetBusy(false); UpdatePlayback(); }
    }
    private void Error(Exception error) { AppPaths.Log(error); if (!closed) { status.Text = "처리하지 못했어요. " + Ui.ErrorText(error); status.Foreground = DesignSystem.Error; } }
    private void SetBusy(bool value) { IsBusy = value; SetControls(); BusyChanged?.Invoke(); }
    private void SetControls()
    {
        open.IsEnabled = existing.IsEnabled = !IsBusy; save.IsEnabled = !IsBusy && draft is not null;
        name.IsEnabled = heading.IsEnabled = root.IsEnabled = rows.IsEnabled = previewAction.IsEnabled = !IsBusy && draft is not null;
        replay.IsEnabled = pause.IsEnabled = !IsBusy && draft is not null;
    }
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == IsVisibleProperty) UpdatePlayback(); }
    private void UpdatePlayback() => preview.SetRunning(!closed && !IsBusy && !loading && !paused && owner.IsVisible && TopLevel.GetTopLevel(this) is not null);
    private async Task<bool> CanDiscard()
    {
        if (!HasUnsavedChanges) return true;
        return await Ui.Confirm(owner, "GLB 설정을 닫을까요?", "저장하지 않은 동작 설정이 있어요.", "닫기", "계속 편집") == 0;
    }
    public Task<bool> CanCloseDraft() => CanDiscard();
    public void Dispose() { if (closed) return; closed = true; generation++; owner.PropertyChanged -= OwnerChanged; preview.Dispose(); }
}
