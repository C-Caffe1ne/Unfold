using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PetPackWindow : Window
{
    private readonly CharacterLibrary library;
    private readonly Func<CharacterPackage, Task> installed;
    private readonly Func<Task<string?>> chooseFile;
    private readonly AnimationView preview = new() { Width = 192, Height = 192 };
    private readonly ComboBox clips = new() { Name = "PackClip", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
    private readonly TextBlock title = Text("Choose a pet pack", 22), version = Text(""), status = Text("Open a .unfoldpet file to inspect its companion before installing."), warnings = Text("");
    private readonly Button open, install;
    private CharacterPack? pack;
    private CharacterPackInstallInfo? target;
    private bool closed, installing;
    private int generation;
    public PetPackWindow(CharacterLibrary library, Func<CharacterPackage, Task> installed, Func<Task<string?>>? chooseFile = null)
    {
        this.library = library; this.installed = installed; this.chooseFile = chooseFile ?? PickFile;
        Title = "Unfold · Pet packs"; Width = 520; Height = 710; MinWidth = 480; MinHeight = 560; Background = Ui.Background;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        open = Ui.AsyncButton("Open pet pack…", OpenPack); open.Name = "OpenPetPack";
        install = Ui.Button("Install", () => _ = InstallPack()); install.Name = "InstallPetPack"; install.IsEnabled = false;
        AutomationProperties.SetName(clips, "Preview animation");
        clips.SelectionChanged += async (_, _) => await PlayClip();
        preview.Completed += () => { if (!closed && clips.SelectedItem as string != "idle") clips.SelectedItem = "idle"; };
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(24), Child = Ui.Column(
            Ui.Text("YOUR COMPANION", 12, Ui.Accent), open, title, version,
            new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(12), Child = preview },
            Ui.Text("Preview animation", 12), clips, warnings, status, Ui.Row(install, Ui.Button("Close", Close))) } };
        Closing += (_, e) => { if (installing) e.Cancel = true; };
        Closed += (_, _) => { closed = true; generation++; preview.Dispose(); DisposePack(); };
    }
    private static TextBlock Text(string value, double size = 14) => new() { Text = value, FontSize = size, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private async Task<string?> PickFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open pet pack", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Unfold pet pack") { Patterns = ["*.unfoldpet"] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private async Task OpenPack()
    {
        CharacterPack? candidate = null;
        try
        {
            var path = await chooseFile(); if (closed || path is null) return;
            generation++; install.IsEnabled = clips.IsEnabled = false; install.Content = "Install"; status.Text = "Checking pet pack…";
            DisposePack(); target = null; clips.ItemsSource = null; preview.SetFrames([], true);
            title.Text = "Choose a pet pack"; version.Text = warnings.Text = "";
            candidate = await Task.Run(() => CharacterPack.Open(path));
            var info = await Task.Run(() => library.InspectInstall(candidate));
            if (closed) return;
            pack = candidate; candidate = null; target = info;
            title.Text = pack.Character.Manifest.Name;
            version.Text = $"Version {pack.ContentVersion}" + (info.InstalledVersion is { } current ? $" · installed {current}" : " · new companion");
            warnings.Text = pack.Audit.Warnings.Count == 0 ? "" : "Preview notes\n" + string.Join("\n", pack.Audit.Warnings);
            clips.ItemsSource = pack.Character.Manifest.Animations.Keys.Order().ToArray(); clips.SelectedItem = "idle"; clips.IsEnabled = true;
            install.Content = info.Action; install.IsEnabled = true;
            status.Text = info.Action == "Install" ? "Ready to install. Your current companions will be kept." :
                $"Ready to {info.Action.ToLowerInvariant()}. This replaces the installed files for this companion.";
            await PlayClip();
        }
        catch (Exception error) { if (!closed) { install.IsEnabled = false; status.Text = "Could not open pack: " + error.Message; } AppPaths.Log(error); }
        finally { candidate?.Dispose(); }
    }
    private async Task PlayClip()
    {
        if (pack is not { } current || clips.SelectedItem is not string key) return;
        var request = ++generation;
        try
        {
            var frames = await Task.Run(() => current.Character.LoadAnimation(key));
            if (closed || request != generation || pack != current) return;
            preview.SetFrames(frames, current.Character.Manifest.Animations[key].Loop, current.Character.Manifest.RenderStyle == "pixel");
        }
        catch (Exception error) { if (!closed && request == generation) { status.Text = error.Message; install.IsEnabled = false; } }
    }
    private async Task InstallPack()
    {
        if (installing || closed || pack is null || target is null) return;
        installing = true; install.IsEnabled = open.IsEnabled = false; status.Text = "Installing companion…";
        CharacterPackage? result = null;
        try
        {
            result = await Task.Run(() => library.Install(pack, target.Revision));
            await installed(result);
            status.Text = "Installed and selected. You can close this window."; install.Content = "Installed";
        }
        catch (Exception error)
        {
            status.Text = result is null ? "Installation failed: " + error.Message + " Reopen the pack to try again." :
                "Pack installed, but could not select it. Reopen Settings to choose the companion. " + error.Message;
            AppPaths.Log(error);
        }
        finally { installing = false; open.IsEnabled = true; }
    }
    private void DisposePack()
    {
        try { pack?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppPaths.Log(error); }
        pack = null;
    }
}
