namespace Diten.MdmService.Domain.Enums;

public enum FirstGskuIdentityRetirementCheckpoint
{
    Prepared = 1,
    AdmissionFenceClosed = 2,
    ChildrenVerified = 3,
    GskuRetired = 4,
    RevisionRetired = 5,
    Completed = 6,
    ManualReconciliationRequired = 7
}
