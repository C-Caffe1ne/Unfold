using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

internal static class PetLibraryTestNavigation
{
    internal static void OpenBuilder(Control root)
    {
        Dispatcher.UIThread.RunJobs();
        root.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (root.GetVisualDescendants().OfType<PetBuilderView>().Any()) return;
        var create = Assert.Single(root.GetVisualDescendants().OfType<Button>(), button => button.Name == "PetLibraryCreate");
        create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        root.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.Single(root.GetVisualDescendants().OfType<PetBuilderView>());
    }
}

[Collection("Timer settings")]
public class PetLibraryTests
{
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Press(Control root, string name) => Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(condition());
    }
    private static string[] Cards(Control root) => root.GetVisualDescendants().OfType<Border>()
        .Where(control => control.Name?.StartsWith("PetLibraryCard_", StringComparison.Ordinal) == true)
        .Select(control => control.Name!["PetLibraryCard_".Length..]).ToArray();

    [AvaloniaFact]
    public void EmptyLibraryStartsWithItsEmptyStateAndCreateFocusesTheEditor()
    {
        using var temp = new TempDirectory();
        var window = new Window { Width = 860, Height = 800 };
        using var page = new PetManagementView(window, new(Path.Combine(temp.Path, "library")), _ => Task.CompletedTask);
        try
        {
            window.Content = page; window.Show(); Layout(window);
            Assert.Equal(L.T("아직 저장한 펫이 없어요."), Find<TextBlock>(window, "PetLibraryEmpty").Text);
            Assert.True(Find<TextBlock>(window, "PetLibraryEmpty").IsVisible);
            PetLibraryTestNavigation.OpenBuilder(window); Layout(window);
            Assert.True(Find<TextBox>(window, "CustomPetName").IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LibrarySearchAndFiltersUseStillImagesWithoutLoadingModels()
    {
        using var scope = new Scope();
        Assert.Equal(3, Cards(scope.Window).Length);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants(), control => control is PetBuilderView);
        var modelField = typeof(CharacterPackage).GetField("model", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Null(modelField.GetValue(scope.Model));
        Assert.All(scope.Window.GetVisualDescendants().OfType<Image>(), image => Assert.NotNull(image.Source));
        Find<TextBox>(scope.Window, "PetLibrarySearch").Text = "library bori"; Layout(scope.Window);
        Assert.Equal([scope.Media.Manifest.Id], Cards(scope.Window));
        Find<TextBox>(scope.Window, "PetLibrarySearch").Text = ""; Layout(scope.Window);
        Press(scope.Window, "PetLibraryFilterModel"); Assert.Equal([scope.Model.Manifest.Id], Cards(scope.Window));
        Press(scope.Window, "PetLibraryFilterSprite"); Assert.Equal(2, Cards(scope.Window).Length);
        scope.Favorites.Add(scope.Media.Manifest.Id);
        Press(scope.Window, "PetLibraryFilterFavorites"); Assert.Equal([scope.Media.Manifest.Id], Cards(scope.Window));
        Find<TextBox>(scope.Window, "PetLibrarySearch").Text = "missing"; Layout(scope.Window);
        Assert.Empty(Cards(scope.Window)); Assert.True(Find<TextBlock>(scope.Window, "PetLibraryEmpty").IsVisible);
        Assert.Null(modelField.GetValue(scope.Model));
    }

    [AvaloniaFact]
    public void AppliedMarkerDistinguishesIdsThatDifferOnlyByCase()
    {
        using var temp = new TempDirectory();
        using var upper = CharacterPack.Open(CharacterPackTests.CreatePack(Path.Combine(temp.Path, "upper"), id: "Cat"));
        using var lower = CharacterPack.Open(CharacterPackTests.CreatePack(Path.Combine(temp.Path, "lower"), id: "cat"));
        CharacterPackage[] packages = [upper.Character, lower.Character];
        var selectedId = "Cat";
        var window = new Window { Width = 860, Height = 800 };
        using var page = new PetManagementView(window, new(Path.Combine(temp.Path, "library")), _ => Task.CompletedTask,
            getPets: () => packages, selectedId: () => selectedId);
        try
        {
            window.Content = page; window.Show(); Layout(window);
            foreach (var id in new[] { "Cat", "cat" })
            {
                selectedId = id; page.RefreshSelection();
                foreach (var package in packages)
                {
                    var selected = package.Manifest.Id == id;
                    Assert.Equal(selected, Find<TextBlock>(window, "PetLibraryApplied_" + package.Manifest.Id).IsVisible);
                    Assert.Equal(!selected, Find<Button>(window, "PetLibraryApply_" + package.Manifest.Id).IsEnabled);
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ApplyingAndEditingAreSeparateAndBuiltInsCannotBeEdited()
    {
        using var scope = new Scope();
        Press(scope.Window, "PetLibraryApply_" + scope.Media.Manifest.Id);
        await Until(() => !scope.Page.IsBusy);
        Assert.Equal(scope.Media.Manifest.Id, scope.SelectedId); Assert.Equal(1, scope.Applications);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants(), control => control is PetBuilderView);
        Assert.True(Find<TextBlock>(scope.Window, "PetLibraryApplied_" + scope.Media.Manifest.Id).IsVisible);
        Assert.False(Find<Button>(scope.Window, "PetLibraryApply_" + scope.Media.Manifest.Id).IsEnabled);
        Assert.False(Find<Button>(scope.Window, "PetLibraryEdit_" + scope.BuiltIn.Manifest.Id).IsVisible);
        Press(scope.Window, "PetLibraryEdit_" + scope.Media.Manifest.Id);
        await Until(() => !scope.Page.IsBusy);
        Layout(scope.Window);
        Assert.Equal(1, scope.Applications);
        Assert.Equal(scope.Media.Manifest.Name, Find<TextBox>(scope.Window, "CustomPetName").Text);
        Assert.True(Find<TextBox>(scope.Window, "CustomPetName").IsFocused);
        Assert.False(scope.Page.HasUnsavedDraft);
    }

    [AvaloniaFact]
    public async Task FailedFavoriteWriteKeepsTheSavedStateAndCanBeRetried()
    {
        using var scope = new Scope(); scope.FailFavorite = true;
        Press(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id);
        await Until(() => !scope.Page.IsBusy);
        Assert.Empty(scope.Favorites); Assert.True(Find<TextBlock>(scope.Window, "PetLibraryError").IsVisible);
        Assert.Equal("☆", Find<Button>(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id).Content);
        scope.FailFavorite = false;
        Press(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id); await Until(() => !scope.Page.IsBusy);
        Assert.Contains(scope.Media.Manifest.Id, scope.Favorites);
        Press(scope.Window, "PetLibraryFilterFavorites");
        var thumbnail = Find<Image>(scope.Window, "PetLibraryThumbnail_" + scope.Media.Manifest.Id);
        Press(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id); await Until(() => !scope.Page.IsBusy);
        Assert.Empty(Cards(scope.Window)); Assert.Null(thumbnail.Source);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task FavoriteKeyboardActivationKeepsFocusAndTabOrderEvenWhenSavingFails(bool fail)
    {
        using var scope = new Scope(); scope.FailFavorite = fail;
        var favorite = Find<Button>(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id);
        Assert.True(favorite.Focus(NavigationMethod.Tab));
        FavoriteKey(scope.Window, Key.Space, PhysicalKey.Space);
        await Until(() => !scope.Page.IsBusy);
        Assert.Equal(!fail, scope.Favorites.Contains(scope.Media.Manifest.Id));
        Assert.True(favorite.IsFocused);
        FavoriteKey(scope.Window, Key.Tab, PhysicalKey.Tab);
        Assert.True(Find<Button>(scope.Window, "PetLibraryApply_" + scope.Media.Manifest.Id).IsFocused);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task RemovingFavoriteFocusesTheNextCardOrThePreviousLastCard(int index)
    {
        using var scope = new Scope();
        scope.Favorites.UnionWith(Cards(scope.Window));
        Press(scope.Window, "PetLibraryFilterFavorites");
        var before = Cards(scope.Window);
        var favorite = Find<Button>(scope.Window, "PetLibraryFavorite_" + before[index]);
        Assert.True(favorite.Focus(NavigationMethod.Tab));
        FavoriteKey(scope.Window, Key.Space, PhysicalKey.Space);
        await Until(() => !scope.Page.IsBusy);
        var remaining = before.Where(id => id != before[index]).ToArray();
        Assert.Equal(remaining, Cards(scope.Window));
        var next = remaining[Math.Min(index, remaining.Length - 1)];
        Assert.True(Find<Button>(scope.Window, "PetLibraryFavorite_" + next).IsFocused);
        FavoriteKey(scope.Window, Key.Tab, PhysicalKey.Tab);
        Assert.True(Find<Button>(scope.Window, "PetLibraryApply_" + next).IsFocused);
    }

    [AvaloniaFact]
    public async Task RemovingTheOnlyFavoriteFocusesTheFavoritesFilter()
    {
        using var scope = new Scope();
        scope.Favorites.Add(scope.Media.Manifest.Id);
        Press(scope.Window, "PetLibraryFilterFavorites");
        Assert.True(Find<Button>(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id).Focus(NavigationMethod.Tab));
        FavoriteKey(scope.Window, Key.Space, PhysicalKey.Space);
        await Until(() => !scope.Page.IsBusy);
        Assert.Empty(Cards(scope.Window));
        Assert.True(Find<Button>(scope.Window, "PetLibraryFilterFavorites").IsFocused);
        FavoriteKey(scope.Window, Key.Tab, PhysicalKey.Tab);
        Assert.True(Find<Button>(scope.Window, "PetLibraryFilterSprite").IsFocused);
    }

    [AvaloniaFact]
    public async Task FavoriteCompletionDoesNotTakeFocusBackFromAnotherControl()
    {
        using var scope = new Scope();
        var completion = new TaskCompletionSource();
        scope.FavoriteWriteDelay = completion.Task;
        Assert.True(Find<Button>(scope.Window, "PetLibraryFavorite_" + scope.Media.Manifest.Id).Focus(NavigationMethod.Tab));
        FavoriteKey(scope.Window, Key.Space, PhysicalKey.Space);
        Assert.True(scope.Page.IsBusy);
        var search = Find<TextBox>(scope.Window, "PetLibrarySearch");
        Assert.True(search.Focus(NavigationMethod.Tab));
        completion.SetResult();
        await Until(() => !scope.Page.IsBusy);
        Assert.True(search.IsFocused);
        Assert.Contains(scope.Media.Manifest.Id, scope.Favorites);
    }

    private static void FavoriteKey(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, key == Key.Space ? " " : null);
        window.KeyRelease(key, RawInputModifiers.None, physical, key == Key.Space ? " " : null);
        Layout(window);
    }

    [AvaloniaFact]
    public void SelectionUpdatesDoNotReloadTheCatalogOrRebuildThumbnails()
    {
        using var scope = new Scope();
        var image = Find<Image>(scope.Window, "PetLibraryThumbnail_" + scope.Media.Manifest.Id);
        var bitmap = image.Source; var reads = scope.CatalogReads;
        for (var i = 0; i < 60; i++)
        {
            scope.SelectedId = i % 2 == 0 ? scope.Media.Manifest.Id : scope.Model.Manifest.Id;
            scope.Page.RefreshSelection();
            Assert.Same(image, Find<Image>(scope.Window, "PetLibraryThumbnail_" + scope.Media.Manifest.Id));
            Assert.Same(bitmap, image.Source);
        }
        Assert.Equal(reads, scope.CatalogReads);
        var library = scope.Window.GetVisualDescendants().OfType<PetLibraryView>().Single();
        scope.Page.Dispose();
        Assert.Equal(0, library.ThumbnailCount); Assert.Null(image.Source);
    }

    [AvaloniaFact]
    public async Task ReturningToLibraryPreservesDraftAndStartingNewRequiresAnExplicitDiscard()
    {
        using var scope = new Scope();
        PetLibraryTestNavigation.OpenBuilder(scope.Window);
        var name = Find<TextBox>(scope.Window, "CustomPetName"); name.Text = "작성 중";
        Press(scope.Window, "PetLibraryBack"); Layout(scope.Window);
        Assert.True(scope.Page.HasUnsavedDraft); Assert.Empty(scope.Window.OwnedWindows);
        Assert.True(Find<TextBox>(scope.Window, "PetLibrarySearch").IsFocused);
        Assert.Equal(L.T("계속 편집"), Find<Button>(scope.Window, "PetLibraryCreate").Content);
        PetLibraryTestNavigation.OpenBuilder(scope.Window);
        Assert.Same(name, Find<TextBox>(scope.Window, "CustomPetName"));
        Press(scope.Window, "PetLibraryStartNew"); await Until(() => scope.Window.OwnedWindows.Any());
        Choose(scope.Window, "계속 편집"); await Until(() => !scope.Page.IsBusy);
        Assert.Equal("작성 중", name.Text); Assert.True(scope.Page.HasUnsavedDraft);
        Press(scope.Window, "PetLibraryStartNew"); await Until(() => scope.Window.OwnedWindows.Any());
        Choose(scope.Window, "변경 버리기"); await Until(() => !scope.Page.IsBusy);
        Assert.NotSame(name, Find<TextBox>(scope.Window, "CustomPetName"));
        Assert.True(string.IsNullOrEmpty(Find<TextBox>(scope.Window, "CustomPetName").Text)); Assert.False(scope.Page.HasUnsavedDraft);
    }

    [AvaloniaFact]
    public async Task CloseGuardReturnsFromLibraryToTheRetainedEditor()
    {
        using var scope = new Scope();
        PetLibraryTestNavigation.OpenBuilder(scope.Window);
        Find<TextBox>(scope.Window, "CustomPetName").Text = "작성 중";
        Press(scope.Window, "PetLibraryBack");
        var closing = scope.Page.CanCloseDraft();
        await Until(() => scope.Window.OwnedWindows.Any());
        Assert.Equal("작성 중", Find<TextBox>(scope.Window, "CustomPetName").Text);
        Choose(scope.Window, "취소"); Assert.False(await closing); Assert.True(scope.Page.HasUnsavedDraft);
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Plum, 860)] [InlineData(AppTheme.Plum, 1120)]
    [InlineData(AppTheme.OatLatte, 860)] [InlineData(AppTheme.OatLatte, 1120)]
    [InlineData(AppTheme.Sage, 860)] [InlineData(AppTheme.Sage, 1120)]
    [InlineData(AppTheme.MidnightBlue, 860)] [InlineData(AppTheme.MidnightBlue, 1120)]
    public async Task LibraryFitsTheSettingsShellAndCapturesEveryTheme(AppTheme theme, double width)
    {
        using var data = new TempDirectory();
        var previousData = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        var previousTheme = DesignSystem.CurrentTheme;
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", data.Path);
        new AppSettings { Theme = theme, ShowPet = false }.Save(Path.Combine(data.Path, "settings.json"));
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        SettingsWindow? window = null;
        try
        {
            PetEditingCoreTests.InstallMedia(runtime.Library, data.Path);
            var modelPath = Path.Combine(data.Path, "model.glb"); File.WriteAllBytes(modelPath, GlbTests.Fixture(morph: true));
            new GlbPetDraft(modelPath) { Name = "3D 친구" }.Save(runtime.Library);
            await runtime.Start(true, true); runtime.Stop();
            window = new SettingsWindow(runtime) { Width = width, Height = 800 };
            window.Show(); Press(window, "SettingsNavPacks"); Layout(window);
            Assert.True(Find<TextBox>(window, "PetLibrarySearch").IsFocused);
            var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            foreach (var name in new[] { "PetLibrarySearch", "PetLibraryCreate", "PetLibraryImport" })
            {
                var control = Find<Control>(window, name); var origin = control.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.X, 0, window.ClientSize.Width - control.Bounds.Width + 1);
                Assert.InRange(origin.Y, 0, window.ClientSize.Height - control.Bounds.Height + 1);
            }
            Assert.NotEmpty(Cards(window));
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/verification/pet-library"));
            Directory.CreateDirectory(directory);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"{theme}-{width:0}x800.png"), PngBitmapEncoderOptions.Default);
        }
        finally
        {
            window?.Hide(); window?.Dispose(); runtime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousData);
            DesignSystem.ApplyTheme(previousTheme);
        }
    }

    private static void Choose(Window owner, string text) => owner.OwnedWindows.Single().GetVisualDescendants()
        .OfType<Button>().Single(button => Equals(button.Content, L.T(text))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previousData = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public CharacterLibrary Library { get; }
        public Window Window { get; } = new() { Width = 860, Height = 800 };
        public PetManagementView Page { get; }
        public CharacterPackage Media { get; }
        public CharacterPackage Model { get; }
        public CharacterPackage BuiltIn { get; }
        public HashSet<string> Favorites { get; } = [];
        public string? SelectedId { get; set; }
        public bool FailFavorite { get; set; }
        public Task FavoriteWriteDelay { get; set; } = Task.CompletedTask;
        public int Applications, CatalogReads;
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Library = new(Path.Combine(temp.Path, "pets"));
            var media = PetEditingCoreTests.InstallMedia(Library, temp.Path);
            new CustomPetDraft(media).Save(Library, "Library Bori");
            var path = Path.Combine(temp.Path, "model.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
            new GlbPetDraft(path) { Name = "모델 친구" }.Save(Library);
            var saved = Library.List(loadModels: false); Media = saved.Single(p => !p.IsGlb); Model = saved.Single(p => p.IsGlb);
            BuiltIn = CharacterLibrary.LoadPackage(Directory.EnumerateDirectories(AppPaths.BuiltInRoot).First(), true);
            CharacterPackage[] pets = [BuiltIn, Media, Model];
            Page = new(Window, Library, package => { Applications++; SelectedId = package.Manifest.Id; return Task.CompletedTask; },
                getPets: () => { CatalogReads++; return pets; }, selectedId: () => SelectedId,
                isFavorite: Favorites.Contains, setFavorite: async (id, value) =>
                {
                    await FavoriteWriteDelay;
                    if (FailFavorite) throw new IOException("favorite write failed");
                    if (value) Favorites.Add(id); else Favorites.Remove(id);
                });
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout(Window);
        }
        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Page.Dispose(); Window.Close();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousData);
            temp.Dispose();
        }
    }
}
