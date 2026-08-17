using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.Billing;

public sealed record BillingPlanDto(
    Guid Id,
    string PlanCode,
    int PlanVersion,
    string Status,
    string Name,
    decimal Amount,
    string Currency,
    string BillingInterval);

public sealed record InvoiceLineRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount);

public sealed record InvoiceLineDto(
    Guid Id,
    string Description,
    string Currency,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal);

public sealed record BillingInvoiceDto(
    Guid Id,
    string? InvoiceNumber,
    string Status,
    Guid BillingPlanId,
    int BillingPlanVersion,
    string BillingPlanNameSnapshot,
    decimal BillingPlanAmountSnapshot,
    string BillingPlanCurrencySnapshot,
    string CustomerReference,
    string Currency,
    DateTimeOffset? DueDate,
    string? Notes,
    DateTimeOffset? IssuedAt,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal BalanceDue,
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed record PaymentRecordDto(
    Guid Id,
    Guid InvoiceId,
    string IdempotencyKey,
    string Currency,
    decimal AppliedAmount,
    decimal RefundedAmount,
    string Status,
    string? Reference);

public sealed record RefundRecordDto(
    Guid Id,
    Guid InvoiceId,
    Guid PaymentRecordId,
    decimal RefundAmount,
    string Currency,
    string Reason);

public sealed record CreateBillingPlanRequest(
    string PlanCode,
    string Name,
    decimal Amount,
    string Currency,
    string BillingInterval);

public sealed record ReviseBillingPlanRequest(
    string Name,
    decimal Amount,
    string Currency,
    string BillingInterval);

public sealed record CreateInvoiceRequest(
    Guid BillingPlanId,
    string CustomerReference,
    DateTimeOffset? DueDate,
    string? Notes,
    IReadOnlyList<InvoiceLineRequest> Lines);

public sealed record UpdateInvoiceRequest(
    string CustomerReference,
    DateTimeOffset? DueDate,
    string? Notes,
    IReadOnlyList<InvoiceLineRequest> Lines);

public sealed record CreatePaymentRequest(
    string IdempotencyKey,
    decimal AppliedAmount,
    string Currency,
    string? Reference);

public sealed record CreateRefundRequest(
    decimal RefundAmount,
    string Currency,
    string Reason);

public static class BillingMapper
{
    public static BillingPlanDto ToDto(BillingPlan plan) =>
        new(plan.Id, plan.PlanCode, plan.PlanVersion, plan.Status.ToString(), plan.Name, plan.Amount, plan.Currency, plan.BillingInterval.ToString());

    public static BillingInvoiceDto ToDto(BillingInvoice invoice) =>
        new(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status.ToString(),
            invoice.BillingPlanId,
            invoice.BillingPlanVersion,
            invoice.BillingPlanNameSnapshot,
            invoice.BillingPlanAmountSnapshot,
            invoice.BillingPlanCurrencySnapshot,
            invoice.CustomerReference,
            invoice.Currency,
            invoice.DueDate,
            invoice.Notes,
            invoice.IssuedAt,
            invoice.SubTotal,
            invoice.TaxTotal,
            invoice.DiscountTotal,
            invoice.GrandTotal,
            invoice.PaidAmount,
            invoice.BalanceDue,
            invoice.Lines.Select(ToDto).ToList());

    public static PaymentRecordDto ToDto(PaymentRecord payment) =>
        new(payment.Id, payment.InvoiceId, payment.IdempotencyKey, payment.Currency, payment.AppliedAmount, payment.RefundedAmount, payment.Status.ToString(), payment.Reference);

    public static RefundRecordDto ToDto(RefundRecord refund) =>
        new(refund.Id, refund.InvoiceId, refund.PaymentRecordId, refund.RefundAmount, refund.Currency, refund.Reason);

    private static InvoiceLineDto ToDto(InvoiceLine line) =>
        new(line.Id, line.Description, line.Currency, line.Quantity, line.UnitPrice, line.DiscountAmount, line.TaxAmount, line.LineTotal);
}
