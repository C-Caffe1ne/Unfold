using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetManagementView : UserControl, IDisposable
{
    private readonly PetPackView packs;
    private readonly CustomPetView builder;
    private readonly TabControl tabs = new() { Name = "PetManagementTabs",
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    public bool IsBusy => packs.IsBusy || builder.IsExporting;

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
            new TabItem { Header = "펫 팩 열기", Content = packs },
            new TabItem { Header = "펫 팩 만들기", Content = builder }
        };
        tabs.SelectedIndex = 0;
        packs.BusyChanged += UpdateBusy;
        builder.BusyChanged += UpdateBusy;
        Content = tabs;
    }

    private void UpdateBusy() => tabs.IsEnabled = !IsBusy;
    public void Dispose() { packs.Dispose(); builder.Dispose(); }
}
