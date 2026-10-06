using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Core;
using Unfold.Desktop;

static class Entry
{
    public static string Output="";
    [STAThread] public static int Main(string[] args)
    {
        Output=args[0];Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR",Path.Combine(Path.GetTempPath(),"Unfold-generic-playback-"+Guid.NewGuid().ToString("N")));
        return AppBuilder.Configure<Probe>().UseSkia().UseHeadless(new(){UseHeadlessDrawing=false}).StartWithClassicDesktopLifetime([]);
    }
}
sealed class Probe : Application
{
    public override void Initialize()=>DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async()=>
        {
            using var runtime=new AppRuntime(desktop);var stages=new List<object>();var code=0;
            try
            {
                var file=Path.Combine(AppPaths.DataRoot,"synthetic.glb");Directory.CreateDirectory(AppPaths.DataRoot);File.WriteAllBytes(file,Synthetic.DenseFixture(12000));
                var package=await Task.Run(()=>new GlbPetDraft(file).Save(runtime.Library));
                await runtime.Start(true,true);runtime.Stop();await runtime.SelectInstalledCharacter(package);
                var settings=(SettingsWindow)desktop.MainWindow!;
                await Sample("pet-192");
                runtime.ShowSettings();settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for(var i=0;i<200&&!settings.GetVisualDescendants().OfType<PetBuilderView>().Any();i++)
                {await Task.Delay(10);Dispatcher.UIThread.RunJobs();settings.UpdateLayout();}
                var builder=settings.GetVisualDescendants().OfType<PetBuilderView>().Single();await builder.OpenPath(file);
                await Sample("pet-and-editor");
                settings.HideToTray();await Sample("editor-hidden");
                await runtime.UpdateSettings(runtime.Settings with{ShowPet=false});await Sample("all-hidden");
                File.WriteAllText(Entry.Output,JsonSerializer.Serialize(new{success=true,environment="Avalonia Headless + Skia; not native OS RAM",coreSha=Hash(typeof(GlbModel).Assembly.Location),desktopSha=Hash(typeof(AppRuntime).Assembly.Location),stages},new JsonSerializerOptions{WriteIndented=true}));
                async Task Sample(string name)
                {
                    await Task.Delay(2000);var allocated=GC.GetTotalAllocatedBytes(true);var start=Stopwatch.GetTimestamp();var gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
                    await Task.Delay(8000);var elapsed=Stopwatch.GetElapsedTime(start).TotalSeconds;var bytes=GC.GetTotalAllocatedBytes(true)-allocated;
                    var owners=new List<(string Name,GlbModel Model)>();var lazyCount=0;
                    foreach(var p in runtime.Characters.Where(p=>p.IsGlb))Inspect("runtime",p);
                    var page=Get(settings,"petPage");var b=Get(page,"builder");var glb=Get(b,"glb");
                    foreach(var (label,combo) in new[]{("media-picker",Get(b,"pets") as ComboBox),("glb-picker",Get(glb,"existing") as ComboBox)})
                        if(combo?.ItemsSource is not null)foreach(var p in combo.ItemsSource.OfType<CharacterPackage>())Inspect(label,p);
                    if(Get(glb,"draft") is GlbPetDraft draft)owners.Add(("draft",draft.Model));
                    stages.Add(new{name,seconds=elapsed,allocatedBytes=bytes,genCollections=Enumerable.Range(0,3).Select(g=>GC.CollectionCount(g)-gc[g]).ToArray(),loadedModels=owners.Select(o=>o.Model).Distinct(ReferenceEqualityComparer.Instance).Count(),lazyPackages=lazyCount,owners=owners.Select(o=>o.Name).ToArray(),pixels=runtime.ActivePet?.PetView.RenderedPixelSize});
                    Console.WriteLine(name+" allocated "+bytes+" bytes; models "+owners.Select(o=>o.Model).Distinct(ReferenceEqualityComparer.Instance).Count());
                    void Inspect(string label,CharacterPackage p)
                    {
                        var cached=Get(p,"model");
                        if(cached is Lazy<GlbModel> lazy && lazy.IsValueCreated) owners.Add((label,lazy.Value));
                        else if(cached is WeakReference<GlbModel> weak && weak.TryGetTarget(out var model)) owners.Add((label,model));
                        else lazyCount++;
                    }
                }
            }
            catch(Exception e){code=1;File.WriteAllText(Entry.Output,JsonSerializer.Serialize(new{success=false,error=e.ToString(),stages}));Console.Error.WriteLine(e);}
            finally{runtime.Dispose();desktop.Shutdown(code);}
        });base.OnFrameworkInitializationCompleted();
    }
    static object? Get(object? o,string name)=>o?.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
