using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class AccountClientTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Uri Callback = new("http://127.0.0.1:43821/auth/callback");
    private static AccountSession Session => new(UserId, "private-access", "private-refresh", DateTimeOffset.UtcNow.AddHours(1));
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static Dictionary<string, string> Query(Uri uri) => uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2))
        .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => handle(request, token);
    }
    private static SupabaseAccountClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle, AccountEnvironment mode = AccountEnvironment.Live)
        => new(new("https://project.supabase.co"), "public-key", mode, new Transport(handle));

    [Fact]
    public async Task GooglePkceChallengeMatchesOnlyTheExchangedVerifierAndAttemptIsSingleUse()
    {
        string? verifier = null;
        var calls = 0;
        using var client = Client(async (request, token) => {
            calls++;
            Assert.Equal("/auth/v1/token", request.RequestUri!.AbsolutePath);
            Assert.Equal("?grant_type=pkce", request.RequestUri.Query);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.Equal("one-time-code", body.GetProperty("auth_code").GetString());
            verifier = body.GetProperty("code_verifier").GetString();
            return Json(new { access_token = "private-access", refresh_token = "private-refresh", expires_in = 3600, user = new { id = UserId, email = "person@example.test" } });
        });
        var attempt = client.BeginGoogleSignIn(Callback);
        var query = Query(attempt.AuthorizationUri);
        Assert.Equal("google", query["provider"]); Assert.Equal("s256", query["code_challenge_method"]);
        Assert.Equal(Callback.AbsoluteUri, query["redirect_to"]);
        var session = await client.CompleteGoogleSignInAsync(attempt, new(Callback + "?code=one-time-code"), TestContext.Current.CancellationToken);
        Assert.Equal(UserId, session.UserId);
        Assert.Equal("person@example.test", session.Email);
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier!))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(expected, query["code_challenge"]);
        Assert.DoesNotContain(verifier!, attempt.AuthorizationUri.AbsoluteUri);
        Assert.DoesNotContain("private-", session.ToString());
        Assert.DoesNotContain("person@", session.ToString());
        await Assert.ThrowsAsync<AccountException>(() => client.CompleteGoogleSignInAsync(attempt, new(Callback + "?code=one-time-code"), TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
        Assert.NotEqual(query["code_challenge"], Query(client.BeginGoogleSignIn(Callback).AuthorizationUri)["code_challenge"]);
    }

    [Theory]
    [InlineData("http://evil.test/auth/callback?code=code")]
    [InlineData("http://127.0.0.1:43822/auth/callback?code=code")]
    [InlineData("http://127.0.0.1:43821/other?code=code")]
    [InlineData("http://127.0.0.1:43821/auth/callback?code=a&code=b")]
    [InlineData("http://127.0.0.1:43821/auth/callback?error=cancelled&code=code")]
    public async Task InvalidCallbacksNeverExchangeCredentials(string callback)
    {
        using var client = Client((_, _) => throw new Xunit.Sdk.XunitException("Network must not be called"));
        var error = await Assert.ThrowsAsync<AccountException>(() => client.CompleteGoogleSignInAsync(client.BeginGoogleSignIn(Callback), new(callback), TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.InvalidCallback, error.Failure);
    }

    [Theory]
    [InlineData("active", PurchaseAccess.Active)]
    [InlineData("unowned", PurchaseAccess.Unowned)]
    [InlineData("revoked", PurchaseAccess.Revoked)]
    public async Task ServerPurchaseStatesRemainDistinct(string status, PurchaseAccess expected)
    {
        using var client = Client((request, _) => {
            Assert.Equal("/functions/v1/get-entitlement", request.RequestUri!.AbsolutePath);
            Assert.Equal("", request.RequestUri.Query);
            Assert.Equal("private-access", request.Headers.Authorization!.Parameter);
            Assert.Equal("public-key", Assert.Single(request.Headers.GetValues("apikey")));
            return Task.FromResult(Json(new { schema_version = 1, user_id = UserId, product_id = "unfold", environment = "live", status, role = "member" }));
        });
        Assert.Equal(expected, await client.GetEntitlementAsync(Session, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("member", AccountRole.Member)]
    [InlineData("admin", AccountRole.Admin)]
    public async Task ServerRoleIsVerifiedWithThePurchaseState(string role, AccountRole expected)
    {
        using var client = Client((_, _) => Task.FromResult(Json(new { schema_version = 1, user_id = UserId,
            product_id = "unfold", environment = "live", status = "active", role })));
        var access = await client.GetAccountAccessAsync(Session, TestContext.Current.CancellationToken);
        Assert.Equal(PurchaseAccess.Active, access.Purchase); Assert.Equal(expected, access.Role);
    }

    [Fact]
    public async Task CheckoutUsesAuthenticatedServerPriceAndAcceptsOnlyLemonHostedUrl()
    {
        var requestId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var orderId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var checkoutId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        using var client = Client(async (request, token) => {
            Assert.Equal("/functions/v1/create-checkout", request.RequestUri!.AbsolutePath);
            Assert.Equal("private-access", request.Headers.Authorization!.Parameter);
            Assert.Equal("public-key", Assert.Single(request.Headers.GetValues("apikey")));
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.Equal("KR", body.GetProperty("market").GetString());
            Assert.Equal(requestId, body.GetProperty("request_id").GetGuid());
            return Json(new { schema_version = 1, order_id = orderId, checkout_id = checkoutId, environment = "live",
                checkout_url = $"https://dokhustudio.lemonsqueezy.com/checkout/custom/{checkoutId}?expires=1&signature=abc" });
        });
        var result = await client.CreateCheckoutAsync(Session, "KR", requestId, TestContext.Current.CancellationToken);
        Assert.Equal(orderId, result.OrderId); Assert.Equal(checkoutId, result.CheckoutId);
        Assert.Equal("dokhustudio.lemonsqueezy.com", result.CheckoutUri.Host);
    }

    [Theory]
    [InlineData("https://evil.test/checkout/custom/44444444-4444-4444-8444-444444444444?expires=1&signature=x")]
    [InlineData("https://dokhustudio.lemonsqueezy.com/checkout/custom/55555555-5555-4555-8555-555555555555?expires=1&signature=x")]
    [InlineData("https://dokhustudio.lemonsqueezy.com/checkout/custom/44444444-4444-4444-8444-444444444444?redirect=evil&signature=x")]
    public async Task CheckoutRejectsUntrustedOrMismatchedUrls(string url)
    {
        var checkoutId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        using var client = Client((_, _) => Task.FromResult(Json(new { schema_version = 1, order_id = Guid.NewGuid(),
            checkout_id = checkoutId, environment = "live", checkout_url = url })));
        var error = await Assert.ThrowsAsync<AccountException>(() => client.CreateCheckoutAsync(Session, "KR", Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.InvalidResponse, error.Failure);
    }

    [Theory]
    [InlineData("test", "unfold", "active", 1)]
    [InlineData("live", "other", "active", 1)]
    [InlineData("live", "unfold", "trial", 1)]
    [InlineData("live", "unfold", "active", 2)]
    public async Task InvalidOrTestEntitlementNeverUnlocksLiveApp(string environment, string product, string status, int version)
    {
        using var client = Client((_, _) => Task.FromResult(Json(new { schema_version = version, user_id = UserId, product_id = product, environment, status, role = "member" })));
        var error = await Assert.ThrowsAsync<AccountException>(() => client.GetEntitlementAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.InvalidResponse, error.Failure);
    }

    [Fact]
    public async Task AnotherUsersEntitlementIsRejected()
    {
        using var client = Client((_, _) => Task.FromResult(Json(new { schema_version = 1, user_id = Guid.NewGuid(), product_id = "unfold", environment = "live", status = "active", role = "admin" })));
        Assert.Equal(AccountFailure.InvalidResponse, (await Assert.ThrowsAsync<AccountException>(() => client.GetEntitlementAsync(Session, TestContext.Current.CancellationToken))).Failure);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("")]
    public async Task UnknownRolesCannotGrantDebugAccess(string role)
    {
        using var client = Client((_, _) => Task.FromResult(Json(new { schema_version = 1, user_id = UserId,
            product_id = "unfold", environment = "live", status = "active", role })));
        Assert.Equal(AccountFailure.InvalidResponse,
            (await Assert.ThrowsAsync<AccountException>(() => client.GetAccountAccessAsync(Session, TestContext.Current.CancellationToken))).Failure);
    }

    [Theory]
    [InlineData(401, AccountFailure.AuthenticationRequired)]
    [InlineData(503, AccountFailure.Unavailable)]
    [InlineData(302, AccountFailure.Unavailable)]
    public async Task ErrorsNeverBecomeUnownedOrLeakProviderBody(int status, AccountFailure expected)
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private-provider-error") }));
        var error = await Assert.ThrowsAsync<AccountException>(() => client.GetEntitlementAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(expected, error.Failure); Assert.DoesNotContain("private-", error.Message);
    }

    [Fact]
    public async Task MalformedIdentityAndNetworkFailureAreRecoverableErrors()
    {
        using var malformed = Client((_, _) => Task.FromResult(Json(new { user_id = "not-a-guid" })));
        Assert.Equal(AccountFailure.InvalidResponse, (await Assert.ThrowsAsync<AccountException>(() => malformed.GetEntitlementAsync(Session, TestContext.Current.CancellationToken))).Failure);
        using var offline = Client((_, _) => throw new HttpRequestException("private-access"));
        Assert.Equal(AccountFailure.Unavailable, (await Assert.ThrowsAsync<AccountException>(() => offline.GetEntitlementAsync(Session, TestContext.Current.CancellationToken))).Failure);
        using var disconnected = Client((_, _) => throw new IOException("private-provider-details"));
        var unavailable = await Assert.ThrowsAsync<AccountException>(() => disconnected.GetEntitlementAsync(Session, TestContext.Current.CancellationToken));
        Assert.Equal(AccountFailure.Unavailable, unavailable.Failure);
        Assert.DoesNotContain("private-", unavailable.Message);
    }

    [Fact]
    public async Task RefreshCannotSwitchAccounts()
    {
        using var client = Client((_, _) => Task.FromResult(Json(new { access_token = "new", refresh_token = "new-refresh", expires_in = 3600, user = new { id = Guid.NewGuid() } })));
        Assert.Equal(AccountFailure.InvalidResponse, (await Assert.ThrowsAsync<AccountException>(() => client.RefreshAsync(Session, TestContext.Current.CancellationToken))).Failure);
    }

    [Fact]
    public void RemoteHttpAndCredentialBearingUrlsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new SupabaseAccountClient(new("http://example.test"), "key", AccountEnvironment.Live));
        Assert.Throws<ArgumentException>(() => new SupabaseAccountClient(new("https://secret@example.test"), "key", AccountEnvironment.Live));
        using var client = Client((_, _) => throw new NotImplementedException());
        Assert.Throws<ArgumentException>(() => client.BeginGoogleSignIn(new("https://example.test/auth/callback")));
    }
}
