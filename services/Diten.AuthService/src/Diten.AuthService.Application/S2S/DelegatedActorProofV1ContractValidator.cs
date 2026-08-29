using System.Globalization;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.S2S;

public sealed class DelegatedActorProofV1ContractValidator
{
    private static readonly HashSet<string> SingletonClaims = new(StringComparer.Ordinal)
    {
        "typ", "iss", "aud", "sub", "client_id", "azp", "actor_type", "tenant_id",
        "delegated_actor_id", "delegated_actor_type", "delegation_id", "operation_id", "scope",
        "request_hash", "nonce", "jti", "iat", "nbf", "exp", "tenant_grant_version",
        "service_principal_version", "credential_generation"
    };

    private static readonly HashSet<string> AllowedClaims = new(SingletonClaims, StringComparer.Ordinal) { "permission" };
    private static readonly HashSet<string> AllowedAudiences = new(StringComparer.Ordinal)
    {
        "diten-management-governance-service",
        "diten-fpa-service",
        "diten-decision-intelligence-service"
    };

    public DelegatedActorProofV1 Validate(IEnumerable<S2SClaim> input)
    {
        var claims = input?.ToArray() ?? throw new S2SContractException("Claims are required.", nameof(input));
        if (claims.Any(x => !AllowedClaims.Contains(x.Type))) throw new S2SContractException("Unknown claim type is forbidden.");

        var grouped = claims.GroupBy(x => x.Type, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        foreach (var name in SingletonClaims)
        {
            if (!grouped.TryGetValue(name, out var values) || values.Length != 1)
                throw new S2SContractException($"Claim '{name}' must occur exactly once.");
        }

        if (!grouped.TryGetValue("permission", out var permissions) || permissions.Length == 0)
            throw new S2SContractException("At least one permission claim is required.");

        string One(string name) => S2SExactValue.Required(grouped[name][0].Value, name);
        RequireExact(One("typ"), DelegatedActorProofV1.ExactType, "typ");
        RequireExact(One("iss"), DelegatedActorProofV1.ExactIssuer, "iss");
        RequireExact(One("actor_type"), DelegatedActorProofV1.ExactActorType, "actor_type");
        RequireExact(One("delegated_actor_type"), DelegatedActorProofV1.ExactDelegatedActorType, "delegated_actor_type");
        RequireExact(One("scope"), DelegatedActorProofV1.ExactScope, "scope");

        var audience = One("aud");
        if (!AllowedAudiences.Contains(audience)) throw new S2SContractException("Audience is not an exact Gate I audience.", "aud");
        var clientId = S2SExactValue.RequiredLowercase(One("client_id"), "client_id");
        var azp = One("azp");
        RequireExact(azp, clientId, "azp");
        var parsedPermissions = permissions.Select(x => S2SExactValue.RequiredLowercase(x.Value, "permission")).ToArray();
        if (parsedPermissions.Distinct(StringComparer.Ordinal).Count() != parsedPermissions.Length)
            throw new S2SContractException("Duplicate permission claim is forbidden.", "permission");

        var issuedAt = PositiveLong(One("iat"), "iat");
        var notBefore = PositiveLong(One("nbf"), "nbf");
        var expiresAt = PositiveLong(One("exp"), "exp");
        if (issuedAt > notBefore || notBefore >= expiresAt || expiresAt - issuedAt > DelegatedActorProofV1.MaximumLifetimeSeconds)
            throw new S2SContractException("NumericDate ordering or lifetime is invalid.");

        var requestHash = One("request_hash");
        RequireBase64UrlStrength(requestHash, 32, "request_hash");
        var jti = One("jti");
        var nonce = One("nonce");
        RequireIdentifierStrength(jti, "jti");
        RequireIdentifierStrength(nonce, "nonce");

        return new DelegatedActorProofV1(
            One("typ"), One("iss"), audience, RequiredGuid(One("sub"), "sub"), clientId, azp,
            jti, nonce, RequiredGuid(One("tenant_id"), "tenant_id"),
            RequiredGuid(One("delegated_actor_id"), "delegated_actor_id"),
            RequiredGuid(One("delegation_id"), "delegation_id"),
            S2SExactValue.RequiredLowercase(One("operation_id"), "operation_id"),
            Array.AsReadOnly(parsedPermissions), One("scope"), requestHash, issuedAt, notBefore, expiresAt,
            PositiveLong(One("tenant_grant_version"), "tenant_grant_version"),
            PositiveLong(One("service_principal_version"), "service_principal_version"),
            PositiveLong(One("credential_generation"), "credential_generation"));
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw new S2SContractException($"Claim '{name}' is not exact.", name);
    }

    private static Guid RequiredGuid(string value, string name) =>
        Guid.TryParseExact(value, "D", out var result) && result != Guid.Empty
            ? result
            : throw new S2SContractException($"Claim '{name}' must be a non-empty canonical Guid.", name);

    private static long PositiveLong(string value, string name) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0
            ? result
            : throw new S2SContractException($"Claim '{name}' must be a positive invariant integer.", name);

    private static void RequireIdentifierStrength(string value, string name)
    {
        if (Guid.TryParseExact(value, "D", out var guid) && guid != Guid.Empty) return;
        RequireBase64UrlStrength(value, 16, name);
    }

    private static void RequireBase64UrlStrength(string value, int minimumBytes, string name)
    {
        if (value.Any(x => !(char.IsAsciiLetterOrDigit(x) || x is '-' or '_')))
            throw new S2SContractException($"Claim '{name}' must be unpadded base64url.", name);
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=');
            if (Convert.FromBase64String(padded).Length < minimumBytes) throw new FormatException();
        }
        catch (FormatException)
        {
            throw new S2SContractException($"Claim '{name}' does not meet the minimum cryptographic strength.", name);
        }
    }
}
