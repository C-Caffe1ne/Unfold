using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class GlbPetView : UserControl, IDisposable
{
    private readonly Window owner;
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<string, Task>? removed;
    private readonly Func<IReadOnlyList<CharacterPackage>>? existingPets;
    private readonly Func<CharacterPackage, Task>? openPackage;
    private readonly Func<Task<string?>> chooseFile;
    private readonly AnimationView preview = new() { Name = "GlbPreview", Width = 240, Height = 240 };
    private readonly TextBlock status = Ui.Caption("");
    private readonly TextBox name = new() { Name = "GlbPetName", MaxLength = 80, PlaceholderText = "펫 이름", Height = DesignSystem.PetControlHeight };
    private readonly ComboBox existing = new() { Name = "GlbExistingPets", PlaceholderText = "편집할 펫 선택" };
    private readonly ComboBox previewAction = new() { Name = "GlbPreviewAction" };
    private readonly StackPanel rows = new() { Name = "GlbMappings", Spacing = 8 };
    private readonly Dictionary<string, Control> actionRows = [];
    private readonly Button open, save, replay, pause, edit, remove, fileAdd, fileRemove;
    private readonly Image fileThumbnail = new() { Name = "GlbFileThumbnail", Width = 32, Height = 40, Stretch = Stretch.Uniform };
    private readonly TextBlock fileLabel = Ui.Caption("파일 없음");
    private Bitmap? thumbnailBitmap;
    private readonly Control editor, existingField;
    private readonly Button? export;
    private readonly Func<string, Task>? created;
    private readonly Func<Task<string?>>? chooseOutput;
    private GlbPetDraft? draft;
    private int generation;
    private bool closed, populating, paused, loading, previewCompleted, previewFailed;
    public bool IsBusy { get; private set; }
    public bool HasUnsavedChanges { get; private set; }
    internal bool HasDraft => draft is not null;
    internal string? EditingId => draft?.Id;
    internal bool IsPreviewLoading => loading;
    private string SelectedAction => GlbPetDraft.Actions[Math.Max(0, previewAction.SelectedIndex)];
    public event Action? BusyChanged;
    public event Action? LibraryChanged;
    public GlbPetView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseFile = null, bool showHeader = true, bool embedded = false,
        Func<string, Task>? created = null, Func<Task<string?>>? chooseOutput = null, Func<Task>? openFile = null,
        Func<string, Task>? removed = null, Func<IReadOnlyList<CharacterPackage>>? existingPets = null,
        Func<CharacterPackage, Task>? openPackage = null)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        status.Name = "GlbStatus"; this.created = created; this.chooseOutput = chooseOutput; this.removed = removed;
        this.existingPets = existingPets; this.openPackage = openPackage;
        status.IsVisible = false;
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) status.IsVisible = !string.IsNullOrWhiteSpace(status.Text); };
        open = ActionButton("파일 열기…", openFile ?? Open); open.Name = embedded ? "OpenPetBuilderFile" : "OpenGlbPet";
        edit = ActionButton("편집", async () => { if (StoredDraft is { } package) await OpenPackage(package); }); edit.Name = "EditGlbPet";
        remove = ActionButton("펫 삭제", Remove); remove.Name = "RemoveGlbPet"; Ui.Danger(remove);
        fileAdd = ActionButton("", openFile ?? Open); fileAdd.Name = "GlbFileAdd";
        fileRemove = Ui.Button("", () =>
        {
            if (closed || IsBusy || draft is null) return;
            ClearDraft(preserveName: true); RestoreExistingSelection(); UpdatePlayback();
        }); fileRemove.Name = "GlbFileRemove";
        CustomPetView.ConfigureSlotButton(fileAdd, "M11,4 H13 V11 H20 V13 H13 V20 H11 V13 H4 V11 H11 Z", "3D 파일 추가");
        CustomPetView.ConfigureSlotButton(fileRemove, "M8,5 V3 H16 V5 H21 V7 H19 L18,21 H6 L5,7 H3 V5 Z M8,9 H10 V18 H8 Z M14,9 H16 V18 H14 Z", "3D 파일 제거");
        Ui.Danger(fileRemove);
        fileLabel.Name = "GlbFileLabel"; fileLabel.MaxLines = 1; fileLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        fileLabel.VerticalAlignment = VerticalAlignment.Center;
        var fileActions = Ui.Row(fileAdd, fileRemove); fileActions.Spacing = 2;
        var fileRow = new Grid { Name = "GlbFileRow", ColumnDefinitions = new("32,8,*,8,Auto") };
        fileRow.Children.Add(fileThumbnail); Grid.SetColumn(fileLabel, 2); fileRow.Children.Add(fileLabel);
        Grid.SetColumn(fileActions, 4); fileRow.Children.Add(fileActions);
        save = ActionButton("저장하고 적용", Save); save.Name = "SaveGlbPet"; Ui.Primary(save);
        replay = ActionButton("처음부터 재생", RestartPreview); replay.Name = "ReplayGlbPet";
        pause = ActionButton("일시정지", async () =>
        {
            if (previewCompleted) { await RestartPreview(); return; }
            paused = !paused; UpdatePlayback();
        }); pause.Name = "PauseGlbPet";
        foreach (var choice in new[] { existing, previewAction }) StyleChoice(choice);
        previewAction.ItemTemplate = new FuncDataTemplate<string>((key, _) => ChoiceText(key is null ? "" :
            GlbPetDraft.ActionName(key)));
        existing.ItemTemplate = new FuncDataTemplate<CharacterPackage>((package, _) => ChoiceText(package?.Manifest.Name ?? ""));
        existing.SelectionChanged += async (_, _) =>
        {
            if (populating || IsBusy || existing.SelectedItem is not CharacterPackage package || package.Manifest.Id == draft?.Id) return;
            if (!package.IsGlb) { if (openPackage is not null) await openPackage(package); RestoreExistingSelection(); return; }
            if (await CanDiscard()) await Read(() => new GlbPetDraft(package), isNew: false);
            RestoreExistingSelection();
        };
        name.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || populating || draft is null || draft.Name == (name.Text ?? "")) return;
            draft.Name = name.Text ?? ""; MarkChanged();
            if (string.IsNullOrWhiteSpace(draft.Name)) status.Text = "저장하려면 펫 이름을 입력해 주세요.";
        };
        previewAction.SelectionChanged += async (_, _) => { if (!populating) { ShowSelectedAction(); await Play(); } };
        preview.PlaybackFailed += error =>
        {
            if (closed) return;
            previewFailed = true; Error(error); UpdatePlayback();
        };
        // Keep the chosen action and its final pose visible. An implicit idle loop
        // would make a one-shot setting look like it is still repeating.
        preview.Completed += () => { if (!closed && !loading) { previewCompleted = true; UpdatePlayback(); } };

        existingField = PetManagementView.Field("펫 선택", existing);
        existing.PlaceholderText = "펫 선택"; AutomationProperties.SetName(existing, "펫 선택");
        open.VerticalAlignment = VerticalAlignment.Bottom; open.Height = DesignSystem.PetControlHeight;
        var stage = new Border { Name = "GlbPreviewStage", Background = DesignSystem.Surface, CornerRadius = DesignSystem.CardRadius,
            Padding = new Thickness(8), Child = new Viewbox { Child = preview, Stretch = Stretch.Uniform } };
        var settingsPane = Ui.Column(Heading("행동 연결"), PetManagementView.Field("상황", previewAction), rows);
        settingsPane.Spacing = 10; settingsPane.Name = "GlbActionPane";
        var workspace = new PetEditorWorkspace(owner, "Glb", stage, Ui.Column(PetManagementView.Field("3D 파일", fileRow), Ui.Row(pause, replay), existingField, Ui.Actions(edit, remove)), settingsPane);
        var identity = workspace.Identity("GlbIdentity", name, open);
        editor = workspace; editor.Name = "GlbEditor";
        var body = Ui.Column(identity, editor);
        var buttons = Ui.Row(save);
        if (created is not null)
        {
            export = ActionButton("펫 팩 만들기…", Export); export.Name = "CreateGlbPetPack"; Ui.Primary(export);
            save.Classes.Remove("primary"); buttons.Children.Add(export);
        }
        var footer = new Grid { ColumnDefinitions = new("*") , RowDefinitions = new("Auto,8,Auto") };
        footer.Children.Add(status); Grid.SetRow(buttons, 2); buttons.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(buttons);
        Content = PetManagementView.Page("펫 팩 만들기", "", body, footer, showHeader);
        owner.PropertyChanged += OwnerChanged; AttachedToVisualTree += (_, _) => { RefreshExisting(); UpdatePlayback(); }; DetachedFromVisualTree += (_, _) => UpdatePlayback();
        RefreshExisting(); SetControls();
    }
    private Button ActionButton(string text, Func<Task> action)
    {
        var button = Ui.Action(text);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            finally { if (!closed) SetControls(); }
        };
        return button;
    }
    private static TextBlock Heading(string text)
    {
        var label = Ui.Text(text, DesignSystem.Section); label.FontWeight = FontWeight.SemiBold; label.TextWrapping = TextWrapping.Wrap; return label;
    }
    private static TextBlock ChoiceText(string text)
    {
        var label = Ui.Text(text); label.TextTrimming = TextTrimming.CharacterEllipsis; ToolTip.SetTip(label, text); return label;
    }
    private static void StyleChoice(ComboBox choice)
    {
        PetManagementView.StyleChoice(choice); choice.HorizontalAlignment = HorizontalAlignment.Stretch;
        choice.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        choice.ItemTemplate = new FuncDataTemplate<string>((value, _) => ChoiceText(value ?? ""));
    }
    internal void RefreshExisting()
    {
        populating = true;
        try { existing.ItemsSource = existingPets?.Invoke() ?? library.List(loadModels: false).Where(p => p.IsGlb).ToArray(); }
        finally { populating = false; }
        RestoreExistingSelection(); SetControls();
    }
    private void RestoreExistingSelection()
    {
        populating = true;
        try { existing.SelectedItem = existing.ItemsSource?.OfType<CharacterPackage>().FirstOrDefault(p => p.Manifest.Id == draft?.Id); }
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
        try { var path = await chooseFile(); if (path is not null && !closed) await Read(() => new GlbPetDraft(path), isNew: true); }
        catch (Exception e) { Error(e); }
    }
    internal async Task OpenPath(string path)
    { if (!IsBusy && !closed && await CanDiscard()) await Read(() => new GlbPetDraft(path), isNew: true); }
    internal async Task OpenPackage(CharacterPackage package)
    { if (!IsBusy && !closed && await CanDiscard()) await Read(() => new GlbPetDraft(package), isNew: false); }
    private async Task Read(Func<GlbPetDraft> read, bool isNew)
    {
        SetBusy(true); loading = false; preview.SetRunning(false); generation++; status.Text = "GLB 파일을 읽는 중…";
        try
        {
            var candidate = await Task.Run(read); if (closed) return;
            if (isNew && draft is null && !string.IsNullOrWhiteSpace(name.Text)) candidate.Name = name.Text;
            ClearThumbnail(); draft = candidate; paused = false; Populate(); HasUnsavedChanges = isNew; RestoreExistingSelection();
            status.Foreground = DesignSystem.Muted;
            status.Text = "";
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
            rows.Children.Clear(); actionRows.Clear();
            foreach (var key in GlbPetDraft.Actions) BuildActionRow(current, key);
            previewAction.ItemsSource = GlbPetDraft.Actions.ToArray(); previewAction.SelectedIndex = 0;
        }
        finally { populating = false; ShowSelectedAction(); SetControls(); }
    }
    private void BuildActionRow(GlbPetDraft current, string key)
    {
        var mapping = current.Mappings.GetValueOrDefault(key);
        var options = (key == "idle" ? current.Model.Animations.Select(a => a.Name) : new[] { "연결 안 함" }.Concat(current.Model.Animations.Select(a => a.Name))).ToArray();
        var clip = new ComboBox { Name = "GlbClip_" + key, ItemsSource = options, SelectedItem = mapping?.ModelClip ?? options[0] };
        var angles = new[] { 0f, 90f, 180f, -90f }.ToList();
        var angle = mapping?.Heading ?? current.Heading;
        if (!angles.Contains(angle)) angles.Add(angle);
        var heading = new ComboBox { Name = "GlbHeading_" + key,
            ItemsSource = angles.Select(value => value switch { 0 => "정면", 90 => "오른쪽 90°", 180 => "뒤 180°", -90 => "왼쪽 90°", _ => $"{value:0.##}°" }).ToArray(),
            SelectedIndex = angles.IndexOf(angle) };
        var repeat = new ComboBox { Name = "GlbRepeat_" + key, ItemsSource = new[] { "한 번", "반복" }, SelectedIndex = mapping?.Loop == true ? 1 : 0 };
        var speeds = new[] { .25, .5, 1, 1.5, 2, 3 }.ToList();
        var playbackSpeed = mapping?.Speed ?? 1;
        if (!speeds.Contains(playbackSpeed)) speeds.Add(playbackSpeed);
        var speed = new ComboBox { Name = "GlbSpeed_" + key,
            ItemsSource = speeds.Select(value => $"{value}배").ToArray(), SelectedIndex = speeds.IndexOf(playbackSpeed) };
        foreach (var choice in new[] { clip, heading, repeat, speed }) StyleChoice(choice);
        AutomationProperties.SetName(clip, GlbPetDraft.ActionName(key) + " 애니메이션");
        AutomationProperties.SetName(heading, GlbPetDraft.ActionName(key) + " 바라보는 방향");
        AutomationProperties.SetName(repeat, GlbPetDraft.ActionName(key) + " 재생 방식");
        AutomationProperties.SetName(speed, GlbPetDraft.ActionName(key) + " 재생 속도");
        var syncing = false;
        void Sync()
        {
            var assigned = current.Mappings.TryGetValue(key, out var value); var fixedLoop = GlbPetDraft.RequiredLoop(key);
            syncing = true;
            try { repeat.SelectedIndex = (value?.Loop ?? fixedLoop ?? false) ? 1 : 0; }
            finally { syncing = false; }
            repeat.IsEnabled = assigned && fixedLoop is null; speed.IsEnabled = heading.IsEnabled = assigned;

        }
        async void Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (populating || syncing || closed || draft != current || speed.SelectedIndex < 0 || heading.SelectedIndex < 0) return;
            try
            {
                current.Set(key, key != "idle" && clip.SelectedIndex <= 0 ? null : clip.SelectedItem as string, repeat.SelectedIndex == 1, speeds[speed.SelectedIndex], angles[heading.SelectedIndex]);
                Sync(); MarkChanged(); RefreshActionChoices();
                if (SelectedAction == key || key == "idle") await Play();
            }
            catch (Exception error) { Error(error); }
        }
        clip.SelectionChanged += Changed; heading.SelectionChanged += Changed; repeat.SelectionChanged += Changed; speed.SelectionChanged += Changed;
        var playback = new Grid { ColumnDefinitions = new("*,12,*") };
        playback.Children.Add(PetManagementView.Field("재생 방식", repeat));
        var speedField = PetManagementView.Field("속도", speed); Grid.SetColumn(speedField, 2); playback.Children.Add(speedField);
        var row = Ui.Column(PetManagementView.Field("애니메이션", clip), PetManagementView.Field("바라보는 방향", heading), playback);
        row.Name = "GlbActionRow_" + key; actionRows[key] = row; rows.Children.Add(row); Sync();
    }
    private void RefreshActionChoices()
    {
        var selected = previewAction.SelectedIndex; populating = true;
        try { previewAction.ItemsSource = GlbPetDraft.Actions.ToArray(); previewAction.SelectedIndex = Math.Max(0, selected); }
        finally { populating = false; }
        ShowSelectedAction();
    }
    private void ShowSelectedAction()
    {
        foreach (var (key, row) in actionRows) row.IsVisible = key == SelectedAction;
    }
    private Task Play() => PlayAction(SelectedAction);
    private Task RestartPreview() { paused = false; return Play(); }
    private async Task PlayAction(string action)
    {
        if (closed || draft is not { } current) return;
        var request = ++generation; loading = true; previewCompleted = previewFailed = false; UpdatePlayback();
        try
        {
            var mapping = current.Mappings.GetValueOrDefault(action) ?? current.Mappings["idle"];
            var definition = new GlbDefinition("model.glb", mapping.Heading ?? current.Heading, current.RootNode); var idle = current.Mappings["idle"].ModelClip!;
            var frames = await Task.Run(() => current.Model.CreateAnimation(mapping.ModelClip!, definition, idle, mapping.Speed));
            if (closed || request != generation) return;
            preview.SetFrames(frames, mapping.Loop, false);
            if (thumbnailBitmap is null) fileThumbnail.Source = thumbnailBitmap = Ui.Bitmap(frames[0].Image);
        }
        catch (Exception e) { if (request == generation) { previewFailed = true; Error(e); } }
        finally { if (request == generation) { loading = false; UpdatePlayback(); } }
    }
    private async Task Save()
    {
        if (IsBusy || closed || !HasUnsavedChanges || draft is not { } current || string.IsNullOrWhiteSpace(current.Name)) return;
        var refreshPreview = loading;
        SetBusy(true); loading = false; generation++; preview.SetRunning(false); status.Text = "저장하고 적용하는 중…";
        try
        {
            var package = await Task.Run(() => current.Save(library)); if (closed) return;
            await installed(package); HasUnsavedChanges = false; RefreshExisting(); LibraryChanged?.Invoke();
            status.Text = "저장 완료"; status.Foreground = DesignSystem.Muted;
        }
        catch (Exception e) { Error(e); }
        finally
        {
            // Saving invalidates an in-flight preview. Restore the chosen action
            // before resuming, so its label cannot describe the previous image.
            if (refreshPreview && !closed) await Play();
            SetBusy(false); UpdatePlayback();
        }
    }
    private async Task Export()
    {
        if (IsBusy || closed || draft is not { } current || string.IsNullOrWhiteSpace(current.Name)) return;
        SetBusy(true); preview.SetRunning(false);
        try
        {
            var path = chooseOutput is not null ? await chooseOutput() : await CustomPetView.PickOutputFile(owner);
            if (path is null || closed) return;
            status.Text = "펫 팩 저장 중…";
            await Task.Run(() => current.Export(path));
            if (closed) return;
            HasUnsavedChanges = false; status.Text = "저장 완료"; status.Foreground = DesignSystem.Muted;
            if (created is not null) await created(path);
        }
        catch (Exception error) { Error(error); }
        finally { SetBusy(false); UpdatePlayback(); }
    }
    private CharacterPackage? StoredDraft => existing.ItemsSource?.OfType<CharacterPackage>().FirstOrDefault(p => p.Manifest.Id == draft?.Id);
    private async Task Remove()
    {
        if (IsBusy || closed || StoredDraft is not { } stored) return;
        SetBusy(true); preview.SetRunning(false);
        try
        {
            if (await Ui.Confirm(owner, "저장한 펫을 삭제할까요?",
                $"'{stored.Manifest.Name}'을 앱에서 삭제해요. 원본 GLB 파일은 유지돼요.", "펫 삭제", "취소") != 0 || closed) return;
            await Task.Run(() => library.Delete(stored.Manifest.Id, draft!.Revision));
            if (closed) return;
            ClearDraft(); RefreshExisting(); LibraryChanged?.Invoke();
            if (removed is not null)
            {
                try { await removed(stored.Manifest.Id); }
                catch (Exception error)
                {
                    AppPaths.Log(error);
                    if (!closed) { status.Text = "펫은 삭제됐지만 화면을 갱신하지 못했어요. " + Ui.ErrorText(error); status.Foreground = DesignSystem.Error; }
                }
            }
        }
        catch (Exception error) { Error(error); }
        finally { SetBusy(false); UpdatePlayback(); }
    }
    private void ClearThumbnail() { fileThumbnail.Source = null; thumbnailBitmap?.Dispose(); thumbnailBitmap = null; }
    private void ClearDraft(bool preserveName = false)
    {
        // Invalidate pending pose requests before dropping every editor-owned model reference.
        generation++; loading = paused = previewCompleted = previewFailed = false;
        preview.SetRunning(false); preview.SetFrames([], false); ClearThumbnail(); draft = null;
        populating = true;
        try
        {
            if (!preserveName) name.Text = ""; rows.Children.Clear(); actionRows.Clear();
            previewAction.ItemsSource = null; existing.SelectedIndex = -1;
        }
        finally { populating = false; }
        HasUnsavedChanges = false; status.Text = ""; status.Foreground = DesignSystem.Muted;
    }
    private void MarkChanged()
    {
        HasUnsavedChanges = true; status.Text = ""; status.Foreground = DesignSystem.Muted; SetControls();
    }
    private void Error(Exception error) { AppPaths.Log(error); if (!closed) { status.Text = "처리하지 못했어요. " + Ui.ErrorText(error); status.Foreground = DesignSystem.Error; } }
    private void SetBusy(bool value) { IsBusy = value; SetControls(); BusyChanged?.Invoke(); }
    internal void SetImportEnabled(bool value) => open.IsEnabled = fileAdd.IsEnabled = value;
    private void SetControls()
    {
        var ready = !IsBusy && draft is not null;
        open.IsEnabled = fileAdd.IsEnabled = !IsBusy; existing.IsEnabled = !IsBusy;
        fileRemove.IsEnabled = ready; fileLabel.Text = draft?.FileName ?? "파일 없음"; ToolTip.SetTip(fileLabel, draft?.FileName);
        remove.IsVisible = StoredDraft is not null; remove.IsEnabled = ready && remove.IsVisible;
        edit.IsVisible = remove.IsVisible; edit.IsEnabled = ready && edit.IsVisible;
        existingField.IsVisible = true;
        editor.IsVisible = true;
        if (export is not null) export.IsEnabled = ready && !string.IsNullOrWhiteSpace(draft?.Name);
        save.IsEnabled = ready && HasUnsavedChanges && !string.IsNullOrWhiteSpace(draft?.Name);
        name.IsEnabled = !IsBusy; rows.IsEnabled = previewAction.IsEnabled = ready;
        replay.IsEnabled = ready && !loading; pause.IsEnabled = ready && !loading && !previewFailed;
        pause.Content = previewCompleted ? "다시 재생" : paused ? "계속 재생" : "일시정지";
    }
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) UpdatePlayback();
    }
    private void UpdatePlayback()
    {
        preview.SetRunning(!closed && !IsBusy && !loading && !paused && !previewFailed && owner.IsVisible && TopLevel.GetTopLevel(this) is not null);
        SetControls();
    }
    private async Task<bool> CanDiscard()
    {
        if (!HasUnsavedChanges) return true;
        return await Ui.Confirm(owner, "저장하지 않은 변경을 버릴까요?", "저장하지 않은 설정은 사라져요. 계속 편집하려면 돌아가 주세요.", "변경 버리기", "계속 편집") == 0;
    }
    public Task<bool> CanCloseDraft() => CanDiscard();
    public void Dispose() { if (closed) return; closed = true; generation++; owner.PropertyChanged -= OwnerChanged; ClearThumbnail(); preview.Dispose(); }
}
