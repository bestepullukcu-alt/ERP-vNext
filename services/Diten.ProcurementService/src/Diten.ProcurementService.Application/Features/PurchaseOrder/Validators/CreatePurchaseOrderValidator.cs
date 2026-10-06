using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Validators;

/// <summary>
/// createPurchaseOrder validation (MOD-0141 §12): supplierId + currency zorunlu; ≥1 satır; her satır itemId+uomId
/// zorunlu, quantity Decimal &amp; > 0, unitPrice Decimal &amp; ≥ 0 (float reddi). (İş kuralı 422 handler'da da
/// zorlanır — contract Unprocessable. lineAmount/totalAmount girdi DEĞİL; server-computed.)
/// </summary>
public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("supplierId zorunlu");
        RuleFor(x => x.Currency).NotEmpty().WithMessage("currency zorunlu");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.UomId).NotEmpty().WithMessage("line.uomId zorunlu");
            line.RuleFor(l => l.Quantity)
                .Must(PurchaseOrderValidationRules.IsValidDecimal).WithMessage("quantity Decimal string olmalı (float YASAK)")
                .Must(PurchaseOrderValidationRules.IsPositiveDecimal).WithMessage("quantity > 0 olmalı");
            line.RuleFor(l => l.UnitPrice)
                .Must(PurchaseOrderValidationRules.IsValidDecimal).WithMessage("unitPrice Decimal string olmalı (float YASAK)")
                .Must(PurchaseOrderValidationRules.IsNonNegativeDecimal).WithMessage("unitPrice ≥ 0 olmalı");
        });
    }
}
