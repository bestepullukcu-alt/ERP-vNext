using FluentValidation;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class SoftDeleteProgramValidator : AbstractValidator<SoftDeleteProgramCommand>
{
    public SoftDeleteProgramValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
