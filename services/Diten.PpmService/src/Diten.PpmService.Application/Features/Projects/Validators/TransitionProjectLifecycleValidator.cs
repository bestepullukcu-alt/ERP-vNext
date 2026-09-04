using FluentValidation;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class TransitionProjectLifecycleValidator : AbstractValidator<TransitionProjectLifecycleCommand>
{
    public TransitionProjectLifecycleValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.TargetState).IsInEnum(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
