using FluentValidation;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class CreatePortfolioValidator : AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(64); RuleFor(x => x.Name).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000); RuleFor(x => x.VisibilityPolicyKey).Null().WithMessage("VisibilityPolicyKey is unavailable until authoritative MOD-0018 validation is integrated."); }
}
