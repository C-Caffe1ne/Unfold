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
        Width = 320, Height = DesignSystem.PetControlHeight, VerticalContentAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox action = new() { Name = "CustomPetAction", ItemsSource = CustomPetDraft.Actions, SelectedIndex = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock pending = Ui.Caption(""), status = Ui.Caption("쉬는 모습은 필수예요. 나머지 동작은 원하는 것만 넣어 주세요.");
    private readonly AnimationView preview = new() { Name = "CustomPetPreview", Width = 220, Height = 220 };
    private readonly TextBlock previewHint = Ui.Caption("아래 행동 카드에 파일을 넣으면 이곳에서 확인할 수 있어요.");
    private readonly Dictionary<string, TextBlock> labels = [];
    private readonly Dictionary<string, Border> actionCards = [];
    private readonly List<Button> actions = [];
    private readonly Button assign, create;
    private readonly Border pendingCard;
    private string? pendingPath;
    private bool busy, closed, exporting;
    private int previewGeneration;
    private string? previewAction;
    private string savedName = "";
    private readonly Dictionary<string, ImportedPetClip> savedClips = [];
    private readonly TextBlock previewLabel = Ui.Caption("");
    private readonly Border previewSurface;
    public bool IsExporting => exporting;
    internal bool IsBusy => busy;
    internal bool HasUnsavedChanges => pendingPath is not null || (name.Text ?? "").Trim() != savedName ||
        draft.Clips.Count != savedClips.Count || draft.Clips.Any(pair => !savedClips.TryGetValue(pair.Key, out var saved) || saved != pair.Value);
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
        assign = AsyncButton("동작에 넣기", AssignPending); assign.Name = "AssignPetMedia";
        var slots = new WrapPanel
        {
            Name = "CustomPetActionSlots",
            Orientation = Orientation.Horizontal,
            ItemWidth = DesignSystem.PetActionWidth,
            ItemHeight = DesignSystem.PetActionHeight,
            ItemSpacing = DesignSystem.Space,
            LineSpacing = DesignSystem.Space,
            ItemsAlignment = WrapPanelItemsAlignment.Start
        };
        for (var index = 0; index < CustomPetDraft.Actions.Count; index++)
        {
            var key = CustomPetDraft.Actions[index];
            var label = Ui.Caption("파일 없음"); label.Name = "CustomPetLabel_" + key; labels[key] = label;
            var select = AsyncButton("파일 선택…", () => SelectFile(key)); select.Name = "CustomPetFile_" + key;
            var remove = Ui.Button("제거", () =>
            {
                draft.RemoveClip(key);
                if (previewAction == key)
                {
                    previewGeneration++; previewAction = null; preview.SetFrames([], true);
                    previewHint.IsVisible = true; previewLabel.Text = "";
                }
                Refresh();
            });
            remove.Name = "CustomPetRemove_" + key;
            var play = AsyncButton("", () => Preview(key)); play.Name = "CustomPetPreview_" + key;
            play.Classes.Add("pet-action-preview");
            play.HorizontalAlignment = HorizontalAlignment.Stretch; play.VerticalAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetName(select, CustomPetDraft.ActionName(key) + " 파일 선택");
            AutomationProperties.SetName(remove, CustomPetDraft.ActionName(key) + " 파일 제거");
            AutomationProperties.SetName(play, CustomPetDraft.ActionName(key) + " 미리보기");
            ConfigureSlotButton(select, "M11,4 H13 V11 H20 V13 H13 V20 H11 V13 H4 V11 H11 Z",
                CustomPetDraft.ActionName(key) + " 파일 선택");
            ConfigureSlotButton(remove,
                "M8,5 V3 H16 V5 H21 V7 H19 L18,21 H6 L5,7 H3 V5 Z M8,9 H10 V18 H8 Z M14,9 H16 V18 H14 Z",
                CustomPetDraft.ActionName(key) + " 파일 제거");
            Ui.Danger(remove);
            actions.AddRange([select, remove, play]);
            var heading = Ui.Text(CustomPetDraft.ActionName(key), DesignSystem.Body);
            heading.FontWeight = FontWeight.SemiBold;
            var requirement = Ui.Caption(key == "idle" ? "필수 · 반복" : "선택 · 한 번");
            label.MaxLines = 2; label.TextTrimming = TextTrimming.CharacterEllipsis;
            var controls = Ui.Row(select, remove); controls.Spacing = 4;
            controls.Name = "CustomPetActions_" + key;
            controls.HorizontalAlignment = HorizontalAlignment.Right; controls.VerticalAlignment = VerticalAlignment.Bottom;
            controls.Margin = new Thickness(10);
            var copy = Ui.Column(heading, requirement, label); copy.Spacing = 3;
            copy.Margin = new Thickness(10, 10, 10, 46); copy.IsHitTestVisible = false;
            label.Margin = new Thickness(0, 6, 0, 0);
            // The full-card button and file actions are siblings: file clicks never bubble through preview.
            var slotContent = new Grid();
            slotContent.Children.Add(play); slotContent.Children.Add(copy); slotContent.Children.Add(controls);
            var slot = new Border
            {
                Name = "CustomPetSlot_" + key,
                Background = DesignSystem.Raised,
                BorderBrush = DesignSystem.Outline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Child = slotContent
            };
            actionCards[key] = slot;
            slots.Children.Add(slot);
        }
        create = AsyncButton("펫 팩 만들기…", () => Export()); create.Name = "CreateCustomPetPack"; Ui.Primary(create);
        create.Height = DesignSystem.PetControlHeight;
        name.TextChanged += (_, _) => Refresh();
        pendingCard = BuildPendingCard();
        var identity = PetManagementView.Field("펫 이름", name);

        previewHint.Name = "CustomPetPreviewHint";
        previewHint.HorizontalAlignment = HorizontalAlignment.Center;
        previewHint.VerticalAlignment = VerticalAlignment.Center;
        previewHint.TextAlignment = TextAlignment.Center;
        previewHint.MaxWidth = 280;
        var previewStage = new Grid { RowDefinitions = new("*,Auto") };
        previewStage.Children.Add(new Viewbox
        {
            Child = preview,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 6)
        });
        previewStage.Children.Add(previewHint);
        previewLabel.Name = "CustomPetPreviewAction";
        previewLabel.HorizontalAlignment = HorizontalAlignment.Center;
        previewLabel.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetRow(previewLabel, 1); previewStage.Children.Add(previewLabel);
        previewSurface = new Border
        {
            Name = "CustomPetPreviewSurface",
            Background = DesignSystem.Surface,
            CornerRadius = DesignSystem.CardRadius,
            Padding = new Thickness(12),
            Height = 272,
            MaxWidth = DesignSystem.PetPreviewWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = previewStage
        };
        var guide = Ui.Caption("GIF는 원본 그대로 사용해요. MP4는 10초 이하 · 128 MiB까지 가져올 수 있어요.\nMP4는 소리 없이 최대 192px · 초당 12프레임으로 변환하며, 영상의 배경은 유지돼요.");
        var actionHeading = Ui.Text("행동별 파일"); actionHeading.FontWeight = FontWeight.SemiBold;
        var actionHeader = new Grid { ColumnDefinitions = new("*,12,Auto") };
        actionHeader.Children.Add(actionHeading);
        var cardHint = Ui.Caption("카드를 눌러 미리보기"); Grid.SetColumn(cardHint, 2); actionHeader.Children.Add(cardHint);
        var actionGroup = Ui.Column(actionHeader, slots, guide);
        var body = Ui.Column(identity, pendingCard, previewSurface, actionGroup);
        body.Spacing = DesignSystem.Inset;
        var footer = new Grid { ColumnDefinitions = new("*,20,Auto") };
        status.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(status);
        Grid.SetColumn(create, 2); footer.Children.Add(create);
        Content = PetManagementView.Page("나만의 펫을 만들어 보세요.", "파일을 동작에 배정한 뒤 펫 팩으로 저장하세요. 저장 후 미리보고 설치할 수 있어요.",
            body, footer, showHeader);
        owner.PropertyChanged += OwnerPropertyChanged;
        AttachedToVisualTree += (_, _) => UpdatePlayback();
        DetachedFromVisualTree += (_, _) => UpdatePlayback();
        UpdatePreviewSize();
        Refresh();
    }
    public static async Task<string?> PickMediaFile(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "파일 가져오기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GIF · MP4") { Patterns = ["*.gif", "*.mp4"] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private Task<string?> PickMedia() => PickMediaFile(owner);
    private async Task<string?> PickOutput()
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new() { Title = "커스텀 펫 팩 저장", DefaultExtension = "unfoldpet",
            SuggestedFileName = "custom-pet.unfoldpet", ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Unfold 펫 팩") { Patterns = ["*.unfoldpet"] }] });
        return file?.TryGetLocalPath();
    }
    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) UpdatePlayback();
        if (e.Property == TopLevel.ClientSizeProperty) UpdatePreviewSize();
    }
    private void UpdatePreviewSize() => previewSurface.Height = owner.ClientSize.Height is > 0 and < 740 ? 156 : 272;
    private void UpdatePlayback() => preview.SetRunning(TopLevel.GetTopLevel(this) is not null && owner.IsVisible && !closed && !busy);
    public void Dispose()
    {
        if (closed) return;
        closed = true; previewGeneration++; owner.PropertyChanged -= OwnerPropertyChanged;
        cancellation.Cancel(); preview.Dispose();
    }
    private async Task AssignPending()
    {
        if (pendingPath is not { } path || action.SelectedItem is not string key) return;
        // Keep the imported file when the replacement is declined so it can go to another action.
        if (!await ConfirmReplace(key, path)) return;
        if (await Assign(key, path))
        {
            pendingPath = null; pending.Text = "동작에 추가했어요. 아래에서 다른 파일을 넣거나 교체할 수 있어요.";
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
                preview.SetFrames(frames, key == "idle", pixel: false);
                previewAction = key;
                previewHint.IsVisible = false;
                previewLabel.Text = "미리보기 · " + CustomPetDraft.ActionName(key);
                RefreshCardSelection();
                UpdatePlayback();
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
            // Each creation is a new pack; keep the draft available for further editing.
            var snapshot = new CustomPetDraft();
            foreach (var (key, clip) in draft.Clips) snapshot.SetClip(key, clip);
            await Task.Run(() => snapshot.Export(petName, path));
            if (closed) return false;
            savedName = petName.Trim(); savedClips.Clear();
            foreach (var pair in snapshot.Clips) savedClips.Add(pair.Key, pair.Value);
            exporting = false;
            status.Foreground = DesignSystem.Muted; status.Text = "펫 팩을 저장했어요. ‘펫 팩 열기’ 탭에서 설치할 수 있어요.";
            if (showCreated) await created(path);
            return true;
        }
        catch (Exception error) { ShowError(error); return false; }
        finally { busy = exporting = false; if (!closed) { Refresh(); BusyChanged?.Invoke(); } }
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
                "펫 이름과 필수 ‘쉬는 모습’을 넣고 가져온 파일을 배정하면 저장할 수 있어요. 계속 작성하거나 초안을 버릴 수 있어요.",
            canSave ? "저장하고 종료" : "계속 작성", "버리기", "취소");
        if (choice == 1) return true;
        return choice == 0 && canSave && await Export(showCreated: false);
    }
    private void Refresh()
    {
        name.IsEnabled = action.IsEnabled = !busy;
        UpdatePlayback();
        pendingCard.IsVisible = pendingPath is not null;
        assign.IsEnabled = !busy && pendingPath is not null;
        create.IsEnabled = !busy && !string.IsNullOrWhiteSpace(name.Text) && draft.Clips.ContainsKey("idle");
        foreach (var (key, label) in labels)
            label.Text = draft.Clips.TryGetValue(key, out var clip)
                ? $"{clip.Width} × {clip.Height}px\n{clip.FrameCount}프레임 · {clip.Duration.TotalSeconds:0.##}초" : "파일 없음";
        foreach (var button in actions)
            button.IsEnabled = !busy && (button.Name!.StartsWith("CustomPetFile_") || draft.Clips.ContainsKey(button.Name[(button.Name.LastIndexOf('_') + 1)..]));
        RefreshCardSelection();
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
