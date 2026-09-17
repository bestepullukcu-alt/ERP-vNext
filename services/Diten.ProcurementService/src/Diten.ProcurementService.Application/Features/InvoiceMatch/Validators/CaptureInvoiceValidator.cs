using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;

/// <summary>
/// captureInvoice validation (MOD-0143 §12): supplierId/poId/invoiceNumber/currency zorunlu; ≥1 satır; her satır
/// itemId zorunlu, quantity Decimal &amp; > 0, unitPrice Decimal &amp; ≥ 0 (float reddi). (İş kuralı 422 handler'da da
/// zorlanır — currency/PO/supplier/item consume + duplicate handler'da; contract Unprocessable/UnknownReference/
/// DuplicateInvoice.)
/// </summary>
public sealed class CaptureInvoiceValidator : AbstractValidator<CaptureInvoiceCommand>
{
    public CaptureInvoiceValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("supplierId zorunlu");
        RuleFor(x => x.PoId).NotEmpty().WithMessage("poId zorunlu");
        RuleFor(x => x.InvoiceNumber).NotEmpty().WithMessage("invoiceNumber zorunlu");
        RuleFor(x => x.Currency).NotEmpty().WithMessage("currency zorunlu");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.Quantity)
                .Must(InvoiceMatchValidationRules.IsValidDecimal).WithMessage("quantity Decimal string olmalı (float YASAK)")
                .Must(InvoiceMatchValidationRules.IsPositiveDecimal).WithMessage("quantity > 0 olmalı");
            line.RuleFor(l => l.UnitPrice)
                .Must(InvoiceMatchValidationRules.IsValidDecimal).WithMessage("unitPrice Decimal string olmalı (float YASAK)")
                .Must(InvoiceMatchValidationRules.IsNonNegativeDecimal).WithMessage("unitPrice ≥ 0 olmalı");
        });
    }
}
