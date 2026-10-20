using System.Collections.Concurrent;
using Unfold.Desktop;

namespace Unfold.Tests;

public class SystemResourceMonitorTests
{
    private static readonly SystemMemoryUsage Memory =
        new(512_000_000, 1_024_000_000, SystemMemoryAccounting.IncludesFileCache);
    private static readonly TimeSpan TestInterval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task DisabledOrHiddenMonitorNeverCallsTheSource()
    {
        var source = new TestSource((call, _) => Reading(call));
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action());
        monitor.SetActive(false, true);
        await Task.Delay(40, TestContext.Current.CancellationToken);
        monitor.SetActive(true, false);
        await Task.Delay(40, TestContext.Current.CancellationToken);
        monitor.Reset();
        await Task.Delay(40, TestContext.Current.CancellationToken);

        Assert.Equal(0, source.Calls);
        Assert.Same(SystemResourceSnapshot.Empty, monitor.Current);
    }

    [Fact]
    public async Task FirstSampleShowsMemoryAndTheNextComputesSystemCpu()
    {
        var clock = new TestClock();
        var source = new TestSource((call, _) => { clock.Advance(TimeSpan.FromSeconds(5)); return Reading(call); });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        var samples = Observe(monitor);
        monitor.SetActive(true, true);
        await Until(() => samples.Count >= 2);
        monitor.SetActive(false, true);
        var captured = samples.ToArray();

        Assert.Null(captured[0].CpuPercent);
        Assert.Equal(Memory, captured[0].Memory);
        Assert.Equal(80d, captured[1].CpuPercent);
        Assert.Equal(1, source.MaximumConcurrent);
    }

    [Fact]
    public async Task ResetAndReopeningDiscardThePreviousCpuBaseline()
    {
        var clock = new TestClock();
        var source = new TestSource((call, _) => { clock.Advance(TimeSpan.FromSeconds(5)); return Reading(call); });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        var samples = Observe(monitor);
        monitor.SetActive(true, true);
        await Until(() => monitor.Current.CpuPercent is not null);

        monitor.SetActive(true, false);
        Assert.Same(SystemResourceSnapshot.Empty, monitor.Current);
        var reopeningIndex = samples.Count;
        monitor.SetActive(true, true);
        await Until(() => samples.Count > reopeningIndex);
        Assert.Null(samples.ToArray()[reopeningIndex].CpuPercent);
        await Until(() => monitor.Current.CpuPercent is not null);

        monitor.SetActive(false, true);
        var resetIndex = samples.Count;
        monitor.SetActive(true, true);
        monitor.Reset();
        await Until(() => samples.Count > resetIndex);
        Assert.Null(samples.ToArray()[resetIndex].CpuPercent);
    }

    [Fact]
    public async Task LongAndBackwardClockGapsRestartTheBaseline()
    {
        var clock = new TestClock();
        var offsets = new[] { 0, 5, 60, 65, 64, 69 };
        var source = new TestSource((call, _) =>
        {
            clock.SetSeconds(offsets[Math.Min(call - 1, offsets.Length - 1)]);
            return Reading(call);
        });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        var samples = Observe(monitor);
        monitor.SetActive(true, true);
        await Until(() => samples.Count >= 6);
        monitor.SetActive(false, true);

        Assert.Equal(new double?[] { null, 80, null, 80, null, 80 },
            samples.Take(6).Select(sample => sample.CpuPercent).ToArray());
    }

    [Fact]
    public void InvalidCounterDifferencesAreUnknownRatherThanInventedPercentages()
    {
        var time = DateTimeOffset.UtcNow;
        var before = new CpuTimeCounters(20, 100);
        var maximumGap = TimeSpan.FromSeconds(15);
        foreach (var after in new[]
        {
            new CpuTimeCounters(20, 100), // No ticks advanced.
            new CpuTimeCounters(10, 200), // Idle counter rolled back.
            new CpuTimeCounters(10, 50),  // Total counter rolled back.
            new CpuTimeCounters(190, 200), // Idle delta exceeds total delta.
            new CpuTimeCounters(201, 200)  // Invalid raw counters.
        })
            Assert.Null(SystemResourceMonitor.CpuPercent(before, after, time, time.AddSeconds(5), maximumGap));
        Assert.Equal(0d, SystemResourceMonitor.CpuPercent(before, new(120, 200), time, time.AddSeconds(5), maximumGap));
        Assert.Equal(100d, SystemResourceMonitor.CpuPercent(before, new(20, 200), time, time.AddSeconds(5), maximumGap));
    }

    [Fact]
    public async Task HidingAndReopeningDuringANativeReadKeepsOneCollectionAndDropsItsOldResult()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var clock = new TestClock();
        var source = new TestSource((call, _) =>
        {
            if (call == 1) { entered.Set(); release.Wait(); }
            clock.Advance(TimeSpan.FromSeconds(5));
            return Reading(call);
        });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        var samples = Observe(monitor);
        try
        {
            monitor.SetActive(true, true);
            await Until(() => entered.IsSet);
            monitor.SetActive(false, true);
            monitor.SetActive(true, true);
            await Task.Delay(40, TestContext.Current.CancellationToken);
            Assert.Equal(1, source.Calls);
            Assert.Empty(samples);
            release.Set();
            await Until(() => samples.Count >= 1);

            Assert.Equal(1, source.MaximumConcurrent);
            Assert.Null(samples.First().CpuPercent);
            Assert.Equal(clock.Epoch.AddSeconds(10), samples.First().SampledAt);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task QueuedUiPostsCannotPublishAfterHideOrDispose()
    {
        var posts = new ConcurrentQueue<Action>();
        var source = new TestSource((call, _) => Reading(call));
        using var monitor = new SystemResourceMonitor(source, TestInterval, posts.Enqueue);
        var samples = Observe(monitor);
        monitor.SetActive(true, true);
        await Until(() => posts.Count >= 2);
        monitor.SetActive(true, false);
        while (posts.TryDequeue(out var post)) post();
        Assert.Empty(samples);
        Assert.Same(SystemResourceSnapshot.Empty, monitor.Current);

        monitor.SetActive(true, true);
        await Until(() => posts.Count >= 2);
        monitor.Dispose();
        while (posts.TryDequeue(out var post)) post();
        Assert.Empty(samples);
        Assert.Same(SystemResourceSnapshot.Empty, monitor.Current);
    }

    [Fact]
    public async Task PartialAndThrownFailuresRecoverWithoutReusingOldCpuCounters()
    {
        var clock = new TestClock();
        var source = new TestSource((call, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            if (call == 2) return new(null, Memory);
            if (call == 3) throw new IOException("Unavailable sample.");
            return Reading(call);
        });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        var samples = Observe(monitor);
        monitor.SetActive(true, true);
        await Until(() => samples.Count >= 5);
        monitor.SetActive(false, true);
        var captured = samples.ToArray();

        Assert.Null(captured[1].CpuPercent); Assert.Equal(Memory, captured[1].Memory);
        Assert.Null(captured[2].CpuPercent); Assert.Null(captured[2].Memory);
        Assert.Null(captured[3].CpuPercent); Assert.Equal(Memory, captured[3].Memory);
        Assert.Equal(80d, captured[4].CpuPercent);
    }

    [Fact]
    public async Task InvalidMemoryDoesNotHideAValidCpuMeasurement()
    {
        var clock = new TestClock();
        var source = new TestSource((call, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            return Reading(call) with { Memory = new(20, 10, SystemMemoryAccounting.IncludesFileCache) };
        });
        using var monitor = new SystemResourceMonitor(source, TestInterval, action => action(), () => clock.Now);
        monitor.SetActive(true, true);
        await Until(() => monitor.Current.CpuPercent is not null);

        Assert.Equal(80d, monitor.Current.CpuPercent);
        Assert.Null(monitor.Current.Memory);
    }

    [Fact]
    public void MemoryDefinitionsKeepCacheAccountingAndRejectInvalidNativeValues()
    {
        var mac = NativeSystemResourceSource.MacMemory(16_384, 1, 4_096);
        Assert.Equal(new SystemMemoryUsage(12_288, 16_384, SystemMemoryAccounting.IncludesFileCache), mac);
        var windows = NativeSystemResourceSource.WindowsMemory(16_384, 8_192);
        Assert.Equal(new SystemMemoryUsage(8_192, 16_384, SystemMemoryAccounting.ExcludesAvailableMemory), windows);
        Assert.Null(NativeSystemResourceSource.MacMemory(0, 0, 4096));
        Assert.Null(NativeSystemResourceSource.MacMemory(100, 1, 0));
        Assert.Null(NativeSystemResourceSource.MacMemory(100, 1, 4096));
        Assert.Null(NativeSystemResourceSource.MacMemory(ulong.MaxValue, 2, ulong.MaxValue));
        Assert.Null(NativeSystemResourceSource.WindowsMemory(0, 0));
        Assert.Null(NativeSystemResourceSource.WindowsMemory(100, 101));
    }

    [Fact]
    [Trait("Category", "NativeResourceProbe")]
    public async Task MacNativeTwoSampleProbeReturnsPhysicalMemoryAndSystemCpuCounters()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal((16, 152, 64), NativeSystemResourceSource.NativeStructureSizes);
        Assert.Equal(0, NativeSystemResourceSource.ReadMacPageSize(out var pageSize));
        Assert.Equal((nuint)Environment.SystemPageSize, pageSize);
        Assert.True(pageSize > 0);
        var source = new NativeSystemResourceSource();
        var before = source.Read(TestContext.Current.CancellationToken);
        var beforeTime = DateTimeOffset.UtcNow;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var after = source.Read(TestContext.Current.CancellationToken);
        var afterTime = DateTimeOffset.UtcNow;

        Assert.NotNull(before.Cpu); Assert.NotNull(after.Cpu);
        Assert.NotNull(before.Memory); Assert.NotNull(after.Memory);
        var memory = Assert.IsType<SystemMemoryUsage>(after.Memory);
        Assert.True(memory.TotalBytes > 0);
        Assert.InRange(memory.UsedBytes, 0UL, memory.TotalBytes);
        Assert.Equal(SystemMemoryAccounting.IncludesFileCache, memory.Accounting);
        var percent = SystemResourceMonitor.CpuPercent(before.Cpu, after.Cpu, beforeTime, afterTime, TimeSpan.FromSeconds(15));
        Assert.InRange(Assert.IsType<double>(percent), 0d, 100d);
    }

    private static SystemResourceReading Reading(int call) => new(new((ulong)call * 20, (ulong)call * 100), Memory);
    private static ConcurrentQueue<SystemResourceSnapshot> Observe(SystemResourceMonitor monitor)
    {
        var samples = new ConcurrentQueue<SystemResourceSnapshot>();
        monitor.Changed += () =>
        {
            var value = monitor.Current;
            if (value.SampledAt != DateTimeOffset.MinValue) samples.Enqueue(value);
        };
        return samples;
    }
    private static async Task Until(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 500 && !predicate(); attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.True(predicate());
    }
    private sealed class TestClock
    {
        public DateTimeOffset Epoch { get; } = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        private long offsetTicks;
        public DateTimeOffset Now => Epoch.AddTicks(Interlocked.Read(ref offsetTicks));
        public void Advance(TimeSpan amount) => Interlocked.Add(ref offsetTicks, amount.Ticks);
        public void SetSeconds(int seconds) => Interlocked.Exchange(ref offsetTicks, TimeSpan.FromSeconds(seconds).Ticks);
    }
    private sealed class TestSource(Func<int, CancellationToken, SystemResourceReading> read) : ISystemResourceSource
    {
        private int calls, active, maximumConcurrent;
        public int Calls => Volatile.Read(ref calls);
        public int MaximumConcurrent => Volatile.Read(ref maximumConcurrent);
        public SystemResourceReading Read(CancellationToken cancellationToken)
        {
            var concurrent = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref maximumConcurrent); }
            while (observed < concurrent && Interlocked.CompareExchange(ref maximumConcurrent, concurrent, observed) != observed);
            try { return read(Interlocked.Increment(ref calls), cancellationToken); }
            finally { Interlocked.Decrement(ref active); }
        }
    }
}
