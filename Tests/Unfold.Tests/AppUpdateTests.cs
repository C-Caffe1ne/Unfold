using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Desktop;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class AppUpdateTests : IDisposable
{
    private readonly TempDirectory temp = new();
    private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
    public AppUpdateTests() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
    public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }

    [Theory]
    [InlineData("1.0.3-beta", "win-x64", "win-x64-beta")]
    [InlineData("1.0.3-beta", "win-arm64", "win-arm64-beta")]
    [InlineData("1.0.3-beta", "osx-arm64", "osx-arm64-beta")]
    [InlineData("1.0.3-beta", "osx-x64", "osx-x64-beta")]
    [InlineData("1.0.3", "win-x64", "win-x64-stable")]
    [InlineData("1.0.3", "win-arm64", "win-arm64-stable")]
    [InlineData("1.0.3", "osx-arm64", "osx-arm64-stable")]
    [InlineData("1.0.3", "osx-x64", "osx-x64-stable")]
    public void OperatingSystemsArchitecturesAndReleaseTracksHaveDistinctFeeds(string version, string runtime, string expected) =>
        Assert.Equal(expected, AppUpdateConfiguration.Channel(version, runtime));

    [Fact]
    public async Task LegacyInstallNeverDownloadsOrAppliesAndDoesNotPretendToBeCurrent()
    {
        var backend = new UpdateTestBackend { IsInstalled = false };
        using var updates = new AppUpdates(backend);
        await updates.Check(); await updates.Download(); Assert.False(updates.PrepareApply());
        Assert.Equal(AppUpdateState.UnsupportedInstall, updates.State);
        Assert.Equal(0, backend.Checks); Assert.Equal(0, backend.Downloads); Assert.Equal(0, backend.Applies);
    }

    [Fact]
    public async Task RepeatedCheckAndDownloadAreCoalescedWithoutPrematureRestart()
    {
        var backend = new UpdateTestBackend { CheckGate = new(), DownloadGate = new() };
        using var updates = new AppUpdates(backend);
        var checking = updates.Check(); await updates.Check();
        Assert.Equal(1, backend.Checks); Assert.False(updates.PrepareApply());
        backend.CheckGate.SetResult(); await checking;
        var downloading = updates.Download(); await updates.Download(); await updates.Check();
        Assert.Equal(1, backend.Downloads); Assert.False(updates.PrepareApply());
        backend.ReportProgress?.Invoke(180); Assert.Equal(100, updates.Progress);
        backend.DownloadGate.SetResult(); await downloading;
        Assert.True(updates.Downloaded); Assert.Equal(AppUpdateState.Ready, updates.State); Assert.Equal(0, backend.Applies);
        Assert.True(updates.PrepareApply()); Assert.False(updates.PrepareApply()); Assert.Equal(1, backend.Applies);
    }

    [Fact]
    public async Task NetworkFailureCanBeRetriedAndDownloadFailureCannotBeApplied()
    {
        var backend = new UpdateTestBackend { CheckFailure = new IOException("offline") };
        using var updates = new AppUpdates(backend);
        await updates.Check(); Assert.Equal(AppUpdateState.Error, updates.State); Assert.Null(updates.Release);
        backend.CheckFailure = null; await updates.Check(); Assert.Equal(AppUpdateState.Available, updates.State);
        backend.DownloadFailure = new IOException("incomplete"); await updates.Download();
        Assert.False(updates.Downloaded); Assert.False(updates.PrepareApply());
        backend.DownloadFailure = null; await updates.Download(); Assert.True(updates.Downloaded);
    }

    [Fact]
    public async Task ApplyFailureKeepsDownloadedPackageForRetry()
    {
        var backend = new UpdateTestBackend { Pending = new("1.0.4-beta", new object()), ApplyFailure = new IOException("locked") };
        using var updates = new AppUpdates(backend);
        Assert.False(updates.PrepareApply()); Assert.True(updates.Downloaded); Assert.Equal(AppUpdateState.Error, updates.State);
        backend.ApplyFailure = null; Assert.True(updates.PrepareApply()); Assert.Equal(2, backend.Applies);
        await updates.Download(); Assert.Equal(0, backend.Downloads);
    }

    [Fact]
    public async Task DisposeCancelsAnInFlightDownloadAndSuppressesLateNotifications()
    {
        var backend = new UpdateTestBackend { DownloadGate = new() };
        var updates = new AppUpdates(backend); await updates.Check();
        var changed = 0; updates.Changed += () => changed++;
        var download = updates.Download(); updates.Dispose(); var before = changed;
        backend.ReportProgress?.Invoke(90); await download;
        Assert.Equal(before, changed); Assert.False(updates.Downloaded); Assert.False(updates.PrepareApply());
    }

    [Fact]
    public async Task ActualSdkDownloadsAndVerifiesAValidPackageFromTheMatchingFeed()
    {
        var fixture = CreateFeed("1.0.4-beta");
        var manager = Manager(fixture.Feed);
        using var updates = new AppUpdates(new VelopackUpdateBackend(manager, "osx-arm64-beta"));
        await updates.Check(); Assert.Equal(AppUpdateState.Available, updates.State);
        Assert.Equal("1.0.4-beta", updates.Release!.Version);
        Assert.Equal("- 업데이트 안내 개선", updates.Release.NotesMarkdown);
        await updates.Download(); Assert.True(updates.Downloaded);
        Assert.Equal(fixture.PackageHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(temp.Path, "packages", Path.GetFileName(fixture.Package))))));
        using var restored = new AppUpdates(new VelopackUpdateBackend(manager, "osx-arm64-beta"));
        Assert.Equal(AppUpdateState.Ready, restored.State); Assert.True(restored.Downloaded);
        Assert.Equal("- 업데이트 안내 개선", restored.Release!.NotesMarkdown);
    }

    [Fact]
    public async Task ActualSdkRejectsCorruptDownloadsBeforeTheyCanBeApplied()
    {
        var fixture = CreateFeed("1.0.4-beta");
        using var updates = new AppUpdates(new VelopackUpdateBackend(Manager(fixture.Feed), "osx-arm64-beta"));
        await updates.Check(); File.WriteAllText(fixture.Package, "tampered package");
        await updates.Download(); Assert.Equal(AppUpdateState.Error, updates.State);
        Assert.Contains("업데이트 파일을 확인", updates.Error); Assert.False(updates.Downloaded); Assert.False(updates.PrepareApply());
    }

    [Fact]
    public async Task AFeedWithAnotherApplicationIdCannotBeDownloaded()
    {
        var fixture = CreateFeed("1.0.4-beta");
        var index = Path.Combine(fixture.Feed, "releases.osx-arm64-beta.json");
        File.WriteAllText(index, File.ReadAllText(index).Replace(AppUpdateConfiguration.PackageId, "Another.Application"));
        using var updates = new AppUpdates(new VelopackUpdateBackend(Manager(fixture.Feed), "osx-arm64-beta"));
        await updates.Check(); Assert.Equal(AppUpdateState.Error, updates.State); Assert.Null(updates.Release);
        await updates.Download(); Assert.False(updates.Downloaded); Assert.False(updates.PrepareApply());
    }

    [Theory]
    [InlineData("1.0.3-beta")]
    [InlineData("1.0.2-beta")]
    public async Task ActualSdkDoesNotOfferTheSameVersionOrADowngrade(string target)
    {
        var fixture = CreateFeed(target);
        using var updates = new AppUpdates(new VelopackUpdateBackend(Manager(fixture.Feed), "osx-arm64-beta"));
        await updates.Check(); Assert.Equal(AppUpdateState.Current, updates.State); Assert.Null(updates.Release);
    }

    [Fact]
    public void LoginLaunchUsesAStableStubAndKeepsUserDataOutsideTheManagedInstallation()
    {
        var root = Path.Combine(temp.Path, AppUpdateConfiguration.PackageId); Directory.CreateDirectory(root);
        var stub = Path.Combine(root, "Unfold.exe"); File.WriteAllText(stub, "launcher");
        Assert.Equal(stub, PlatformServices.LoginExecutable(Path.Combine(root, "current", "Unfold.exe"), root));
        Assert.Equal("legacy.exe", PlatformServices.LoginExecutable("legacy.exe", null));
        Assert.NotEqual("Unfold", AppUpdateConfiguration.PackageId);
        Assert.NotEqual("DokhuStudio.Unfold", AppUpdateConfiguration.PackageId);
    }

    [Fact]
    public void UninstallOnlyRemovesTheStartupEntryBelongingToThisManagedInstallation()
    {
        var root = Path.Combine(temp.Path, AppUpdateConfiguration.PackageId);
        var command = $"\"{Path.Combine(root, "Unfold.exe")}\" --background";
        Assert.True(PlatformServices.MatchesManagedLogin(command, root));
        Assert.False(PlatformServices.MatchesManagedLogin(command, root + "-another-install"));
        Assert.False(PlatformServices.MatchesManagedLogin(command, null));
        Assert.False(PlatformServices.MatchesManagedLogin("legacy.exe --background", root));
    }

    [Theory]
    [InlineData("osx-x64-beta")]
    [InlineData("osx-arm64-stable")]
    [InlineData("win-x64-beta")]
    public async Task AnotherChannelCannotBecomeAPendingOrRemoteUpdate(string wrongChannel)
    {
        var fixture = CreateFeed("1.0.4-beta", wrongChannel);
        var packages = Path.Combine(temp.Path, "packages"); Directory.CreateDirectory(packages);
        File.Copy(fixture.Package, Path.Combine(packages, Path.GetFileName(fixture.Package)));
        File.Copy(Path.Combine(fixture.Feed, $"releases.{wrongChannel}.json"),
            Path.Combine(fixture.Feed, "releases.osx-arm64-beta.json"));
        using var updates = new AppUpdates(new VelopackUpdateBackend(Manager(fixture.Feed), "osx-arm64-beta"));
        Assert.Equal(AppUpdateState.Idle, updates.State); Assert.False(updates.Downloaded);
        await updates.Check(); Assert.Equal(AppUpdateState.Error, updates.State);
        Assert.Null(updates.Release); Assert.False(updates.PrepareApply());
    }

    private UpdateManager Manager(string feed) => new(new SimpleFileSource(new DirectoryInfo(feed)),
        new UpdateOptions { ExplicitChannel = "osx-arm64-beta", AllowVersionDowngrade = false },
        new TestVelopackLocator(AppUpdateConfiguration.PackageId, "1.0.3-beta", Path.Combine(temp.Path, "packages")));

    private (string Feed, string Package, string PackageHash) CreateFeed(string version, string channel = "osx-arm64-beta")
    {
        var feed = Path.Combine(temp.Path, "feed"); Directory.CreateDirectory(feed);
        var name = $"{AppUpdateConfiguration.PackageId}-{version}-{channel}-full.nupkg";
        var package = Path.Combine(feed, name);
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("package.nuspec").Open());
            writer.Write($"<package><metadata><id>{AppUpdateConfiguration.PackageId}</id><version>{version}</version><authors>Unfold</authors><description>Isolated update test</description><channel>{channel}</channel><releaseNotes>- 업데이트 안내 개선</releaseNotes></metadata></package>");
        }
        var bytes = File.ReadAllBytes(package); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        File.WriteAllText(Path.Combine(feed, $"releases.{channel}.json"), JsonSerializer.Serialize(new { Assets = new[] {
            new { PackageId = AppUpdateConfiguration.PackageId, Version = version, Type = "Full", FileName = name,
                SHA1 = Convert.ToHexString(SHA1.HashData(bytes)), SHA256 = hash, Size = bytes.Length, NotesMarkdown = "- 업데이트 안내 개선" }
        } }));
        return (feed, package, hash);
    }
}

internal sealed class UpdateTestBackend : IAppUpdateBackend
{
    public bool IsInstalled { get; init; } = true;
    public AppUpdateRelease? Pending { get; init; }
    internal AppUpdateRelease? CheckResult = new("1.0.4-beta", new object());
    internal int Checks, Downloads, Applies;
    internal TaskCompletionSource? CheckGate, DownloadGate;
    internal Exception? CheckFailure, DownloadFailure, ApplyFailure;
    internal Action<int>? ReportProgress;
    public async Task<AppUpdateRelease?> Check(CancellationToken token)
    {
        Checks++; if (CheckGate is not null) await CheckGate.Task.WaitAsync(token);
        if (CheckFailure is not null) throw CheckFailure;
        return CheckResult;
    }
    public async Task Download(AppUpdateRelease release, Action<int> progress, CancellationToken token)
    {
        Downloads++; ReportProgress = progress; progress(12);
        if (DownloadGate is not null) await DownloadGate.Task.WaitAsync(token);
        if (DownloadFailure is not null) throw DownloadFailure;
    }
    public void ApplyAfterExit(AppUpdateRelease release) { Applies++; if (ApplyFailure is not null) throw ApplyFailure; }
}
