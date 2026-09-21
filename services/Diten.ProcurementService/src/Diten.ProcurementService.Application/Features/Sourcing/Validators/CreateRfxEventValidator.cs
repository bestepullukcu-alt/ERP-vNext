using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Sourcing.Validators;

/// <summary>
/// createRfxEvent validation (MOD-0145 §12): Title zorunlu/trim; ≥1 satır; her satır itemId+uomId zorunlu,
/// quantity Decimal regex &amp; > 0 (float reddi). (İş kuralı 422 handler'da da zorlanır — contract Unprocessable.)
/// </summary>
public sealed class CreateRfxEventValidator : AbstractValidator<CreateRfxEventCommand>
{
    public CreateRfxEventValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("title zorunlu")
            .MaximumLength(400);

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.UomId).NotEmpty().WithMessage("line.uomId zorunlu");
            line.RuleFor(l => l.Quantity)
                .Must(SourcingValidationRules.IsValidDecimal).WithMessage("quantity Decimal string olmalı (float YASAK)")
                .Must(SourcingValidationRules.IsPositiveDecimal).WithMessage("quantity > 0 olmalı");
        });
    }
}
