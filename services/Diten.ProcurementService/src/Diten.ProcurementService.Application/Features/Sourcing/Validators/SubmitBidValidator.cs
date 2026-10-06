using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Sourcing.Validators;

/// <summary>
/// submitBid validation (MOD-0145 §12): supplierId zorunlu; ≥1 satır; her satır itemId zorunlu, unitPrice Decimal
/// regex &amp; ≥ 0 (float reddi); leadTimeDays (varsa) ≥ 0.
/// </summary>
public sealed class SubmitBidValidator : AbstractValidator<SubmitBidCommand>
{
    public SubmitBidValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("supplierId zorunlu");

        RuleFor(x => x.Lines)
            .NotNull().WithMessage("en az bir satır zorunlu")
            .Must(l => l is not null && l.Count > 0).WithMessage("en az bir satır zorunlu");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).NotEmpty().WithMessage("line.itemId zorunlu");
            line.RuleFor(l => l.UnitPrice)
                .Must(SourcingValidationRules.IsValidDecimal).WithMessage("unitPrice Decimal string olmalı (float YASAK)")
                .Must(SourcingValidationRules.IsNonNegativeDecimal).WithMessage("unitPrice ≥ 0 olmalı");
            line.RuleFor(l => l.LeadTimeDays)
                .Must(d => d is null || d >= 0).WithMessage("leadTimeDays ≥ 0 olmalı");
        });
    }
}
