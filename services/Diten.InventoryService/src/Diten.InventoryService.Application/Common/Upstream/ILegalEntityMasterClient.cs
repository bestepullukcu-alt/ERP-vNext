namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// MDM legal-entity master, read-only (waiver W-0173-02): is this id an ACTIVE legal entity of the CALLER's tenant?
/// The tenant is never passed — MDM resolves it from the caller's own token, forwarded through the Gateway.
/// </summary>
public interface ILegalEntityMasterClient
{
    /// <exception cref="LegalEntityMasterUnavailableException">MDM unreachable, 5xx, timeout or an answer outside its shape.</exception>
    Task<LegalEntityMembership> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken = default);
}

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
