namespace Diten.Platform.Domain.Enums;

public enum PayrollIntegrationLinkState
{
    PendingValidation = 1,
    Deferred = 2,
    Validated = 3,
    Rejected = 4,
    Superseded = 5
}
