using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

// LSKU FIX1 B1 (c): an admission the GSKU still holds although no child row was ever written for its command.
public sealed record GskuOrphanChildAdmission(
    Guid GskuId,
    int GskuVersion,
    GskuChildIdentityKind ChildKind,
    string CreationCommandId,
    string RequestFingerprint,
    DateTimeOffset AcquiredAtUtc);
