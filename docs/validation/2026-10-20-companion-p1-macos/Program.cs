using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;
internal static class Entry {
 [STAThread] public static int Main(string[] args) => ProgramBuilder().StartWithClassicDesktopLifetime(args);
 static AppBuilder ProgramBuilder() => AppBuilder.Configure<ProbeApp>().UsePlatformDetect()
 .With(new MacOSPlatformOptions {ShowInDock=true})
 .With(new SkiaOptions {MaxGpuResourceSizeBytes=4*1024*1024})
 .With(new AvaloniaNativePlatformOptions {RenderingMode=[AvaloniaNativeRenderingMode.OpenGl,AvaloniaNativeRenderingMode.Software]});
}
public class ProbeApp:Application {
 AppRuntime? runtime; DispatcherTimer? observer; readonly List<Window> windows=[];
 readonly string folder="/private/tmp/unfold-p1-macos-20261020";
 public override void Initialize()=>DesignSystem.Install(this);
 public override void OnFrameworkInitializationCompleted(){
  var life=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!; life.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  Window.WindowOpenedEvent.AddClassHandler<Window>((w,e)=>{
   if(!windows.Contains(w)) windows.Add(w);
   w.AddHandler(InputElement.KeyDownEvent,(_,k)=>Log(new{kind="key",window=w.Title,key=k.Key.ToString(),source=(k.Source as Control)?.Name}),RoutingStrategies.Tunnel,true);
   w.AddHandler(InputElement.PointerPressedEvent,(_,p)=>Log(new{kind="pointer",window=w.Title,source=(p.Source as Control)?.Name,point=p.GetPosition(w).ToString()}),RoutingStrategies.Tunnel,true);
  });
  life.Exit+=(_,_)=>runtime?.Dispose();
  Dispatcher.UIThread.Post(async()=>{
   try {
    runtime=new AppRuntime(life); await runtime.Start(true,true); runtime.Stop();
    var draft=new CustomPetDraft();
    draft.SetClip("idle",ImportedPetClip.FromImage("qa.png",ImageCodec.EncodePng(runtime.Characters.First().StillImage)));
    draft.Save(runtime.Library,"QA 작은 친구"); await runtime.Reload();
    typeof(AppRuntime).GetProperty(nameof(AppRuntime.DiagnosticMode))!.SetValue(runtime,false);
    runtime.ShowSettings();
    var settings=windows.OfType<SettingsWindow>().Single();
    settings.MinWidth=860;settings.MinHeight=800;settings.Width=1120;settings.Height=800;settings.ShowActivated=true;settings.Position=new(100,70);settings.Activate();
    if(runtime.ActivePet is {} pet){pet.MinWidth=pet.MinHeight=0;pet.Position=new(1100,500);}
    observer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
    observer.Tick+=async(_,_)=>{
     try {
      var command=Path.Combine(folder,"command.json");
      if(File.Exists(command)){
       var text=File.ReadAllText(command);File.Delete(command);using var d=JsonDocument.Parse(text);var c=d.RootElement;
       switch(c.GetProperty("action").GetString()){
        case "place":if(runtime.ActivePet is {} p)p.Position=new(c.GetProperty("x").GetInt32(),c.GetProperty("y").GetInt32());break;
        case "window":settings.WindowState=WindowState.Normal;settings.Width=c.GetProperty("width").GetDouble();settings.Height=c.GetProperty("height").GetDouble();settings.Position=new(c.GetProperty("x").GetInt32(),c.GetProperty("y").GetInt32());settings.Activate();break;
        case "quit":runtime.Dispose();life.Shutdown();return;
       }
       Log(new{kind="arrangement",command=text});
      }
      var state=new {pid=Environment.ProcessId,data=AppPaths.DataRoot,diagnostic=runtime.DiagnosticMode,selected=runtime.Selected?.Manifest.Id,scale=runtime.Settings.PetScalePercent,sound=runtime.Settings.ReminderSoundsEnabled,resources=runtime.Settings.ShowSystemResources,
       windows=windows.Where(w=>w.IsVisible).Select(w=>new{type=w.GetType().Name,title=w.Title,position=w.Position.ToString(),size=w.ClientSize.ToString(),scaling=w.DesktopScaling,active=w.IsActive,
        screens=w.Screens.All.Select(s=>new{bounds=s.Bounds.ToString(),work=s.WorkingArea.ToString(),scale=s.Scaling}),
        controls=w.GetVisualDescendants().OfType<Control>().Where(c=>c.IsVisible && (c is Button or TextBox or CheckBox or MenuItem || c.Name is not null)).Select(c=>new{type=c.GetType().Name,name=c.Name,text=c switch {TextBlock t=>t.Text,TextBox t=>t.Text,ContentControl t=>t.Content?.ToString(),_=>null},focused=c.IsFocused,enabled=c.IsEnabled,bounds=c.Bounds.ToString(),screen=c.PointToScreen(new Point()).ToString()})})};
      File.WriteAllText(Path.Combine(folder,"state.json"),JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=true}));
     }catch(Exception ex){Log(new{kind="observer-error",error=ex.ToString()});}
     await Task.CompletedTask;
    };observer.Start();Log(new{kind="ready",pid=Environment.ProcessId});
   }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());life.Shutdown(1);}
  });base.OnFrameworkInitializationCompleted();
 }
 void Log(object value)=>File.AppendAllText(Path.Combine(folder,"events.jsonl"),JsonSerializer.Serialize(new{time=DateTimeOffset.Now,data=value})+"\n");
}
