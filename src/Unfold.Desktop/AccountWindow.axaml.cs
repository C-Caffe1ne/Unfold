using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Unfold.Core;

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
    internal AccountWindow(AccountScreenModel model, Func<Task>? quit = null, Func<AccountIdentity?, Task>? accountChanged = null)
    {
        AvaloniaXamlLoader.Load(this);
        Model = model; DataContext = model;
        try { companion = new Bitmap(Path.Combine(AccountScreenContent.AssetRoot, model.Content.CompanionImage)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException) { }
        this.FindControl<Image>("AccountCompanion")!.Source = companion;
        this.FindControl<Button>("AccountPrimary")!.Click += async (_, _) =>
        {
            await Model.PrimaryAsync();
            if (Model.PurchaseReady)
            {
                if (accountChanged is not null) await accountChanged(Model.VerifiedAccount);
                Close();
            }
        };
        this.FindControl<Button>("AccountSecondary")!.Click += async (_, _) =>
        {
            Model.Secondary();
            if (accountChanged is not null) await accountChanged(null);
        };
        this.FindControl<Button>("AccountQuit")!.Click += async (_, _) =>
        {
            if (quit is null) Close();
            else await quit();
        };
        this.FindControl<Grid>("AccountDragHandle")!.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(args);
        };
        SizeChanged += (_, _) => Classes.Set("compact", ClientSize.Width < 780);
        Closed += (_, _) => { Model.Dispose(); companion?.Dispose(); };
    }
}
