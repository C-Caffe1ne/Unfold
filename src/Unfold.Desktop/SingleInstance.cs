using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Unfold.Desktop;

internal sealed class SingleInstance : IDisposable
{
    private static string PipeName => "unfold-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppPaths.DataRoot)))[..24];
    private readonly CancellationTokenSource stop = new();
    public SingleInstance(Action activate) { _ = Listen(activate); }
    public static void RequestActivation()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500); client.WriteByte(1);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException) { AppPaths.Log(ex); }
    }
    private async Task Listen(Action activate)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(stop.Token);
                var data = new byte[1];
                if (await server.ReadAsync(data, stop.Token) == 1 && data[0] == 1) Avalonia.Threading.Dispatcher.UIThread.Post(activate);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException error) { AppPaths.Log(error); await Task.Delay(500, CancellationToken.None); }
        }
    }
    public void Dispose() { stop.Cancel(); }
}
