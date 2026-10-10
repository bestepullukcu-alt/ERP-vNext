namespace Diten.SupplyChainService.Application.Common;

/// <summary>
/// R-2 (PR #134): LegalEntityId arrives on the request instead of in the token, so the service — not the caller —
/// decides whether it belongs to the tenant the JWT names. Fail-closed by construction: the only non-rejecting
/// outcome is <see cref="LegalEntityScopeOutcome.Valid"/>, and an unreachable MDM maps to
/// <see cref="LegalEntityScopeOutcome.Unavailable"/> rather than passing the request through.
/// </summary>
public interface ILegalEntityScopeValidator
{
    Task<LegalEntityScopeOutcome> ValidateAsync(
        Guid tenantId,
        Guid legalEntityId,
        string authorization,
        Guid correlationId,
        CancellationToken cancellationToken);
}

public enum LegalEntityScopeOutcome
{
    /// <summary>MDM confirmed the legal entity is the tenant's and is Active.</summary>
    Valid,

    /// <summary>
    /// MDM answered 404. It does not distinguish "belongs to another tenant" from "exists but is not Active" —
    /// its repository composes TenantFilter into the lookup, so a foreign id is indistinguishable from a missing
    /// one. Callers report this as the module's own NOT_FOUND so the answer discloses nothing either way.
    /// </summary>
    NotReferenceable,

    /// <summary>MDM could not be reached or answered 401/5xx. The request is refused, never allowed through.</summary>
    Unavailable,
}
