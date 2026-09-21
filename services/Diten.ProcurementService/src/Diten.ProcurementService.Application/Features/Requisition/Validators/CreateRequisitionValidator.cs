using Diten.ProcurementService.Application.Features.Requisition.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Requisition.Validators;

/// <summary>
/// createRequisition validation (MOD-0141 §12): ≥1 satır; her satır itemId+uomId zorunlu, quantity Decimal regex
/// &amp; > 0 (float reddi). (İş kuralı 422 handler'da da zorlanır — contract Unprocessable.)
/// </summary>
public sealed class CreateRequisitionValidator : AbstractValidator<CreateRequisitionCommand>
{
    public CreateRequisitionValidator()
    {
        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.UomId).NotEmpty().WithMessage("line.uomId zorunlu");
            line.RuleFor(l => l.Quantity)
                .Must(RequisitionValidationRules.IsValidDecimal).WithMessage("quantity Decimal string olmalı (float YASAK)")
                .Must(RequisitionValidationRules.IsPositiveDecimal).WithMessage("quantity > 0 olmalı");
        });
    }
}
