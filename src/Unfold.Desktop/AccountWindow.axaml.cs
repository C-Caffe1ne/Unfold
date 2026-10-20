using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Svg;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class AccountWindow : Window
{
    internal AccountScreenModel Model { get; }
    private readonly Bitmap? companion;
    private readonly Bitmap brandLogo, brandLogoLight;
    public AccountWindow() : this(CreateModel()) { }
    private static AccountScreenModel CreateModel()
    {
        var content = AccountScreenContent.Load();
        return new(content, new DesktopAccountService(content));
    }
    internal AccountWindow(AccountScreenModel model, Func<Task>? quit = null,
        bool requiresPurchase = false, Func<bool>? allowClose = null, Action<AppLanguage>? changeLanguage = null)
    {
        AvaloniaXamlLoader.Load(this);
        NativeWindowChrome.Configure(this);
        var layout = (Control)Content!; Content = null;
        Content = NativeWindowChrome.Host(this, layout);
        Model = model; DataContext = model;
        brandLogo = LoadBrandLogo("logo-lilac-640.png");
        brandLogoLight = LoadBrandLogo("logo-light-640.png");
        var logo = this.FindControl<Image>("AccountBrandLogo")!;
        void UpdateBrandLogo() => logo.Source = ActualThemeVariant == ThemeVariant.Dark ? brandLogoLight : brandLogo;
        ActualThemeVariantChanged += (_, _) => UpdateBrandLogo();
        UpdateBrandLogo();
        this.FindControl<Image>("AccountGoogleIcon")!.Source = new SvgImage
        {
            Source = SvgSource.Load("avares://Unfold/Assets/Icons/Google/google.svg", null)
        };
        try { companion = new Bitmap(Path.Combine(AccountScreenContent.AssetRoot, model.Content.CompanionImage)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException) { }
        this.FindControl<Image>("AccountCompanion")!.Source = companion;
        this.FindControl<Button>("AccountPrimary")!.Click += async (_, _) =>
        {
            await Model.PrimaryAsync();
            if (Model.PurchaseReady) Close();
        };
        this.FindControl<Button>("AccountSecondary")!.Click += async (_, _) => await Model.SecondaryAsync();
        var codeDialogOpen = false;
        this.FindControl<Button>("AccountCode")!.Click += async (_, _) =>
        {
            if (codeDialogOpen || !Model.CanRedeemCode) return;
            codeDialogOpen = true;
            try
            {
                var copy = Model.Copy;
                var redeemed = await Ui.PromptCode(this, copy.CodeDialogTitle, copy.CodeLabel, copy.CodeConfirmButton,
                    copy.CancelButton, Model.RedeemCodeAsync);
                if (redeemed && Model.PurchaseReady) Close();
            }
            finally { codeDialogOpen = false; }
        };
        Closing += (_, args) =>
        {
            if (!requiresPurchase || Model.PurchaseReady || allowClose?.Invoke() == true) return;
            args.Cancel = true;
            // The title bar close button cannot dismiss the purchase gate; it offers the same quit as the visible button.
            if (!args.IsProgrammatic && args.CloseReason == WindowCloseReason.WindowClosing && quit is not null)
                Dispatcher.UIThread.Post(() => _ = quit());
        };
        this.FindControl<Button>("AccountQuit")!.Click += async (_, _) =>
        {
            if (quit is null) Close();
            else await quit();
        };
        if (changeLanguage is not null) ConfigureLanguage(changeLanguage);
        this.FindControl<Border>("AccountDragRegion")!.PointerPressed += (_, args) =>
        {
            // The extended macOS title bar role already started AppKit's move for this press.
            if (IsExtendedIntoWindowDecorations || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            BeginMoveDrag(args);
            args.Handled = true;
        };
        SizeChanged += (_, _) => Classes.Set("compact", ClientSize.Width < 780);
        Closed += (_, _) => { Model.Dispose(); companion?.Dispose(); brandLogo.Dispose(); brandLogoLight.Dispose(); };
    }

    // The first screen a new user sees, so the language can be chosen before signing in.
    private void ConfigureLanguage(Action<AppLanguage> changeLanguage)
    {
        var languages = L.Languages.ToList();
        var picker = this.FindControl<ComboBox>("AccountLanguage")!;
        picker.ItemsSource = languages.Select(L.NativeName).ToArray();
        picker.SelectedIndex = languages.IndexOf(L.Current);
        picker.ContainerPrepared += (_, e) => e.Container.Classes.Add("home-choice-item");
        picker.IsVisible = true;
        AutomationProperties.SetName(picker, L.T("언어"));
        picker.SelectionChanged += async (_, _) =>
        {
            if (picker.SelectedIndex < 0 || languages[picker.SelectedIndex] == L.Current) return;
            var language = languages[picker.SelectedIndex];
            // Let the drop-down finish closing before the screen text changes under it.
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
            try { changeLanguage(language); }
            catch (Exception error) { await Ui.Error(this, error); }
            picker.SelectedIndex = languages.IndexOf(L.Current);
            AutomationProperties.SetName(picker, L.T("언어"));
        };
    }

    private static Bitmap LoadBrandLogo(string fileName)
    {
        using var asset = AssetLoader.Open(new Uri($"avares://Unfold/Assets/Icons/Brand/{fileName}"));
        return new Bitmap(asset);
    }
}
