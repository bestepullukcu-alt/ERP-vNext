namespace Diten.Platform.Domain.Enums.Billing;

public enum BillingActivityEventType
{
    InvoiceCreated = 0,
    InvoiceUpdated = 1,
    InvoiceIssued = 2,
    InvoiceCancelled = 3,
    PaymentApplied = 4,
    RefundCreated = 5,
    PlanCreated = 6,
    PlanActivated = 7,
    PlanVersionCreated = 8
}
