using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetBuilderView : UserControl, IDisposable
{
    private readonly Window owner;
    private readonly CustomPetView media;
    private readonly GlbPetView glb;
    private readonly ContentControl editor = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Func<Task<string?>> chooseFile;
    private readonly Func<string, Task> openPack;
    private readonly CharacterLibrary library;
    private readonly ComboBox pets = new() { Name = "MediaExistingPets", PlaceholderText = "펫 선택",
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private bool refreshingPets;
    private bool opening, closed;
    public bool IsBusy => opening || media.IsBusy || glb.IsBusy;
    public bool HasUnsavedChanges => media.HasUnsavedChanges || glb.HasUnsavedChanges;
    public event Action? BusyChanged;
    public PetBuilderView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed, Func<string, Task> created,
        Func<Task<string?>>? chooseMedia = null, Func<Task<string?>>? chooseOutput = null)
    {
        this.owner = owner; this.library = library; openPack = created; chooseFile = chooseMedia ?? PickFile;
        PetManagementView.StyleChoice(pets);
        pets.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<CharacterPackage>((package, _) =>
        {
            var label = Ui.Text(package?.Manifest.Name ?? ""); label.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(label, package?.Manifest.Name); return label;
        });
        Avalonia.Automation.AutomationProperties.SetName(pets, "펫 선택");
        pets.SelectionChanged += async (_, _) =>
        {
            if (refreshingPets || IsBusy || pets.SelectedItem is not CharacterPackage package) return;
            await OpenPackage(package); RefreshPets();
        };
        media = new(owner, created, chooseMedia: chooseMedia, chooseOutput: chooseOutput, showHeader: false,
            openFile: OpenFile, petSelection: PetManagementView.Field("펫 선택", pets));
        glb = new(owner, library, installed, showHeader: false, embedded: true, created: created, chooseOutput: chooseOutput, openFile: OpenFile);
        AttachedToVisualTree += (_, _) => RefreshPets();
        media.BusyChanged += UpdateBusy; glb.BusyChanged += UpdateBusy;
        editor.Content = media; Content = editor;
    }
    private async Task OpenFile()
    {
        if (IsBusy || closed) return;
        opening = true; UpdateBusy();
        try
        {
            var path = await chooseFile();
            if (path is not null && !closed) await OpenPath(path);
        }
        catch (Exception error) { AppPaths.Log(error); await Ui.Confirm(owner, "파일 열기 실패", Ui.ErrorText(error), "확인"); }
        finally { opening = false; if (!closed) UpdateBusy(); }
    }
    internal async Task OpenPath(string path)
    {
        if (closed) return;
        if (string.Equals(Path.GetExtension(path), ".unfoldpet", StringComparison.OrdinalIgnoreCase))
        { await openPack(path); return; }
        if (string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
        { editor.Content = glb; await glb.OpenPath(path); }
        else { editor.Content = media; await media.OpenPath(path); }
    }
    internal async Task OpenPackage(CharacterPackage package)
    { if (!closed) { editor.Content = glb; await glb.OpenPackage(package); } }
    internal void RefreshPets()
    {
        if (closed) return;
        refreshingPets = true;
        try { pets.ItemsSource = library.List().Where(package => package.IsGlb).ToArray(); pets.SelectedIndex = -1; }
        finally { refreshingPets = false; }
        glb.RefreshExisting();
    }
    private async Task<string?> PickFile()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "펫 파일 열기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("펫 팩 · GLB · " + PetMediaImporter.SupportedFileTypes) { Patterns = ["*.unfoldpet", "*.glb", ..PetMediaImporter.FilePatterns] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private void UpdateBusy() { media.SetImportEnabled(!IsBusy); glb.SetImportEnabled(!IsBusy); BusyChanged?.Invoke(); }
    internal async Task<bool> CanCloseDraft()
    {
        if (IsBusy) return false;
        if (media.HasUnsavedChanges) { editor.Content = media; if (!await media.CanCloseDraft()) return false; }
        if (glb.HasUnsavedChanges) { editor.Content = glb; if (!await glb.CanCloseDraft()) return false; }
        return true;
    }
    public void Dispose() { if (closed) return; closed = true; media.Dispose(); glb.Dispose(); }
}
