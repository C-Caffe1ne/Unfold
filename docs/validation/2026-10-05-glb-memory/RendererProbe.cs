using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Core;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: <model.glb>"); return 2;
}
var model = GlbModel.Load(args[0]);
var name = model.Animations.FirstOrDefault(a => a.Name.Contains("idle", StringComparison.OrdinalIgnoreCase))?.Name ?? model.Animations[0].Name;
var clip = model.CreateAnimation(name, new("model.glb"), name);
var results = new List<object>();
foreach (var size in new[] { 192, 384, 576, 1024 })
{
    for (var i = 1; i <= 5; i++) _ = clip.GetFrame(i % clip.Count, size);
    const int count = 40;
    var hashes = new string[count]; var times = new double[count]; var memories = new long[count];
    var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    long allocated = 0; PixelImage? last = null;
    for (var i = 0; i < count; i++)
    {
        var start = GC.GetAllocatedBytesForCurrentThread(); var tick = Stopwatch.GetTimestamp();
        last = clip.GetFrame((i + 10) % clip.Count, size).Image;
        times[i] = Stopwatch.GetElapsedTime(tick).TotalMilliseconds;
        allocated += GC.GetAllocatedBytesForCurrentThread() - start;
        hashes[i] = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(last.Pixels.AsSpan())));
        using var p = Process.GetCurrentProcess(); memories[i] = p.WorkingSet64;
        Thread.Sleep(50);
    }
    results.Add(new { size, count, allocatedBytesPerFrame = allocated / count, renderMsAverage = times.Average(), workingSetMax = memories.Max(), workingSetLast = memories[^1], managedBytes = GC.GetTotalMemory(false), genCollections = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - collections[g]), hashes });
    GC.KeepAlive(last);
}
Console.WriteLine(JsonSerializer.Serialize(new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription, coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(GlbModel).Assembly.Location))), model = Path.GetFileName(args[0]), model.TriangleCount, clip = name, samples = results }, new JsonSerializerOptions { WriteIndented = true }));

return 0;
