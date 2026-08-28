using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Web.Services.Auth;

/// <summary>
/// MOD-0014 — the cookie-JWT → <see cref="HttpContext.User"/> bridge, and its eager refresh.
///
/// <para>Extracted from Program.cs so the behaviour can be tested: this is authentication, and the failure mode it
/// fixes was silent — the page rendered while every subsequent API call was logged out.</para>
///
/// <para><b>The bug this class exists to prevent.</b> The bridge runs in TWO passes: <see cref="AuthenticateAsync"/>
/// before <c>UseAuthentication()</c>, and <see cref="ReapplyPrincipal"/> after it (cookie auth can replace
/// <c>context.User</c>). When the first pass REFRESHED an expired token it wrote the new cookies to the RESPONSE —
/// <c>context.Request.Cookies</c> still held the expired one. The second pass re-read the request, saw the expired
/// token, and cleared the cookies it had just been given. The browser kept a logged-out session while the page it
/// was looking at rendered fine.</para>
///
/// <para><b>The rule that prevents its return:</b> the second pass never re-validates and never clears. The first
/// pass is the single owner of every cookie decision; the second only re-applies the principal it already
/// computed, handed over through <see cref="PrincipalItemKey"/>.</para>
/// </summary>
public sealed class TokenBridge
{
    /// <summary>Where pass 1 leaves the principal for pass 2. Also the signal that pass 1 already decided.</summary>
    internal const string PrincipalItemKey = "Diten.TokenBridge.Principal";

    /// <summary>
    /// How long a COMPLETED refresh stays shareable.
    ///
    /// <para>AuthService rotates refresh tokens and treats reuse of a rotated one as theft — it revokes every
    /// session the user has. Dropping the in-flight entry the instant the first caller finished meant a request
    /// arriving milliseconds later (the browser still holding the old cookie, because the new one is only on a
    /// response in flight) started a SECOND refresh with the same, now-rotated token — tripping reuse detection
    /// and killing all sessions. Keeping the finished result briefly lets those stragglers reuse it instead.</para>
    /// </summary>
    internal static readonly TimeSpan CompletedRefreshGrace = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, Lazy<Task<AuthBridgeResult>>> _refreshInFlight = new();
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _completedAt = new();

    private readonly ILogger<TokenBridge>? _logger;

    /*
     * The logger is DIAGNOSTIC ONLY — nothing below changes what this class does.
     *
     * Sessions have dropped silently twice, long after login, and four hypotheses were eliminated without finding
     * the cause (token lifetimes bound correctly, single-flight keyed correctly, culture changes survived, the
     * culture middleware leaves the auth cookie alone). Every remaining explanation lives on one of the three
     * paths below, and all three are currently silent — so the next occurrence produces no evidence either.
     *
     * NEVER log the access token, the refresh token, or any secret: only WHY the session ended, what the gateway
     * answered, and which user it happened to.
     */
    public TokenBridge(TimeProvider? timeProvider = null, ILogger<TokenBridge>? logger = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>
    /// The subject of an EXPIRED token, read without validating it — the token has already failed validation by
    /// the time this is used, so this is a best-effort label for the log line and nothing else. Never throws.
    /// </summary>
    /// <summary>
    /// The ACCESS token's own expiry, read from the freshly issued token. This is the lifetime that decides how
    /// soon the next refresh happens; it is minutes, while the refresh token's is days. Never throws.
    /// </summary>
    private static object AccessTokenExpiry(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return "unknown";
        }

        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(accessToken).ValidTo;
        }
        catch
        {
            return "unreadable";
        }
    }

    private static string SubjectFor(string accessToken)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.Subject ?? "unknown";
        }
        catch
        {
            return "unreadable";
        }
    }

    /// <summary>Refresh calls actually issued — the single-flight and no-double-refresh assertions read this.</summary>
    internal int RefreshCallCount;

    /// <summary>
    /// Pass 1: validate the access token, and if it has merely EXPIRED, refresh it once and adopt the new tokens.
    /// This pass owns every cookie write and every cookie clear.
    /// </summary>
    public async Task AuthenticateAsync(
        HttpContext context,
        TokenValidationParameters validationParameters,
        IAuthGateway authGateway,
        IAuthCookieService authCookieService,
        Func<string, Guid?> readTenantId)
    {
        var accessToken = AuthTokenCookies.GetAccessToken(context.Request);
        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            SetPrincipal(context, handler.ValidateToken(accessToken, validationParameters, out _));
            return;
        }
        catch (SecurityTokenExpiredException)
        {
            // Fall through: expiry is the one failure that is recoverable.
        }
        catch (Exception)
        {
            // Malformed, wrong signature, wrong issuer/audience — not recoverable, and not a session we should
            // keep. Deliberately narrow: this branch is NOT reached for expiry.
            authCookieService.ClearTokens(context.Response);
            return;
        }

        var refreshToken = AuthTokenCookies.GetRefreshToken(context.Request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // Expired with nothing to refresh from: end the session cleanly rather than leaving a dead cookie.
            _logger?.LogWarning(
                "Session ended cleanly for {Subject}: the access token expired and no refresh token cookie was "
                + "present, so there was nothing to renew from. Path {Path}.",
                SubjectFor(accessToken), context.Request.Path.Value);
            authCookieService.ClearTokens(context.Response);
            return;
        }

        try
        {
            var refreshResult = await RefreshOnceAsync(accessToken, refreshToken, readTenantId(accessToken), authGateway);

            if (!refreshResult.Success ||
                string.IsNullOrWhiteSpace(refreshResult.AccessToken) ||
                string.IsNullOrWhiteSpace(refreshResult.RefreshToken) ||
                !refreshResult.ExpiresAt.HasValue)
            {
                // Only a definitive "re-authenticate" ends the session. A transient failure leaves the cookies
                // alone so the next request can try again.
                _logger?.LogWarning(
                    "Token refresh REFUSED for {Subject}. reauthRequired={ReauthRequired} (session {Outcome}), "
                    + "gateway said: {Error}. Path {Path}.",
                    SubjectFor(accessToken),
                    refreshResult.ReauthRequired,
                    refreshResult.ReauthRequired ? "ended" : "kept for a retry",
                    refreshResult.ErrorMessage ?? "(no reason supplied)",
                    context.Request.Path.Value);

                if (refreshResult.ReauthRequired)
                {
                    authCookieService.ClearTokens(context.Response);
                }

                return;
            }

            authCookieService.WriteTokens(
                context.Response,
                refreshResult.AccessToken,
                refreshResult.RefreshToken,
                refreshResult.ExpiresAt.Value);

            /*
             * ⚠ THE COOKIE IS NOT ENOUGH, AND THIS IS THE WHOLE BUG. WriteTokens sets a Set-Cookie header:
             * that reaches the BROWSER, on the way out, and changes nothing about the request now in flight.
             * HttpRequest.Cookies is a snapshot of what the browser already sent, and nothing in this
             * repository writes to it. So without the line below, every consumer downstream of this middleware
             * — 57 call sites — spends the rest of THIS request using the token that was just replaced, and
             * the refresh only takes effect on the next one.
             *
             * Recorded at exactly the same moment as the cookie write, because a request in which those two
             * disagree is the failure this is fixing, wearing a different hat.
             */
            RefreshedTokens.Record(context, refreshResult.AccessToken, refreshResult.RefreshToken);

            /*
             * ExpiresAt is the REFRESH token's expiry, not the access token's — it is what WriteTokens receives as
             * its refreshExpiresAtUtc argument. Calling it "the access token is valid until…" invited exactly the
             * wrong conclusion: a log reading "valid until 10 August" was about to be used to rule out expiry as
             * the cause of a logout, when the access token actually lives for minutes, not days.
             *
             * Both are reported, each under its own name, so neither can be mistaken for the other.
             */
            _logger?.LogInformation(
                "Token refreshed for {Subject}. Access token valid until {AccessExpiresAt:o}; refresh token until "
                + "{RefreshExpiresAt:o}.",
                SubjectFor(accessToken),
                AccessTokenExpiry(refreshResult.AccessToken),
                refreshResult.ExpiresAt.Value);

            SetPrincipal(context, handler.ValidateToken(refreshResult.AccessToken, validationParameters, out _));
        }
        catch (Exception ex)
        {
            // Soft failure (network, gateway down): keep the cookies so the session survives a blip. Logged
            // because "the session survived a blip" and "the session quietly stopped refreshing" look identical
            // from the outside, and one of them is the fault being hunted.
            _logger?.LogWarning(
                ex,
                "Token refresh FAILED for {Subject} without a verdict; cookies kept so the next request retries. "
                + "Path {Path}.",
                SubjectFor(accessToken), context.Request.Path.Value);
        }
    }

    /// <summary>
    /// Pass 2, after <c>UseAuthentication()</c>: restore the principal pass 1 computed.
    ///
    /// <para>It NEVER re-reads the request token and NEVER clears cookies. Both were the bug: the request still
    /// carries the pre-refresh token, so any decision made from it undoes the refresh that just succeeded.</para>
    /// </summary>
    public void ReapplyPrincipal(HttpContext context)
    {
        if (context.Items.TryGetValue(PrincipalItemKey, out var stored) && stored is ClaimsPrincipal principal)
        {
            context.User = principal;
        }
    }

    /// <summary>
    /// One refresh per refresh-token, shared by concurrent callers and by stragglers inside the grace window.
    /// </summary>
    private async Task<AuthBridgeResult> RefreshOnceAsync(
        string accessToken,
        string refreshToken,
        Guid? tenantId,
        IAuthGateway authGateway)
    {
        EvictExpiredEntries();

        var task = _refreshInFlight.GetOrAdd(refreshToken, _ => new Lazy<Task<AuthBridgeResult>>(() =>
        {
            Interlocked.Increment(ref RefreshCallCount);
            // CancellationToken.None: one caller giving up must not abort a refresh others are awaiting.
            return authGateway.RefreshAsync(accessToken, refreshToken, tenantId, CancellationToken.None);
        })).Value;

        try
        {
            return await task;
        }
        finally
        {
            // Stamp completion instead of removing: the entry stays reusable for the grace window above.
            _completedAt[refreshToken] = _timeProvider.GetUtcNow();
        }
    }

    private void EvictExpiredEntries()
    {
        if (_completedAt.IsEmpty)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        foreach (var entry in _completedAt)
        {
            if (now - entry.Value >= CompletedRefreshGrace)
            {
                _completedAt.TryRemove(entry.Key, out _);
                _refreshInFlight.TryRemove(entry.Key, out _);
            }
        }
    }

    private static void SetPrincipal(HttpContext context, ClaimsPrincipal principal)
    {
        context.User = principal;
        context.Items[PrincipalItemKey] = principal;
    }
}
