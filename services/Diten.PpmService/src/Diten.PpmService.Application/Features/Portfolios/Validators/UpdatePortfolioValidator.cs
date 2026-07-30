using FluentValidation;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class UpdatePortfolioValidator : AbstractValidator<UpdatePortfolioCommand>
{
    public UpdatePortfolioValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(64); RuleFor(x => x.Name).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000); RuleFor(x => x.VisibilityPolicyKey).Null(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
