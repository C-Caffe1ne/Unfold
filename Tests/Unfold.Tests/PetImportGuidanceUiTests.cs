using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetImportGuidanceUiTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; } = new() { Width = 1120, Height = 800 };
        public CharacterLibrary Library { get; }
        public CustomPetView? Media { get; }
        public GlbPetView? Glb { get; }
        public string GlbPath { get; }
        public string Root => temp.Path;
        public Editor(bool glb)
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Library = new(Path.Combine(temp.Path, "library"));
            GlbPath = Path.Combine(temp.Path, "retry-pet.glb"); File.WriteAllBytes(GlbPath, GlbTests.Fixture(morph: true));
            Control page = glb
                ? Glb = new(Window, Library, _ => Task.CompletedTask, chooseFile: () => Task.FromResult<string?>(GlbPath))
                : Media = new(Window, _ => Task.CompletedTask, library: Library, installed: _ => Task.CompletedTask);
            Window.Content = Ui.PageFrame(Window, page); Window.Show(); Layout();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public async Task Open()
        {
            if (Glb is not null) await Glb.OpenPath(GlbPath);
            else await Media!.OpenPath(CustomPetDraftTests.Fixture());
            await Until(() => Glb?.IsPreviewLoading != true); Layout();
        }
        public void ToggleWithKeyboard(Expander guide)
        {
            var header = guide.GetVisualDescendants().OfType<ToggleButton>().Single(c => c.Name == "ExpanderHeader");
            header.Focus(NavigationMethod.Tab);
            Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " "); Layout();
            Assert.True(header.IsFocused);
        }
        public void Dispose()
        {
            Media?.Dispose(); Glb?.Dispose(); Window.Close();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private static byte[] RewriteGlb(Action<JsonObject> edit)
    {
        var bytes = GlbTests.Fixture(morph: true);
        var oldLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        var root = JsonNode.Parse(bytes.AsSpan(20, oldLength))!.AsObject(); edit(root);
        var json = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
        var length = (json.Length + 3) / 4 * 4; var binary = bytes.AsSpan(28 + oldLength).ToArray();
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write(28 + length + binary.Length);
        writer.Write(length); writer.Write(0x4E4F534Au); writer.Write(json);
        for (var i = json.Length; i < length; i++) writer.Write((byte)' ');
        writer.Write(binary.Length); writer.Write(0x004E4942u); writer.Write(binary); return stream.ToArray();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningAndClosingGuidePreservesDraftAndKeyboardFocus(bool glb)
    {
        using var editor = new Editor(glb); await editor.Open();
        var prefix = glb ? "GlbPet" : "CustomPet";
        var name = editor.Find<TextBox>(glb ? "GlbPetName" : "CustomPetName"); name.Text = "작성한 친구";
        var action = editor.Find<ComboBox>(glb ? "GlbPreviewAction" : "CustomPetSelectedAction");
        if (glb)
        {
            action.SelectedItem = "click"; editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
            await Until(() => !editor.Glb!.IsPreviewLoading);
            editor.Find<NumericUpDown>("GlbRotationX").Value = 35;
            editor.Find<NumericUpDown>("GlbRotationY").Value = 50;
            editor.Find<NumericUpDown>("GlbRotationZ").Value = -20;
        }
        else editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
        editor.Layout();
        var changed = glb ? editor.Glb!.HasUnsavedChanges : editor.Media!.HasUnsavedChanges;
        var save = editor.Find<Button>(glb ? "SaveGlbPet" : "CreateCustomPetPack"); var canSave = save.IsEnabled;
        var guide = editor.Find<Expander>(prefix + "PreparationGuide"); Assert.False(guide.IsExpanded);
        editor.ToggleWithKeyboard(guide); Assert.True(guide.IsExpanded);
        editor.ToggleWithKeyboard(guide); Assert.False(guide.IsExpanded);
        Assert.Equal("작성한 친구", name.Text); Assert.Equal(glb ? "click" : "idle", action.SelectedItem);
        Assert.Equal(changed, glb ? editor.Glb!.HasUnsavedChanges : editor.Media!.HasUnsavedChanges);
        Assert.Equal(canSave, save.IsEnabled);
        if (glb)
        {
            Assert.Equal("Idle", editor.Find<ComboBox>("GlbClip_click").SelectedItem);
            Assert.Equal(35, editor.Find<NumericUpDown>("GlbRotationX").Value);
            Assert.Equal(50, editor.Find<NumericUpDown>("GlbRotationY").Value);
            Assert.Equal(-20, editor.Find<NumericUpDown>("GlbRotationZ").Value);
        }
        else Assert.Equal(2, editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex);
    }

    [AvaloniaFact]
    public async Task MediaSaveHintNamesOnlyMissingRequirementsAndKeepsImportErrorSeparate()
    {
        using var editor = new Editor(glb: false);
        var hint = editor.Find<TextBlock>("CustomPetSaveGuidance"); var save = editor.Find<Button>("CreateCustomPetPack");
        Assert.Contains("이름", hint.Text); Assert.Contains("기본", hint.Text); Assert.False(save.IsEnabled);
        editor.Find<TextBox>("CustomPetName").Text = "친구";
        editor.Find<ComboBox>("CustomPetSelectedAction").SelectedItem = "click"; await editor.Open();
        Assert.Contains("기본", hint.Text); Assert.DoesNotContain("이름", hint.Text); Assert.False(save.IsEnabled);
        editor.Find<ComboBox>("CustomPetSelectedAction").SelectedItem = "idle"; await editor.Open();
        Assert.False(hint.IsVisible); Assert.True(save.IsEnabled);
        var selector = editor.Find<ComboBox>("CustomPetSelectedAction");
        var idleLabel = Assert.IsType<TextBlock>(selector.ItemTemplate!.Build("idle"));
        Assert.Contains("필수", idleLabel.Text); Assert.Equal("idle", selector.SelectedItem);
        Assert.Equal(3, editor.Find<ComboBox>("CustomPetPlayback_idle").ItemCount);
        editor.Find<TextBox>("CustomPetName").Text = " "; editor.Layout();
        Assert.Contains("이름", hint.Text); Assert.DoesNotContain("기본", hint.Text); Assert.False(save.IsEnabled);
        selector.SelectedItem = "attention";
        await editor.Media!.OpenPath(Path.Combine(editor.Root, "unsupported.txt"));
        var error = editor.Find<TextBlock>("CustomPetStatus"); Assert.True(error.IsVisible); var message = error.Text;
        editor.Find<Expander>("CustomPetPreparationGuide").IsExpanded = true; editor.Layout();
        Assert.Equal(message, error.Text); Assert.True(hint.IsVisible); Assert.Contains("이름", hint.Text);
        Assert.Equal("pet-motion.gif", editor.Find<TextBlock>("CustomPetLabel_idle").Text);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownGlbReadFailureExplainsRecoveryPreservesPreviousDraftAndAllowsRetry(bool existing)
    {
        using var editor = new Editor(glb: true);
        string? oldId = null;
        if (existing)
        {
            var draft = new GlbPetDraft(editor.GlbPath) { Name = "저장한 친구" };
            draft.Set("click", "Idle", true, .5, rotation: new(35, 50, -20));
            var stored = draft.Save(editor.Library); await editor.Glb!.OpenPackage(stored);
            editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
            await Until(() => !editor.Glb.IsPreviewLoading); oldId = editor.Glb.EditingId;
            Assert.False(editor.Glb.HasUnsavedChanges);
        }
        var bad = Path.Combine(editor.Root, "required-extension.glb");
        File.WriteAllBytes(bad, RewriteGlb(root => root["extensionsRequired"] = new JsonArray("KHR_draco_mesh_compression")));
        await editor.Glb!.OpenPath(bad); editor.Layout();
        var error = editor.Find<TextBlock>("GlbStatus"); Assert.True(error.IsVisible);
        Assert.Contains("확장", error.Text); Assert.Contains("내보내", error.Text);
        Assert.DoesNotContain("KHR_draco", error.Text); Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
        if (existing)
        {
            Assert.Equal(oldId, editor.Glb.EditingId); Assert.Equal("저장한 친구", editor.Find<TextBox>("GlbPetName").Text);
            Assert.Equal("click", editor.Find<ComboBox>("GlbPreviewAction").SelectedItem);
            Assert.Equal("Idle", editor.Find<ComboBox>("GlbClip_click").SelectedItem);
            Assert.Equal(1, editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex);
            Assert.Equal(1, editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex);
            Assert.Equal(35, editor.Find<NumericUpDown>("GlbRotationX").Value);
            Assert.Equal(50, editor.Find<NumericUpDown>("GlbRotationY").Value);
            Assert.Equal(-20, editor.Find<NumericUpDown>("GlbRotationZ").Value);
            Assert.False(editor.Glb.HasUnsavedChanges); Assert.Single(editor.Library.List());
        }
        else Assert.False(editor.Glb.HasDraft);
        await editor.Glb.OpenPath(editor.GlbPath); editor.Layout();
        Assert.True(editor.Glb.HasDraft); Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
        Assert.Equal("retry-pet", editor.Find<TextBox>("GlbPetName").Text); Assert.False(error.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlbHintDistinguishesNameRequiredIdleLoopAndUnassignedPreview(bool staticModel)
    {
        using var editor = new Editor(glb: true);
        if (staticModel) File.WriteAllBytes(editor.GlbPath, RewriteGlb(root => root.Remove("animations")));
        await editor.Open();
        var actionHint = editor.Find<TextBlock>("GlbSelectedActionGuidance"); Assert.Contains("반복", actionHint.Text);
        var name = editor.Find<TextBox>("GlbPetName"); name.Text = " ";
        var saveHint = editor.Find<TextBlock>("GlbSaveGuidance"); Assert.Contains("이름", saveHint.Text);
        Assert.DoesNotContain("기본 파일", saveHint.Text); Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
        name.Text = "친구"; Assert.False(saveHint.IsVisible); Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Until(() => !editor.Glb!.IsPreviewLoading);
        Assert.Contains("연결 안 함", actionHint.Text); Assert.Contains("기본", actionHint.Text);
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = staticModel ? "Static" : "Idle";
        await Until(() => !editor.Glb!.IsPreviewLoading);
        Assert.DoesNotContain("연결 안 함", actionHint.Text ?? "");
        if (staticModel)
        {
            var preparation = editor.Find<Expander>("GlbPetPreparationGuide"); preparation.IsExpanded = true; editor.Layout();
            var paragraphs = preparation.GetVisualDescendants().OfType<TextBlock>().Select(c => c.Text ?? "");
            Assert.Contains(paragraphs, text => text.Contains("애니메이션 없는") && text.Contains("Static"));
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 480, 560)]
    [InlineData(false, 640, 560)]
    [InlineData(false, 1120, 800)]
    [InlineData(true, 480, 560)]
    [InlineData(true, 640, 560)]
    [InlineData(true, 1120, 800)]
    public async Task GuideUsesBodyScrollOutsideWorkspaceAndDoesNotResizePreview(bool glb, double width, double height)
    {
        using var editor = new Editor(glb); await editor.Open();
        editor.Window.Width = width; editor.Window.Height = height; editor.Layout();
        var prefix = glb ? "GlbPet" : "CustomPet";
        var guidePanel = editor.Find<StackPanel>(prefix + "ImportGuidance");
        var guide = editor.Find<Expander>(prefix + "PreparationGuide");
        var identity = editor.Find<Grid>(glb ? "GlbIdentity" : "CustomPetIdentity");
        var workspace = editor.Find<Control>(glb ? "GlbEditor" : "CustomPetWorkspace");
        var body = Assert.IsType<StackPanel>(guidePanel.Parent);
        Assert.Same(body, identity.Parent); Assert.Same(body, workspace.Parent);
        Assert.Equal(body.Children.IndexOf(identity) + 1, body.Children.IndexOf(guidePanel));
        Assert.Equal(body.Children.IndexOf(guidePanel) + 1, body.Children.IndexOf(workspace));
        var stage = editor.Find<Border>(glb ? "GlbPreviewStage" : "CustomPetPreviewSurface"); var oldHeight = stage.Bounds.Height;
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll"); var oldExtent = scroll.Extent.Height;
        guide.IsExpanded = true; editor.Layout();
        Assert.True(scroll.Extent.Height > oldExtent); Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        Assert.InRange(Math.Abs(stage.Bounds.Height - oldHeight), 0, 1);
        Assert.True(guide.Bounds.Width <= scroll.Viewport.Width + 1);
        var save = editor.Find<Button>(glb ? "SaveGlbPet" : "CreateCustomPetPack");
        var point = save.TranslatePoint(default, editor.Window)!.Value;
        Assert.InRange(point.Y + save.Bounds.Height, 0, editor.Window.ClientSize.Height);
        guide.IsExpanded = false; editor.Layout(); Assert.InRange(Math.Abs(stage.Bounds.Height - oldHeight), 0, 1);
    }
}
