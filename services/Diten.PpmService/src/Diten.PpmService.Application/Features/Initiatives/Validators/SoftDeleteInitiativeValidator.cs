using FluentValidation;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class SoftDeleteInitiativeValidator : AbstractValidator<SoftDeleteInitiativeCommand>
{
    public SoftDeleteInitiativeValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
