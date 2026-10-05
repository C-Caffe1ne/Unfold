using System.Text.Json;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

internal static class Entry
{
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<ProbeApp>().UsePlatformDetect()
        .With(new MacOSPlatformOptions { ShowInDock = true })
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
        .StartWithClassicDesktopLifetime(args);
}
public class ProbeApp : Application
{
    public override void Initialize() => DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var life = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        life.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async () =>
        {
            var runtime = new AppRuntime(life); runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            var doc = new PixelDocument(32, 32) { Name = "Input fixture" };
            for(var y=8;y<24;y++) for(var x=8;x<24;x++) doc.Layers[0].Frames[0][y*32+x] = (x>=14 && x<=17 && y>=14 && y<=17) ? 0 : 0xFFD279C6;
            var fixture = runtime.Library.Save(doc);
            var glb = new GlbPetDraft("/Users/hwanghyeonseong/Downloads/Kazusa.glb").Save(runtime.Library);
            await runtime.Reload(); runtime.Stop();
            var events = new List<object>(); var label = new TextBlock { FontSize=18, Text="2D", Margin=new Thickness(24) };
            var surface = new Border { Background=Brushes.LightBlue };
            var controls = new StackPanel { Orientation=Orientation.Horizontal, Spacing=12, Margin=new Thickness(20), VerticalAlignment=VerticalAlignment.Bottom };
            var grid=new Grid(); grid.Children.Add(surface);grid.Children.Add(label);grid.Children.Add(controls);
            var host = new Window { Title="Unfold Input Probe", Width=700, Height=530, Content=grid, Position=new Avalonia.PixelPoint(200,160) };
            life.MainWindow=host; host.Show(); host.Activate();
            var mode="2D"; var behind=0; var onPet=0;
            void Save()
            {
                var pet=runtime.ActivePet!;
                MacPetWindow.TryGetPointer(pet,out var cursor);
                var origin=pet.PetView.PointToScreen(default);
                File.WriteAllText(Path.Combine(AppPaths.DataRoot,"state.json"),JsonSerializer.Serialize(new {
                    mode,behind,onPet,events,petOrigin=new {origin.X,origin.Y},scale=pet.DesktopScaling,
                    petPosition=new {pet.Position.X,pet.Position.Y}, petSize=pet.ClientSize,
                    clickThrough=pet.IsClickThrough, cursor, animation=pet.ActiveAnimation,
                    reminder=runtime.Reminder.Notice.ToString(), os=Environment.OSVersion.ToString()
                },new JsonSerializerOptions{WriteIndented=true}));
                label.Text=$"{mode}   Behind: {behind}   Pet: {onPet}";
            }
            async Task Select(CharacterPackage pack,string name)
            {
                mode=name; await runtime.UpdateSettings(runtime.Settings with {SelectedCharacterId=pack.Manifest.Id,ShowPet=true,ReminderSoundsEnabled=false,PetScalePercent=100,BubbleDirection=BubbleDirection.Bottom});
                var pet=runtime.ActivePet!; pet.Position=host.PointToScreen(new Point(250,135));pet.UpdateLayout();Save();
            }
            void Button(string name,Action action) {var b=new Button{Content=name};b.Click+=(_,_)=>action();controls.Children.Add(b);}
            Button("2D",()=>_ = Select(fixture,"2D")); Button("GLB",()=>_ = Select(glb,"GLB"));
            Button("Reminder",()=>_ = runtime.ShowReminder());
            Button("Check routing",()=>_ = VerifyRouting());
            async Task VerifyRouting()
            {
                try
                {
                    // Stop polling only while querying deterministic native input
                    // states; no synthesized OS input or cursor movement is used.
                    var pet=runtime.ActivePet!;
                    var poll=(DispatcherTimer)typeof(PetWindow).GetField("hitTimer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(pet)!;
                    poll.Stop();
                    var results=new List<object>();
                    foreach(var pack in new[]{fixture,glb})
                    {
                        await Select(pack,pack==fixture?"2D":"GLB");poll.Stop();
                        pet.PetView.SetRunning(false);await Task.Delay(200);pet.UpdateLayout();
                        var solid=(from y in Enumerable.Range(60,100) from x in Enumerable.Range(60,80)
                            let pt=new Point(x,y) where pet.PetView.OpaqueAt(pt,false) select pt).First();
                        foreach(var local in new[]{new Point(5,5),new Point(96,96),solid})
                        {
                            var client=pet.PetView.TranslatePoint(local,pet)!.Value;
                            var accepts=pet.AcceptsPointerAt(client);
                            MacPetWindow.SetClickThrough(pet,!accepts);
                            await Task.Delay(80);
                            var handle=pet.TryGetPlatformHandle()!.Handle;
                            var screen=Native.Convert(handle,Native.Sel("convertPointToScreen:"),new Native.P(client.X,pet.ClientSize.Height-client.Y));
                            var selected=Native.Hit(Native.Class("NSWindow"),Native.Sel("windowNumberAtPoint:belowWindowWithWindowNumber:"),screen,0);
                            var petId=Native.Get(handle,Native.Sel("windowNumber"));
                            var hostId=Native.Get(host.TryGetPlatformHandle()!.Handle,Native.Sel("windowNumber"));
                            var ignores=(Native.Get(handle,Native.Sel("ignoresMouseEvents")) & 255)!=0;
                            // Another app can cover the probe host. Compare with
                            // the actual next window, rather than assuming host.
                            var below=Native.Hit(Native.Class("NSWindow"),Native.Sel("windowNumberAtPoint:belowWindowWithWindowNumber:"),screen,petId);
                            results.Add(new {mode,local,accepts,ignores,selected=(long)selected,petId=(long)petId,hostId=(long)hostId,below=(long)below,
                                success=ignores==!accepts && selected==(accepts?petId:below) && selected!=0});
                        }
                        pet.PetView.SetRunning(true);
                    }
                    MacPetWindow.SetClickThrough(pet,pet.IsClickThrough);poll.Start();
                    File.WriteAllText(Path.Combine(AppPaths.DataRoot,"routing.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
                    MacPetWindow.TryGetPointer(pet,out var pointer);
                    pet.UpdateClickThrough();
                    var h=pet.TryGetPlatformHandle()!.Handle;
                    var actual=Native.Convert(h,Native.Sel("convertPointToScreen:"),new Native.P(pointer.X,pet.ClientSize.Height-pointer.Y));
                    var expected=Native.Point(Native.Class("NSEvent"),Native.Sel("mouseLocation"));
                    File.WriteAllText(Path.Combine(AppPaths.DataRoot,"native-pointer.json"),JsonSerializer.Serialize(new {
                        cursorConversionVerified=Math.Abs(actual.X-expected.X)<1 && Math.Abs(actual.Y-expected.Y)<1,
                        pollingVerified=pet.IsClickThrough==!pet.AcceptsPointerAt(pointer) &&
                            ((Native.Get(h,Native.Sel("ignoresMouseEvents"))&255)!=0)==pet.IsClickThrough
                    },new JsonSerializerOptions{WriteIndented=true}));
                    label.Text="Routing check saved";
                }
                catch(Exception error) {File.WriteAllText(Path.Combine(AppPaths.DataRoot,"routing-error.txt"),error.ToString());}
            }
            Button("Finish",()=> {Save();runtime.Dispose();host.Close();life.Shutdown();});
            await Select(fixture,"2D");
            surface.PointerPressed+=(_,e)=> {behind++;var p=e.GetPosition(surface); events.Add(new{recipient="behind",mode,button=e.GetCurrentPoint(surface).Properties.PointerUpdateKind.ToString(),point=p});Save();};
            var petWindow=runtime.ActivePet!;
            petWindow.AddHandler(InputElement.PointerPressedEvent,(_,e)=>{onPet++;events.Add(new {recipient="pet",mode,button=e.GetCurrentPoint(petWindow).Properties.PointerUpdateKind.ToString(),point=e.GetPosition(petWindow.PetView)});Save();},RoutingStrategies.Tunnel,true);
            petWindow.AddHandler(InputElement.PointerReleasedEvent,(_,e)=> {events.Add(new{recipient="pet-release",mode,position=petWindow.Position});Save();},RoutingStrategies.Bubble,true);
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>Save();timer.Start();
            life.Exit+=(_,_)=>{timer.Stop();runtime.Dispose();};
            await Task.Delay(400); await VerifyRouting(); Save(); timer.Stop(); runtime.Dispose(); host.Close(); life.Shutdown();
        });
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal readonly record struct P(double X,double Y);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="sel_registerName")] internal static extern nint Sel(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_getClass")] internal static extern nint Class(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] internal static extern nint Get(nint receiver,nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] internal static extern P Point(nint receiver,nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] internal static extern P Convert(nint receiver,nint selector,P point);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")] internal static extern nint Hit(nint receiver,nint selector,P point,nint below);
}
