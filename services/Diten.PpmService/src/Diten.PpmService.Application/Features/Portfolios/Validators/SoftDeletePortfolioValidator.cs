using FluentValidation;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class SoftDeletePortfolioValidator : AbstractValidator<SoftDeletePortfolioCommand>
{
    public SoftDeletePortfolioValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
