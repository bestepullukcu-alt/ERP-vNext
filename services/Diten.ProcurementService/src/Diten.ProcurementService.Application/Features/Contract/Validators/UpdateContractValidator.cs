using Diten.ProcurementService.Application.Features.Contract.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Contract.Validators;

/// <summary>
/// updateContract validation (ASSUMPTION-0144-01 additive): contractId + title zorunlu (title max 200); effectiveFrom
/// zorunlu + geçerli ISO tarih; effectiveTo verilirse geçerli + ≥ effectiveFrom; her clause satırı clauseId zorunlu.
/// State gate (Draft-only) + consume fail-closed + clause varlığı handler'da zorlanır.
/// </summary>
public sealed class UpdateContractValidator : AbstractValidator<UpdateContractCommand>
{
    public UpdateContractValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty().WithMessage("contractId zorunlu");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("title zorunlu")
            .MaximumLength(200).WithMessage("title en fazla 200 karakter");

        RuleFor(x => x.EffectiveFrom)
            .NotEmpty().WithMessage("effectiveFrom zorunlu")
            .Must(ContractValidationRules.IsValidDate).WithMessage("effectiveFrom geçerli tarih (yyyy-MM-dd) olmalı");

        RuleFor(x => x.EffectiveTo)
            .Must(ContractValidationRules.IsValidDate).When(x => !string.IsNullOrWhiteSpace(x.EffectiveTo))
            .WithMessage("effectiveTo geçerli tarih (yyyy-MM-dd) olmalı");

        RuleFor(x => x)
            .Must(x => ContractValidationRules.IsEffectiveRangeValid(x.EffectiveFrom, x.EffectiveTo))
            .WithMessage("effectiveTo ≥ effectiveFrom olmalı");

        RuleForEach(x => x.Clauses).ChildRules(clause =>
        {
            clause.RuleFor(c => c.ClauseId).NotEmpty().WithMessage("clause.clauseId zorunlu");
        });
    }
}
