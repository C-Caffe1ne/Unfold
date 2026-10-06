using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetManagementView : UserControl, IDisposable
{
    private readonly Window owner;
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>>? choosePack;
    private readonly PetBuilderView builder;
    private PetPackWindow? importWindow;
    public bool IsBusy => builder.IsBusy || importWindow?.IsBusy == true;
    internal bool HasUnsavedDraft => builder.HasUnsavedChanges;
    internal Task<bool> CanCloseDraft() => builder.CanCloseDraft();

    public PetManagementView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? choosePack = null, Func<Task<string?>>? chooseMedia = null,
        Func<Task<string?>>? chooseOutput = null, bool showPageHeaders = true, Func<string, Task>? removed = null)
    {
        this.owner = owner; this.library = library; this.installed = installed; this.choosePack = choosePack;
        builder = new(owner, library, installed, OpenPack, chooseMedia: chooseMedia, chooseOutput: chooseOutput, removed: removed);
        Content = builder;
    }
    private async Task OpenPack(string path)
    {
        if (importWindow is not null) return;
        PetPackWindow? window = null;
        window = new PetPackWindow(library, async package =>
        {
            await installed(package);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => window?.Close());
        }, choosePack);
        importWindow = window;
        try
        {
            var dialog = window.ShowDialog(owner);
            await window.OpenPath(path);
            await dialog;
        }
        finally { importWindow = null; builder.RefreshPets(); }
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
        var page = (Grid)Ui.PageContent(title, description, capped, footer, "펫 관리", showHeader);
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

    public void Dispose() { importWindow?.Close(); builder.Dispose(); }
}
