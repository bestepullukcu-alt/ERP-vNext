namespace Diten.SafetyStockService.PolicyState;

public enum PolicyRevisionError
{
    InvalidInput,
    VersionConflict,
    InvalidTransition,
    SelfApproval,
    MissingRejectionReason,
    DuplicateRevisionId,
    UnchangedContent
}
