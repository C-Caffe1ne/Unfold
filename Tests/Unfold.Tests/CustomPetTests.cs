using Avalonia;
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
    private static async Task<Window> Dialog(Window owner)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (owner.OwnedWindows.FirstOrDefault() is { } dialog) return dialog;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Expected a replacement confirmation.");
    }
    private static Button Choice(Window dialog, string label) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label));
    private static async Task<Window> Answer(Window owner, string label)
    {
        var dialog = await Dialog(owner);
        Choice(dialog, label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs(); return dialog;
    }
    private static string Label(Window window, string key) => Find<TextBlock>(window, "CustomPetLabel_" + key).Text ?? "";
    private static void AssertClipMetadataOnly(string label)
    {
        Assert.Matches(@"^\d+ × \d+px\n\d+프레임 · \d+(?:\.\d+)?초$", label);
        Assert.DoesNotContain(".gif", label);
        Assert.DoesNotContain(".mp4", label);
    }
    // The imported file is consumed and the picker moved in the same synchronous step,
    // so this note is the signal that the whole assignment finished.
    private static Task Assigned(Window window) =>
        Until(() => (Find<TextBlock>(window, "CustomPetPending").Text ?? "").StartsWith("동작에 추가했어요"));
    private static string Copy(string directory, string name)
    {
        var path = Path.Combine(directory, name); File.Copy(CustomPetDraftTests.Fixture(), path, true); return path;
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
            Press(window, "CustomPetFile_idle"); await Answer(window, "바꾸기");
            await Until(() => Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Assert.Equal("테스트 펫", Find<TextBox>(window, "CustomPetName").Text);
            Press(window, "CreateCustomPetPack"); await Until(() => window.CreatedPackPath is not null);
            using var pack = CharacterPack.Open(output); Assert.Equal("테스트 펫", pack.Character.Manifest.Name);
            Assert.Single(pack.Character.Manifest.Animations); Assert.Equal(4, pack.Character.LoadAnimation("idle").Count);
        }
        finally { window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
    [AvaloniaFact]
    public async Task ReplacingAFilledActionAsksFirstAndCancellingKeepsTheExistingClip()
    {
        using var temp = new TempDirectory();
        var picked = Copy(temp.Path, "first.gif");
        var window = new CustomPetWindow(picked, () => Task.FromResult<string?>(picked));
        try
        {
            window.Show(); Find<TextBox>(window, "CustomPetName").Text = "테스트 펫";
            window.UpdateLayout();
            var idleSlot = Find<Border>(window, "CustomPetSlot_idle");
            var emptySlotSize = idleSlot.Bounds.Size;
            Press(window, "AssignPetMedia"); await Assigned(window);
            window.UpdateLayout();
            AssertClipMetadataOnly(Label(window, "idle"));
            Assert.Equal(emptySlotSize.Width, idleSlot.Bounds.Width, 1);
            Assert.Equal(emptySlotSize.Height, idleSlot.Bounds.Height, 1);
            picked = Copy(temp.Path, "second.gif");
            Press(window, "CustomPetFile_idle");
            var confirm = await Dialog(window);
            Assert.Contains("쉬는 모습", confirm.Title);
            Assert.Contains(confirm.GetVisualDescendants().OfType<TextBlock>(),
                text => (text.Text ?? "").Contains("first.gif") && (text.Text ?? "").Contains("second.gif"));
            Assert.True(Choice(confirm, "취소").IsFocused); Assert.False(Choice(confirm, "바꾸기").IsDefault);
            Choice(confirm, "취소").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !window.OwnedWindows.Any() && Find<Button>(window, "CustomPetFile_idle").IsEnabled);
            AssertClipMetadataOnly(Label(window, "idle"));
            Assert.True(Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Assert.True(Find<Button>(window, "CustomPetPreview_idle").IsEnabled);
            // Closing the confirmation with its titlebar is also a cancel.
            Press(window, "CustomPetFile_idle"); (await Dialog(window)).Close();
            await Until(() => !window.OwnedWindows.Any() && Find<Button>(window, "CustomPetFile_idle").IsEnabled);
            AssertClipMetadataOnly(Label(window, "idle"));
            Assert.True(Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Press(window, "CustomPetFile_stretch");
            await Until(() => Find<Button>(window, "CustomPetRemove_stretch").IsEnabled);
            AssertClipMetadataOnly(Label(window, "stretch"));
            AssertClipMetadataOnly(Label(window, "idle"));
        }
        finally { foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); window.Close(); }
    }
    [AvaloniaFact]
    public async Task AnApprovedReplacementThatFailsValidationKeepsTheExistingClip()
    {
        using var temp = new TempDirectory();
        var picked = Copy(temp.Path, "first.gif");
        var window = new CustomPetWindow(picked, () => Task.FromResult<string?>(picked));
        try
        {
            window.Show(); Find<TextBox>(window, "CustomPetName").Text = "테스트 펫";
            Press(window, "AssignPetMedia"); await Assigned(window);
            picked = Path.Combine(temp.Path, "broken.gif"); File.WriteAllText(picked, "broken");
            Press(window, "CustomPetFile_idle"); await Answer(window, "바꾸기");
            await Until(() => Find<TextBlock>(window, "CustomPetStatus").Foreground == DesignSystem.Error);
            AssertClipMetadataOnly(Label(window, "idle"));
            Assert.True(Find<Button>(window, "CreateCustomPetPack").IsEnabled);
            Assert.True(Find<Button>(window, "CustomPetPreview_idle").IsEnabled);
        }
        finally { foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); window.Close(); }
    }
    [AvaloniaFact]
    public async Task InitialImportMovesThePickerAndActionButtonsFillRemainingSlots()
    {
        using var temp = new TempDirectory();
        var picked = Copy(temp.Path, "first.gif");
        var window = new CustomPetWindow(picked, () => Task.FromResult<string?>(picked));
        try
        {
            window.Show();
            var actions = Find<ComboBox>(window, "CustomPetAction");
            Press(window, "AssignPetMedia"); await Assigned(window);
            Assert.Equal("attention", actions.SelectedItem);
            Press(window, "CustomPetFile_click");
            await Until(() => Find<Button>(window, "CustomPetRemove_click").IsEnabled);
            Assert.Equal("attention", actions.SelectedItem);
            foreach (var key in new[] { "attention", "stretch", "celebrate" })
            { Press(window, "CustomPetFile_" + key); await Until(() => Find<Button>(window, "CustomPetRemove_" + key).IsEnabled); }
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "ImportPetMedia");
        }
        finally { foreach (var owned in window.OwnedWindows.ToArray()) owned.Close(); window.Close(); }
    }
    [AvaloniaFact]
    public void ActionCardsWrapWithoutHorizontalScrollingAtEverySupportedWidth()
    {
        var file = CustomPetDraftTests.Fixture();
        var window = new CustomPetWindow(file, () => Task.FromResult<string?>(file));
        string[] controls = ["CustomPetName", "AssignPetMedia", "CustomPetAction",
            "CustomPetFile_idle", "CustomPetRemove_idle", "CustomPetPreview_idle"];
        try
        {
            window.Show();
            // The default 660x880 and the 520x620 minimum both scrolled the body, and the
            // scrollbar used to be painted over the name field and action controls.
            foreach (var size in new[] { new Size(660, 880), new Size(590, 750), new Size(520, 620) })
            {
                window.Width = size.Width; window.Height = size.Height;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Equal(size.Width, window.ClientSize.Width, 0);
                Assert.Equal(size.Height, window.ClientSize.Height, 0);
                PageBodyScrollGeometry.ClearsScrollBar(window, controls);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<ScrollViewer>(),
                    view => view.Name == "CustomPetActionSlotsScroll");
                var slots = Find<WrapPanel>(window, "CustomPetActionSlots");
                var pageScroll = PageBodyScrollGeometry.Scroll(window);
                Assert.True(pageScroll.Extent.Width <= pageScroll.Viewport.Width + 1);
                var slotRows = slots.Children.Select(card => Math.Round(card.Bounds.Y, 1)).Distinct().Count();
                Assert.True(slotRows > 1, $"Action cards did not wrap at {size.Width}×{size.Height}.");
                foreach (var card in slots.Children)
                {
                    Assert.True(card.Bounds.X >= -.5);
                    Assert.True(card.Bounds.Right <= slots.Bounds.Width + .5,
                        $"{card.Name} ends at {card.Bounds.Right}; panel width is {slots.Bounds.Width}.");
                }
                PageBodyScrollGeometry.Scroll(window).ScrollToEnd();
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                PageBodyScrollGeometry.ClearsScrollBar(window, controls);
                PageBodyScrollGeometry.Scroll(window).ScrollToHome();
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            }
            // A window tall enough to hold the whole form keeps its full width: the gutter
            // belongs to the scrollbar, not to every page.
            window.Width = 1400; window.Height = 1600;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            PageBodyScrollGeometry.KeepsFullWidthWithoutScrollBar(window);
            Assert.Single(Find<WrapPanel>(window, "CustomPetActionSlots").Children
                .Select(card => Math.Round(card.Bounds.Y, 1)).Distinct());
        }
        finally { window.Close(); }
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
            Assert.Equal(5, Find<ComboBox>(parent, "CustomPetAction").ItemCount);
            Assert.DoesNotContain(parent.GetVisualDescendants().OfType<Button>(), button => button.Name == "ImportPetMedia");
            Assert.DoesNotContain(parent.GetVisualDescendants().OfType<ScrollViewer>(),
                view => view.Name == "CustomPetActionSlotsScroll");
            Assert.True(Find<WrapPanel>(parent, "CustomPetActionSlots").Children
                .Select(card => Math.Round(card.Bounds.Y, 1)).Distinct().Count() > 1);
            Assert.Empty(parent.OwnedWindows);
            tabs.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, Find<ComboBox>(parent, "PackClip").ItemCount); Assert.True(Find<Button>(parent, "InstallPetPack").IsEnabled);
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Find<TextBox>(parent, "CustomPetName").Text = "탭에서 만든 펫";
            Press(parent, "CustomPetFile_idle"); await Until(() => Find<Button>(parent, "CreateCustomPetPack").IsEnabled);
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
