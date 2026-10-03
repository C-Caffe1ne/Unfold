using System.Diagnostics;

namespace Unfold.Desktop;

internal static class MediaConversion
{
    public static async Task Run(string executable, IEnumerable<string> arguments, string failure, string timeoutMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("미디어 변환을 시작하지 못했어요.");
        var errors = ReadErrors(process.StandardError);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync(CancellationToken.None); await errors;
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidDataException(timeoutMessage);
        }
        var details = await errors;
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidDataException(failure, new IOException(details));
    }
    private static async Task<string> ReadErrors(StreamReader reader)
    {
        var result = new System.Text.StringBuilder(); var buffer = new char[1024]; int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            if (result.Length < 2048) result.Append(buffer, 0, Math.Min(count, 2048 - result.Length));
        return result.ToString().Trim();
    }
}
