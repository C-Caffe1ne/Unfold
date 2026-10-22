using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetManagementView : UserControl, IDisposable
{
    private readonly Window owner;
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>>? chooseMedia, chooseOutput;
    private readonly Func<string, Task>? removed;
    private readonly PetLibraryView libraryView;
    private readonly ContentControl page = new()
    { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private PetBuilderView? builder;
    private Control? editorPage;
    private Button? back, startNew;
    private string? appliedId;
    private bool importing, switching, disposed;
    public bool IsBusy => builder?.IsBusy == true || importing || switching || libraryView.IsBusy;
    internal bool HasUnsavedDraft => builder?.HasUnsavedChanges == true;
    internal async Task<bool> CanCloseDraft()
    {
        if (builder is null) return true;
        ShowEditor();
        return await builder.CanCloseDraft();
    }

    public PetManagementView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseMedia = null,
        Func<Task<string?>>? chooseOutput = null, bool showPageHeaders = true, Func<string, Task>? removed = null,
        Func<IReadOnlyList<CharacterPackage>>? getPets = null, Func<string?>? selectedId = null,
        Func<string, bool>? isFavorite = null, Func<string, bool, Task>? setFavorite = null)
    {
        this.owner = owner; this.library = library; this.installed = installed;
        this.chooseMedia = chooseMedia; this.chooseOutput = chooseOutput; this.removed = removed;
        libraryView = new(getPets ?? (() => library.List(loadModels: false)), selectedId ?? (() => appliedId),
            isFavorite ?? (_ => false), setFavorite, Apply, OpenEditor, CanEdit, OpenBuilder, Import);
        libraryView.BusyChanged += UpdateBusy;
        page.Content = libraryView; Content = page;
        AttachedToVisualTree += (_, _) => RefreshLibrary();
    }
    internal void RefreshLibrary()
    {
        if (disposed) return;
        libraryView.RefreshLibrary(); libraryView.SetDraftAvailable(HasUnsavedDraft);
    }
    internal void RefreshSelection()
    {
        if (disposed) return;
        libraryView.RefreshSelection(); libraryView.SetDraftAvailable(HasUnsavedDraft);
    }
    private void EnsureBuilder()
    {
        if (builder is not null) return;
        builder = new(owner, library, Apply, OpenPack, chooseMedia: chooseMedia, chooseOutput: chooseOutput, removed: Removed);
        builder.LibraryChanged += RefreshLibrary; builder.BusyChanged += UpdateBusy;
        back = Ui.Button(L.T("목록으로"), ShowLibrary); back.Name = "PetLibraryBack";
        startNew = Ui.AsyncButton(L.T("새로 만들기"), StartNew); startNew.Name = "PetLibraryStartNew";
        var controls = Ui.Row(back, startNew);
        var layout = new Grid { RowDefinitions = new("Auto,12,*") };
        layout.Children.Add(controls); Grid.SetRow(builder, 2); layout.Children.Add(builder);
        editorPage = layout;
    }
    private void ShowEditor()
    {
        if (disposed) return;
        EnsureBuilder(); page.Content = editorPage;
        FocusEditor();
    }
    private void FocusEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!disposed && page.Content == editorPage) builder?.FocusEditor();
        }, DispatcherPriority.Loaded);
    }
    internal void ShowLibrary()
    {
        if (disposed || builder?.IsBusy == true || importing || switching) return;
        page.Content = libraryView; RefreshLibrary(); libraryView.FocusSearch();
    }
    private Task OpenBuilder()
    {
        if (!disposed && builder?.IsBusy != true && !importing && !switching)
        {
            if (builder is not null && !HasUnsavedDraft) ResetBuilder();
            ShowEditor();
        }
        return Task.CompletedTask;
    }
    private async Task StartNew()
    {
        if (disposed || builder?.IsBusy == true || importing || switching) return;
        switching = true; UpdateBusy();
        try
        {
            if (HasUnsavedDraft && await Ui.Confirm(owner, L.T("저장하지 않은 변경을 버릴까요?"),
                L.T("저장하지 않은 이름과 행동 설정은 사라져요."), L.T("변경 버리기"), L.T("계속 편집")) != 0) return;
            if (disposed) return;
            ResetBuilder(); ShowEditor();
        }
        finally { switching = false; if (!disposed) UpdateBusy(); }
    }
    private void ResetBuilder()
    {
        builder?.Dispose(); builder = null; editorPage = null;
    }
    private bool CanEdit(CharacterPackage package) => !package.IsBuiltIn && library.CanManage(package.Manifest.Id);
    /// <summary>Opens the editor for the pet in use. A pet that cannot be edited leaves the page as it is.</summary>
    internal async Task EditPet(CharacterPackage package)
    {
        if (disposed || !CanEdit(package)) return;
        // Its open draft, saved or not, is already that pet; reopening would ask to discard it.
        if (builder?.EditingId == package.Manifest.Id) { ShowEditor(); return; }
        await OpenEditor(package);
    }
    private async Task OpenEditor(CharacterPackage package)
    {
        if (disposed || builder?.IsBusy == true || importing || switching) return;
        ShowEditor();
        await builder!.OpenPackage(package);
        FocusEditor();
    }
    private async Task Import()
    {
        if (disposed || builder?.IsBusy == true || importing || switching) return;
        EnsureBuilder();
        await builder!.OpenFile();
        if (!disposed && builder.HasUnsavedChanges) ShowEditor();
        RefreshLibrary();
    }
    private async Task Apply(CharacterPackage package)
    {
        await installed(package);
        if (disposed) return;
        appliedId = package.Manifest.Id; RefreshLibrary();
    }
    private async Task Removed(string id)
    {
        if (removed is not null) await removed(id);
        if (appliedId == id) appliedId = null;
        RefreshLibrary();
    }
    private void UpdateBusy()
    {
        if (back is not null) back.IsEnabled = !IsBusy;
        if (startNew is not null) startNew.IsEnabled = !IsBusy;
    }
    private async Task OpenPack(string path)
    {
        if (importing || disposed) return;
        importing = true;
        try
        {
            await PetPackImport.Save(path, library, Apply, async (pack, target) =>
            {
                var action = target.Action switch
                {
                    "Update" => L.T("업데이트"), "Reinstall" => L.T("재설치"), "Replace" => L.T("교체"), _ => L.T("저장")
                };
                var message = L.F("{0} · 버전 {1}", pack.Character.Manifest.Name, pack.ContentVersion) + "\n" +
                    (target.InstalledVersion is { } current ? L.F("설치된 버전 {0}을 {1}할까요?", current, action) : L.T("이 펫을 저장할까요?"));
                var choice = await Ui.Confirm(owner, L.F("펫 {0}", action), message, action, L.T("취소"));
                return !disposed && choice == 0;
            });
        }
        finally { importing = false; if (!disposed) { builder?.RefreshPets(); RefreshLibrary(); } }
    }

    internal static StackPanel Field(string label, Control input)
    {
        var caption = Ui.Text(label);
        AutomationProperties.SetLabeledBy(input, caption); Ui.KeyboardFocusLabel(input, caption);
        var field = Ui.Column(caption, input); field.Spacing = DesignSystem.Space;
        return field;
    }

    internal static Control Page(string title, string description, Control body, Control footer, bool showHeader)
    {
        var capped = Ui.CenteredBody(body, DesignSystem.PetContentWidth, "PetPageBody");
        var page = (Grid)Ui.PageContent(title, description, capped, footer, L.T("펫 관리"), showHeader);
        page.RowDefinitions[^2].Height = new GridLength(24);
        var actionBar = page.Children.OfType<Border>().Single(control => control.Name == "PageActions");
        actionBar.BorderThickness = new Thickness(0); actionBar.Padding = new Thickness(0);
        return page;
    }

    internal static void StyleChoice(ComboBox choice, bool previewOption = false)
    {
        var style = previewOption ? "pet-preview-choice" : "pet-choice";
        choice.Classes.Add(style); choice.Height = DesignSystem.PetControlHeight;
        choice.MaxDropDownHeight = 240;
        choice.ContainerPrepared += (_, args) => args.Container.Classes.Add(style + "-item");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; builder?.Dispose(); libraryView.Dispose();
    }
}
