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
    private readonly PetBuilderView builder;
    private bool importing, disposed;
    public bool IsBusy => builder.IsBusy || importing;
    internal bool HasUnsavedDraft => builder.HasUnsavedChanges;
    internal Task<bool> CanCloseDraft() => builder.CanCloseDraft();

    public PetManagementView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? chooseMedia = null,
        Func<Task<string?>>? chooseOutput = null, bool showPageHeaders = true, Func<string, Task>? removed = null)
    {
        this.owner = owner; this.library = library; this.installed = installed;
        builder = new(owner, library, installed, OpenPack, chooseMedia: chooseMedia, chooseOutput: chooseOutput, removed: removed);
        Content = builder;
    }
    private async Task OpenPack(string path)
    {
        if (importing || disposed) return;
        importing = true;
        try
        {
            await PetPackImport.Save(path, library, installed, async (pack, target) =>
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
        finally { importing = false; if (!disposed) builder.RefreshPets(); }
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

    public void Dispose() { disposed = true; builder.Dispose(); }
}
