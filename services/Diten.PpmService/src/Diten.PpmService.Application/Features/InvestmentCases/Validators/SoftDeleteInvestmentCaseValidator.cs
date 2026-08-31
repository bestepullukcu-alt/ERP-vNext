using FluentValidation;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class SoftDeleteInvestmentCaseValidator : AbstractValidator<SoftDeleteInvestmentCaseCommand>
{
    public SoftDeleteInvestmentCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
