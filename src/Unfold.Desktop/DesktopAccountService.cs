using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Desktop;

internal sealed record AccountConnection(Uri ProjectUrl, string PublishableKey, int CallbackPort, AccountEnvironment Environment)
{
    public static AccountConnection? Load()
    {
        try
        {
            var file = Path.Combine(AccountScreenContent.AssetRoot, "connection.json");
            var config = JsonSerializer.Deserialize<AccountConnection>(File.ReadAllText(file), AccountScreenContent.JsonOptions);
            var url = System.Environment.GetEnvironmentVariable("UNFOLD_SUPABASE_URL");
            var key = System.Environment.GetEnvironmentVariable("UNFOLD_SUPABASE_PUBLISHABLE_KEY");
            if (config is null) return null;
            if (url is not null) config = config with { ProjectUrl = new Uri(url) };
            if (key is not null) config = config with { PublishableKey = key };
            if (config.ProjectUrl is null || config.CallbackPort is < 1024 or > 65535
                || string.IsNullOrWhiteSpace(config.PublishableKey) || !config.PublishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal)) return null;
            // Validate with the same origin/key contract as the HTTP client, without making a request.
            using var check = new SupabaseAccountClient(config.ProjectUrl, config.PublishableKey, config.Environment);
            return config;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or UriFormatException) { return null; }
    }
}

internal sealed class DesktopAccountService : IAccountScreenService
{
    private readonly AccountConnection? config;
    private readonly SupabaseAccountClient? client;
    private readonly string browserReturn;
    public bool CanSignIn => client is not null;
    public DesktopAccountService(AccountScreenContent content)
    {
        browserReturn = content.Copy.BrowserReturn;
        config = AccountConnection.Load();
        if (config is not null) client = new(config.ProjectUrl, config.PublishableKey, config.Environment);
    }
    public async Task<AccountSession> SignInAsync(CancellationToken token)
    {
        if (client is null || config is null) throw new AccountException(AccountFailure.Unavailable);
        try
        {
            // Bind before opening the browser. A busy port never launches a broken sign-in attempt.
            using var callback = new AccountLoopbackCallback(config.CallbackPort, browserReturn);
            var attempt = client.BeginGoogleSignIn(callback.RedirectUri);
            using var browser = Process.Start(new ProcessStartInfo(attempt.AuthorizationUri.AbsoluteUri) { UseShellExecute = true });
            var uri = await callback.ReceiveAsync(token);
            return await client.CompleteGoogleSignInAsync(attempt, uri, token);
        }
        catch (Exception error) when (error is SocketException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { throw new AccountException(AccountFailure.Unavailable); }
    }
    public Task<PurchaseAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token) =>
        client?.GetEntitlementAsync(session, token) ?? Task.FromException<PurchaseAccess>(new AccountException(AccountFailure.Unavailable));
    public void Dispose() => client?.Dispose();
}

/// <summary>A bounded, one-attempt HTTP callback bound exclusively to IPv4 loopback.</summary>
internal sealed class AccountLoopbackCallback : IDisposable
{
    private readonly TcpListener listener;
    private readonly string browserReturn;
    public Uri RedirectUri { get; }
    public AccountLoopbackCallback(int port, string browserReturn)
    {
        this.browserReturn = browserReturn;
        listener = new(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        listener.Start(4);
        RedirectUri = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/auth/callback");
    }
    public async Task<Uri> ReceiveAsync(CancellationToken token)
    {
        while (true)
        {
            using var socket = await listener.AcceptTcpClientAsync(token);
            await using var stream = socket.GetStream();
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var bytes = new byte[16 * 1024];
                var count = 0;
                var header = "";
                while (count < bytes.Length)
                {
                    var read = await stream.ReadAsync(bytes.AsMemory(count), requestTimeout.Token);
                    if (read == 0) break;
                    count += read; header = Encoding.ASCII.GetString(bytes, 0, count);
                    if (header.Contains("\r\n\r\n", StringComparison.Ordinal)) break;
                }
                var lines = header.Split("\r\n", StringSplitOptions.None);
                var request = lines[0].Split(' ');
                var host = lines.Where(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)).ToArray();
                var valid = count < bytes.Length && header.Contains("\r\n\r\n", StringComparison.Ordinal)
                    && request.Length == 3 && request[0] == "GET" && request[2] is "HTTP/1.0" or "HTTP/1.1"
                    && host.Length == 1 && host[0][5..].Trim() == RedirectUri.Authority
                    && request[1].StartsWith("/auth/callback?", StringComparison.Ordinal);
                Uri? callback = null;
                valid = valid && Uri.TryCreate(RedirectUri.GetLeftPart(UriPartial.Authority) + request[1], UriKind.Absolute, out callback)
                    && callback.AbsolutePath == RedirectUri.AbsolutePath && callback.Fragment.Length == 0;
                // OAuth cancellation may be returned as a fragment, which HTTP never sends.
                var cancelled = request.Length == 3 && request[0] == "GET" && request[1] == RedirectUri.AbsolutePath
                    && host.Length == 1 && host[0][5..].Trim() == RedirectUri.Authority;
                var body = Encoding.UTF8.GetBytes(valid || cancelled ? browserReturn : "Invalid callback request.");
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid || cancelled ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, requestTimeout.Token);
                await stream.WriteAsync(body, requestTimeout.Token);
                if (valid) return callback!;
                if (cancelled) throw new OperationCanceledException(token);
            }
            catch (OperationCanceledException error) when (!token.IsCancellationRequested && error.CancellationToken != token)
            { /* Drop incomplete HTTP requests, but propagate sign-in cancellation. */ }
            catch (IOException) when (!token.IsCancellationRequested) { /* Browser closed a connection; keep listening. */ }
        }
    }
    public void Dispose() => listener.Stop();
}
