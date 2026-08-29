using FluentValidation;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class TransitionPortfolioLifecycleValidator : AbstractValidator<TransitionPortfolioLifecycleCommand>
{
    public TransitionPortfolioLifecycleValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.TargetState).IsInEnum(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
