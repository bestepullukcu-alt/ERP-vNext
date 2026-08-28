using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Diten.Platform.API.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using JsonWebToken = Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;

namespace Diten.Platform.API.Security;

public static class TrustedServiceTokenValidationExtensions
{
    public const string AuthenticationScheme = "TrustedServiceToken";
    public const string RequiredAudience = "TRUSTED_AUDIT_SOURCE_INGEST";
    public const string RequiredServiceName = "Diten.MDM";

    private static readonly HashSet<string> RequiredClaimTypes = new(StringComparer.Ordinal)
    {
        JwtRegisteredClaimNames.Iss,
        JwtRegisteredClaimNames.Aud,
        JwtRegisteredClaimNames.Sub,
        "actor_type",
        "service_name",
        "tenant_id",
        JwtRegisteredClaimNames.Jti,
        JwtRegisteredClaimNames.Iat,
        JwtRegisteredClaimNames.Nbf,
        JwtRegisteredClaimNames.Exp
    };

    public static AuthenticationBuilder AddTrustedServiceTokenValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TrustedServiceTokenValidationOptions>(
            configuration.GetSection(TrustedServiceTokenValidationOptions.SectionName));

        var clock = timeProvider ?? TimeProvider.System;
        return new AuthenticationBuilder(services)
            .AddJwtBearer(AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                var validationOptions = configuration
                    .GetSection(TrustedServiceTokenValidationOptions.SectionName)
                    .Get<TrustedServiceTokenValidationOptions>() ?? new TrustedServiceTokenValidationOptions();
                options.TokenValidationParameters = CreateTokenValidationParameters(validationOptions, clock);
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity?.IsAuthenticated != true
                            || !HasExactServiceClaims(context.SecurityToken, validationOptions.Issuer))
                        {
                            context.Fail("The service identity token claim contract is invalid.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }

    public static TokenValidationParameters CreateTokenValidationParameters(
        TrustedServiceTokenValidationOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!IsExactIdentifier(options.Issuer, 256))
        {
            throw new InvalidOperationException("Trusted service token issuer configuration is invalid.");
        }

        var currentKey = CreatePublicKey(options.CurrentKeyId, options.CurrentPublicKeyPem);
        var hasAnyPreviousFact = options.PreviousKeyId is not null
            || options.PreviousPublicKeyPem is not null
            || options.PreviousValidUntilUtc is not null;
        RsaSecurityKey? previousKey = null;
        if (hasAnyPreviousFact)
        {
            if (options.PreviousValidUntilUtc is null
                || options.PreviousValidUntilUtc.Value.Offset != TimeSpan.Zero)
            {
                throw new InvalidOperationException("Trusted service token previous-key configuration is incomplete.");
            }

            previousKey = CreatePublicKey(options.PreviousKeyId, options.PreviousPublicKeyPem);
            if (string.Equals(currentKey.KeyId, previousKey.KeyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Trusted service token key identifiers must be distinct.");
            }
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = RequiredAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "service_identity_role_forbidden",
            AlgorithmValidator = static (algorithm, _, _, _) =>
                string.Equals(algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal),
            IssuerSigningKeyResolver = (_, _, keyId, _) =>
            {
                if (!IsExactIdentifier(keyId))
                {
                    return [];
                }

                if (string.Equals(keyId, currentKey.KeyId, StringComparison.Ordinal))
                {
                    return [currentKey];
                }

                if (previousKey is not null
                    && options.PreviousValidUntilUtc is { } previousValidUntilUtc
                    && timeProvider.GetUtcNow() < previousValidUntilUtc
                    && string.Equals(keyId, previousKey.KeyId, StringComparison.Ordinal))
                {
                    return [previousKey];
                }

                return [];
            }
        };
    }

    public static bool HasExactServiceClaims(SecurityToken? securityToken, string issuer)
    {
        if (!IsExactIdentifier(issuer, 256))
        {
            return false;
        }

        var claims = securityToken switch
        {
            JwtSecurityToken jwt => jwt.Claims.ToArray(),
            JsonWebToken jwt => jwt.Claims.ToArray(),
            _ => []
        };
        if (claims.Length != RequiredClaimTypes.Count
            || claims.Any(claim => !RequiredClaimTypes.Contains(claim.Type)))
        {
            return false;
        }

        if (!HasOneInteger(claims, JwtRegisteredClaimNames.Iat, out var issuedAt)
            || !HasOneInteger(claims, JwtRegisteredClaimNames.Nbf, out var notBefore)
            || !HasOneInteger(claims, JwtRegisteredClaimNames.Exp, out var expires)
            || issuedAt != notBefore
            || notBefore > long.MaxValue - 300
            || expires != notBefore + 300)
        {
            return false;
        }

        return HasOneExact(claims, JwtRegisteredClaimNames.Iss, issuer)
            && HasOneExact(claims, JwtRegisteredClaimNames.Aud, RequiredAudience)
            && HasOneGuid(claims, JwtRegisteredClaimNames.Sub)
            && HasOneExact(claims, "actor_type", "service")
            && HasOneExact(claims, "service_name", RequiredServiceName)
            && HasOneGuid(claims, "tenant_id")
            && HasOneGuid(claims, JwtRegisteredClaimNames.Jti);
    }

    private static RsaSecurityKey CreatePublicKey(string? keyId, string? publicKeyPem)
    {
        if (!IsExactIdentifier(keyId) || string.IsNullOrWhiteSpace(publicKeyPem))
        {
            throw new InvalidOperationException("Trusted service token public-key configuration is invalid.");
        }

        if (publicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trusted service token validation accepts public keys only.");
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(publicKeyPem);
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("Trusted service token public-key configuration is invalid.", exception);
        }

        if (rsa.KeySize < 2048)
        {
            throw new InvalidOperationException("Trusted service token public key must be at least 2048 bits.");
        }

        return new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = keyId };
    }

    private static bool HasOneExact(IReadOnlyCollection<Claim> claims, string type, string value) =>
        claims.Count(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)
            && string.Equals(claim.Value, value, StringComparison.Ordinal)) == 1
        && claims.Count(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)) == 1;

    private static bool HasOneGuid(IReadOnlyCollection<Claim> claims, string type)
    {
        var values = claims.Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)).ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0].Value, "D", out var parsed)
            && parsed != Guid.Empty;
    }

    private static bool HasOneInteger(IReadOnlyCollection<System.Security.Claims.Claim> claims, string type, out long value)
    {
        var values = claims.Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)).ToArray();
        value = default;
        return values.Length == 1
            && long.TryParse(values[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsExactIdentifier(string? value, int maxLength = 128) =>
        value is { Length: > 0 } && value.Length <= maxLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
