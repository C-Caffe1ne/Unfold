using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Desktop;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
    throw new Exception("Use a disposable Windows CI runner.");
var root = Path.GetFullPath(args[0]);
var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppUpdateConfiguration.PackageId);
if (!string.Equals(root, expected, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected installation.");
var evidence = args[1];
var locator = new WindowsVelopackLocator(new ProbeProcess(root, evidence), new NullVelopackLogger());
if (locator.RootAppDir != root || locator.CurrentlyInstalledVersion?.ToString() != "1.1.1-beta")
    throw new Exception("Wrong installed baseline.");
File.WriteAllText(Path.Combine(evidence,"before-check.json"),JsonSerializer.Serialize(new { installedVersion=locator.CurrentlyInstalledVersion!.ToString(), channel=locator.Channel, isolatedWindowsCI=true }));
// Hosted runners share anonymous GitHub API quotas. The job token has contents:read only.
var token = Environment.GetEnvironmentVariable("UNFOLD_QA_GITHUB_TOKEN") ?? throw new Exception("Missing temporary CI read token.");
var manager = new UpdateManager(new GithubSource(AppUpdateConfiguration.Repository, token, true),
    new UpdateOptions { ExplicitChannel = "win-x64-beta", AllowVersionDowngrade = false }, locator);
using var updates = new AppUpdates(new VelopackUpdateBackend(manager, "win-x64-beta"));
await updates.Check();
if (updates.State != AppUpdateState.Available || updates.Release?.Version != "1.1.2-beta")
    throw new Exception($"Check failed: {updates.State}: {updates.Error}");
if (updates.Release!.NotesMarkdown?.Contains("시작 시 업데이트 안내") != true) throw new Exception("Release notes missing.");
await updates.Download();
if (!updates.Downloaded) throw new Exception($"Download failed: {updates.Error}");
var package = Directory.GetFiles(locator.PackagesDir!, "*1.1.2-beta*-full.nupkg").Single();
using var stream = File.OpenRead(package);
var digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
if (digest != "6d176691dff660a368d7e0d33143f9cd3f7340eef580475747c93f7396bd2a02")
    throw new Exception("Published package changed.");
stream.Close();
File.WriteAllText(Path.Combine(evidence,"before-apply.json"),JsonSerializer.Serialize(new { fromVersion="1.1.1-beta", version=updates.Release!.Version, packageSha256=digest, state=updates.State.ToString() }));
if (!updates.PrepareApply()) throw new Exception($"Apply failed to start: {updates.Error}");
Console.WriteLine("Real Update.exe started; exiting the awaited process.");

sealed class ProbeProcess(string root, string evidence) : IProcessImpl
{
    public string GetCurrentProcessPath() => Path.Combine(root, "current", "Unfold.exe");
    public uint GetCurrentProcessId() => (uint)Environment.ProcessId;
    public void Exit(int code) => Environment.Exit(code);
    public void StartProcess(string executable, IEnumerable<string> arguments, string working, bool showWindow)
    {
        if (!string.Equals(executable, Path.Combine(root,"Update.exe"), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Unexpected updater helper.");
        var original = arguments.ToList();
        if (!original.Contains("apply") || original.Contains("--norestart")) throw new Exception("Wrong apply request.");
        File.WriteAllText(Path.Combine(evidence,"helper-command.json"),JsonSerializer.Serialize(new { helper="Update.exe", arguments=original, restartTestArgument="--smoke-test" }));
        var info = new ProcessStartInfo(executable) { WorkingDirectory=working, UseShellExecute=false };
        foreach (var value in original) info.ArgumentList.Add(value);
        info.ArgumentList.Add("--"); info.ArgumentList.Add("--smoke-test");
        using var child = System.Diagnostics.Process.Start(info) ?? throw new Exception("Updater helper failed to start.");
    }
}
namespace Unfold.Desktop
{
    internal static class AppRelease { internal const string Version="1.1.1-beta"; }
    internal static class AppPaths { internal static void Log(Exception error) => Console.Error.WriteLine(error); }
}
