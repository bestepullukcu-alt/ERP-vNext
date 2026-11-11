using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.PvgService.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Diten.PvgService.Api.Tests;

// WP-0230-DL4B (AC-5): test identity helpers. Every value is fake; no key, secret or real token is committed.
// SetTenant / SetActor build the validated principal the authentication middleware would have built, for tests that
// call the endpoint methods directly; CreateToken signs a JWT for tests that run the real middleware chain.
internal static class PvgTestIdentity
{
    public const string TenantA = "8f2b6c0e-1d4a-4c3b-9e5f-0a1b2c3d4e5f";
    public const string TenantB = "1c9d8e7f-6a5b-4c3d-8e2f-9a0b1c2d3e4f";
    public const string AuthenticationType = "PvgTest";

    public static void SetTenant(HttpContext context, string tenant)
    {
        context.Request.Headers[PvgCaseIntakeRequestContext.TenantHeader] = tenant;
        Identity(context).AddClaim(new Claim(PvgCaseIntakeRequestContext.TenantClaimType, tenant));
    }

    public static void SetActor(HttpContext context, string subject, string actorType)
    {
        var identity = Identity(context);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
        identity.AddClaim(new Claim(PvgCaseIntakeRequestContext.ActorKindClaimType, actorType));
    }

    public static string CreateToken(
        string secret,
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        DateTime expiresUtc)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            // NotBefore and IssuedAt are explicit: otherwise JsonWebTokenHandler sets them to "now" and a token that
            // is already expired fails with IDX12401 (Expires <= NotBefore) instead of being produced.
            NotBefore = expiresUtc.AddHours(-1),
            IssuedAt = expiresUtc.AddHours(-1),
            Expires = expiresUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // "header.payload." with an empty signature part (alg none). The payload carries every claim a valid token would,
    // so an unsigned token is rejected for being unsigned and not for failing issuer, audience or lifetime checks.
    public static string CreateUnsignedToken(
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        DateTime expiresUtc)
    {
        var payload = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["exp"] = new DateTimeOffset(expiresUtc).ToUnixTimeSeconds(),
            ["nbf"] = new DateTimeOffset(expiresUtc.AddHours(-1)).ToUnixTimeSeconds(),
            ["iat"] = new DateTimeOffset(expiresUtc.AddHours(-1)).ToUnixTimeSeconds()
        };
        foreach (var claim in claims)
        {
            payload[claim.Type] = claim.Value;
        }

        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var body = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        return header + "." + body + ".";
    }

    private static ClaimsIdentity Identity(HttpContext context)
    {
        var identity = context.User.Identities.FirstOrDefault(candidate => candidate.AuthenticationType == AuthenticationType);
        if (identity is null)
        {
            identity = new ClaimsIdentity(AuthenticationType);
            context.User = new ClaimsPrincipal(identity);
        }

        return identity;
    }
}
