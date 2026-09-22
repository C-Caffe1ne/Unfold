using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetManagementView : UserControl, IDisposable
{
    private readonly PetPackView packs;
    private readonly CustomPetView builder;
    private readonly TabControl tabs = new() { Name = "PetManagementTabs", Padding = new Thickness(0, 20, 0, 0),
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    public bool IsBusy => packs.IsBusy || builder.IsBusy;
    internal bool HasUnsavedDraft => builder.HasUnsavedChanges;
    internal Task<bool> CanCloseDraft() { tabs.SelectedIndex = 1; return builder.CanCloseDraft(); }

    public PetManagementView(Window owner, CharacterLibrary library, Func<CharacterPackage, Task> installed,
        Func<Task<string?>>? choosePack = null, Func<Task<string?>>? chooseMedia = null,
        Func<Task<string?>>? chooseOutput = null, bool showPageHeaders = true)
    {
        packs = new(owner, library, installed, choosePack, showPageHeaders);
        builder = new(owner, async path =>
        {
            tabs.SelectedIndex = 0;
            await packs.OpenPath(path);
        }, chooseMedia: chooseMedia, chooseOutput: chooseOutput, showHeader: showPageHeaders);
        AutomationProperties.SetName(tabs, "펫 추가 방식");
        tabs.ItemsSource = new[]
        {
            Tab("펫 팩 열기", packs),
            Tab("펫 팩 만들기", builder)
        };
        tabs.SelectedIndex = 0;
        packs.BusyChanged += UpdateBusy;
        builder.BusyChanged += UpdateBusy;
        Content = tabs;
    }

    private static TabItem Tab(string title, Control content) => new()
    {
        Header = title, Content = content, FontSize = DesignSystem.Section, FontWeight = FontWeight.SemiBold,
        Padding = new Thickness(0, 4, 0, 10), Margin = new Thickness(0, 0, 24, 0)
    };

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
        var page = (Grid)Ui.PageContent(title, description, capped, footer, "펫 추가", showHeader);
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

    private void UpdateBusy() => tabs.IsEnabled = !IsBusy;
    public void Dispose() { packs.Dispose(); builder.Dispose(); }
}
