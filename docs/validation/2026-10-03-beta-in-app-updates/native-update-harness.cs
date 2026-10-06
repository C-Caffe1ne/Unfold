using System.Diagnostics;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
var root=Path.GetFullPath(args[0]); var app=Path.Combine(root,"Unfold.app");
var manager=new UpdateManager(new SimpleFileSource(new DirectoryInfo(args[1])),
    new UpdateOptions { ExplicitChannel="osx-arm64-beta", AllowVersionDowngrade=false },new NativeLocator(root,app));
if (!manager.IsInstalled) throw new Exception("Fixture is not a managed installation");
var update=await manager.CheckForUpdatesAsync() ?? throw new Exception("No update found");
await manager.DownloadUpdatesAsync(update,p=>Console.WriteLine($"progress {p}"));
Console.WriteLine("verified target "+update.TargetFullRelease.Version);
manager.WaitExitThenApplyUpdates(update.TargetFullRelease,silent:true,restart:true,restartArgs:new[]{"--smoke-test"});
Console.WriteLine("native helper launched; exiting for replacement");
class NativeLocator : TestVelopackLocator {
 private readonly IProcessImpl native;
 public NativeLocator(string root,string app) : base("DokhuStudio.Unfold.Updates","1.0.2-beta",Path.Combine(root,"packages"),
  Path.Combine(app,"Contents/MacOS"),app,Path.Combine(app,"Contents/MacOS/UpdateMac"),"osx-arm64-beta",
  processPath:Path.Combine(app,"Contents/MacOS/Unfold")) { native=new NativeProcess(root,app); }
 public override IProcessImpl Process => native;
}
class NativeProcess(string root,string app) : IProcessImpl {
 public string GetCurrentProcessPath()=>Path.Combine(app,"Contents/MacOS/Unfold");
 public uint GetCurrentProcessId()=>(uint)Environment.ProcessId;
 public void Exit(int exitCode)=>Environment.Exit(exitCode);
 public void StartProcess(string executable,IEnumerable<string> arguments,string workDir,bool showWindow) {
  var items=arguments.ToArray(); var info=new ProcessStartInfo(executable) {UseShellExecute=false,WorkingDirectory=workDir};
  foreach(var arg in items) info.ArgumentList.Add(arg);
  var child=Process.Start(info) ?? throw new Exception("Helper was not launched");
  File.WriteAllText(Path.Combine(root,"helper.json"),JsonSerializer.Serialize(new { executable,arguments=items,pid=child.Id }));
 }
}
