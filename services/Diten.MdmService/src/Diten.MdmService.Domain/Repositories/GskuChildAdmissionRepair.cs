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
    int? ReservationVersion = null);
