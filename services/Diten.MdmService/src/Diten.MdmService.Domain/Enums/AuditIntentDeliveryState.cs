namespace Diten.MdmService.Domain.Enums;

public enum AuditIntentDeliveryState
{
    Pending = 1,
    Processing = 2,
    Delivered = 3,
    DeadLetter = 4
}
