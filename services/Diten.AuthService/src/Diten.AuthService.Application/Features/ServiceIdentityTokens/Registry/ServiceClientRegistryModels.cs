namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;

/// <summary>
/// MOD-0033-FU02 §F.1 — one <c>ServiceClientRegistry:Clients[]</c> entry exactly as configuration carried it, before
/// any validation. <paramref name="Index"/> is the entry's position key and is the only thing an error names besides
/// the field: no value of any field ever reaches an error message or a log line.
/// </summary>
public sealed record ServiceClientRegistryEntryInput(
    string Index,
    string? ClientCode,
    string? ServiceName,
    string? AllowedAudience,
    string? CredentialHash,
    string? CredentialVersion,
    IReadOnlyList<string?> Tenants,
    bool HasUnknownField,
    bool TenantsIsNotAList = false);

/// <summary>A validated declaration: a first-party service client and the tenants it is granted for.</summary>
public sealed record ServiceClientDeclaration(
    string ClientCode,
    string ServiceName,
    string AllowedAudience,
    string CredentialHash,
    string CredentialVersion,
    IReadOnlyList<Guid> Tenants);

/// <summary>Stops Auth's startup. The message names the entry index and the field — never a value.</summary>
public sealed class ServiceClientRegistryConfigurationException : Exception
{
    public ServiceClientRegistryConfigurationException(string message) : base(message)
    {
    }
}

public enum ServiceClientRegistryOutcome
{
    IdentityCreated = 1,
    IdentityUnchanged = 2,
    /// <summary>Exists with a different id, hash, version, service name or audience. Left exactly as it is.</summary>
    IdentityDiffers = 3,
    IdentityRevoked = 4,
    IdentityDeleted = 5,
    GrantCreated = 6,
    GrantUnchanged = 7,
    /// <summary>Somebody disabled it. It stays disabled.</summary>
    GrantDisabled = 8,
    GrantDeleted = 9,
    /// <summary>The persisted identity is not the declared one (or is revoked/deleted): no grant is created for it.</summary>
    GrantsSkipped = 10
}

/// <summary><paramref name="Fields"/> holds field NAMES only (which facts differ), never their values.</summary>
public sealed record ServiceClientRegistryNotice(
    ServiceClientRegistryOutcome Outcome,
    string ClientCode,
    Guid? TenantId,
    IReadOnlyList<string> Fields);
