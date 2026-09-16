using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class CustomPetDraftTests
{
    internal static string Fixture(string name = "pet-motion.gif") => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static ImportedPetClip Clip() => ImportedPetClip.FromGif("my pet.gif", File.ReadAllBytes(Fixture()));
    [Fact]
    public void FiveActionsExportReopenInstallAndPreserveGifTimingAndPixels()
    {
        using var temp = new TempDirectory(); var draft = new CustomPetDraft(); var clip = Clip();
        foreach (var action in CustomPetDraft.Actions) draft.SetClip(action, clip);
        var output = Path.Combine(temp.Path, "보리.unfoldpet"); draft.Export("나의 보리", output);
        using var pack = CharacterPack.Open(output);
        Assert.Equal("나의 보리", pack.Character.Manifest.Name); Assert.Equal(5, pack.Character.Manifest.Animations.Count);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); Assert.Empty(library.List());
        var installed = library.Install(pack, null);
        foreach (var key in CustomPetDraft.Actions)
        {
            Assert.Equal(key == "idle", installed.Manifest.Animations[key].Loop);
            Assert.Equal(File.ReadAllBytes(Fixture()), File.ReadAllBytes(Path.Combine(installed.DirectoryPath, key + ".gif")));
            var source = clip.LoadFrames(); var actual = installed.LoadAnimation(key);
            Assert.Equal(source.Count, actual.Count);
            for (var i = 0; i < actual.Count; i++)
            { Assert.Equal(source[i].Duration, actual[i].Duration); Assert.Equal(source[i].Image.Pixels, actual[i].Image.Pixels); }
        }
        Assert.Equal("Reinstall", library.InspectInstall(pack).Action);
    }
    [Fact]
    public void RequiredIdleNameAndFailedExportPreserveExistingDestination()
    {
        using var temp = new TempDirectory(); var output = Path.Combine(temp.Path, "pet.unfoldpet"); File.WriteAllText(output, "keep");
        var draft = new CustomPetDraft(); draft.SetClip("click", Clip());
        Assert.Throws<InvalidDataException>(() => draft.Export("보리", output));
        draft.SetClip("idle", Clip());
        Assert.Throws<ArgumentException>(() => draft.Export(" ", output));
        Assert.Equal("keep", File.ReadAllText(output));
        draft.RemoveClip("click"); draft.Export("보리", output);
        using var pack = CharacterPack.Open(output); Assert.Single(pack.Character.Manifest.Animations);
    }
    [Fact]
    public void ImportIsASnapshotAndInvalidReplacementDoesNotAlterDraft()
    {
        var bytes = File.ReadAllBytes(Fixture()); var clip = ImportedPetClip.FromGif("source.gif", bytes);
        Array.Fill<byte>(bytes, 0); var draft = new CustomPetDraft(); draft.SetClip("idle", clip);
        Assert.Throws<InvalidDataException>(() => draft.SetClip("idle", ImportedPetClip.FromGif("bad.gif", bytes)));
        Assert.Throws<ArgumentException>(() => draft.SetClip("unsupported", clip));
        Assert.Same(clip, draft.Clips["idle"]); Assert.True(clip.LoadFrames().Count > 1);
    }
}

public class PetMediaImporterTests
{
    public static bool HasVideoTool => PetMediaImporter.FindFFmpeg() is not null;
    [Fact]
    public async Task GifImportKeepsOriginalFramesAndUnsupportedFilesAreRejected()
    {
        var clip = await PetMediaImporter.Import(CustomPetDraftTests.Fixture(), TestContext.Current.CancellationToken);
        Assert.Equal(4, clip.FrameCount); Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
        await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import("test.txt", TestContext.Current.CancellationToken));
    }
    [Fact(Skip = "Prepare media tools to run native MP4 conversion.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(PetMediaImporterTests))]
    public async Task ActualMp4ConversionCreatesInstallablePackWithChangingFrames()
    {
        using var temp = new TempDirectory(); var clip = await PetMediaImporter.Import(CustomPetDraftTests.Fixture("pet-motion.mp4"), TestContext.Current.CancellationToken);
        Assert.Equal(12, clip.FrameCount); Assert.InRange(clip.Width, 1, 192); Assert.InRange(clip.Height, 1, 192);
        var frames = clip.LoadFrames(); Assert.False(frames[0].Image.Pixels.SequenceEqual(frames[^1].Image.Pixels));
        var draft = new CustomPetDraft(); draft.SetClip("idle", clip); draft.SetClip("stretch", clip);
        var output = Path.Combine(temp.Path, "video-pet.unfoldpet"); draft.Export("영상 펫", output);
        using var pack = CharacterPack.Open(output); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        Assert.Equal(12, library.Install(pack, null).LoadAnimation("stretch").Count);
    }
    [Fact(Skip = "Prepare media tools to run native MP4 conversion.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(PetMediaImporterTests))]
    public async Task LongAndBrokenMp4AreRejectedInsteadOfSilentlyTruncated()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import(CustomPetDraftTests.Fixture("pet-too-long.mp4"), TestContext.Current.CancellationToken));
        Assert.Contains("10초", error.Message);
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "broken.mp4"); File.WriteAllText(path, "not a video");
        await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import(path, TestContext.Current.CancellationToken));
    }
}

[Collection("Timer settings")]
public class CustomPetWindowTests
{
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Press(Window window, string name) => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 400 && !predicate(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(predicate());
    }
    [AvaloniaFact]
    public async Task ImportedFileCanBeAssignedReplacedRemovedAndExportedOnlyWithIdle()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        var output = Path.Combine(temp.Path, "my-pet.unfoldpet");
        var file = CustomPetDraftTests.Fixture();
        var window = new CustomPetWindow(file, () => Task.FromResult<string?>(file), () => Task.FromResult<string?>(output));
        try
        {
            window.Show(); Find<TextBox>(window, "CustomPetName").Text = "테스트 펫";
            Find<ComboBox>(window, "CustomPetAction").SelectedItem = "click";
            Press(window, "AssignPetMedia"); await Until(() => !Find<Button>(window, "AssignPetMedia").IsEnabled && Find<Button>(window, "CustomPetRemove_click").IsEnabled);
            Assert.False(Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Press(window, "CustomPetFile_idle"); await Until(() => Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Press(window, "CustomPetRemove_click"); Assert.False(Find<Button>(window, "CustomPetPreview_click").IsEnabled);
            file = Path.Combine(temp.Path, "broken.gif"); File.WriteAllText(file, "broken");
            Press(window, "CustomPetFile_idle"); await Until(() => Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Assert.Equal("테스트 펫", Find<TextBox>(window, "CustomPetName").Text);
            Press(window, "CreateCustomPetPack"); await Until(() => window.CreatedPackPath is not null);
            using var pack = CharacterPack.Open(output); Assert.Equal("테스트 펫", pack.Character.Manifest.Name);
            Assert.Single(pack.Character.Manifest.Animations); Assert.Equal(4, pack.Character.LoadAnimation("idle").Count);
        }
        finally { window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
    [AvaloniaFact]
    public async Task PetTabsKeepPreviewAndDraftThenOpenCreatedPackWithoutAnotherWindow()
    {
        using var temp = new TempDirectory(); var packFile = CharacterPackTests.CreatePack(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        string? output = null;
        var selected = new List<CharacterPackage>();
        var parent = new PetPackWindow(library, pet => { selected.Add(pet); return Task.CompletedTask; },
            () => Task.FromResult<string?>(packFile), () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()),
            () => Task.FromResult<string?>(output));
        try
        {
            parent.Show(); Press(parent, "OpenPetPack"); await Until(() => Find<Button>(parent, "PausePackPreview").IsEnabled);
            var tabs = Find<TabControl>(parent, "PetManagementTabs");
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs(); parent.UpdateLayout();
            Press(parent, "ImportPetMedia"); Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, Find<ComboBox>(parent, "CustomPetAction").ItemCount);
            Assert.Empty(parent.OwnedWindows);
            tabs.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, Find<ComboBox>(parent, "PackClip").ItemCount); Assert.True(Find<Button>(parent, "InstallPetPack").IsEnabled);
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Find<TextBox>(parent, "CustomPetName").Text = "탭에서 만든 펫";
            Press(parent, "AssignPetMedia"); await Until(() => Find<Button>(parent, "CreateCustomPetPack").IsEnabled);
            tabs.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Assert.True(Find<Button>(parent, "CreateCustomPetPack").IsEnabled);
            Press(parent, "CreateCustomPetPack"); Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, tabs.SelectedIndex); Assert.Empty(library.List());
            Assert.True(Find<Button>(parent, "CreateCustomPetPack").IsEnabled);
            output = Path.Combine(temp.Path, "tabs.unfoldpet");
            Press(parent, "CreateCustomPetPack"); await Until(() => tabs.SelectedIndex == 0);
            await Until(() => Find<Button>(parent, "InstallPetPack").IsEnabled && tabs.IsEnabled);
            Assert.Empty(library.List()); Assert.Empty(parent.OwnedWindows);
            Press(parent, "InstallPetPack"); await Until(() => selected.Count == 1 && tabs.IsEnabled);
            Assert.Equal("탭에서 만든 펫", selected[0].Manifest.Name);
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Assert.Equal("탭에서 만든 펫", Find<TextBox>(parent, "CustomPetName").Text);
            Assert.True(Find<Button>(parent, "CreateCustomPetPack").IsEnabled);
            Find<TextBox>(parent, "CustomPetName").Text = "다음 펫";
            output = Path.Combine(temp.Path, "second.unfoldpet");
            Press(parent, "CreateCustomPetPack"); await Until(() => tabs.SelectedIndex == 0);
            await Until(() => tabs.IsEnabled && Find<Button>(parent, "InstallPetPack").IsEnabled);
            Press(parent, "InstallPetPack"); await Until(() => selected.Count == 2 && tabs.IsEnabled);
            Assert.NotEqual(selected[0].Manifest.Id, selected[1].Manifest.Id);
            Assert.Equal(2, library.List().Count);
        }
        finally { parent.Close(); }
    }
}
