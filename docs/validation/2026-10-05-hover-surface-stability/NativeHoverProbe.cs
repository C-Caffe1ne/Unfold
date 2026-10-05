using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
                await runtime.Start(true, true); runtime.Stop();
                var document = new PixelDocument(16,16) { Name="Hover surface fixture" };
                for(var y=4;y<12;y++) for(var x=4;x<12;x++) document.Layers[0].Frames[0][y*16+x]=0xFFBD9DDD;
                var sprite=runtime.Library.Save(document);
                var draft=new GlbPetDraft("/Users/hwanghyeonseong/Downloads/Kazusa.glb"); draft.Set("hover",null);
                var glb=draft.Save(runtime.Library);
                var results=new List<object>();
                foreach(var package in new[]{sprite,glb})
                {
                    await runtime.SelectInstalledCharacter(package);
                    var pet=runtime.ActivePet!;
                    var poll=(DispatcherTimer)typeof(PetWindow).GetField("hitTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pet)!;
                    poll.Stop(); pet.PetView.SetRunning(false);
                    // Exercise the real display layout without polling the user's desktop cursor.
                    typeof(AppRuntime).GetProperty("DiagnosticMode")!.SetValue(runtime,false);
                    pet.MinWidth = pet.MinHeight = 0;
                    if(OperatingSystem.IsMacOS()) MacPetWindow.SetClickThrough(pet,true);
                    var hover=typeof(PetWindow).GetMethod("UpdateHover",BindingFlags.NonPublic|BindingFlags.Instance)!;
                    foreach(var direction in Enum.GetValues<BubbleDirection>())
                    {
                        await runtime.UpdateSettings(runtime.Settings with {BubbleDirection=direction,PetScalePercent=100});
                        poll.Stop(); pet.PetView.SetRunning(false);
                        pet.Position += new Avalonia.PixelPoint(400,280)-pet.PetAnchor;
                        pet.RefreshSpeech(); pet.UpdateLayout(); await Task.Delay(150);
                        var before=pet.ClientSize; var position=pet.Position; var anchor=pet.PetAnchor; var nativeBefore=NativeFrame(pet);
                        var sizes=0;var moves=0;var nativeChanges=0;var anchorChanges=0;
                        void Moved(object? sender,PixelPointEventArgs e)=>moves++;
                        void Sized(object? sender,SizeChangedEventArgs e)=>sizes++;
                        pet.PositionChanged+=Moved;pet.SizeChanged+=Sized;
                        var local=(from y in Enumerable.Range(20,150) from x in Enumerable.Range(20,150)
                            let p=new Point(x,y) where pet.PetView.OpaqueAt(p,false) select p).First();
                        for(var i=0;i<4;i++)
                        {
                            hover.Invoke(pet,new object?[]{local}); pet.RefreshSpeech();pet.UpdateLayout();await Task.Delay(60);
                            if(NativeFrame(pet)!=nativeBefore)nativeChanges++;
                            if(pet.PetAnchor!=anchor)anchorChanges++;
                            if(i==0) Capture(pet,Path.Combine(AppPaths.DataRoot,$"{(package.IsGlb?"glb":"sprite")}-{direction}-hover.png"));
                            hover.Invoke(pet,new object?[]{null}); pet.RefreshSpeech();pet.UpdateLayout();await Task.Delay(60);
                            if(NativeFrame(pet)!=nativeBefore)nativeChanges++;
                            if(pet.PetAnchor!=anchor)anchorChanges++;
                        }
                        pet.PositionChanged-=Moved;pet.SizeChanged-=Sized;
                        results.Add(new {kind=package.IsGlb?"GLB":"sprite",direction=direction.ToString(),before,after=pet.ClientSize,
                            position,finalPosition=pet.Position,anchor,sizes,moves,nativeChanges,anchorChanges,
                            stable=sizes==0&&moves==0&&nativeChanges==0&&anchorChanges==0});
                    }
                    typeof(AppRuntime).GetProperty("DiagnosticMode")!.SetValue(runtime,true);
                }
                File.WriteAllText(Path.Combine(AppPaths.DataRoot,"result.json"),JsonSerializer.Serialize(new {results,os=Environment.OSVersion.ToString(),physicalInput=false},CharacterLibrary.JsonOptions));
                runtime.Dispose(); life.Shutdown();
            }
            catch(Exception ex){ File.WriteAllText(Path.Combine(AppPaths.DataRoot,"error.txt"),ex.ToString());runtime.Dispose();life.Shutdown(1);}
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static string NativeFrame(Window window)
    {
        if(!OperatingSystem.IsMacOS())return $"{window.Position}/{window.ClientSize}";
        var frame=GetFrame(window.TryGetPlatformHandle()!.Handle,Selector("frame"));return $"{frame.X}/{frame.Y}/{frame.Width}/{frame.Height}";
    }
    [StructLayout(LayoutKind.Sequential)]private struct Frame{public double X,Y,Width,Height;}
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="sel_registerName")]private static extern nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")]private static extern Frame GetFrame(nint receiver,nint selector);
    private static void Capture(Control control,string path)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        bitmap.Render(control);bitmap.Save(path,PngBitmapEncoderOptions.Default);
    }
}
