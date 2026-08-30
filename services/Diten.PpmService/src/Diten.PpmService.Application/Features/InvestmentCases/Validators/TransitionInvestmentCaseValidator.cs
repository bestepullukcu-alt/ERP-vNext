using FluentValidation;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class TransitionInvestmentCaseValidator : AbstractValidator<TransitionInvestmentCaseLifecycleCommand>
{
    public TransitionInvestmentCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetState).IsInEnum();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
