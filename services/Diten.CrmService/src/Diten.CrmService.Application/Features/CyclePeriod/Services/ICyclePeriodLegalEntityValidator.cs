namespace Diten.CrmService.Application.Features.CyclePeriod.Services;

/// <summary>
/// MOD-0165 FU07 — proves an MDM legal entity may be referenced BEFORE a legal-entity-scoped period is persisted.
/// <para><b>It is a write-path component, not part of the read seam.</b> <c>ICyclePeriodReader</c> never holds it: a
/// list, a detail read or a resolve must not depend on another service being reachable, or an MDM outage would stop a
/// tenant reading its own calendar.</para>
/// <para><b>404 and unreachable are different answers.</b> A dependency that spoke and said "no such entity" (or "not
/// referenceable", or "not ACTIVE") makes the write invalid — 400. A timeout, a 5xx, an auth rejection or a malformed
/// body means we do not KNOW, which is 503 with nothing persisted. Turning a 403 into "no such entity" would tell the
/// author a legal entity does not exist when in truth we were not allowed to look.</para>
/// </summary>
public interface ICyclePeriodLegalEntityValidator
{
    Task<CyclePeriodLegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken);
}

/// <summary>
/// The verdict. <see cref="DependencyUnavailable"/> is the load-bearing distinction: it is the only case where the
/// handler answers 503 and asks the author to retry, rather than telling them their input was wrong.
/// </summary>
public sealed record CyclePeriodLegalEntityValidation(bool IsReferenceable, bool DependencyUnavailable)
{
    public static readonly CyclePeriodLegalEntityValidation Valid = new(true, false);
    public static readonly CyclePeriodLegalEntityValidation NotReferenceable = new(false, false);
    public static readonly CyclePeriodLegalEntityValidation Unavailable = new(false, true);
}
