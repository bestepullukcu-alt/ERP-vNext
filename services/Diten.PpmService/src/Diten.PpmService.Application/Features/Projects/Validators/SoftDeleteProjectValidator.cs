using FluentValidation;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class SoftDeleteProjectValidator : AbstractValidator<SoftDeleteProjectCommand>
{
    public SoftDeleteProjectValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
