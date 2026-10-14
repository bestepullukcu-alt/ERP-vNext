namespace Diten.InventoryService.Application.Common;

/// <summary>
/// The origin of the request's legal entity, written to every posting / reservation audit row (W-0173-02).
/// </summary>
public static class LegalEntitySources
{
    /// <summary>The signed <c>legal_entity_id</c> claim.</summary>
    public const string Token = "token";

    /// <summary>
    /// The <c>X-Legal-Entity-Id</c> header of a claim-less token, verified to be an ACTIVE legal entity of the caller's
    /// tenant — but NOT that this user belongs to it (no user↔LE data exists: the accepted risk of W-0173-02).
    /// </summary>
    public const string HeaderUnverifiedMembership = "header-unverified-membership";

    /// <summary>
    /// MOD-0175 Q3 — the e-signature subject provider called by MOD-0022 with a claim-less token and no company header: the
    /// company is the one of the record being signed (read-only path; ASSUMPTION-0175-Q3-02, the W-0173-02 risk).
    /// </summary>
    public const string SubjectRecord = "subject-record";
}
