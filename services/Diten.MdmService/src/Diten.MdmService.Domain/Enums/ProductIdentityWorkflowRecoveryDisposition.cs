namespace Diten.MdmService.Domain.Enums;

public enum ProductIdentityWorkflowRecoveryDisposition
{
    None = 0,
    Retryable = 1,
    AwaitingMakerReplay = 2,
    ManualReconciliationRequired = 3
}
