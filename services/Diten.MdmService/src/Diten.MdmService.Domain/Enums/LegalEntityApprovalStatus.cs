namespace Diten.MdmService.Domain.Enums;

// MOD-0220 FULL — governance approval state. Evidence-gated transitions (MOD-0023/0031) are DEFERRED;
// in Faz 1 this is operator-set metadata only. Int values are PERSISTED — do not reorder.
public enum LegalEntityApprovalStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}
