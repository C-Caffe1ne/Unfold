using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetSaveResetTests
{
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Press(Control root, string name) =>
        Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 400 && !ready(); i++)
        { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }

    [AvaloniaFact]
    public async Task BuilderResetsOnlyAfterSuccessfulSaveAndLeavesTheSavedArchiveUsable()
    {
        using var data = new DataScope();
        var owner = new Window { Width = 860, Height = 680 };
        string? output = null, created = null;
        using var view = new CustomPetView(owner, path => { created = path; return Task.CompletedTask; },
            initialPath: CustomPetDraftTests.Fixture(),
            chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()),
            chooseOutput: () => Task.FromResult(output), showHeader: false);
        owner.Content = view; owner.Show();
        try
        {
            var name = Find<TextBox>(owner, "CustomPetName"); name.Text = "새 친구";
            var action = Find<ComboBox>(owner, "CustomPetAction"); action.SelectedItem = "stretch";
            Press(owner, "CustomPetFile_idle"); await Until(() => !view.IsBusy);
            var originalMetadata = Find<TextBlock>(owner, "CustomPetLabel_idle").Text;
            var originalPending = Find<TextBlock>(owner, "CustomPetPending").Text;

            // Cancelling the destination picker preserves the entire editable draft.
            Press(owner, "CreateCustomPetPack"); await Until(() => !view.IsBusy);
            AssertDraftPreserved(); Assert.Null(created);

            output = data.Root; // A directory cannot be replaced by the archive.
            Press(owner, "CreateCustomPetPack"); await Until(() => !view.IsBusy);
            AssertDraftPreserved(); Assert.Null(created);
            Assert.Equal(DesignSystem.Error, Find<TextBlock>(owner, "CustomPetStatus").Foreground);

            output = Path.Combine(data.Root, "new-friend.unfoldpet");
            Press(owner, "CreateCustomPetPack"); await Until(() => !view.IsBusy);
            Assert.Equal(output, created); Assert.False(view.HasUnsavedChanges);
            Assert.Equal("", name.Text); Assert.Equal("idle", action.SelectedItem);
            Assert.Equal("", Find<TextBlock>(owner, "CustomPetPending").Text);
            Assert.Equal("", Find<TextBlock>(owner, "CustomPetPreviewAction").Text);
            Assert.False(Find<Button>(owner, "AssignPetMedia").IsEnabled);
            Assert.False(Find<Button>(owner, "CreateCustomPetPack").IsEnabled);
            foreach (var key in CustomPetDraft.Actions)
            {
                Assert.Equal("파일 없음", Find<TextBlock>(owner, "CustomPetLabel_" + key).Text);
                Assert.False(Find<Button>(owner, "CustomPetPreview_" + key).IsEnabled);
                Assert.False(Find<Button>(owner, "CustomPetRemove_" + key).IsEnabled);
                Assert.Equal(DesignSystem.Outline, Find<Border>(owner, "CustomPetSlot_" + key).BorderBrush);
            }
            Assert.False(Find<AnimationView>(owner, "CustomPetPreview").OpaqueAt(new Point(110, 110)));
            Assert.Equal("펫 팩을 저장했어요.", Find<TextBlock>(owner, "CustomPetStatus").Text);
            Assert.True(await view.CanCloseDraft()); Assert.Empty(owner.OwnedWindows);
            using var pack = CharacterPack.Open(output);
            Assert.Equal("새 친구", pack.Character.Manifest.Name);
            Assert.Equal(4, pack.Character.LoadAnimation("idle").Count);

            void AssertDraftPreserved()
            {
                Assert.True(view.HasUnsavedChanges); Assert.Equal("새 친구", name.Text);
                Assert.Equal("stretch", action.SelectedItem);
                Assert.Equal(originalMetadata, Find<TextBlock>(owner, "CustomPetLabel_idle").Text);
                Assert.Equal(originalPending, Find<TextBlock>(owner, "CustomPetPending").Text);
                Assert.Contains("쉬는 모습", Find<TextBlock>(owner, "CustomPetPreviewAction").Text);
                Assert.True(Find<Button>(owner, "AssignPetMedia").IsEnabled);
                Assert.True(Find<Button>(owner, "CreateCustomPetPack").IsEnabled);
            }
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task PackOpenCancellationAndSaveConflictPreserveTheCurrentInputAndPreview()
    {
        using var data = new DataScope();
        var library = new CharacterLibrary(Path.Combine(data.Root, "library"));
        using var first = CharacterPack.Open(CharacterPackTests.CreatePack(data.Root));
        var existing = library.Install(first, null);
        string? file = CharacterPackTests.CreatePack(data.Root, "2.0.0");
        var selected = false;
        var owner = new Window { Width = 860, Height = 680 };
        using var view = new PetPackView(owner, library, _ => { selected = true; return Task.CompletedTask; },
            () => Task.FromResult<string?>(file), showHeader: false);
        owner.Content = view; owner.Show();
        try
        {
            Press(owner, "OpenPetPack"); await Until(() => !view.IsBusy);
            Assert.True(Find<Button>(owner, "InstallPetPack").IsEnabled);
            Press(owner, "PausePackPreview");
            Find<ComboBox>(owner, "PackClip").SelectedItem = "stretch";
            await Until(() => Find<Button>(owner, "PausePackPreview").IsEnabled);
            Find<ComboBox>(owner, "PackSize").SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs(); owner.UpdateLayout();
            var playback = Find<TextBlock>(owner, "PackPlaybackStatus").Text;

            file = null;
            Press(owner, "OpenPetPack"); await Until(() => !view.IsBusy);
            AssertPreviewPreserved(); Assert.True(Find<Button>(owner, "InstallPetPack").IsEnabled);

            // An external edit after opening invalidates the expected library revision.
            File.AppendAllText(Path.Combine(existing.DirectoryPath, "character.json"), " ");
            Press(owner, "InstallPetPack"); await Until(() => !view.IsBusy);
            AssertPreviewPreserved(); Assert.False(selected);
            Assert.Contains("저장하지 못했어요.", Find<TextBlock>(owner, "PackStatus").Text);
            Assert.Equal("1.0.0", library.InspectInstall(first).InstalledVersion);

            void AssertPreviewPreserved()
            {
                Assert.Equal("stretch", Find<ComboBox>(owner, "PackClip").SelectedItem);
                Assert.Equal(5, Find<ComboBox>(owner, "PackClip").ItemCount);
                Assert.Same(DesignSystem.Surface, Find<Border>(owner, "PackPreviewSurface").Background);
                Assert.Equal(2, Find<ComboBox>(owner, "PackSize").SelectedIndex);
                Assert.Equal(playback, Find<TextBlock>(owner, "PackPlaybackStatus").Text);
                Assert.True(Find<Button>(owner, "PausePackPreview").IsEnabled);
                Assert.True(Find<WrapPanel>(owner, "PackInfo").IsVisible);
                Assert.True(Find<AnimationView>(owner, "PackPreview").OpaqueAt(new Point(192, 192)));
            }
        }
        finally { owner.Close(); }
    }

    private sealed class DataScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temp.Path;
        public DataScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
