using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbEditorUxTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        public Window Window { get; } = new() { Width = 840, Height = 780 };
        public GlbPetView Page { get; }
        public CharacterLibrary Library { get; }
        public Editor(bool saved = false, double? savedSpeed = null)
        {
            var path = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
            Library = new(Path.Combine(temp.Path, "library"));
            if (saved)
            {
                var draft = new GlbPetDraft(path) { Name = "저장한 펫" };
                if (savedSpeed is { } speed) draft.Set("click", "Idle", false, speed);
                draft.Save(Library);
            }
            Page = new(Window, Library, _ => Task.CompletedTask, () => Task.FromResult<string?>(path));
            Window.Content = Page; Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public async Task Open()
        {
            Press("OpenGlbPet"); await Until(() => !Page.IsBusy && Find<Button>("SaveGlbPet").IsEnabled);
        }
        public static async Task Until(Func<bool> ready)
        {
            for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
            Assert.True(ready());
        }
        public void Dispose() { Page.Dispose(); Window.Close(); temp.Dispose(); }
    }

    [AvaloniaFact]
    public async Task ImportedCustomSpeedAllowsOtherMappingChangesAndSurvivesSaveAndReopen()
    {
        using var editor = new Editor(saved: true, savedSpeed: 1.25);
        editor.Find<ComboBox>("GlbExistingPets").SelectedIndex = 0;
        await Editor.Until(() => !editor.Page.IsBusy && editor.Page.HasDraft);
        var speed = editor.Find<ComboBox>("GlbSpeed_click");
        Assert.True(speed.SelectedIndex >= 0);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        editor.Find<NumericUpDown>("GlbRotationY").Value = 180;
        editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        editor.Find<ComboBox>("GlbClip_click").SelectedIndex = 0;
        Assert.False(editor.Find<ComboBox>("GlbRepeat_click").IsEnabled);
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        editor.Find<NumericUpDown>("GlbRotationY").Value = 180;
        editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
        Assert.True(editor.Page.HasUnsavedChanges);
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy);
        Assert.False(editor.Page.HasUnsavedChanges, editor.Find<TextBlock>("GlbStatus").Text);
        var package = editor.Library.List().Single();
        Assert.Equal(1.25, package.Manifest.Animations["click"].Speed);
        Assert.Equal(180, package.Manifest.Animations["click"].Heading);
        Assert.True(package.Manifest.Animations["click"].Loop);
        await editor.Page.OpenPackage(package);
        Assert.True(editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex >= 0);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.Equal(180, editor.Find<NumericUpDown>("GlbRotationY").Value);
        Assert.Equal(1, editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex);
    }

    [AvaloniaFact]
    public async Task SavedPetOpensCleanAndSaveRequiresAValidChange()
    {
        using var editor = new Editor(saved: true);
        editor.Find<ComboBox>("GlbExistingPets").SelectedIndex = 0;
        await Editor.Until(() => !editor.Page.IsBusy && editor.Find<TextBox>("GlbPetName").Text == "저장한 펫");
        Assert.False(editor.Page.HasUnsavedChanges);
        Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
        editor.Find<TextBox>("GlbPetName").Text = "  ";
        Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
        editor.Find<TextBox>("GlbPetName").Text = "바꾼 이름";
        Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy && !editor.Page.HasUnsavedChanges);
        Assert.Equal("바꾼 이름", editor.Library.List().Single().Manifest.Name);
        Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
    }

    [AvaloniaFact]
    public async Task OneShotPreviewStaysOnTheSelectedActionAndReplayResumesAPause()
    {
        using var editor = new Editor(); await editor.Open();
        editor.Find<ComboBox>("GlbPreviewAction").SelectedIndex = GlbPetDraft.Actions.ToList().IndexOf("click");
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        var preview = editor.Find<AnimationView>("GlbPreview");
        await Editor.Until(() => !preview.Repeats);
        await Task.Delay(600, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        Assert.False(preview.Repeats); // Finishing must not silently switch to an idle loop.
        editor.Press("ReplayGlbPet"); await Editor.Until(() => editor.Find<Button>("PauseGlbPet").IsEnabled);
        editor.Press("PauseGlbPet"); Assert.Equal("계속 재생", editor.Find<Button>("PauseGlbPet").Content);
        editor.Press("ReplayGlbPet");
        await Editor.Until(() => Equals(editor.Find<Button>("PauseGlbPet").Content, "일시정지"));
    }

    [AvaloniaFact]
    public async Task UnassignedActionsDisableIrrelevantPlaybackSettings()
    {
        using var editor = new Editor(); await editor.Open();
        Assert.False(editor.Find<ComboBox>("GlbRepeat_look").IsEnabled);
        Assert.False(editor.Find<ComboBox>("GlbSpeed_look").IsEnabled);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedIndex = GlbPetDraft.Actions.ToList().IndexOf("look");
        editor.Find<ComboBox>("GlbClip_look").SelectedItem = "Idle";
        Assert.True(editor.Find<ComboBox>("GlbRepeat_look").IsEnabled);
        Assert.True(editor.Find<ComboBox>("GlbSpeed_look").IsEnabled);
        editor.Find<ComboBox>("GlbRepeat_look").SelectedIndex = 1;
        await Editor.Until(() => editor.Find<AnimationView>("GlbPreview").Repeats);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Library.List().Single().Manifest.Animations["look"].Loop);
    }
    [AvaloniaTheory]
    [InlineData("idle")]
    [InlineData("held")]
    [InlineData("walk")]
    [InlineData("pickup")]
    [InlineData("land")]
    [InlineData("click")]
    [InlineData("attention")]
    [InlineData("stretch")]
    [InlineData("celebrate")]
    [InlineData("sleep")]
    [InlineData("look")]
    [InlineData("yawn")]
    [InlineData("sulk")]
    public async Task AllAssignedActionsCanChooseAndReopenTheirPlayback(string action)
    {
        using var editor = new Editor(); await editor.Open();
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = action;
        editor.Find<ComboBox>("GlbClip_" + action).SelectedItem = "Idle";
        var repeat = editor.Find<ComboBox>("GlbRepeat_" + action);
        Assert.True(repeat.IsEnabled);
        repeat.SelectedIndex = 1;
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.True(editor.Find<AnimationView>("GlbPreview").Repeats);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy);
        var saved = Assert.Single(editor.Library.List());
        Assert.True(saved.Manifest.Animations[action].Loop);
        await editor.Page.OpenPackage(saved);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = action;
        repeat = editor.Find<ComboBox>("GlbRepeat_" + action);
        Assert.True(repeat.IsEnabled); Assert.Equal(1, repeat.SelectedIndex);
        repeat.SelectedIndex = 0;
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.False(editor.Find<AnimationView>("GlbPreview").Repeats);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy);
        saved = Assert.Single(editor.Library.List());
        Assert.False(saved.Manifest.Animations[action].Loop);
        await editor.Page.OpenPackage(saved);
        Assert.Equal(0, editor.Find<ComboBox>("GlbRepeat_" + action).SelectedIndex);
        Assert.True(editor.Find<ComboBox>("GlbRepeat_" + action).IsEnabled);
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "GlbRepeatHint_" + action);
    }

    [AvaloniaTheory]
    [InlineData(480, 560, false)]
    [InlineData(860, 680, true)]
    public async Task OnlyTheChosenActionIsShownWithoutHorizontalOverflow(double width, double height, bool wide)
    {
        using var editor = new Editor(); await editor.Open();
        editor.Window.Width = width; editor.Window.Height = height;
        editor.Find<ComboBox>("GlbPreviewAction").SelectedIndex = GlbPetDraft.Actions.ToList().IndexOf("land");
        Dispatcher.UIThread.RunJobs(); editor.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); editor.Window.UpdateLayout();
        foreach (var key in GlbPetDraft.Actions)
            Assert.Equal(key == "land", editor.Find<StackPanel>("GlbActionRow_" + key).IsVisible);
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll");
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"{scroll.Extent.Width} > {scroll.Viewport.Width}");
        var preview = editor.Find<Grid>("GlbPreviewPane"); var settings = editor.Find<StackPanel>("GlbActionPane");
        Assert.Equal(wide, Grid.GetColumn(settings) == 2);
        Assert.True(preview.Bounds.Width > 0 && settings.Bounds.Width > 0);
    }

    [AvaloniaFact]
    public async Task SavingDuringAPreviewChangeKeepsTheChosenActionAndSpeed()
    {
        using var editor = new Editor(); await editor.Open();
        editor.Find<ComboBox>("GlbPreviewAction").SelectedIndex = GlbPetDraft.Actions.ToList().IndexOf("click");
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex = 1;
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy && !editor.Page.IsPreviewLoading);
        Assert.False(editor.Find<AnimationView>("GlbPreview").Repeats);
        Assert.Equal("click", editor.Find<ComboBox>("GlbPreviewAction").SelectedItem);
        Assert.Equal(1, editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex);
        Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
    }

    [AvaloniaFact]
    public async Task EachActionKeepsItsHeadingAcrossSelectionSaveAndReopen()
    {
        using var editor = new Editor(); await editor.Open();
        var action = editor.Find<ComboBox>("GlbPreviewAction");
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        editor.Find<NumericUpDown>("GlbRotationY").Value = 180;
        action.SelectedItem = "click";
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.Equal(0, editor.Find<NumericUpDown>("GlbRotationY").Value);
        editor.Find<NumericUpDown>("GlbRotationX").Value = 30;
        action.SelectedItem = "idle";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.Equal(180, editor.Find<NumericUpDown>("GlbRotationY").Value);
        action.SelectedItem = "click";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.Equal(30, editor.Find<NumericUpDown>("GlbRotationX").Value);
        editor.Press("SaveGlbPet"); await Editor.Until(() => !editor.Page.IsBusy);
        var package = editor.Library.List().Single();
        Assert.Equal(new GlbRotation(0, 180, 0), package.Manifest.Animations["idle"].Rotation);
        Assert.Equal(new GlbRotation(30, 0, 0), package.Manifest.Animations["click"].Rotation);
        Assert.Null(package.Manifest.Model!.RootNode);
        await editor.Page.OpenPackage(package);
        Assert.Equal(180, editor.Find<NumericUpDown>("GlbRotationY").Value);
        action.SelectedItem = "click";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.Equal(30, editor.Find<NumericUpDown>("GlbRotationX").Value);
    }

    [AvaloniaFact]
    public async Task RotationIsBelowPreviewAndHeadingControlsAreRemovedInEveryTheme()
    {
        using var editor = new Editor(); await editor.Open();
        var original = DesignSystem.CurrentTheme;
        try
        {
            foreach (var theme in DesignSystem.Themes)
            {
                DesignSystem.ApplyTheme(theme.Id);
                Dispatcher.UIThread.RunJobs(); editor.Window.UpdateLayout();
                var rotation = editor.Find<NumericUpDown>("GlbRotationY");
                var stage = editor.Find<Border>("GlbPreviewStage");
                Assert.True(rotation.IsEffectivelyVisible);
                Assert.True(rotation.TranslatePoint(default, editor.Window)!.Value.Y >= stage.TranslatePoint(default, editor.Window)!.Value.Y);
                Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<Control>(), c =>
                    c.Name is "GlbRoot" or "GlbAdvanced" or "GlbHeading" || c.Name?.StartsWith("GlbHeading_") == true);
            }
        }
        finally { DesignSystem.ApplyTheme(original); }
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "look";
        await Editor.Until(() => !editor.Page.IsPreviewLoading);
        Assert.False(editor.Find<NumericUpDown>("GlbRotationY").IsEnabled);
    }
}
