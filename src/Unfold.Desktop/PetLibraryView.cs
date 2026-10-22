using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed class PetLibraryView : UserControl, IDisposable
{
    private enum Filter { All, Favorites, Sprite, Model }
    private sealed record Card(CharacterPackage Package, Border Surface, Image Image, Bitmap Bitmap,
        Button Apply, Button Edit, Button Favorite, TextBlock Applied);
    private readonly Func<IReadOnlyList<CharacterPackage>> getPets;
    private readonly Func<string?> selectedId;
    private readonly Func<string, bool> isFavorite;
    private readonly Func<string, bool, Task>? setFavorite;
    private readonly Func<CharacterPackage, Task> apply, edit;
    private readonly Func<CharacterPackage, bool> canEdit;
    private readonly TextBox search = new() { Name = "PetLibrarySearch", PlaceholderText = L.T("펫 검색") };
    private readonly WrapPanel cards = new() { Name = "PetLibraryCards", ItemSpacing = 12, LineSpacing = 12 };
    private readonly TextBlock empty = Ui.Caption(""), error = Ui.Caption("");
    private readonly Button create, import;
    private readonly List<Card> visibleCards = [];
    private readonly Dictionary<Filter, Button> filters = [];
    private CharacterPackage[] packages = [];
    private Filter filter;
    private bool loaded, disposed;
    public bool IsBusy { get; private set; }
    internal int ThumbnailCount => visibleCards.Count;
    internal void FocusSearch() => search.Focus();
    public event Action? BusyChanged;

    public PetLibraryView(Func<IReadOnlyList<CharacterPackage>> getPets, Func<string?> selectedId,
        Func<string, bool> isFavorite, Func<string, bool, Task>? setFavorite,
        Func<CharacterPackage, Task> apply, Func<CharacterPackage, Task> edit,
        Func<CharacterPackage, bool> canEdit, Func<Task> createPet, Func<Task> importPet)
    {
        this.getPets = getPets; this.selectedId = selectedId; this.isFavorite = isFavorite;
        this.setFavorite = setFavorite; this.apply = apply; this.edit = edit; this.canEdit = canEdit;
        create = Action("PetLibraryCreate", L.T("펫 만들기"), createPet); Ui.Primary(create);
        import = Action("PetLibraryImport", L.T("가져오기"), importPet);
        var heading = Ui.Text(L.T("내 펫"), DesignSystem.Title); heading.FontWeight = FontWeight.SemiBold;
        var header = new Grid { ColumnDefinitions = new("*,Auto") };
        header.Children.Add(heading);
        var actions = Ui.Actions(import, create); Grid.SetColumn(actions, 1); header.Children.Add(actions);
        AutomationProperties.SetName(search, L.T("펫 검색"));
        search.TextChanged += (_, _) => RenderCards();
        var choices = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        foreach (var (value, text) in new[] { (Filter.All, L.T("전체")), (Filter.Favorites, L.T("즐겨찾기")),
            (Filter.Sprite, L.T("2D")), (Filter.Model, L.T("3D")) })
        {
            var choice = Ui.Button(text, () =>
            {
                filter = value; UpdateFilters(); RenderCards();
            });
            choice.Name = "PetLibraryFilter" + value;
            filters[value] = choice; choices.Children.Add(choice);
        }
        empty.Name = "PetLibraryEmpty"; error.Name = "PetLibraryError";
        error.Foreground = DesignSystem.Error; error.IsVisible = false;
        var body = Ui.Column(header, search, choices, error, empty, cards);
        body.Spacing = DesignSystem.Inset;
        Content = Ui.PageBodyScroll(Ui.CenteredBody(body, DesignSystem.PetContentWidth, "PetPageBody"));
        UpdateFilters();
    }

    private Button Action(string name, string text, Func<Task> action)
    {
        var button = Ui.Action(text); button.Name = name;
        button.Click += async (_, _) => await Run(action);
        return button;
    }
    private async Task Run(Func<Task> action)
    {
        if (disposed || IsBusy) return;
        IsBusy = true; error.IsVisible = false; UpdateControls(); BusyChanged?.Invoke();
        try { await action(); }
        catch (Exception failure)
        {
            AppPaths.Log(failure);
            if (!disposed) { error.Text = Ui.ErrorText(failure); error.IsVisible = true; }
        }
        finally
        {
            IsBusy = false;
            if (!disposed) { RefreshSelection(); UpdateControls(); BusyChanged?.Invoke(); }
        }
    }
    private async Task ToggleFavorite(Button button, string id, NavigationMethod navigation)
    {
        if (disposed || IsBusy || setFavorite is null) return;
        var owner = TopLevel.GetTopLevel(this);
        var restoreFocus = button.IsFocused;
        var index = visibleCards.FindIndex(card => ReferenceEquals(card.Favorite, button));
        await Run(() => setFavorite(id, !isFavorite(id)));

        // Busy disables the focused button; refreshing a Favorites filter can remove it entirely.
        if (!restoreFocus || disposed || !IsEffectivelyVisible || owner is null ||
            TopLevel.GetTopLevel(this) != owner ||
            owner.FocusManager?.GetFocusedElement() is { } focused && !ReferenceEquals(focused, button))
            return;
        var target = visibleCards.FirstOrDefault(card => card.Package.Manifest.Id == id)?.Favorite;
        if (target is null && visibleCards.Count > 0)
            target = visibleCards[Math.Clamp(index, 0, visibleCards.Count - 1)].Favorite;
        (target ?? filters[filter]).Focus(navigation);
    }
    internal void SetDraftAvailable(bool available) => create.Content = available ? L.T("계속 편집") : L.T("펫 만들기");
    internal void RefreshLibrary()
    {
        if (disposed) return;
        var next = getPets().OrderBy(p => p.IsBuiltIn ? 0 : 1)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (loaded && packages.Length == next.Length && packages.Zip(next).All(pair => ReferenceEquals(pair.First, pair.Second)))
        { RefreshSelection(); return; }
        loaded = true;
        packages = next;
        RenderCards();
    }
    internal void RefreshSelection()
    {
        if (disposed) return;
        if (!FilteredPets().Select(p => p.Manifest.Id).SequenceEqual(visibleCards.Select(c => c.Package.Manifest.Id)))
        { RenderCards(); return; }
        foreach (var card in visibleCards) UpdateCard(card);
    }
    private IEnumerable<CharacterPackage> FilteredPets()
    {
        var query = search.Text?.Trim() ?? "";
        return packages.Where(p => (query.Length == 0 || p.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            && (filter switch
            {
                Filter.Favorites => isFavorite(p.Manifest.Id),
                Filter.Sprite => !p.IsGlb,
                Filter.Model => p.IsGlb,
                _ => true
            }));
    }
    private void RenderCards()
    {
        if (disposed) return;
        ClearCards();
        foreach (var package in FilteredPets())
        {
            var id = package.Manifest.Id;
            var bitmap = Ui.Bitmap(Thumbnail(package.StillImage));
            var image = new Image { Name = "PetLibraryThumbnail_" + id, Source = bitmap, Height = 108, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, package.Manifest.RenderStyle == "pixel"
                ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
            var favorite = Ui.Action(""); favorite.Name = "PetLibraryFavorite_" + id; favorite.Content = FavoriteStar();
            var navigation = NavigationMethod.Unspecified;
            favorite.GotFocus += (_, args) => navigation = args.NavigationMethod;
            favorite.Click += async (_, _) => await ToggleFavorite(favorite, id, navigation);
            favorite.Classes.Add("pet-favorite"); favorite.Width = 38; favorite.Padding = new Thickness(0);
            var applied = Ui.Caption(L.T("적용 중")); applied.Name = "PetLibraryApplied_" + id;
            applied.Foreground = DesignSystem.Success;
            var top = new Grid { ColumnDefinitions = new("*,Auto") };
            top.Children.Add(applied); Grid.SetColumn(favorite, 1); top.Children.Add(favorite);
            var title = Ui.Text(package.DisplayName); title.FontWeight = FontWeight.SemiBold;
            title.TextTrimming = TextTrimming.CharacterEllipsis; ToolTip.SetTip(title, package.DisplayName);
            var applyButton = Action("PetLibraryApply_" + id, L.T("적용"), () => apply(package));
            AutomationProperties.SetName(applyButton, L.F("{0} 적용", package.DisplayName));
            var editButton = Action("PetLibraryEdit_" + id, L.T("편집"), () => edit(package));
            AutomationProperties.SetName(editButton, L.F("{0} 편집", package.DisplayName));
            var buttons = new Grid { ColumnDefinitions = new("*,8,*") };
            applyButton.HorizontalAlignment = editButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            buttons.Children.Add(applyButton); Grid.SetColumn(editButton, 2); buttons.Children.Add(editButton);
            editButton.IsVisible = canEdit(package);
            if (!editButton.IsVisible) Grid.SetColumnSpan(applyButton, 3);
            var content = Ui.Column(top, image, title, Ui.Caption(package.IsGlb ? L.T("3D") : L.T("2D")), buttons);
            content.Spacing = 8;
            var surface = Ui.Card(content, 16); surface.Name = "PetLibraryCard_" + id; surface.Width = 224;
            surface.BorderThickness = new Thickness(1);
            var card = new Card(package, surface, image, bitmap, applyButton, editButton, favorite, applied);
            visibleCards.Add(card); cards.Children.Add(surface); UpdateCard(card);
        }
        empty.Text = packages.Length == 0 ? L.T("아직 저장한 펫이 없어요.") : L.T("검색 결과가 없어요.");
        empty.IsVisible = visibleCards.Count == 0;
        UpdateControls();
    }
    private void UpdateCard(Card card)
    {
        var selected = string.Equals(card.Package.Manifest.Id, selectedId(), StringComparison.Ordinal);
        var favorite = isFavorite(card.Package.Manifest.Id);
        card.Applied.IsVisible = selected;
        card.Surface.BorderBrush = selected ? DesignSystem.Accent : DesignSystem.OutlineSubtle;
        card.Apply.IsEnabled = !IsBusy && !selected;
        card.Apply.Classes.Set("primary", !selected);
        card.Favorite.Classes.Set("favorite", favorite);
        var label = favorite ? L.F("{0} 즐겨찾기 해제", card.Package.DisplayName) : L.F("{0} 즐겨찾기 추가", card.Package.DisplayName);
        AutomationProperties.SetName(card.Favorite, label); ToolTip.SetTip(card.Favorite, label);
        card.Favorite.IsEnabled = !IsBusy && setFavorite is not null;
        card.Edit.IsEnabled = !IsBusy;
    }
    private void UpdateFilters()
    {
        foreach (var (value, button) in filters)
        {
            button.Classes.Set("primary", value == filter);
            AutomationProperties.SetName(button, value == filter ? L.F("{0}, 선택됨", button.Content) : button.Content?.ToString());
        }
    }
    private void UpdateControls()
    {
        create.IsEnabled = import.IsEnabled = !IsBusy;
        foreach (var card in visibleCards) UpdateCard(card);
    }
    private void ClearCards()
    {
        cards.Children.Clear();
        foreach (var card in visibleCards) { card.Image.Source = null; card.Bitmap.Dispose(); }
        visibleCards.Clear();
    }
    // An outline over a separate fill, so hover and the saved state can tint the inside differently.
    private static Panel FavoriteStar()
    {
        var star = StreamGeometry.Parse("M12 3 L14.35 9.36 L21.13 9.63 L15.8 13.84 L17.64 20.37 L12 16.6 " +
            "L6.36 20.37 L8.2 13.84 L2.87 9.63 L9.65 9.36 Z");
        var fill = new Avalonia.Controls.Shapes.Path { Data = star, Classes = { "favorite-fill" } };
        var outline = new Avalonia.Controls.Shapes.Path { Data = star, StrokeThickness = 1.6,
            StrokeJoin = PenLineJoin.Round, Classes = { "favorite-outline" } };
        return new Panel { Width = 24, Height = 24, IsHitTestVisible = false, Children = { fill, outline } };
    }
    private static PixelImage Thumbnail(PixelImage source)
    {
        const int limit = 216;
        var scale = Math.Min(1, limit / (double)Math.Max(source.Width, source.Height));
        if (scale == 1) return source;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var pixels = new uint[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = source.Pixels[Math.Min(source.Height - 1, y * source.Height / height) * source.Width
                    + Math.Min(source.Width - 1, x * source.Width / width)];
        return new(width, height, pixels);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ClearCards(); packages = [];
    }
}
