namespace Diten.Platform.Application.Contracts.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §8.3 — the company short code (MOD-0220 <c>LegalEntity.Code</c>) read from MDM lookup-validation, in the
/// TOKEN's tenant with the forwarded user token. No cache; the Platform keeps no copy of short codes (K-5).
/// </summary>
public interface ILegalEntityShortCodeReader
{
    Task<LegalEntityShortCodeResult> ReadAsync(Guid tenantId, Guid legalEntityId, CancellationToken cancellationToken = default);
}

public enum LegalEntityShortCodeOutcome
{
    /// <summary>Active, consistent, approved or approval not reported (lookup-validation 1.0.0), code matches exactly.</summary>
    Found = 1,

    /// <summary>MDM 404: absent, deleted, another tenant's or not active — one answer.</summary>
    NotFound = 2,

    /// <summary>The code does not match <c>^[A-Z]{2,5}$</c> exactly (no normalisation).</summary>
    ShortCodeInvalid = 3,

    /// <summary>MDM reports an approval status other than APPROVED (DRAFT, SUBMITTED, REJECTED).</summary>
    NotEligible = 4,

    /// <summary>MDM 403: the forwarded user lacks <c>mdm.legal-entities.read</c>. Permanent.</summary>
    ReadPermissionMissing = 5,

    /// <summary>MDM unreachable, 5xx, 400, 401, past the 3 s budget, or an inconsistent / unreadable answer.</summary>
    Unavailable = 6
}

public sealed record LegalEntityShortCodeResult(LegalEntityShortCodeOutcome Outcome, string? Code, string? Reason)
{
    public static LegalEntityShortCodeResult Found(string code) => new(LegalEntityShortCodeOutcome.Found, code, null);
    public static LegalEntityShortCodeResult Of(LegalEntityShortCodeOutcome outcome, string reason) => new(outcome, null, reason);
}
