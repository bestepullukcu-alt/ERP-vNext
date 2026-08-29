using FluentValidation;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class CreateInitiativeValidator : AbstractValidator<CreateInitiativeCommand>
{
    public CreateInitiativeValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(64); RuleFor(x => x.Name).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000); RuleFor(x => x.PortfolioId).NotEqual(Guid.Empty); RuleFor(x => x.VisibilityPolicyKey).Null().WithMessage("VisibilityPolicyKey is unavailable until authoritative MOD-0018 validation is integrated."); }
}
