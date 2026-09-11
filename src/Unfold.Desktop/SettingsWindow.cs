using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class SettingsWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly TextBlock countdown = Ui.Text("60:00", 52, Ui.Accent), state = Ui.Text("Ready", 13);
    private readonly TextBlock characterName = Ui.Text("", 20, Ui.Accent);
    private readonly WrapPanel characters = new() { Orientation = Orientation.Horizontal };
    private readonly AnimationView preview = new() { Width = 120, Height = 120 };
    private readonly Button edit, delete, pause;
    private readonly CheckBox showPet;
    private CharacterPackage? previewCharacter;
    private IReadOnlyList<CharacterPackage>? cardSource;
    private bool updating;
    public SettingsWindow(AppRuntime runtime)
    {
        this.runtime = runtime; Title = "Unfold · Stretch & Create"; Width = 600; Height = 790; MinWidth = 550; MinHeight = 600;
        Background = Ui.Background;
        var interval = new NumericUpDown { Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 5, Width = 130, FormatString = "0" };
        var idle = new NumericUpDown { Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, Width = 130, FormatString = "0" };
        pause = Ui.Button("Pause", runtime.TogglePause);
        var apply = Ui.AsyncButton("Apply reminder settings", async () =>
        {
            try { await runtime.UpdateSettings(runtime.Settings with { IntervalMinutes = (int)(interval.Value ?? 60), IdleMinutes = (int)(idle.Value ?? 5) }); }
            catch (Exception error) { await Ui.Error(this, error); }
        });
        showPet = new CheckBox { Content = "Show desktop pet", IsChecked = runtime.Settings.ShowPet };
        showPet.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            try { await runtime.UpdateSettings(runtime.Settings with { ShowPet = showPet.IsChecked == true }); }
            catch (Exception error) { await Ui.Error(this, error); }
        };
        var login = new CheckBox { Content = "Launch at login" };
        try { login.IsChecked = PlatformServices.StartsAtLogin(); } catch (Exception error) { AppPaths.Log(error); }
        login.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            try { PlatformServices.SetStartAtLogin(login.IsChecked == true); }
            catch (Exception error) { updating = true; login.IsChecked = false; updating = false; await Ui.Error(this, error); }
        };
        // Built but left out of the layout below: the MVP ships without the
        // pixel editor entry points, and Refresh() still drives their state.
        edit = Ui.AsyncButton("Edit", () => runtime.OpenEditor(runtime.Selected));
        delete = Ui.AsyncButton("Delete", async () => { if (runtime.Selected is { } selected) await runtime.DeleteCharacter(selected); });
        var body = Ui.Column(Ui.Text("UNFOLD", 14, Ui.Accent), Ui.Text("Make room for a small break.", 26),
            new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = Ui.Column(
                Ui.Text("NEXT STRETCH", 12), countdown, state, Ui.Row(pause, Ui.Button("Reset", runtime.Reset), Ui.AsyncButton("Stretch now", runtime.ShowReminder))) },
            Ui.Row(Ui.Column(Ui.Text("Remind me every (min)", 12), interval), Ui.Column(Ui.Text("Pause when away (min)", 12), idle)), apply,
            new Separator(), Ui.Text("YOUR COMPANION", 12, Ui.Accent),
            Ui.Row(preview, Ui.Column(characterName, Ui.Text("Pick a companion below to swap it right away.", 12))), characters,
            showPet, login, new Separator(), Ui.Row(Ui.Text("Closing this window keeps Unfold in the tray.", 12), Ui.AsyncButton("Quit", runtime.Quit)));
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(28), Child = body } };
        Closing += (_, e) => { e.Cancel = true; HideToTray(); };
        Opened += (_, _) => preview.SetRunning(true);
        runtime.Changed += Refresh; Closed += (_, _) => { runtime.Changed -= Refresh; preview.Dispose(); };
        Refresh();
    }
    public void HideToTray() { Hide(); preview.SetRunning(false); }
    public void ResumePreview() => preview.SetRunning(true);
    private async void Refresh()
    {
        if (updating) return;
        CharacterPackage? selected;
        updating = true;
        try
        {
            countdown.Text = $"{(int)runtime.Clock.Remaining.TotalMinutes:00}:{runtime.Clock.Remaining.Seconds:00}";
            state.Text = runtime.ActivityError ?? (runtime.Clock.Paused ? "Paused by you" : runtime.Clock.IdlePaused ? "Paused while you're away" : "Counting active time");
            pause.Content = runtime.Clock.Paused ? "Resume" : "Pause";
            showPet.IsChecked = runtime.Settings.ShowPet;
            selected = runtime.Selected;
            characterName.Text = selected?.Manifest.Name ?? "No character";
            RefreshCards(selected);
            edit.IsEnabled = delete.IsEnabled = selected is { IsBuiltIn: false };
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = error.Message; return; }
        finally { updating = false; }
        // Decoding the idle clip runs outside the re-entrancy guard: a swap clicked
        // while the preview is still loading must not be swallowed by it.
        if (selected is null || previewCharacter == selected) return;
        previewCharacter = selected;
        try
        {
            var frames = await runtime.Clip("idle");
            if (runtime.Selected == selected) preview.SetFrames(frames, true, selected.Manifest.RenderStyle == "pixel");
            if (!IsVisible) preview.SetRunning(false);
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = error.Message; }
    }
    /// <summary>Cards are rebuilt only when the library changes; otherwise this just
    /// moves the selection ring, because Refresh runs once a second.</summary>
    private void RefreshCards(CharacterPackage? selected)
    {
        if (!ReferenceEquals(cardSource, runtime.Characters))
        {
            foreach (var card in characters.Children.OfType<Button>())
                if (card.Content is Border { Child: Panel content })
                    foreach (var portrait in content.Children.OfType<Image>()) (portrait.Source as IDisposable)?.Dispose();
            characters.Children.Clear();
            foreach (var character in runtime.Characters) characters.Children.Add(Card(character));
            cardSource = runtime.Characters;
        }
        foreach (var card in characters.Children.OfType<Button>())
            if (card.Content is Border frame) frame.BorderBrush = ReferenceEquals(card.Tag, selected) ? Ui.Accent : Brushes.Transparent;
    }
    private Button Card(CharacterPackage character)
    {
        var portrait = new Image { Width = 72, Height = 72, Stretch = Stretch.Uniform };
        // Sprite cell 0 is the neutral pose, so the card identifies the cat without
        // decoding — and without animating — every clip in the library at once.
        try { portrait.Source = Ui.Bitmap(character.Frame(0)); }
        catch (Exception error) { AppPaths.Log(error); }
        RenderOptions.SetBitmapInterpolationMode(portrait,
            character.Manifest.RenderStyle == "pixel" ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        var label = Ui.Text(character.Manifest.Name, 12);
        label.HorizontalAlignment = HorizontalAlignment.Center; label.TextAlignment = TextAlignment.Center;
        // The ring lives on an inner border so the Fluent hover/pressed states,
        // which retemplate the button's own border, cannot hide the selection.
        var frame = new Border
        {
            Background = Ui.Panel, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 8),
            BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent,
            Child = new StackPanel { Spacing = 6, Children = { portrait, label } },
        };
        var card = new Button
        {
            Tag = character, Content = frame, Padding = default, Margin = new Thickness(0, 0, 10, 10),
            Background = Brushes.Transparent, BorderThickness = default, CornerRadius = new CornerRadius(12),
        };
        ToolTip.SetTip(card, $"Use {character.Manifest.Name}");
        card.Click += async (_, _) =>
        {
            try { await runtime.SelectCharacter(character); }
            catch (Exception error) { await Ui.Error(this, error); }
        };
        return card;
    }
}
