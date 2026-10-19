using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

// LSKU FIX1 B1 (c): an admission the GSKU still holds although no child row was ever written for its command.
public sealed record GskuOrphanChildAdmission(
    Guid GskuId,
    int GskuVersion,
    GskuChildIdentityKind ChildKind,
    string CreationCommandId,
    string RequestFingerprint,
    DateTimeOffset AcquiredAtUtc,
    // LSKU FIX2 item 5: set when the admission is held only by a code consumed for an identity whose row never came —
    // the repair burns that reservation first, then releases.
    Guid? ReservationToBurnId = null,
    Guid? ReservationIdentityId = null,
    int? ReservationVersion = null,
    // FIX3 M6: set when the admission is held only by a code RESERVED long ago and never consumed — the repair consumes
    // it for no identity and burns it (codes are never handed out again), then releases.
    bool ReservationNeverConsumed = false);

/// <summary>
/// WP-MDM-SCOPE-INT-01 FIX2 item 1: one page of the orphan walk — every orphan of each holder GSKU on one page of holders
/// (GSKU id order, after the cursor). <see cref="NextAfterGskuId"/> null: the walk is over. A caller that filters (the
/// Enforced repair) walks on instead of re-reading the first page for ever.
/// </summary>
public sealed record GskuOrphanAdmissionPage(IReadOnlyList<GskuOrphanChildAdmission> Orphans, Guid? NextAfterGskuId)
{
    /// <summary>The bound of one walk, in holder pages (the repository's own read had it as MaximumHolderPages).</summary>
    public const int MaximumPages = 50;
}
