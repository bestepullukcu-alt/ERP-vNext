using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IBillingRepository
{
    Task<BillingPlan> CreatePlanAsync(BillingPlan plan, CancellationToken ct = default);
    Task<BillingPlan?> GetPlanAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BillingPlan>> GetPlansAsync(CancellationToken ct = default);
    Task<BillingPlan?> GetLatestPlanVersionAsync(string planCode, CancellationToken ct = default);
    Task<bool> IsPlanUsedByInvoiceAsync(Guid planId, int planVersion, CancellationToken ct = default);
    Task<bool> ActivatePlanAsync(Guid planId, CancellationToken ct = default);

    Task<BillingInvoice> CreateInvoiceAsync(BillingInvoice invoice, CancellationToken ct = default);
    Task<BillingInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(CancellationToken ct = default);
    Task<bool> UpdateDraftInvoiceAsync(BillingInvoice invoice, CancellationToken ct = default);
    Task<(bool Succeeded, BillingInvoice? Invoice)> IssueInvoiceAsync(Guid invoiceId, string actorUserId, CancellationToken ct = default);
    Task<(bool Succeeded, BillingInvoice? Invoice)> CancelInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

    Task<PaymentRecord?> GetPaymentByIdempotencyKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken ct = default);
    Task<(bool Succeeded, PaymentRecord? Payment, BillingInvoice? Invoice)> ApplyPaymentAsync(Guid invoiceId, PaymentRecord payment, CancellationToken ct = default);
    Task<(bool Succeeded, RefundRecord? Refund, BillingInvoice? Invoice)> CreateRefundAsync(Guid paymentRecordId, RefundRecord refund, CancellationToken ct = default);
}
