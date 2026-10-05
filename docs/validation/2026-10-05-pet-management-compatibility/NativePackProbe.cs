using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

internal static class Entry
{
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<ProbeApp>().UsePlatformDetect()
        .With(new MacOSPlatformOptions { ShowInDock = false })
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
        .StartWithClassicDesktopLifetime(args);
}
public class ProbeApp : Application
{
    public override void Initialize() => DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var life = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!; life.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async () =>
        {
            using var runtime = new AppRuntime(life);
            try
            {
                await runtime.Start(true,true); runtime.Stop();
                var input="/Users/hwanghyeonseong/Downloads/kazusa.unfoldpet";
                var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)));
                using var archive=CharacterPack.Open(input);
                var original=runtime.Library.Install(archive,null);
                var newer=new GlbPetDraft(original).Save(runtime.Library);
                await runtime.SelectInstalledCharacter(newer);
                var before=runtime.Library.InspectInstall(archive);
                if(before.Action!="Replace" || before.InstalledVersion!="1.0.1")throw new Exception("Replacement setup failed.");
                using var window=new SettingsWindow(runtime);
                AppRuntime.PrepareDiagnosticWindow(window);window.Show();
                T Find<T>(string name) where T:Control => window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
                Find<Button>("SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(120);window.UpdateLayout();
                if(window.GetVisualDescendants().OfType<TabControl>().Any(c=>c.Name=="PetManagementTabs"))throw new Exception("Legacy tabs remain.");
                var builder=window.GetVisualDescendants().OfType<PetBuilderView>().Single();
                var opening=builder.OpenPath(input);
                await Until(()=>window.OwnedWindows.OfType<PetPackWindow>().Any());
                var import=window.OwnedWindows.OfType<PetPackWindow>().Single();
                var install=import.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="InstallPetPack");
                await Until(()=>install.IsEnabled);import.UpdateLayout();
                Capture(import,"kazusa-replace-preview.png");
                install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await opening;
                var after=runtime.Library.InspectInstall(archive);
                var selected=runtime.Selected!;
                if(after.InstalledVersion!="1.0.0" || selected.Manifest.Id!=archive.Id)throw new Exception("Old pack was not selected.");
                var mappingsPreserved=JsonSerializer.Serialize(selected.Manifest.Animations,CharacterLibrary.JsonOptions)==JsonSerializer.Serialize(archive.Character.Manifest.Animations,CharacterLibrary.JsonOptions);
                if(!mappingsPreserved)throw new Exception("Legacy mappings changed.");
                if(hash!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))))throw new Exception("Source archive changed.");
                await builder.OpenPackage(selected);
                var glb=window.GetVisualDescendants().OfType<GlbPetView>().Single();
                await Until(()=>!glb.IsBusy&&!glb.IsPreviewLoading);
                var layouts=new List<object>();
                foreach(var size in new[]{new Size(1120,800),new Size(860,680),new Size(640,560)})
                {
                    window.MinWidth=640;window.MinHeight=560;window.Width=size.Width;window.Height=size.Height;
                    await Task.Delay(200);window.UpdateLayout();
                    var inputName=Find<TextBox>("GlbPetName");var pane=Find<Grid>("GlbPreviewPane");var actions=Find<StackPanel>("GlbActionPane");
                    var pets=Find<ComboBox>("GlbExistingPets");var pause=Find<Button>("PauseGlbPet");var open=Find<Button>("OpenPetBuilderFile");
                    var scroll=Find<ScrollViewer>("PageBodyScroll");
                    var halfWidth=Math.Abs(inputName.Bounds.Width-pane.Bounds.Width/2)<=1;
                    var stage=Find<Border>("GlbPreviewStage");var title=pane.Children.OfType<TextBlock>().Single();
                    var equalHeight=size.Width<1000 || Math.Abs(stage.Bounds.Height-Math.Max(140,actions.DesiredSize.Height-title.DesiredSize.Height-8))<=1;
                    var pickerBelow=pets.TranslatePoint(default,window)!.Value.Y>=pause.TranslatePoint(default,window)!.Value.Y+pause.Bounds.Height;
                    var openRight=open.TranslatePoint(default,window)!.Value.X>=inputName.TranslatePoint(default,window)!.Value.X+inputName.Bounds.Width;
                    if(!halfWidth||!equalHeight||!pickerBelow||!openRight||scroll.Extent.Width>scroll.Viewport.Width+1)throw new Exception("Layout contract failed.");
                    Capture(window,$"glb-{size.Width}-{size.Height}.png");
                    layouts.Add(new{width=size.Width,height=size.Height,halfWidth,equalHeight,pickerBelow,openRight});
                }
                foreach(var theme in DesignSystem.Themes)
                {
                    runtime.SetTheme(theme.Id);window.Width=1120;window.Height=800;await Task.Delay(100);window.UpdateLayout();
                    Capture(window,$"glb-theme-{theme.Id}.png");
                }
                await builder.OpenPath("/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold/Tests/Unfold.Tests/Fixtures/pet-motion.gif");
                await Task.Delay(100);window.UpdateLayout();Capture(window,"media-builder.png");
                File.WriteAllText(Path.Combine(AppPaths.DataRoot,"result.json"),JsonSerializer.Serialize(new{success=true,input,inputSha256=hash,beforeVersion=before.InstalledVersion,afterVersion=after.InstalledVersion,mappingsPreserved,selectedId=selected.Manifest.Id,actionCount=selected.Manifest.Animations.Count,layouts,physicalInput=false,os=Environment.OSVersion.ToString()},CharacterLibrary.JsonOptions));
                window.HideToTray();runtime.Dispose();life.Shutdown();
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(AppPaths.DataRoot,"error.txt"),ex.ToString());runtime.Dispose();life.Shutdown(1);}
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Until(Func<bool> ready){for(var i=0;i<600&&!ready();i++)await Task.Delay(25);if(!ready())throw new TimeoutException();}
    private static void Capture(Control control,string file){using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));bitmap.Render(control);bitmap.Save(Path.Combine(AppPaths.DataRoot,file),PngBitmapEncoderOptions.Default);}
}
