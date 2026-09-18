using Diten.ProcurementService.Application.Features.Contract.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Contract.Validators;

/// <summary>
/// createContract validation (MOD-0144 §12): supplierId zorunlu; title zorunlu + max 200; effectiveFrom zorunlu +
/// geçerli ISO tarih; effectiveTo verilirse geçerli ISO tarih ve ≥ effectiveFrom; her clause satırı clauseId zorunlu.
/// (Consume fail-closed: supplierId MOD-0140 / rfxId MOD-0145 varlığı handler'da 404 UNKNOWN_REFERENCE ile zorlanır;
/// clauseId clause library varlığı handler'da 422 ile zorlanır — bu validator yalnız şekil/format kapısı.)
/// </summary>
public sealed class CreateContractValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("supplierId zorunlu");

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
