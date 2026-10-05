using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class CustomPetWindow : Window
{
    public string? CreatedPackPath { get; private set; }
    public CustomPetWindow(string initialPath, Func<Task<string?>>? chooseMedia = null, Func<Task<string?>>? chooseOutput = null)
    {
        Title = "Unfold · 커스텀 펫 만들기"; Width = 660; Height = 880; MinWidth = 520; MinHeight = 620;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var page = new CustomPetView(this, path => { CreatedPackPath = path; Close(); return Task.CompletedTask; },
            initialPath, chooseMedia, chooseOutput);
        Content = Ui.PageFrame(this, page, inset: 16);
        Closing += (_, e) => { if (page.IsExporting) e.Cancel = true; };
        Closed += (_, _) => page.Dispose();
    }
    public static Task<string?> PickMediaFile(Window owner) => CustomPetView.PickMediaFile(owner);
}

internal sealed class CustomPetView : UserControl, IDisposable
{
    private readonly CustomPetDraft draft = new();
    private readonly Window owner;
    private readonly Func<string, Task> created;
    private readonly Func<Task<string?>> chooseMedia;
    private readonly Func<Task<string?>> chooseOutput;
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextBox name = new() { Name = "CustomPetName", PlaceholderText = "펫 이름", MaxLength = 80,
        Height = DesignSystem.PetControlHeight, VerticalContentAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox action = new() { Name = "CustomPetAction", ItemsSource = CustomPetDraft.Actions, SelectedIndex = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock pending = Ui.Caption(""), status = Ui.Caption("");
    private readonly AnimationView preview = new() { Name = "CustomPetPreview", Width = 220, Height = 220 };
    private readonly ComboBox selectedAction = new() { Name = "CustomPetSelectedAction", ItemsSource = CustomPetDraft.Actions, SelectedIndex = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly Dictionary<string, Button> replayButtons = [];
    private readonly Button pause;
    private bool paused, completed, selecting;
    private readonly Dictionary<string, TextBlock> labels = [];
    private readonly Dictionary<string, Border> actionCards = [];
    private readonly List<Button> actions = [];
    private readonly Dictionary<string, Image> thumbnails = [];
    private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> thumbnailBitmaps = [];
    private readonly Dictionary<string, ComboBox> playbackMenus = [];
    private readonly Button assign, create;
    private readonly Border pendingCard;
    private string? pendingPath;
    private bool busy, closed, exporting;
    private int previewGeneration;
    private string? previewAction;
    private readonly Border previewSurface;
    public bool IsExporting => exporting;
    internal bool IsBusy => busy;
    internal bool HasUnsavedChanges => pendingPath is not null || !string.IsNullOrWhiteSpace(name.Text) || draft.Clips.Count > 0;
    public event Action? BusyChanged;

    public CustomPetView(Window owner, Func<string, Task> created, string? initialPath = null,
        Func<Task<string?>>? chooseMedia = null, Func<Task<string?>>? chooseOutput = null, bool showHeader = true)
    {
        this.owner = owner; this.created = created;
        this.chooseMedia = chooseMedia ?? PickMedia; this.chooseOutput = chooseOutput ?? PickOutput;
        pendingPath = initialPath; pending.Text = initialPath is null ? "" : "가져온 파일: " + Path.GetFileName(initialPath);
        action.ItemTemplate = new FuncDataTemplate<string>((key, _) => Ui.Text(CustomPetDraft.ActionName(key ?? "")));
        AutomationProperties.SetName(action, "가져온 파일에 적용할 동작"); AutomationProperties.SetName(name, "커스텀 펫 이름");
        status.Name = "CustomPetStatus"; pending.Name = "CustomPetPending";
        status.IsVisible = false;
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) status.IsVisible = !string.IsNullOrWhiteSpace(status.Text); };
        assign = AsyncButton("동작에 넣기", AssignPending); assign.Name = "AssignPetMedia";
        selectedAction.ItemTemplate = new FuncDataTemplate<string>((key, _) => Ui.Text(CustomPetDraft.ActionName(key ?? "")));
        PetManagementView.StyleChoice(selectedAction); AutomationProperties.SetName(selectedAction, "상황");
        selectedAction.SelectionChanged += async (_, _) =>
        {
            if (selecting || selectedAction.SelectedItem is not string key) return;
            ShowSelectedAction();
            if (draft.Clips.ContainsKey(key)) await Preview(key);
            else ClearPreview();
        };
        pause = Ui.Button("일시정지", () =>
        {
            if (completed && selectedAction.SelectedItem is string key) { _ = Replay(key); return; }
            paused = !paused; Refresh();
        }); pause.Name = "PauseCustomPet";
        preview.Completed += () => { if (!closed) { completed = true; Refresh(); } };
        var previewButtons = new StackPanel { Spacing = 8, Orientation = Orientation.Horizontal };
        previewButtons.Children.Add(pause);
        var slots = new StackPanel { Name = "CustomPetActionSlots", Spacing = 8 };
        foreach (var key in CustomPetDraft.Actions)
        {
            var thumbnail = new Image { Name = "CustomPetThumbnail_" + key, Width = 32, Height = 40, Stretch = Stretch.Uniform };
            thumbnails[key] = thumbnail;
            var label = Ui.Caption("파일 없음"); label.Name = "CustomPetLabel_" + key;
            label.MaxLines = 1; label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center; labels[key] = label;
            var menu = new ComboBox { Name = "CustomPetPlayback_" + key, ItemsSource = new[] { "한 번", "반복", "핑퐁" },
                SelectedIndex = draft.Playback(key), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(menu, CustomPetDraft.ActionName(key) + " 반복 설정");
            PetManagementView.StyleChoice(menu); playbackMenus[key] = menu;
            menu.SelectionChanged += async (_, _) =>
            {
                if (menu.SelectedIndex < 0) return;
                draft.SetPlayback(key, menu.SelectedIndex);
                if (previewAction == key) await Preview(key);
            };
            var play = AsyncButton("처음부터 재생", () => Replay(key)); play.Name = "CustomPetPreview_" + key;
            replayButtons[key] = play; previewButtons.Children.Add(play);
            var select = AsyncButton("", () => SelectFile(key)); select.Name = "CustomPetFile_" + key;
            var remove = Ui.Button("", () =>
            {
                draft.RemoveClip(key);
                menu.SelectedIndex = draft.Playback(key);
                if (previewAction == key)
                {
                    ClearPreview();
                }
                Refresh();
            }); remove.Name = "CustomPetRemove_" + key;
            AutomationProperties.SetName(play, CustomPetDraft.ActionName(key) + " 처음부터 재생");
            ConfigureSlotButton(select, "M11,4 H13 V11 H20 V13 H13 V20 H11 V13 H4 V11 H11 Z", CustomPetDraft.ActionName(key) + " 파일추가");
            ConfigureSlotButton(remove, "M8,5 V3 H16 V5 H21 V7 H19 L18,21 H6 L5,7 H3 V5 Z M8,9 H10 V18 H8 Z M14,9 H16 V18 H14 Z", CustomPetDraft.ActionName(key) + " 삭제");
            Ui.Danger(remove); actions.AddRange([play, select, remove]);
            var controls = Ui.Row(select, remove); controls.Spacing = 2; controls.Name = "CustomPetActions_" + key;
            var fileRow = new Grid { ColumnDefinitions = new("32,8,*,8,Auto") };
            fileRow.Children.Add(thumbnail); Grid.SetColumn(label, 2); fileRow.Children.Add(label);
            Grid.SetColumn(controls, 4); fileRow.Children.Add(controls);
            var row = Ui.Column(PetManagementView.Field("파일", fileRow), PetManagementView.Field("재생 방식", menu));
            var slot = new Border { Name = "CustomPetSlot_" + key, BorderBrush = DesignSystem.Outline,
                BorderThickness = new Thickness(1), Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Child = row };
            actionCards[key] = slot; slots.Children.Add(slot);
        }
        create = AsyncButton("펫 팩 만들기…", () => Export()); create.Name = "CreateCustomPetPack"; Ui.Primary(create);
        create.Height = DesignSystem.PetControlHeight;
        name.TextChanged += (_, _) => Refresh();
        pendingCard = BuildPendingCard();
        var identity = PetManagementView.Field("펫 이름", name);

        previewSurface = new Border
        {
            Name = "CustomPetPreviewSurface", Background = DesignSystem.Surface, CornerRadius = DesignSystem.CardRadius,
            Padding = new Thickness(8), Child = new Viewbox { Child = preview, Stretch = Stretch.Uniform }
        };
        var settingsPane = Ui.Column(PetEditorWorkspace.Heading("행동 연결"), PetManagementView.Field("상황", selectedAction), slots);
        settingsPane.Name = "CustomPetActionPane"; settingsPane.Spacing = 10;
        var workspace = new PetEditorWorkspace(owner, "CustomPet", previewSurface, previewButtons, settingsPane);
        var body = Ui.Column(pendingCard, identity, workspace);
        body.Spacing = DesignSystem.Inset;
        var footer = new Grid { ColumnDefinitions = new("*,20,Auto") };
        status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(status);
        Grid.SetColumn(create, 2); footer.Children.Add(create);
        Content = PetManagementView.Page("나만의 펫을 만들어 보세요.", "",
            body, footer, showHeader);
        owner.PropertyChanged += OwnerPropertyChanged;
        AttachedToVisualTree += (_, _) => UpdatePlayback();
        DetachedFromVisualTree += (_, _) => UpdatePlayback();
        Refresh();
    }
    public static async Task<string?> PickMediaFile(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "파일 가져오기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(PetMediaImporter.SupportedFileTypes) { Patterns = PetMediaImporter.FilePatterns }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private Task<string?> PickMedia() => PickMediaFile(owner);
    private Task<string?> PickOutput() => PickOutputFile(owner);
    internal static async Task<string?> PickOutputFile(Window owner)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new() { Title = "커스텀 펫 팩 저장", DefaultExtension = "unfoldpet",
            SuggestedFileName = "custom-pet.unfoldpet", ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Unfold 펫 팩") { Patterns = ["*.unfoldpet"] }] });
        return file?.TryGetLocalPath();
    }
    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) UpdatePlayback();
    }
    private void UpdatePlayback() => preview.SetRunning(TopLevel.GetTopLevel(this) is not null && owner.IsVisible && !closed && !busy && !paused && !completed);
    public void Dispose()
    {
        if (closed) return;
        closed = true; previewGeneration++; owner.PropertyChanged -= OwnerPropertyChanged;
        cancellation.Cancel(); preview.Dispose();
        foreach (var bitmap in thumbnailBitmaps.Values) bitmap.Dispose(); thumbnailBitmaps.Clear();
    }
    internal async Task OpenPath(string path)
    {
        if (busy || closed || selectedAction.SelectedItem is not string key) return;
        if (await ConfirmReplace(key, path)) await Assign(key, path);
    }
    private void ClearPreview()
    {
        previewGeneration++; previewAction = null; completed = false; preview.SetFrames([], true); Refresh();
    }
    private void ShowSelectedAction()
    {
        foreach (var (key, card) in actionCards) card.IsVisible = Equals(selectedAction.SelectedItem, key);
        foreach (var (key, button) in replayButtons) button.IsVisible = Equals(selectedAction.SelectedItem, key);
    }
    private Task Replay(string key) { paused = false; return Preview(key); }
    private async Task AssignPending()
    {
        if (pendingPath is not { } path || action.SelectedItem is not string key) return;
        // Keep the imported file when the replacement is declined so it can go to another action.
        if (!await ConfirmReplace(key, path)) return;
        if (await Assign(key, path))
        {
            pendingPath = null; pending.Text = "동작에 추가했어요.";
            SelectNextEmptyAction(); Refresh();
        }
    }
    private async Task SelectFile(string key)
    {
        if (busy || closed) return;
        try
        {
            var path = await chooseMedia(); if (path is null || closed) return;
            if (await ConfirmReplace(key, path)) await Assign(key, path);
        }
        catch (Exception error) { ShowError(error); }
    }
    // Replacing an action drops the clip it already holds, so name both files and
    // let the safe choice keep the draft, the preview and the create gate untouched.
    private async Task<bool> ConfirmReplace(string key, string path)
    {
        if (busy || closed || !draft.Clips.TryGetValue(key, out var clip)) return true;
        var name = CustomPetDraft.ActionName(key);
        var choice = await Ui.Confirm(owner, name + " 파일을 바꿀까요?",
            $"지금은 ‘{clip.FileName}’ 파일이 들어 있어요. 새로 가져온 ‘{Path.GetFileName(path)}’ 파일로 바꾸면 기존 파일은 펫 팩에 담기지 않아요. 다른 동작은 그대로예요.",
            "바꾸기", "취소");
        if (closed) return false;
        if (choice == 0) return true;
        status.Foreground = DesignSystem.Muted; status.Text = name + "의 기존 파일을 그대로 두었어요.";
        return false;
    }
    // Point the picker at an action that still needs a file so the next import
    // does not land on the slot that was just filled.
    private void SelectNextEmptyAction()
    {
        var keys = CustomPetDraft.Actions; var start = Math.Max(action.SelectedIndex, 0);
        for (var offset = 1; offset < keys.Count; offset++)
        {
            var index = (start + offset) % keys.Count;
            if (!draft.Clips.ContainsKey(keys[index])) { action.SelectedIndex = index; return; }
        }
    }
    private async Task<bool> Assign(string key, string path)
    {
        if (busy || closed) return false;
        busy = true; preview.SetRunning(false); Refresh(); status.Text = "파일을 확인하고 있어요…";
        try
        {
            var clip = await PetMediaImporter.Import(path, cancellation.Token);
            if (closed) return false;
            draft.SetClip(key, clip);
            var bitmap = Ui.Bitmap(clip.LoadFrames()[0].Image);
            if (thumbnailBitmaps.Remove(key, out var previous)) previous.Dispose();
            thumbnailBitmaps[key] = bitmap; thumbnails[key].Source = bitmap;
            status.Foreground = DesignSystem.Muted; status.Text = CustomPetDraft.ActionName(key) + "에 파일을 넣었어요.";
            await Preview(key); return true;
        }
        catch (OperationCanceledException) when (closed) { return false; }
        catch (Exception error) { ShowError(error); return false; }
        finally { busy = false; if (!closed) Refresh(); }
    }
    private async Task Preview(string key)
    {
        if (closed || !draft.Clips.TryGetValue(key, out var clip)) return;
        var request = ++previewGeneration;
        try
        {
            var frames = await Task.Run(clip.LoadFrames, cancellation.Token);
            if (!closed && request == previewGeneration && draft.Clips.TryGetValue(key, out var current) && current == clip)
            {
                preview.SetFrames(frames, draft.Playback(key) != 0, pixel: false, pingPong: draft.Playback(key) == 2);
                previewAction = key;
                completed = false;
                selecting = true; selectedAction.SelectedItem = key; selecting = false; ShowSelectedAction();
                Refresh();
            }
        }
        catch (OperationCanceledException) when (closed) { }
        catch (Exception error) { ShowError(error); }
    }
    private async Task<bool> Export(bool showCreated = true)
    {
        if (busy || closed) return false;
        busy = true; exporting = true; Refresh(); BusyChanged?.Invoke();
        try
        {
            var path = await chooseOutput(); if (path is null || closed) return false;
            var petName = name.Text ?? "";
            status.Text = "펫 팩을 만들고 검증하고 있어요…";
            // Keep the draft intact until the archive has been written and validated.
            var snapshot = new CustomPetDraft();
            foreach (var (key, clip) in draft.Clips) { snapshot.SetClip(key, clip); snapshot.SetPlayback(key, draft.Playback(key)); }
            await Task.Run(() => snapshot.Export(petName, path));
            if (closed) return false;
            ResetDraft();
            exporting = false;
            status.Foreground = DesignSystem.Muted; status.Text = "펫 팩을 저장했어요.";
            if (showCreated) await created(path);
            return true;
        }
        catch (Exception error) { ShowError(error); return false; }
        finally { busy = exporting = false; if (!closed) { Refresh(); BusyChanged?.Invoke(); } }
    }
    private void ResetDraft()
    {
        previewGeneration++; previewAction = null; preview.SetFrames([], true);
        completed = paused = false; selectedAction.SelectedIndex = 0;
        foreach (var key in CustomPetDraft.Actions) { draft.RemoveClip(key); playbackMenus[key].SelectedIndex = draft.Playback(key); }
        pendingPath = null; pending.Text = ""; action.SelectedIndex = 0; name.Text = "";
        Refresh();
    }
    internal async Task<bool> CanCloseDraft()
    {
        if (busy)
        {
            status.Text = "파일 작업이 끝난 뒤 종료해 주세요.";
            return false;
        }
        if (!HasUnsavedChanges) return true;
        var canSave = !string.IsNullOrWhiteSpace(name.Text) && draft.Clips.ContainsKey("idle") && pendingPath is null;
        var choice = await Ui.Confirm(owner, "작성 중인 펫을 저장할까요?",
            canSave ? "저장하지 않고 종료하면 작성 중인 이름과 행동 배정이 사라져요." :
                "펫 이름과 필수 ‘기본’을 넣고 가져온 파일을 배정하면 저장할 수 있어요. 계속 작성하거나 초안을 버릴 수 있어요.",
            canSave ? "저장하고 종료" : "계속 작성", "버리기", "취소");
        if (choice == 1) return true;
        return choice == 0 && canSave && await Export(showCreated: false);
    }
    private void Refresh()
    {
        name.IsEnabled = action.IsEnabled = selectedAction.IsEnabled = !busy;
        pause.IsEnabled = !busy && previewAction is not null;
        pause.Content = completed ? "다시 재생" : paused ? "계속 재생" : "일시정지";
        UpdatePlayback();
        pendingCard.IsVisible = pendingPath is not null;
        assign.IsEnabled = !busy && pendingPath is not null;
        create.IsEnabled = !busy && !string.IsNullOrWhiteSpace(name.Text) && draft.Clips.ContainsKey("idle");
        foreach (var (key, label) in labels)
        {
            var clip = draft.Clips.GetValueOrDefault(key);
            label.Text = clip?.FileName ?? "파일 없음";
            ToolTip.SetTip(label, clip?.FileName);
            playbackMenus[key].IsEnabled = !busy && clip is not null;
            if (clip is null && thumbnailBitmaps.Remove(key, out var old)) { thumbnails[key].Source = null; old.Dispose(); }
        }
        foreach (var button in actions)
            button.IsEnabled = !busy && (button.Name!.StartsWith("CustomPetFile_") || draft.Clips.ContainsKey(button.Name[(button.Name.LastIndexOf('_') + 1)..]));
        ShowSelectedAction(); RefreshCardSelection(); BusyChanged?.Invoke();
    }
    private void RefreshCardSelection()
    {
        foreach (var (key, card) in actionCards)
            card.BorderBrush = previewAction == key ? DesignSystem.Cream : DesignSystem.Outline;
    }
    private void ShowError(Exception error)
    {
        AppPaths.Log(error);
        if (closed) return;
        status.Foreground = DesignSystem.Error;
        status.Text = error is InvalidDataException or ArgumentException or InvalidOperationException ? error.Message : Ui.ErrorText(error);
    }
    private Button AsyncButton(string label, Func<Task> action)
    {
        var button = Ui.Action(label);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception error) { ShowError(error); }
            finally { if (!closed) Refresh(); }
        };
        return button;
    }

    private Border BuildPendingCard()
    {
        var fields = new Grid { ColumnDefinitions = new("*,12,Auto") };
        fields.Children.Add(Ui.Field("적용할 동작", action));
        assign.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(assign, 2); fields.Children.Add(assign);
        return Ui.Card(Ui.Column(pending, fields), 12);
    }

    private static void ConfigureSlotButton(Button button, string path, string label)
    {
        button.Content = new PathIcon
        {
            Data = Geometry.Parse(path), Width = 16, Height = 16
        };
        button.Width = 32; button.Height = 32; button.MinHeight = 32; button.Padding = new Thickness(6);
        button.Classes.Add("compact");
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        ToolTip.SetShowDelay(button, 500);
    }
}
