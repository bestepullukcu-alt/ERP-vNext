using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;

/// <summary>
/// MOD-0033-FU02 §F.1 — fail-closed validation of the declared service clients. It runs over the WHOLE section
/// before anything is written: one bad entry stops Auth's startup and no entry is applied.
///
/// <para>The accepted service names and audiences are the issuer's own closed list
/// (<see cref="ServiceIdentityTokenAudiencePolicy"/>) and the hash format is the production verifier's
/// (<see cref="IServiceClientCredentialVerifier.IsStateCoherent"/>) — neither is restated here.</para>
/// </summary>
public static class ServiceClientRegistryValidator
{
    public const string SectionName = "ServiceClientRegistry";
    private const string WellFormedVersionProbe = "v1";

    public static IReadOnlyList<ServiceClientDeclaration> Validate(
        IReadOnlyList<ServiceClientRegistryEntryInput> entries,
        IServiceClientCredentialVerifier credentials)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(credentials);

        var declarations = new List<ServiceClientDeclaration>(entries.Count);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.HasUnknownField) throw Invalid(entry, "an unknown field", "is not part of the contract");
            // "Tenants": "<id>" (a value, not a list) would otherwise read as "no tenants": a client with no grant.
            if (entry.TenantsIsNotAList) throw Invalid(entry, nameof(entry.Tenants), "must be a list");

            var clientCode = Required(entry, nameof(entry.ClientCode), entry.ClientCode);
            var serviceName = Required(entry, nameof(entry.ServiceName), entry.ServiceName);
            var audience = Required(entry, nameof(entry.AllowedAudience), entry.AllowedAudience);
            var hash = Required(entry, nameof(entry.CredentialHash), entry.CredentialHash);
            var version = Required(entry, nameof(entry.CredentialVersion), entry.CredentialVersion);

            if (!IsExactCode(clientCode)) throw Invalid(entry, nameof(entry.ClientCode), "is malformed");
            if (!ServiceIdentityTokenAudiencePolicy.IsAllowedAudience(audience))
                throw Invalid(entry, nameof(entry.AllowedAudience), "is not an audience the issuer accepts");
            if (!ServiceIdentityTokenAudiencePolicy.IsAllowedPair(serviceName, audience))
                throw Invalid(entry, nameof(entry.ServiceName), "is not a service the issuer accepts");
            if (!credentials.IsStateCoherent(Probe(hash, WellFormedVersionProbe)))
                throw Invalid(entry, nameof(entry.CredentialHash), "is malformed");
            if (!credentials.IsStateCoherent(Probe(hash, version)))
                throw Invalid(entry, nameof(entry.CredentialVersion), "is malformed");

            var tenants = new List<Guid>(entry.Tenants.Count);
            for (var index = 0; index < entry.Tenants.Count; index++)
            {
                var field = $"{nameof(entry.Tenants)}[{index}]";
                if (string.IsNullOrWhiteSpace(entry.Tenants[index])) throw Invalid(entry, field, "is required");
                if (!Guid.TryParseExact(entry.Tenants[index], "D", out var tenantId) || tenantId == Guid.Empty)
                    throw Invalid(entry, field, "is malformed");
                if (tenants.Contains(tenantId)) throw Invalid(entry, field, "is a duplicate");
                tenants.Add(tenantId);
            }

            if (!codes.Add(clientCode)) throw Invalid(entry, nameof(entry.ClientCode), "is a duplicate");
            declarations.Add(new ServiceClientDeclaration(clientCode, serviceName, audience, hash, version, tenants));
        }

        return declarations;
    }

    private static string Required(ServiceClientRegistryEntryInput entry, string field, string? value) =>
        string.IsNullOrWhiteSpace(value) ? throw Invalid(entry, field, "is required") : value;

    private static ServiceClientIdentity Probe(string hash, string version) =>
        new() { ActiveCredentialHash = hash, ActiveCredentialVersion = version };

    // The same shape the token endpoint and the MDM client accept for the client id header.
    private static bool IsExactCode(string value) =>
        value.Length <= 128 && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(character => char.IsControl(character) || character == ',');

    private static ServiceClientRegistryConfigurationException Invalid(
        ServiceClientRegistryEntryInput entry, string field, string problem) =>
        new($"Configuration error: {SectionName}:Clients[{entry.Index}] {field} {problem}.");
}
