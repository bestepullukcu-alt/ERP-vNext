using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// MOD-0014 token bridge. This is authentication: the tests below encode both what must work (a session
/// surviving an expiring access token) and what must keep failing closed (a tampered token ending the session).
///
/// <para>The regression they exist for: pass 1 refreshed the token and wrote new cookies to the response, then
/// pass 2 re-read the REQUEST — which still held the expired token — and cleared them. The page rendered, and
/// every API call in the same session came back logged out.</para>
/// </summary>
public sealed class TokenBridgeTests
{
    private const string Secret = "token-bridge-tests-signing-secret-that-is-long-enough-for-hs256";
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // ── The bug ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_expired_access_token_is_refreshed_and_the_cookies_are_KEPT()
    {
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var cookies = new RecordingCookieService();
        var bridge = new TokenBridge();

        await bridge.AuthenticateAsync(context, ValidationParameters(), Gateway(), cookies, _ => TenantId);

        Assert.True(cookies.Wrote);
        // The whole point: the refreshed session must NOT be cleared.
        Assert.False(cookies.Cleared);
        Assert.True(context.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Pass_2_does_not_undo_the_refresh_even_though_the_request_still_holds_the_old_token()
    {
        // The exact shape of the outage: the request cookie is untouched by a refresh (WriteTokens only writes to
        // the RESPONSE), so pass 2 must not make any decision from it.
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var cookies = new RecordingCookieService();
        var bridge = new TokenBridge();

        await bridge.AuthenticateAsync(context, ValidationParameters(), Gateway(), cookies, _ => TenantId);
        var afterPass1 = context.User;

        // Cookie auth may replace the principal; pass 2 restores it.
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        bridge.ReapplyPrincipal(context);

        Assert.False(cookies.Cleared);
        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Same(afterPass1, context.User);
    }

    [Fact]
    public async Task The_refreshed_token_is_readable_by_the_rest_of_THIS_request()
    {
        /*
         * ⚠ THE HALF THE TEST ABOVE NEVER COVERED. "Pass 2 does not undo the refresh even though the request
         * still holds the old token" names the defect exactly — and then solves it only INSIDE the bridge, by
         * making pass 2 ignore the request cookie. Everything downstream of the bridge was left reading that
         * same stale cookie: 57 call sites across 53 files, every one of them using the token that had just
         * been replaced, for the whole of the request in which the refresh happened.
         *
         * This asserts the property the downstream actually needs: after the bridge refreshes, the ordinary
         * accessor everybody goes through returns the NEW token, while the request still carries the old one.
         *
         * MUTATION GUARD: delete the RefreshedTokens.Record call in TokenBridge and this goes red — the
         * accessor falls back to the cookie and hands back the expired token.
         */
        var expired = ExpiredToken();
        var context = ContextWith(expired, "refresh-1");
        var expected = SuccessResult();

        await new TokenBridge().AuthenticateAsync(
            context, ValidationParameters(), new FakeAuthGateway(expected), new RecordingCookieService(),
            _ => TenantId);

        Assert.NotEqual(expired, expected.AccessToken);

        // What the browser sent is unchanged — that is production, not a test artefact.
        Assert.Equal(expired, context.Request.Cookies[AuthTokenCookies.AccessTokenCookie]);

        // What the rest of the request reads is the new one.
        Assert.Equal(expected.AccessToken, AuthTokenCookies.GetAccessToken(context.Request));
        Assert.True(RefreshedTokens.RefreshedInThisRequest(context));
    }

    [Fact]
    public async Task A_request_that_did_NOT_refresh_reports_no_refresh()
    {
        /*
         * Non-vacuity for the guard the fifteen proxy endpoints now rely on. If "this request refreshed" were
         * true of every request, ClearAuthCookies would never clear anything and a genuinely dead session
         * would never end — the user would loop through failing calls instead of being sent to sign in.
         */
        var live = Token(DateTime.UtcNow.AddMinutes(30));
        var context = ContextWith(live, "refresh-1");

        await new TokenBridge().AuthenticateAsync(
            context, ValidationParameters(), Gateway(), new RecordingCookieService(), _ => TenantId);

        Assert.False(RefreshedTokens.RefreshedInThisRequest(context));
        Assert.Equal(live, AuthTokenCookies.GetAccessToken(context.Request));
    }

    [Fact]
    public async Task The_refreshed_session_still_carries_the_users_claims_for_the_next_API_call()
    {
        // "The page renders but the APIs are logged out" was the symptom; the principal must survive intact.
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var bridge = new TokenBridge();

        await bridge.AuthenticateAsync(
            context, ValidationParameters(), Gateway(), new RecordingCookieService(), _ => TenantId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        bridge.ReapplyPrincipal(context);

        Assert.Equal("user-1", context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(TenantId.ToString(), context.User.FindFirst("tenant_id")?.Value);
    }

    // ── Failing closed, still ─────────────────────────────────────────────────

    [Fact]
    public async Task A_tampered_or_malformed_token_still_clears_the_cookies()
    {
        var context = ContextWith("not-a-jwt", "refresh-1");
        var cookies = new RecordingCookieService();
        var gateway = Gateway();

        await new TokenBridge().AuthenticateAsync(context, ValidationParameters(), gateway, cookies, _ => TenantId);

        Assert.True(cookies.Cleared);
        Assert.False(cookies.Wrote);
        // A broken token is never worth a refresh attempt.
        Assert.Equal(0, gateway.CallCount);
        Assert.NotEqual(true, context.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_clears_the_cookies()
    {
        var foreign = Token(DateTime.UtcNow.AddMinutes(30), "a-completely-different-signing-secret-value-here!!");
        var context = ContextWith(foreign, "refresh-1");
        var cookies = new RecordingCookieService();

        await new TokenBridge().AuthenticateAsync(
            context, ValidationParameters(), Gateway(), cookies, _ => TenantId);

        Assert.True(cookies.Cleared);
    }

    [Fact]
    public async Task An_expired_token_with_no_refresh_token_ends_the_session_cleanly()
    {
        var context = ContextWith(ExpiredToken(), refreshToken: null);
        var cookies = new RecordingCookieService();
        var gateway = Gateway();

        await new TokenBridge().AuthenticateAsync(context, ValidationParameters(), gateway, cookies, _ => TenantId);

        Assert.True(cookies.Cleared);
        Assert.Equal(0, gateway.CallCount);
        Assert.NotEqual(true, context.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task A_refresh_token_the_server_rejects_ends_the_session()
    {
        var context = ContextWith(ExpiredToken(), "refresh-expired");
        var cookies = new RecordingCookieService();
        var gateway = new FakeAuthGateway(FailureResult("expired", reauthRequired: true));

        await new TokenBridge().AuthenticateAsync(context, ValidationParameters(), gateway, cookies, _ => TenantId);

        Assert.True(cookies.Cleared);
        Assert.False(cookies.Wrote);
    }

    [Fact]
    public async Task A_transient_refresh_failure_keeps_the_session_rather_than_logging_the_user_out()
    {
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var cookies = new RecordingCookieService();
        // Gateway down: not the user's fault, and not a reason to destroy their session.
        var gateway = new FakeAuthGateway(FailureResult("gateway unreachable", reauthRequired: false));

        await new TokenBridge().AuthenticateAsync(context, ValidationParameters(), gateway, cookies, _ => TenantId);

        Assert.False(cookies.Cleared);
    }

    [Fact]
    public async Task An_exception_from_the_gateway_does_not_clear_the_cookies()
    {
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var cookies = new RecordingCookieService();

        await new TokenBridge().AuthenticateAsync(
            context, ValidationParameters(), new ThrowingAuthGateway(), cookies, _ => TenantId);

        Assert.False(cookies.Cleared);
    }

    // ── Never refresh twice with the same (rotating) token ────────────────────

    [Fact]
    public async Task One_request_triggers_exactly_ONE_refresh_call()
    {
        var bridge = new TokenBridge();
        var gateway = Gateway();
        var context = ContextWith(ExpiredToken(), "refresh-1");

        await bridge.AuthenticateAsync(context, ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId);
        bridge.ReapplyPrincipal(context);

        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task Concurrent_requests_share_a_single_refresh()
    {
        var bridge = new TokenBridge();
        var gateway = new FakeAuthGateway(SuccessResult(), delay: TimeSpan.FromMilliseconds(50));

        var requests = Enumerable.Range(0, 8).Select(_ =>
            bridge.AuthenticateAsync(
                ContextWith(ExpiredToken(), "refresh-1"),
                ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId));

        await Task.WhenAll(requests);

        // AuthService revokes EVERY session if a rotated refresh token is replayed, so a second call here would
        // log the user out of everything.
        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task A_straggler_arriving_after_the_refresh_completed_does_not_replay_the_rotated_token()
    {
        // The browser still holds the OLD refresh cookie until the response carrying the new one lands. A request
        // sent in that window used to start a SECOND refresh with the already-rotated token — reuse detection,
        // and every session revoked.
        var bridge = new TokenBridge();
        var gateway = Gateway();

        await bridge.AuthenticateAsync(
            ContextWith(ExpiredToken(), "refresh-1"), ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId);
        await bridge.AuthenticateAsync(
            ContextWith(ExpiredToken(), "refresh-1"), ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId);

        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task A_different_refresh_token_still_gets_its_own_refresh()
    {
        // The grace window must not swallow a genuinely different session's refresh.
        var bridge = new TokenBridge();
        var gateway = Gateway();

        await bridge.AuthenticateAsync(
            ContextWith(ExpiredToken(), "refresh-1"), ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId);
        await bridge.AuthenticateAsync(
            ContextWith(ExpiredToken(), "refresh-2"), ValidationParameters(), gateway, new RecordingCookieService(), _ => TenantId);

        Assert.Equal(2, gateway.CallCount);
    }

    // ── Pass 2 in isolation ───────────────────────────────────────────────────

    [Fact]
    public void Pass_2_never_clears_cookies_and_never_reads_the_request()
    {
        // With no hand-over from pass 1 there is nothing to re-apply — and crucially, nothing to destroy.
        var context = ContextWith(ExpiredToken(), "refresh-1");
        var before = context.User;

        new TokenBridge().ReapplyPrincipal(context);

        Assert.Same(before, context.User);
        // No Set-Cookie at all: pass 2 neither writes nor deletes cookies.
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void Pass_2_is_STRUCTURALLY_incapable_of_clearing_cookies_or_revalidating()
    {
        // Stronger than asserting behaviour: the signature denies it the means. ReapplyPrincipal receives no
        // cookie service and no validation parameters, so it cannot clear a cookie or judge the request token
        // however it is edited later. This is what makes the regression unrepresentable rather than merely absent.
        var parameters = typeof(TokenBridge)
            .GetMethod(nameof(TokenBridge.ReapplyPrincipal))!
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        Assert.Equal([typeof(HttpContext)], parameters);
        Assert.DoesNotContain(typeof(IAuthCookieService), parameters);
        Assert.DoesNotContain(typeof(TokenValidationParameters), parameters);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FakeAuthGateway Gateway() => new(SuccessResult());

    private static AuthBridgeResult SuccessResult() => new(
        Success: true,
        AccessToken: Token(DateTime.UtcNow.AddMinutes(30)),
        RefreshToken: "refresh-2",
        ExpiresAt: DateTime.UtcNow.AddDays(7),
        User: null,
        ErrorMessage: null);

    private static AuthBridgeResult FailureResult(string message, bool reauthRequired) => new(
        Success: false,
        AccessToken: null,
        RefreshToken: null,
        ExpiresAt: null,
        User: null,
        ErrorMessage: message,
        ReauthRequired: reauthRequired);

    private static string ExpiredToken() => Token(DateTime.UtcNow.AddMinutes(-5));

    private static string Token(DateTime expires, string? secret = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? Secret));
        var jwt = new JwtSecurityToken(
            issuer: "diten",
            audience: "diten",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("tenant_id", TenantId.ToString())
            ],
            // A token already expired at creation still needs notBefore <= now.
            notBefore: expires.AddMinutes(-60),
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "diten",
        ValidateAudience = true,
        ValidAudience = "diten",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static DefaultHttpContext ContextWith(string accessToken, string? refreshToken)
    {
        var context = new DefaultHttpContext();
        var cookies = new List<string> { $"{AuthTokenCookies.AccessTokenCookie}={accessToken}" };
        if (refreshToken is not null)
        {
            cookies.Add($"{AuthTokenCookies.RefreshTokenCookie}={refreshToken}");
        }

        context.Request.Headers.Cookie = string.Join("; ", cookies);
        return context;
    }

    private sealed class RecordingCookieService : IAuthCookieService
    {
        public bool Wrote { get; private set; }
        public bool Cleared { get; private set; }

        public void WriteTokens(HttpResponse response, string accessToken, string refreshToken, DateTime refreshExpiresAtUtc)
            => Wrote = true;

        public void ClearTokens(HttpResponse response) => Cleared = true;
    }

    /// <summary>
    /// Only RefreshAsync is reachable from the bridge; every other member throws so an accidental widening of
    /// the bridge's responsibilities shows up as a failing test rather than silent behaviour.
    /// </summary>
    private abstract class AuthGatewayStub : IAuthGateway
    {
        public abstract Task<AuthBridgeResult> RefreshAsync(
            string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default);

        public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> ChangeTenantPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => NotUsed();
        public Task<bool> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default) => throw Unexpected();
        public Task<AuthBridgeResult> ResetPlatformPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default) => NotUsed();
        public Task<AuthBridgeResult> ResetTenantPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default) => NotUsed();
        public Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default) => throw Unexpected();

        private static Task<AuthBridgeResult> NotUsed() => throw Unexpected();

        private static NotSupportedException Unexpected()
            => new("The token bridge must only ever call RefreshAsync.");
    }

    private sealed class FakeAuthGateway(AuthBridgeResult result, TimeSpan? delay = null) : AuthGatewayStub
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override async Task<AuthBridgeResult> RefreshAsync(
            string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            if (delay.HasValue)
            {
                await Task.Delay(delay.Value, ct);
            }

            return result;
        }
    }

    private sealed class ThrowingAuthGateway : AuthGatewayStub
    {
        public override Task<AuthBridgeResult> RefreshAsync(
            string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default)
            => throw new HttpRequestException("auth service unreachable");
    }
}
