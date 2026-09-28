using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace Unfold.Desktop;

public sealed partial class AccountWindow : Window
{
    internal AccountScreenModel Model { get; }
    private readonly Bitmap? companion;
    public AccountWindow() : this(CreateModel()) { }
    private static AccountScreenModel CreateModel()
    {
        var content = AccountScreenContent.Load();
        return new(content, new DesktopAccountService(content));
    }
    internal AccountWindow(AccountScreenModel model)
    {
        AvaloniaXamlLoader.Load(this);
        Model = model; DataContext = model;
        try { companion = new Bitmap(Path.Combine(AccountScreenContent.AssetRoot, model.Content.CompanionImage)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException) { }
        this.FindControl<Image>("AccountCompanion")!.Source = companion;
        this.FindControl<Button>("AccountPrimary")!.Click += async (_, _) => await Model.PrimaryAsync();
        this.FindControl<Button>("AccountSecondary")!.Click += (_, _) => Model.Secondary();
        this.FindControl<Button>("AccountOpenApp")!.Click += (_, _) => Close();
        this.FindControl<Grid>("AccountDragHandle")!.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(args);
        };
        SizeChanged += (_, _) => Classes.Set("compact", ClientSize.Width < 780);
        Closed += (_, _) => { Model.Dispose(); companion?.Dispose(); };
    }
}
