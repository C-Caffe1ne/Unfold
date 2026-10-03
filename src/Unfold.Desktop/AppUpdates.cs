using System.Runtime.InteropServices;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace Unfold.Desktop;

internal static class AppUpdateConfiguration
{
    // Keep the managed Windows installation separate from LOCALAPPDATA/Unfold user data.
    internal const string PackageId = "DokhuStudio.Unfold.Updates";
    internal const string Repository = "https://github.com/C-Caffe1ne/Unfold";
    internal static string Channel(string version, string runtime) =>
        runtime is "win-x64" or "win-arm64" or "osx-arm64" or "osx-x64"
            ? $"{runtime}-{(version.Contains('-') ? "beta" : "stable")}" : throw new PlatformNotSupportedException();
    internal static string Runtime => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "unsupported") +
        (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "-arm64" : "-x64");
}

internal enum AppUpdateState { Idle, UnsupportedInstall, Checking, Current, Available, Downloading, Ready, Applying, Error }
internal sealed record AppUpdateRelease(string Version, object Package);

internal interface IAppUpdateBackend
{
    bool IsInstalled { get; }
    AppUpdateRelease? Pending { get; }
    Task<AppUpdateRelease?> Check(CancellationToken token);
    Task Download(AppUpdateRelease release, Action<int> progress, CancellationToken token);
    void ApplyAfterExit(AppUpdateRelease release);
}

internal sealed class VelopackUpdateBackend : IAppUpdateBackend
{
    private readonly UpdateManager manager;
    private readonly string channel;
    internal VelopackUpdateBackend(UpdateManager? manager = null, string? channel = null)
    {
        this.channel = channel ?? AppUpdateConfiguration.Channel(AppRelease.Version, AppUpdateConfiguration.Runtime);
        this.manager = manager ?? new UpdateManager(new GithubSource(AppUpdateConfiguration.Repository, null,
            AppRelease.Version.Contains('-')), new UpdateOptions
            {
                ExplicitChannel = this.channel,
                AllowVersionDowngrade = false
            }, VelopackLocator.CreateDefaultForPlatform());
    }
    public bool IsInstalled => manager.IsInstalled && manager.AppId == AppUpdateConfiguration.PackageId;
    public AppUpdateRelease? Pending => manager.UpdatePendingRestart is { } asset &&
        IsExpectedPackage(asset) && asset.Version > manager.CurrentVersion
        ? new(asset.Version.ToString(), asset) : null;
    private bool IsExpectedPackage(VelopackAsset asset) => asset.PackageId == AppUpdateConfiguration.PackageId &&
        asset.Type == VelopackAssetType.Full && asset.FileName == Path.GetFileName(asset.FileName) &&
        asset.FileName.EndsWith($"-{channel}-full.nupkg", StringComparison.OrdinalIgnoreCase);
    public async Task<AppUpdateRelease?> Check(CancellationToken token)
    {
        var update = await manager.CheckForUpdatesAsync().WaitAsync(token);
        if (update is not null && !IsExpectedPackage(update.TargetFullRelease))
            throw new InvalidDataException("The update package does not match this application and release channel.");
        return update is null ? null : new(update.TargetFullRelease.Version.ToString(), update);
    }
    public Task Download(AppUpdateRelease release, Action<int> progress, CancellationToken token) =>
        manager.DownloadUpdatesAsync((UpdateInfo)release.Package, progress, token);
    public void ApplyAfterExit(AppUpdateRelease release) => manager.WaitExitThenApplyUpdates(
        release.Package is UpdateInfo info ? info.TargetFullRelease : (VelopackAsset)release.Package,
        silent: false, restart: true);
}

/// <summary>One update operation per app; closing the update window does not lose its download.</summary>
internal sealed class AppUpdates : IDisposable
{
    private readonly IAppUpdateBackend backend;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    internal event Action? Changed;
    internal AppUpdateState State { get; private set; }
    internal AppUpdateRelease? Release { get; private set; }
    internal bool Downloaded { get; private set; }
    internal int Progress { get; private set; }
    internal string? Error { get; private set; }
    internal bool Busy => State is AppUpdateState.Checking or AppUpdateState.Downloading or AppUpdateState.Applying;

    internal AppUpdates(IAppUpdateBackend? backend = null)
    {
        this.backend = backend ?? new VelopackUpdateBackend();
        try
        {
            if (!this.backend.IsInstalled) { State = AppUpdateState.UnsupportedInstall; return; }
            if (this.backend.Pending is { } pending) { Release = pending; Downloaded = true; State = AppUpdateState.Ready; }
        }
        catch (Exception error) { Fail(error); }
    }

    internal async Task Check()
    {
        if (disposed || Busy || Downloaded || State == AppUpdateState.UnsupportedInstall) return;
        Error = null; State = AppUpdateState.Checking; Notify();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var release = await backend.Check(timeout.Token);
            if (disposed) return;
            Release = release; State = release is null ? AppUpdateState.Current : AppUpdateState.Available;
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error) { if (!disposed) Fail(error); }
        finally { if (!disposed) Notify(); }
    }

    internal async Task Download()
    {
        if (disposed || Busy || Downloaded || Release is not { } release) return;
        Error = null; Progress = 0; State = AppUpdateState.Downloading; Notify();
        try
        {
            await backend.Download(release, progress =>
            {
                if (disposed || State != AppUpdateState.Downloading) return;
                Progress = Math.Clamp(progress, 0, 100); Notify();
            }, lifetime.Token);
            if (disposed) return;
            Downloaded = true; Progress = 100; State = AppUpdateState.Ready;
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error) { if (!disposed) Fail(error); }
        finally { if (!disposed) Notify(); }
    }

    internal bool PrepareApply()
    {
        if (disposed || Busy || !Downloaded || Release is not { } release) return false;
        try
        {
            backend.ApplyAfterExit(release);
            Error = null; State = AppUpdateState.Applying; Notify(); return true;
        }
        catch (Exception error) { Fail(error); Notify(); return false; }
    }
    private void Fail(Exception error)
    {
        AppPaths.Log(error); State = AppUpdateState.Error;
        Error = error switch
        {
            ChecksumFailedException => "업데이트 파일을 확인하지 못했어요. 다시 다운로드해 주세요.",
            AcquireLockFailedException => "다른 업데이트 작업이 진행 중이에요. 잠시 후 다시 시도해 주세요.",
            _ when Downloaded => "업데이트를 적용하지 못했어요. 다시 시도해 주세요.",
            _ => "업데이트 정보를 가져오거나 파일을 다운로드하지 못했어요. 연결을 확인하고 다시 시도해 주세요."
        };
    }
    internal void ReportApplyError(Exception error) { if (!disposed) { Fail(error); Notify(); } }
    private void Notify() => Changed?.Invoke();
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); Changed = null; lifetime.Dispose(); }
}
