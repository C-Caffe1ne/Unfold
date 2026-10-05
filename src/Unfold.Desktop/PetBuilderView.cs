using Avalonia;
using Avalonia.Automation;
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
    private readonly ComboBox format = new() { Name = "PetBuilderFormat", ItemsSource = new[] { "GIF · MP4 · 이미지", "GLB" }, SelectedIndex = 0 };
    private readonly ContentControl editor = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Button open;
    private readonly Func<Task<string?>> chooseFile;
    private bool opening, closed;
    public bool IsBusy => opening || media.IsBusy || glb.IsBusy;
    public bool HasUnsavedChanges => media.HasUnsavedChanges || glb.HasUnsavedChanges;
    public event Action? BusyChanged;
    public PetBuilderView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed, Func<string, Task> created,
        Func<Task<string?>>? chooseMedia = null, Func<Task<string?>>? chooseOutput = null)
    {
        this.owner = owner; chooseFile = chooseMedia ?? PickFile;
        media = new(owner, created, chooseMedia: chooseMedia, chooseOutput: chooseOutput, showHeader: false);
        glb = new(owner, library, installed, showHeader: false, embedded: true, created: created, chooseOutput: chooseOutput);
        media.BusyChanged += UpdateBusy; glb.BusyChanged += UpdateBusy;
        PetManagementView.StyleChoice(format); AutomationProperties.SetName(format, "파일 형식");
        format.SelectionChanged += (_, _) => editor.Content = format.SelectedIndex == 1 ? glb : media;
        open = Ui.Action("파일 열기…"); open.Name = "OpenPetBuilderFile"; open.Height = DesignSystem.PetControlHeight;
        open.Click += async (_, _) =>
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
        };
        format.Width = 180;
        var source = Ui.Row(format, open); source.Spacing = 12;
        var body = new Grid { RowDefinitions = new("Auto,16,*") };
        body.Children.Add(Ui.CenteredBody(source, DesignSystem.PetContentWidth, "PetBuilderSource"));
        Grid.SetRow(editor, 2); body.Children.Add(editor); editor.Content = media; Content = body;
    }
    internal async Task OpenPath(string path)
    {
        if (closed) return;
        if (string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
        { format.SelectedIndex = 1; await glb.OpenPath(path); }
        else { format.SelectedIndex = 0; await media.OpenPath(path); }
    }
    private async Task<string?> PickFile()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new() { Title = "펫 파일 열기", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GLB · " + PetMediaImporter.SupportedFileTypes) { Patterns = ["*.glb", ..PetMediaImporter.FilePatterns] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private void UpdateBusy() { format.IsEnabled = open.IsEnabled = !IsBusy; BusyChanged?.Invoke(); }
    internal async Task<bool> CanCloseDraft()
    {
        if (IsBusy) return false;
        if (media.HasUnsavedChanges) { format.SelectedIndex = 0; if (!await media.CanCloseDraft()) return false; }
        if (glb.HasUnsavedChanges) { format.SelectedIndex = 1; if (!await glb.CanCloseDraft()) return false; }
        return true;
    }
    public void Dispose() { if (closed) return; closed = true; media.Dispose(); glb.Dispose(); }
}
