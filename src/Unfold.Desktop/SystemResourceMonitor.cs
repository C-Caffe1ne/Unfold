using Avalonia.Threading;

namespace Unfold.Desktop;

/// <summary>Physical memory in use, excluding memory the system can hand out at once.</summary>
internal readonly record struct SystemMemoryUsage(ulong UsedBytes, ulong TotalBytes);
internal readonly record struct CpuTimeCounters(ulong Idle, ulong Total);
internal readonly record struct SystemResourceReading(CpuTimeCounters? Cpu, SystemMemoryUsage? Memory);
internal sealed record SystemResourceSnapshot(double? CpuPercent, SystemMemoryUsage? Memory, DateTimeOffset SampledAt)
{
    public static SystemResourceSnapshot Empty { get; } = new(null, null, DateTimeOffset.MinValue);
}

internal interface ISystemResourceSource
{
    SystemResourceReading Read(CancellationToken cancellationToken);
}

internal sealed class SystemResourceMonitor : IDisposable
{
    private readonly object stateLock = new();
    // Shared across sessions: a native call cannot be forcibly cancelled when the UI hides.
    private readonly SemaphoreSlim sampling = new(1, 1);
    private readonly ISystemResourceSource source;
    private readonly TimeSpan interval;
    private readonly TimeSpan maximumSampleGap;
    private readonly Action<Action> dispatch;
    private readonly Func<DateTimeOffset> utcNow;
    private CancellationTokenSource? cancellation;
    private SystemResourceSnapshot current = SystemResourceSnapshot.Empty;
    private long generation;
    private bool active;
    private bool disposed;

    public SystemResourceMonitor(ISystemResourceSource? source = null, TimeSpan? interval = null,
        Action<Action>? dispatch = null, Func<DateTimeOffset>? utcNow = null)
    {
        this.source = source ?? new NativeSystemResourceSource();
        this.interval = interval ?? TimeSpan.FromSeconds(5);
        if (this.interval <= TimeSpan.Zero || this.interval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(interval));
        maximumSampleGap = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(15).Ticks, this.interval.Ticks * 3));
        this.dispatch = dispatch ?? (action => Dispatcher.UIThread.Post(action));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public SystemResourceSnapshot Current { get { lock (stateLock) return current; } }
    public event Action? Changed;

    public void SetActive(bool enabled, bool visible) => ChangeSession(enabled && visible, false);
    public void Reset() => ChangeSession(null, true);

    private void ChangeSession(bool? requestedActive, bool force)
    {
        CancellationTokenSource? old;
        CancellationTokenSource? next;
        long nextGeneration;
        lock (stateLock)
        {
            if (disposed) return;
            var nextActive = requestedActive ?? active;
            if (!force && nextActive == active) return;
            old = cancellation;
            active = nextActive;
            nextGeneration = ++generation;
            current = SystemResourceSnapshot.Empty;
            next = active ? new CancellationTokenSource() : null;
            cancellation = next;
            old?.Cancel();
        }
        NotifyReset(nextGeneration);
        if (next is not null) _ = Task.Run(() => SampleLoop(next, nextGeneration));
    }

    private async Task SampleLoop(CancellationTokenSource session, long sessionGeneration)
    {
        var token = session.Token;
        CpuTimeCounters? previousCpu = null;
        DateTimeOffset? previousTime = null;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                SystemResourceReading reading;
                await sampling.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    reading = source.Read(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // Optional diagnostics must not interrupt the pet when the OS cannot provide them.
                    reading = default;
                }
                finally { sampling.Release(); }
                token.ThrowIfCancellationRequested();
                var time = utcNow();
                var cpu = ValidCpu(reading.Cpu) ? reading.Cpu : null;
                var memory = reading.Memory is { TotalBytes: > 0 } used && used.UsedBytes <= used.TotalBytes
                    ? reading.Memory : null;
                var percent = CpuPercent(previousCpu, cpu, previousTime, time, maximumSampleGap);
                previousCpu = cpu;
                previousTime = time;
                Publish(new(percent, memory, time), sessionGeneration);
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { session.Dispose(); }
    }

    private void Publish(SystemResourceSnapshot snapshot, long sessionGeneration)
    {
        dispatch(() =>
        {
            lock (stateLock)
            {
                if (disposed || !active || generation != sessionGeneration) return;
                current = snapshot;
            }
            Changed?.Invoke();
        });
    }

    private void NotifyReset(long sessionGeneration)
    {
        dispatch(() =>
        {
            lock (stateLock)
            {
                if (disposed || generation != sessionGeneration) return;
                // A later sample in the same session may already have reached the dispatcher.
                if (!ReferenceEquals(current, SystemResourceSnapshot.Empty)) return;
            }
            Changed?.Invoke();
        });
    }

    private static bool ValidCpu(CpuTimeCounters? cpu) => cpu is { } value && value.Idle <= value.Total;
    internal static double? CpuPercent(CpuTimeCounters? previous, CpuTimeCounters? current,
        DateTimeOffset? previousTime, DateTimeOffset time, TimeSpan maximumGap)
    {
        if (!ValidCpu(previous) || !ValidCpu(current) || previousTime is null) return null;
        var elapsed = time - previousTime.Value;
        if (elapsed <= TimeSpan.Zero || elapsed > maximumGap) return null;
        var before = previous!.Value;
        var after = current!.Value;
        if (after.Total <= before.Total || after.Idle < before.Idle) return null;
        var total = after.Total - before.Total;
        var idle = after.Idle - before.Idle;
        return idle <= total ? 100d * (total - idle) / total : null;
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
            active = false;
            generation++;
            current = SystemResourceSnapshot.Empty;
            cancellation?.Cancel();
            cancellation = null;
            Changed = null;
        }
        // In-flight native reads still own the semaphore; it has no OS handle unless requested.
    }
}
