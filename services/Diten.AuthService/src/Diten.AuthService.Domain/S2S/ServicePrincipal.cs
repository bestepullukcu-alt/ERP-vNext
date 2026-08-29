using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.S2S;

public sealed class ServicePrincipal : GlobalEntityBase
{
    private ServicePrincipal()
    {
    }

    public ServicePrincipal(
        Guid servicePrincipalId,
        string clientId,
        string displayName,
        IEnumerable<string> ownerModuleIds,
        IEnumerable<string> allowedAudiences,
        IEnumerable<string> allowedProtocolScopes,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset? expiresAtUtc,
        string createdBy)
    {
        if (servicePrincipalId == Guid.Empty) throw new S2SContractException("Service principal id is required.", nameof(servicePrincipalId));
        if (expiresAtUtc is not null && expiresAtUtc <= notBeforeUtc) throw new S2SContractException("Expiry must be after not-before.", nameof(expiresAtUtc));

        Id = servicePrincipalId;
        ServicePrincipalId = servicePrincipalId;
        ClientId = S2SExactValue.RequiredLowercase(clientId, nameof(clientId));
        DisplayName = S2SExactValue.Required(displayName, nameof(displayName));
        OwnerModuleIds = S2SExactValue.RequiredDistinct(ownerModuleIds, nameof(ownerModuleIds));
        AllowedAudiences = S2SExactValue.RequiredDistinctLowercase(allowedAudiences, nameof(allowedAudiences));
        AllowedProtocolScopes = S2SExactValue.RequiredDistinctLowercase(allowedProtocolScopes, nameof(allowedProtocolScopes));
        NotBeforeUtc = notBeforeUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedBy = S2SExactValue.Required(createdBy, nameof(createdBy));
    }

    public Guid ServicePrincipalId { get; private init; }
    public string ClientId { get; private init; } = string.Empty;
    public string DisplayName { get; private init; } = string.Empty;
    public IReadOnlyList<string> OwnerModuleIds { get; private init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedAudiences { get; private init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedProtocolScopes { get; private init; } = Array.Empty<string>();
    public ServicePrincipalStatus Status { get; private set; } = ServicePrincipalStatus.Pending;
    public DateTimeOffset NotBeforeUtc { get; private init; }
    public DateTimeOffset? ExpiresAtUtc { get; private init; }
    public long PrincipalVersion { get; private set; } = 1;
    public long CredentialGeneration { get; private set; }
    public long ProofValidationFence { get; private set; }

    public bool AllowsAudience(string audience) => AllowedAudiences.Contains(audience, StringComparer.Ordinal);
    public bool AllowsProtocolScope(string scope) => AllowedProtocolScopes.Contains(scope, StringComparer.Ordinal);

    public void TransitionTo(ServicePrincipalStatus target, string actor, DateTimeOffset now)
    {
        if (!CanTransition(Status, target))
            throw new S2SContractException($"Transition from {Status} to {target} is forbidden.", nameof(target));

        Status = target;
        PrincipalVersion++;
        UpdatedAt = now;
        UpdatedBy = S2SExactValue.Required(actor, nameof(actor));
    }

    public void AdvanceCredentialGeneration(long generation, string actor, DateTimeOffset now)
    {
        if (generation != CredentialGeneration + 1)
            throw new S2SContractException("Credential generation must advance exactly once.", nameof(generation));
        CredentialGeneration = generation;
        PrincipalVersion++;
        UpdatedAt = now;
        UpdatedBy = S2SExactValue.Required(actor, nameof(actor));
    }

    public static bool CanTransition(ServicePrincipalStatus current, ServicePrincipalStatus target) =>
        (current, target) switch
        {
            (ServicePrincipalStatus.Pending, ServicePrincipalStatus.Active) => true,
            (ServicePrincipalStatus.Pending, ServicePrincipalStatus.Revoked) => true,
            (ServicePrincipalStatus.Pending, ServicePrincipalStatus.Retired) => true,
            (ServicePrincipalStatus.Active, ServicePrincipalStatus.Suspended) => true,
            (ServicePrincipalStatus.Active, ServicePrincipalStatus.Revoked) => true,
            (ServicePrincipalStatus.Active, ServicePrincipalStatus.Retired) => true,
            (ServicePrincipalStatus.Suspended, ServicePrincipalStatus.Active) => true,
            (ServicePrincipalStatus.Suspended, ServicePrincipalStatus.Revoked) => true,
            (ServicePrincipalStatus.Suspended, ServicePrincipalStatus.Retired) => true,
            _ => false
        };
}
