using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unfold.Core;

public enum AccountEnvironment { Test, Live }
public enum PurchaseAccess { Unowned, Active, Revoked }
public enum AccountFailure { AuthenticationRequired, Unavailable, InvalidResponse, InvalidCallback }

public sealed class AccountException(AccountFailure failure) : Exception($"Account request failed: {failure}")
{
    public AccountFailure Failure { get; } = failure;
}

// Kept in memory only here. OS credential storage belongs to the later desktop integration.
// Not records: generated ToString() must not expose tokens or PKCE verifiers.
public sealed class AccountSession(Guid userId, string accessToken, string refreshToken, DateTimeOffset expiresAt)
{
    public Guid UserId { get; } = userId;
    public string AccessToken { get; } = accessToken;
    public string RefreshToken { get; } = refreshToken;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => nameof(AccountSession);
}

public sealed class GoogleSignInAttempt
{
    internal GoogleSignInAttempt(Guid owner, Uri authorizationUri, Uri redirectUri, string verifier)
    { Owner = owner; AuthorizationUri = authorizationUri; RedirectUri = redirectUri; Verifier = verifier; }
    public Uri AuthorizationUri { get; }
    internal Guid Owner { get; }
    internal Uri RedirectUri { get; }
    internal string Verifier { get; }
    internal DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    private int consumed;
    internal bool Consume() => Interlocked.Exchange(ref consumed, 1) == 0;
    public override string ToString() => nameof(GoogleSignInAttempt);
}

/// <summary>Opt-in network client; not wired into AppRuntime until account UI and secure storage are ready.</summary>
public sealed class SupabaseAccountClient : IDisposable
{
    private readonly HttpClient http;
    private readonly Uri projectUrl;
    private readonly string publicKey;
    private readonly string environment;
    private readonly Guid owner = Guid.NewGuid();

    public SupabaseAccountClient(Uri projectUrl, string publicKey, AccountEnvironment environment, HttpMessageHandler? transport = null)
    {
        if (!projectUrl.IsAbsoluteUri || (projectUrl.Scheme != "https" && !(projectUrl.Scheme == "http" && projectUrl.IsLoopback))
            || projectUrl.UserInfo.Length != 0 || projectUrl.AbsolutePath != "/" || projectUrl.Query.Length != 0 || projectUrl.Fragment.Length != 0)
            throw new ArgumentException("Use an HTTPS project origin or local development origin.", nameof(projectUrl));
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        if (publicKey.Any(char.IsWhiteSpace)) throw new ArgumentException("Invalid public key.", nameof(publicKey));
        this.environment = environment switch { AccountEnvironment.Test => "test", AccountEnvironment.Live => "live", _ => throw new ArgumentOutOfRangeException(nameof(environment)) };
        this.projectUrl = projectUrl; this.publicKey = publicKey;
        http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    }

    public GoogleSignInAttempt BeginGoogleSignIn(Uri redirectUri)
    {
        if (!redirectUri.IsAbsoluteUri || redirectUri.Scheme != "http" || !redirectUri.IsLoopback
            || redirectUri.AbsolutePath != "/auth/callback" || redirectUri.Query.Length != 0
            || redirectUri.Fragment.Length != 0 || redirectUri.UserInfo.Length != 0)
            throw new ArgumentException("Use the registered local callback URI.", nameof(redirectUri));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = new Uri(projectUrl, "auth/v1/authorize?provider=google&redirect_to="
            + Uri.EscapeDataString(redirectUri.AbsoluteUri) + "&code_challenge=" + challenge + "&code_challenge_method=s256");
        return new(owner, authorization, redirectUri, verifier);
    }

    public async Task<AccountSession> CompleteGoogleSignInAsync(GoogleSignInAttempt attempt, Uri callback, CancellationToken cancellationToken = default)
    {
        if (attempt.Owner != owner || !callback.IsAbsoluteUri || callback.GetLeftPart(UriPartial.Path) != attempt.RedirectUri.GetLeftPart(UriPartial.Path)
            || callback.Fragment.Length != 0 || callback.UserInfo.Length != 0
            || DateTimeOffset.UtcNow - attempt.CreatedAt > TimeSpan.FromMinutes(10))
            throw new AccountException(AccountFailure.InvalidCallback);
        var pairs = callback.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).ToArray();
        var codes = pairs.Where(pair => pair[0] == "code").ToArray();
        if (pairs.Any(pair => pair[0] is "error" or "error_code") || codes.Length != 1 || codes[0].Length != 2
            || string.IsNullOrWhiteSpace(codes[0][1]) || codes[0][1].Length > 8192 || !attempt.Consume())
            throw new AccountException(AccountFailure.InvalidCallback);
        var code = Uri.UnescapeDataString(codes[0][1].Replace('+', ' '));
        var json = await SendAsync(HttpMethod.Post, "auth/v1/token?grant_type=pkce", null,
            new { auth_code = code, code_verifier = attempt.Verifier }, cancellationToken);
        return ReadSession(json);
    }

    public async Task<AccountSession> RefreshAsync(AccountSession session, CancellationToken cancellationToken = default)
    {
        var refreshed = ReadSession(await SendAsync(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token", null,
            new { refresh_token = session.RefreshToken }, cancellationToken));
        if (refreshed.UserId != session.UserId) throw new AccountException(AccountFailure.InvalidResponse);
        return refreshed;
    }

    public async Task<PurchaseAccess> GetEntitlementAsync(AccountSession session, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync(HttpMethod.Get, "functions/v1/get-entitlement", session.AccessToken, null, cancellationToken);
        EntitlementResponse? result;
        try { result = json.Deserialize<EntitlementResponse>(); }
        catch (JsonException) { throw new AccountException(AccountFailure.InvalidResponse); }
        if (result is null || result.Version != 1 || result.UserId != session.UserId || session.UserId == Guid.Empty
            || result.ProductId != "unfold" || result.Environment != environment)
            throw new AccountException(AccountFailure.InvalidResponse);
        return result.Status switch {
            "active" => PurchaseAccess.Active, "unowned" => PurchaseAccess.Unowned, "revoked" => PurchaseAccess.Revoked,
            _ => throw new AccountException(AccountFailure.InvalidResponse),
        };
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, string? token, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(projectUrl, path));
        request.Headers.Add("apikey", publicKey);
        request.Headers.Accept.Add(new("application/json"));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                || (path.StartsWith("auth/v1/token", StringComparison.Ordinal) && response.StatusCode == HttpStatusCode.BadRequest))
                throw new AccountException(AccountFailure.AuthenticationRequired);
            if (!response.IsSuccessStatusCode) throw new AccountException(AccountFailure.Unavailable);
            if (response.Content.Headers.ContentLength > 64 * 1024) throw new AccountException(AccountFailure.InvalidResponse);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            var bytes = new byte[64 * 1024 + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token);
                if (read == 0) break;
                length += read;
            }
            if (length == bytes.Length) throw new AccountException(AccountFailure.InvalidResponse);
            return JsonSerializer.Deserialize<JsonElement>(bytes.AsSpan(0, length));
        }
        catch (HttpRequestException) { throw new AccountException(AccountFailure.Unavailable); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new AccountException(AccountFailure.Unavailable); }
        catch (JsonException) { throw new AccountException(AccountFailure.InvalidResponse); }
    }

    private static AccountSession ReadSession(JsonElement json)
    {
        try
        {
            var session = json.Deserialize<SessionResponse>();
            if (session?.User is null || session.User.Id == Guid.Empty || session.User.Anonymous
                || string.IsNullOrWhiteSpace(session.AccessToken) || string.IsNullOrWhiteSpace(session.RefreshToken)
                || session.ExpiresIn is <= 0 or > 86400)
                throw new AccountException(AccountFailure.InvalidResponse);
            return new(session.User.Id, session.AccessToken, session.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(session.ExpiresIn));
        }
        catch (JsonException) { throw new AccountException(AccountFailure.InvalidResponse); }
    }
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public void Dispose() => http.Dispose();

    private sealed record SessionResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("user")] UserResponse User);
    private sealed record UserResponse([property: JsonPropertyName("id")] Guid Id, [property: JsonPropertyName("is_anonymous")] bool Anonymous);
    private sealed record EntitlementResponse(
        [property: JsonPropertyName("schema_version")] int Version,
        [property: JsonPropertyName("user_id")] Guid UserId,
        [property: JsonPropertyName("product_id")] string ProductId,
        [property: JsonPropertyName("environment")] string Environment,
        [property: JsonPropertyName("status")] string Status);
}
