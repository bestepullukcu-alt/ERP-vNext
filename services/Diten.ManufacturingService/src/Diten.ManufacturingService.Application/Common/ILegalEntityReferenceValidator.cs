namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// MDM (legal entity master) seam — proves that the legal entity a request names belongs to the caller's tenant and
/// is ACTIVE and referenceable, through the existing MDM endpoint <c>GET /api/legal-entities/{id}/lookup-validation</c>
/// (the MVP-1 / CRM CyclePeriod pattern). <b>Fail-closed:</b> "the dependency said no" and "we could not ask" are
/// different answers, and neither lets the request through.
/// </summary>
public interface ILegalEntityReferenceValidator
{
    Task<LegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken ct);
}

public enum LegalEntityValidation
{
    Valid,

    /// <summary>MDM answered: unknown to this tenant (404), not ACTIVE, or not referenceable → 422.</summary>
    NotReferenceable,

    /// <summary>MDM could not be asked or answered unusably (timeout, 5xx, auth rejection, malformed) → 503.</summary>
    Unavailable
}
