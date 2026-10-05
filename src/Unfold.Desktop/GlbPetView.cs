using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly TextBlock status = Ui.Caption("");
    private readonly TextBox name = new() { Name = "GlbPetName", MaxLength = 80, PlaceholderText = "펫 이름", Height = DesignSystem.PetControlHeight };
    private readonly ComboBox existing = new() { Name = "GlbExistingPets", PlaceholderText = "편집할 펫 선택" };
    private readonly ComboBox heading = new() { Name = "GlbHeading", ItemsSource = new[] { "정면", "오른쪽 90°", "뒤 180°", "왼쪽 90°" }, SelectedIndex = 0 };
    private readonly ComboBox root = new() { Name = "GlbRoot", PlaceholderText = "자동 선택" };
    private readonly ComboBox previewAction = new() { Name = "GlbPreviewAction" };
    private readonly StackPanel rows = new() { Name = "GlbMappings", Spacing = 8 };
    private readonly Dictionary<string, Control> actionRows = [];
    private readonly Button open, save, replay, pause;
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
    internal bool IsPreviewLoading => loading;
    private string SelectedAction => GlbPetDraft.Actions[Math.Max(0, previewAction.SelectedIndex)];
    public event Action? BusyChanged;
    public GlbPetView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseFile = null, bool showHeader = true, bool embedded = false,
        Func<string, Task>? created = null, Func<Task<string?>>? chooseOutput = null)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        status.Name = "GlbStatus"; this.created = created; this.chooseOutput = chooseOutput;
        status.IsVisible = false;
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) status.IsVisible = !string.IsNullOrWhiteSpace(status.Text); };
        open = ActionButton("GLB 파일 열기…", Open); open.Name = "OpenGlbPet"; open.IsVisible = !embedded;
        save = ActionButton("저장하고 적용", Save); save.Name = "SaveGlbPet"; Ui.Primary(save);
        replay = ActionButton("처음부터 재생", RestartPreview); replay.Name = "ReplayGlbPet";
        pause = ActionButton("일시정지", async () =>
        {
            if (previewCompleted) { await RestartPreview(); return; }
            paused = !paused; UpdatePlayback();
        }); pause.Name = "PauseGlbPet";
        foreach (var choice in new[] { existing, heading, root, previewAction }) StyleChoice(choice);
        previewAction.ItemTemplate = new FuncDataTemplate<string>((key, _) => ChoiceText(key is null ? "" :
            GlbPetDraft.ActionName(key)));
        existing.ItemTemplate = new FuncDataTemplate<CharacterPackage>((package, _) => ChoiceText(package?.Manifest.Name ?? ""));
        existing.SelectionChanged += async (_, _) =>
        {
            if (populating || IsBusy || existing.SelectedItem is not CharacterPackage package || package.Manifest.Id == draft?.Id) return;
            if (await CanDiscard()) await Read(() => new GlbPetDraft(package), isNew: false);
            RestoreExistingSelection();
        };
        heading.SelectionChanged += async (_, _) =>
        {
            if (populating || draft is null || heading.SelectedIndex < 0) return;
            draft.Heading = heading.SelectedIndex switch { 1 => 90, 2 => 180, 3 => -90, _ => 0 }; MarkChanged(); await Play();
        };
        root.SelectionChanged += async (_, _) =>
        {
            if (populating || draft is null) return;
            draft.RootNode = root.SelectedIndex <= 0 ? null : root.SelectedItem as string; MarkChanged(); await Play();
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

        existingField = PetManagementView.Field("저장한 펫", existing);
        existing.PlaceholderText = "저장한 펫 편집"; AutomationProperties.SetName(existing, "저장한 펫 편집");
        var source = new Grid { ColumnDefinitions = embedded ? new("*") : new("Auto,16,*") };
        open.VerticalAlignment = VerticalAlignment.Bottom; open.Height = DesignSystem.PetControlHeight;
        source.Children.Add(open); Grid.SetColumn(existingField, embedded ? 0 : 2); source.Children.Add(existingField);
        var stage = new Border { Name = "GlbPreviewStage", Background = DesignSystem.Surface, CornerRadius = DesignSystem.CardRadius,
            Padding = new Thickness(8), Child = new Viewbox { Child = preview, Stretch = Stretch.Uniform } };
        var settingsPane = Ui.Column(Heading("행동 연결"), PetManagementView.Field("상황", previewAction), rows);
        settingsPane.Spacing = 10; settingsPane.Name = "GlbActionPane";
        var workspace = new PetEditorWorkspace(owner, "Glb", stage, Ui.Row(pause, replay), settingsPane);
        var direction = Ui.Column(PetManagementView.Field("바라보는 방향", heading), PetManagementView.Field("몸 방향 기준 뼈대", root));
        var advanced = new Expander { Name = "GlbAdvanced", Header = "표시 방향 · 고급 설정", Content = direction,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        editor = Ui.Column(PetManagementView.Field("펫 이름", name), workspace, advanced); editor.Name = "GlbEditor";
        var body = Ui.Column(source, editor);
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
    private void RefreshExisting()
    {
        populating = true;
        try { existing.ItemsSource = library.List().Where(p => p.IsGlb).ToArray(); }
        finally { populating = false; }
        RestoreExistingSelection();
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
    private async Task Read(Func<GlbPetDraft> read, bool isNew)
    {
        SetBusy(true); loading = false; preview.SetRunning(false); generation++; status.Text = "GLB 파일을 읽는 중…";
        try
        {
            var candidate = await Task.Run(read); if (closed) return;
            draft = candidate; paused = false; Populate(); HasUnsavedChanges = isNew; RestoreExistingSelection();
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
            heading.SelectedIndex = current.Heading switch { 90 => 1, 180 => 2, -90 => 3, _ => 0 };
            root.ItemsSource = new[] { "자동 선택" }.Concat(current.Model.NodeNames.Distinct()).ToArray(); root.SelectedItem = current.RootNode ?? "자동 선택";
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
        var repeat = new ComboBox { Name = "GlbRepeat_" + key, ItemsSource = new[] { "한 번", "반복" }, SelectedIndex = mapping?.Loop == true ? 1 : 0 };
        var speeds = new[] { .25, .5, 1, 1.5, 2, 3 };
        var speed = new ComboBox { Name = "GlbSpeed_" + key, ItemsSource = new[] { "0.25배", "0.5배", "1배", "1.5배", "2배", "3배" }, SelectedIndex = System.Array.IndexOf(speeds, mapping?.Speed ?? 1) };
        foreach (var choice in new[] { clip, repeat, speed }) StyleChoice(choice);
        AutomationProperties.SetName(clip, GlbPetDraft.ActionName(key) + " 애니메이션");
        AutomationProperties.SetName(repeat, GlbPetDraft.ActionName(key) + " 재생 방식");
        AutomationProperties.SetName(speed, GlbPetDraft.ActionName(key) + " 재생 속도");
        var syncing = false;
        void Sync()
        {
            var assigned = current.Mappings.TryGetValue(key, out var value); var fixedLoop = GlbPetDraft.RequiredLoop(key);
            syncing = true;
            try { repeat.SelectedIndex = (value?.Loop ?? fixedLoop ?? false) ? 1 : 0; }
            finally { syncing = false; }
            repeat.IsEnabled = assigned && fixedLoop is null; speed.IsEnabled = assigned;

        }
        async void Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (populating || syncing || closed || draft != current || speed.SelectedIndex < 0) return;
            try
            {
                current.Set(key, key != "idle" && clip.SelectedIndex <= 0 ? null : clip.SelectedItem as string, repeat.SelectedIndex == 1, speeds[speed.SelectedIndex]);
                Sync(); MarkChanged(); RefreshActionChoices();
                if (SelectedAction == key || key == "idle") await Play();
            }
            catch (Exception error) { Error(error); }
        }
        clip.SelectionChanged += Changed; repeat.SelectionChanged += Changed; speed.SelectionChanged += Changed;
        var playback = new Grid { ColumnDefinitions = new("*,12,*") };
        playback.Children.Add(PetManagementView.Field("재생 방식", repeat));
        var speedField = PetManagementView.Field("속도", speed); Grid.SetColumn(speedField, 2); playback.Children.Add(speedField);
        var row = Ui.Column(PetManagementView.Field("애니메이션", clip), playback);
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
            var definition = new GlbDefinition("model.glb", current.Heading, current.RootNode); var idle = current.Mappings["idle"].ModelClip!;
            var frames = await Task.Run(() => current.Model.CreateAnimation(mapping.ModelClip!, definition, idle, mapping.Speed));
            if (closed || request != generation) return;
            preview.SetFrames(frames, mapping.Loop, false);
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
            await installed(package); HasUnsavedChanges = false; RefreshExisting();
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
    private void MarkChanged()
    {
        HasUnsavedChanges = true; status.Text = ""; status.Foreground = DesignSystem.Muted; SetControls();
    }
    private void Error(Exception error) { AppPaths.Log(error); if (!closed) { status.Text = "처리하지 못했어요. " + Ui.ErrorText(error); status.Foreground = DesignSystem.Error; } }
    private void SetBusy(bool value) { IsBusy = value; SetControls(); BusyChanged?.Invoke(); }
    private void SetControls()
    {
        var ready = !IsBusy && draft is not null;
        open.IsEnabled = !IsBusy; existing.IsEnabled = !IsBusy;
        existingField.IsVisible = existing.ItemsSource?.Cast<object>().Any() == true;
        editor.IsVisible = draft is not null;
        if (export is not null) export.IsEnabled = ready && !string.IsNullOrWhiteSpace(draft?.Name);
        save.IsEnabled = ready && HasUnsavedChanges && !string.IsNullOrWhiteSpace(draft?.Name);
        name.IsEnabled = heading.IsEnabled = root.IsEnabled = rows.IsEnabled = previewAction.IsEnabled = ready;
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
    public void Dispose() { if (closed) return; closed = true; generation++; owner.PropertyChanged -= OwnerChanged; preview.Dispose(); }
}
