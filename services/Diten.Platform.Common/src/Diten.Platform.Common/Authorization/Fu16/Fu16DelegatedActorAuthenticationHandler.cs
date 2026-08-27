using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.Platform.Common.Authorization.Fu16;

public sealed class Fu16DelegatedActorAuthenticationHandler(
    IOptionsMonitor<Fu16AuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IFu16ValidationKeyProvider keyProvider,
    IFu16AuthorityDecisionClient authorityClient,
    IFu16TrustedRequestContextAccessor contextAccessor)
    : AuthenticationHandler<Fu16AuthenticationOptions>(options, logger, encoder)
{
    private const string TokenType = "diten-delegated-actor-proof+jwt";
    private const string FailureItem = "Diten.FU16.AuthenticationFailure";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (!Request.Headers.TryGetValue("Authorization", out var values) || values.Count != 1)
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_missing_bearer"));
            var authorization = values[0];
            if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)
                || authorization.Length == 7)
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_bearer"));
            var token = authorization[7..];
            var header = ReadClosedHeader(token);
            if (!string.Equals(header.Type, TokenType, StringComparison.Ordinal))
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_typ"));
            if (!string.Equals(header.Algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_alg"));

            var unvalidated = new JwtSecurityTokenHandler { MapInboundClaims = false }.ReadJwtToken(token);
            var issuer = unvalidated.Issuer;
            if (string.IsNullOrWhiteSpace(issuer) || !Options.TrustedIssuers.Contains(issuer))
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_issuer"));
            var keyResult = await keyProvider.ResolveAsync(issuer, header.Kid, Context.RequestAborted).ConfigureAwait(false);
            var keyFailure = ValidateKey(keyResult);
            if (keyFailure is not null) return Fail(keyFailure);

            var parameters = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = keyResult.Key,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateIssuer = true,
                ValidIssuers = Options.TrustedIssuers,
                ValidateAudience = true,
                ValidAudiences = Options.TrustedAudiences,
                ValidateLifetime = true,
                ClockSkew = Options.ClockTolerance,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role"
            };
            ClaimsPrincipal principal;
            JwtSecurityToken jwt;
            try
            {
                principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
                    .ValidateToken(token, parameters, out var validated);
                jwt = (JwtSecurityToken)validated;
            }
            catch (SecurityTokenException)
            {
                return Fail(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_signature_or_claims"));
            }
            ValidateClosedPayload(jwt);

            var request = await BuildAuthorityRequestAsync(principal, jwt, issuer).ConfigureAwait(false);
            var decision = await authorityClient.AuthorizeAndConsumeReplayAsync(request, Context.RequestAborted)
                .ConfigureAwait(false);
            if (decision.Disposition != Fu16AuthorityDisposition.Allowed)
                return Fail(decision.Disposition switch
                {
                    Fu16AuthorityDisposition.Denied => Fu16AuthenticationFailure.Forbidden(decision.StableCode),
                    Fu16AuthorityDisposition.Conflict => Fu16AuthenticationFailure.Conflict(decision.StableCode),
                    _ => Fu16AuthenticationFailure.Unavailable(decision.StableCode)
                });
            if (decision.TenantGrantVersion != request.TenantGrantVersion
                || decision.ServicePrincipalVersion != request.ServicePrincipalVersion
                || decision.CredentialGeneration != request.CredentialGeneration)
                return Fail(Fu16AuthenticationFailure.Conflict("fu16_authorization_fence_changed"));

            var context = new Fu16TrustedRequestContext(
                request.TenantId, request.EffectiveActorId, request.DelegatedActorId, request.DelegationId,
                request.ServicePrincipalId, request.CredentialId, request.ClientId, issuer,
                jwt.Audiences.Single(), TokenType, request.ProtocolScope, request.OperationId,
                request.Permissions, request.RequestHash, request.TenantGrantVersion,
                request.ServicePrincipalVersion, request.CredentialGeneration, request.IssuedAtUtc,
                request.NotBeforeUtc, request.ExpiresAtUtc).Validate();
            contextAccessor.Publish(context);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Fu16AuthenticationFailureException failure) { return Fail(failure.Failure); }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return Fail(Fu16AuthenticationFailure.BadRequest("fu16_malformed_proof"));
        }
        catch
        {
            return Fail(Fu16AuthenticationFailure.Unavailable("fu16_authority_unavailable"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Context.Items[FailureItem] is Fu16AuthenticationFailure failure)
        {
            Response.StatusCode = failure.HttpStatus;
            Response.Headers["X-Diten-Failure-Code"] = failure.StableCode;
            return Task.CompletedTask;
        }
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private AuthenticateResult Fail(Fu16AuthenticationFailure failure)
    {
        Context.Items[FailureItem] = failure;
        return AuthenticateResult.Fail(new Fu16AuthenticationFailureException(failure));
    }

    private async Task<Fu16AuthorityRequest> BuildAuthorityRequestAsync(
        ClaimsPrincipal principal, JwtSecurityToken jwt, string issuer)
    {
        var tenant = GuidClaim(principal, "tenant_id");
        var permissions = principal.FindAll("permission").Select(static x => x.Value).ToArray();
        if (permissions.Length == 0 || permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
            throw new FormatException("Invalid permission set.");
        var operation = Claim(principal, "operation_id");
        var requestHash = await Fu16CanonicalRequestBinding.ComputeAsync(
            Request, tenant, operation, permissions, Context.RequestAborted).ConfigureAwait(false);
        if (!string.Equals(requestHash, Claim(principal, "request_hash"), StringComparison.Ordinal))
            throw new Fu16AuthenticationFailureException(Fu16AuthenticationFailure.Unauthorized("fu16_request_binding_mismatch"));
        var issuedAt = UnixClaim(principal, JwtRegisteredClaimNames.Iat);
        var notBefore = UnixClaim(principal, JwtRegisteredClaimNames.Nbf);
        var expires = UnixClaim(principal, JwtRegisteredClaimNames.Exp);
        if (expires - issuedAt > Options.MaximumProofLifetime || issuedAt > notBefore)
            throw new Fu16AuthenticationFailureException(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_lifetime"));
        var audiences = jwt.Audiences.ToArray();
        if (audiences.Length != 1)
            throw new Fu16AuthenticationFailureException(Fu16AuthenticationFailure.Unauthorized("fu16_invalid_audience"));
        var audience = audiences[0];
        return new(
            tenant, GuidClaim(principal, "effective_actor_id"), GuidClaim(principal, "delegated_actor_id"),
            GuidClaim(principal, "delegation_id"), GuidClaim(principal, "service_principal_id"),
            GuidClaim(principal, "credential_id"), Claim(principal, "client_id"), issuer, audience,
            Claim(principal, "protocol_scope"), operation, permissions, requestHash,
            Claim(principal, JwtRegisteredClaimNames.Jti), Claim(principal, "nonce"),
            LongClaim(principal, "tenant_grant_version"), LongClaim(principal, "service_principal_version"),
            LongClaim(principal, "credential_generation"), issuedAt, notBefore, expires);
    }

    private Fu16AuthenticationFailure? ValidateKey(Fu16ValidationKeyResult result)
    {
        if (result.Disposition == Fu16ValidationKeyDisposition.Unavailable)
            return Fu16AuthenticationFailure.Unavailable("fu16_key_provider_unavailable");
        if (result.Disposition != Fu16ValidationKeyDisposition.Found || result.Key is null)
            return Fu16AuthenticationFailure.Unauthorized("fu16_unknown_or_inactive_key");
        var size = result.Key switch
        {
            RsaSecurityKey rsa when rsa.Rsa is not null => rsa.Rsa.KeySize,
            RsaSecurityKey rsa => (rsa.Parameters.Modulus?.Length ?? 0) * 8,
            _ => 0
        };
        if (size < 3072) return Fu16AuthenticationFailure.Unauthorized("fu16_rsa_key_too_small");
        var now = TimeProvider.System.GetUtcNow();
        if (result.NotBeforeUtc > now || result.ExpiresAtUtc <= now)
            return Fu16AuthenticationFailure.Unauthorized("fu16_inactive_key");
        return null;
    }

    private static ClosedHeader ReadClosedHeader(string token)
    {
        var part = token.Split('.', 2)[0];
        var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(part));
        using var document = JsonDocument.Parse(json);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? typ = null, alg = null, kid = null;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new JsonException("Duplicate JOSE header.");
            if (property.Name is "jku" or "jwk" or "x5c") throw new JsonException("Embedded key material is forbidden.");
            if (property.Name == "typ") typ = property.Value.GetString();
            else if (property.Name == "alg") alg = property.Value.GetString();
            else if (property.Name == "kid") kid = property.Value.GetString();
            else throw new JsonException("Unknown JOSE header.");
        }
        if (string.IsNullOrWhiteSpace(typ) || string.IsNullOrWhiteSpace(alg) || string.IsNullOrWhiteSpace(kid))
            throw new JsonException("Incomplete JOSE header.");
        return new(typ, alg, kid);
    }

    private static void ValidateClosedPayload(JwtSecurityToken jwt)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            JwtRegisteredClaimNames.Iss, JwtRegisteredClaimNames.Aud, JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf, JwtRegisteredClaimNames.Exp, JwtRegisteredClaimNames.Jti,
            "tenant_id", "effective_actor_id", "delegated_actor_id", "delegation_id",
            "service_principal_id", "credential_id", "client_id", "protocol_scope", "operation_id",
            "permission", "request_hash", "tenant_grant_version", "service_principal_version",
            "credential_generation", "nonce"
        };
        if (jwt.Payload.Keys.Any(key => !allowed.Contains(key)))
            throw new Fu16AuthenticationFailureException(Fu16AuthenticationFailure.Unauthorized("fu16_unknown_claim"));
    }

    private static string Claim(ClaimsPrincipal principal, string type) =>
        principal.FindFirst(type)?.Value is { Length: > 0 } value ? value : throw new FormatException(type);
    private static Guid GuidClaim(ClaimsPrincipal principal, string type) =>
        Guid.TryParseExact(Claim(principal, type), "D", out var value) && value != Guid.Empty
            ? value : throw new FormatException(type);
    private static long LongClaim(ClaimsPrincipal principal, string type) =>
        long.TryParse(Claim(principal, type), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value : throw new FormatException(type);
    private static DateTimeOffset UnixClaim(ClaimsPrincipal principal, string type) =>
        DateTimeOffset.FromUnixTimeSeconds(LongClaim(principal, type));
    private sealed record ClosedHeader(string Type, string Algorithm, string Kid);
}
