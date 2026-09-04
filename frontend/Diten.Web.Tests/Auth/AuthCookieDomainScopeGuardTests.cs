using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Web.Tests.Auth;

/*
 * THE GUARD — THE AUTH COOKIE MUST STAY HOST-ONLY, BECAUSE LOGIN'S SAFETY DEPENDS ON IT.
 *
 * WHAT THIS PROTECTS, AND WHY IT IS NOT A STYLE RULE. The gateway refuses any request whose tenant signals
 * contradict the token it carries (gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:158-163,
 * owner decision 2026-08-30). That rule has NO login exemption — deliberately. Login survives it only because a
 * login request never reaches the gateway carrying another tenant's token, and one of the two reasons for that
 * lives in THIS file's subject: AuthCookieService.BuildCookieOptions does not set `Domain`, so per RFC 6265 the
 * cookie is host-only and a-guid.diten.com's token is never sent to b-guid.diten.com.
 *
 * ⚠ THAT IS AN ASSUMPTION HELD UP BY THE ABSENCE OF ONE LINE. Adding `Domain = ".diten.com"` looks like a
 * convenience ("share the session across tenant hosts") and silently converts sign-in into a dead end: the
 * browser attaches tenant A's token to tenant B's login page, the gateway answers 400 Tenant mismatch, and the
 * user cannot sign in AND cannot recover by signing in again — the very act that would fix it is the act refused.
 *
 * MEASURED, NOT REFLECTED. BuildCookieOptions is private, so this drives the public entry point over a real
 * HttpResponse and reads the Set-Cookie headers that ASP.NET Core actually emits. A reflection test would pass
 * against a CookieOptions that never reaches a header.
 */
public sealed class AuthCookieDomainScopeGuardTests
{
    /// <summary>
    /// The consequence, spelled out where a failing build will print it. ⚠ Names what BREAKS, not what changed:
    /// "Domain should be null" tells the next reader nothing about why their change locked users out.
    /// </summary>
    private const string WhatBreaks =
        "Auth cookie is now parent-domain scoped. That makes tenant A's token reach tenant B's host, and the "
        + "gateway then refuses the LOGIN request itself (400 Tenant mismatch) — the user cannot sign in and "
        + "cannot recover, because signing in again is the request being refused. See gateway "
        + "TenantResolutionMiddleware:158-163 and its test Login_that_DOES_carry_a_token_is_refused_like_any_"
        + "other_path. If parent-domain scope is intentional, the gateway needs a login exemption FIRST.";

    [Fact]
    public void Written_auth_cookies_are_host_only()
    {
        var response = WriteTokens();

        var setCookies = response.Headers.SetCookie.ToArray();
        Assert.NotEmpty(setCookies);

        foreach (var setCookie in setCookies)
        {
            Assert.False(
                setCookie!.Contains("domain=", StringComparison.OrdinalIgnoreCase),
                $"{WhatBreaks}{Environment.NewLine}Offending Set-Cookie header: {setCookie}");
        }
    }

    /// <summary>
    /// The CLEARING side, which is the half that gets forgotten. A delete written with a Domain does not remove a
    /// host-only cookie (and vice versa): a mismatch here strands a stale token in the browser, which the gateway
    /// then reads as a contradicting tenant signal on the next login.
    /// </summary>
    [Fact]
    public void Cleared_auth_cookies_are_host_only_too()
    {
        var response = NewResponse();
        new AuthCookieService().ClearTokens(response);

        var setCookies = response.Headers.SetCookie.ToArray();
        Assert.NotEmpty(setCookies);

        foreach (var setCookie in setCookies)
        {
            Assert.False(
                setCookie!.Contains("domain=", StringComparison.OrdinalIgnoreCase),
                $"{WhatBreaks}{Environment.NewLine}Offending Set-Cookie header: {setCookie}");
        }
    }

    /// <summary>
    /// THE CONTROL. Without it this guard passes on a service that writes NO cookies at all, or on a header
    /// collection this test reads incorrectly — either of which would make the assertions above vacuous. It also
    /// pins that "no Domain" is the only attribute being claimed: the rest of the hardening is still emitted.
    /// </summary>
    [Fact]
    public void Control_the_cookies_are_really_written_and_still_hardened()
    {
        var response = WriteTokens();
        var setCookies = response.Headers.SetCookie.ToArray();

        Assert.Contains(setCookies, c => c!.StartsWith($"{AuthTokenCookies.AccessTokenCookie}=", StringComparison.Ordinal));
        Assert.Contains(setCookies, c => c!.StartsWith($"{AuthTokenCookies.RefreshTokenCookie}=", StringComparison.Ordinal));
        Assert.All(setCookies, c => Assert.Contains("httponly", c!, StringComparison.OrdinalIgnoreCase));
        Assert.All(setCookies, c => Assert.Contains("path=/", c!, StringComparison.OrdinalIgnoreCase));
    }

    private static HttpResponse WriteTokens()
    {
        var response = NewResponse();
        new AuthCookieService().WriteTokens(
            response,
            accessToken: "access-token-value",
            refreshToken: "refresh-token-value",
            refreshExpiresAtUtc: DateTime.UtcNow.AddDays(7));

        return response;
    }

    private static HttpResponse NewResponse()
    {
        // A real request host, so nothing about this measurement depends on the "localhost" special cases that
        // hid the sibling defect in the gateway.
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("a-guid.diten.com");
        context.Request.Scheme = "https";
        return context.Response;
    }
}
