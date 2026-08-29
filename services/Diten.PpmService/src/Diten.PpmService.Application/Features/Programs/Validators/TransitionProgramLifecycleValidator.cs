using FluentValidation;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class TransitionProgramLifecycleValidator : AbstractValidator<TransitionProgramLifecycleCommand>
{
    public TransitionProgramLifecycleValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.TargetState).IsInEnum(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
