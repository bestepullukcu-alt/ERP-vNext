namespace Diten.MdmService.Domain.Repositories;

// LSKU FIX1 B1: refusals of the child-admission release and of its operator repair. None reaches a screen: the
// release's refusal is read by the LSKU creation itself, the repair's by the operator call that has no screen.
public static class ChildCreationAdmissionCodes
{
    public const string ChildRowExists = "CHILD_CREATION_ROW_EXISTS";
    public const string RepairContractInvalid = "CHILD_ADMISSION_REPAIR_CONTRACT_INVALID";
    public const string RepairIdempotencyConflict = "CHILD_ADMISSION_REPAIR_IDEMPOTENCY_CONFLICT";
}
