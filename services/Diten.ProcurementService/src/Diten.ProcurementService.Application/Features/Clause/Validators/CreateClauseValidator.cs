using Diten.ProcurementService.Application.Features.Clause.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Clause.Validators;

/// <summary>
/// createClause validation (MOD-0144 §12): category/title/body zorunlu (trim). (Category+Title) duplicate kontrolü
/// handler'da 409 DUPLICATE_CLAUSE ile zorlanır — bu validator yalnız şekil kapısı.
/// </summary>
public sealed class CreateClauseValidator : AbstractValidator<CreateClauseCommand>
{
    public CreateClauseValidator()
    {
        RuleFor(x => x.Category).NotEmpty().WithMessage("category zorunlu");
        RuleFor(x => x.Title).NotEmpty().WithMessage("title zorunlu");
        RuleFor(x => x.Body).NotEmpty().WithMessage("body zorunlu");
    }
}
