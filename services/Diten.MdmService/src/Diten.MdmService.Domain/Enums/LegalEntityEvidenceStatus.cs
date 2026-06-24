namespace Diten.MdmService.Domain.Enums;

// MOD-0220 FULL — evidence completion state. Evidence-gated workflow is DEFERRED; Faz 1 stores this as
// operator-set metadata (Approved→Active evidence requirement is a STUB). Int values PERSISTED.
public enum LegalEntityEvidenceStatus
{
    NotStarted = 1,
    Complete = 2,
    Verified = 3
}
