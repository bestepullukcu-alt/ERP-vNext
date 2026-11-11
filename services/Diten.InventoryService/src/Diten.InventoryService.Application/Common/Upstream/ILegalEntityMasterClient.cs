namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// MDM legal-entity master, read-only (waiver W-0173-02): is this id an ACTIVE legal entity of the CALLER's tenant?
/// The tenant is never passed — MDM resolves it from the caller's own token, forwarded through the Gateway.
/// </summary>
public interface ILegalEntityMasterClient
{
    /// <exception cref="LegalEntityMasterUnavailableException">MDM unreachable, 5xx, timeout or an answer outside its shape.</exception>
    Task<LegalEntityMembership> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// WP-MVP1-FIX-LIVE2: the code and name of one legal entity for DISPLAY (evidence packs) — MDM
    /// <c>GET /api/legal-entities/{id}</c> (<c>mdm.legal-entities.read</c>) with the caller's own token, through the same
    /// Gateway path. Read-only and never throws for an MDM answer: unknown, refused, unreachable, timeout or a body outside its
    /// shape → null, and the caller shows the id with "(okunamadı)". A name is never invented.
    /// </summary>
    Task<LegalEntityName?> GetLegalEntityNameAsync(Guid legalEntityId, CancellationToken cancellationToken = default);
}

/// <summary>A legal entity's code and display name (MDM <c>displayName</c>, else <c>legalName</c>).</summary>
public sealed record LegalEntityName(Guid LegalEntityId, string Code, string Name);

/// <summary>What MDM said about one legal entity for the caller.</summary>
public enum LegalEntityMembership
{
    /// <summary>An active legal entity of the caller's tenant.</summary>
    Active,

    /// <summary>Unknown, inactive or another tenant's (MDM: 404 "not referenceable").</summary>
    NotReferenceable,

    /// <summary>MDM refused the caller (401/403 — e.g. no <c>mdm.legal-entities.read</c>).</summary>
    CallerRefused
}

/// <summary>MDM could not answer → 503 <c>LEGAL_ENTITY_MASTER_UNAVAILABLE</c>, nothing read or written.</summary>
public sealed class LegalEntityMasterUnavailableException : Exception
{
    public LegalEntityMasterUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
