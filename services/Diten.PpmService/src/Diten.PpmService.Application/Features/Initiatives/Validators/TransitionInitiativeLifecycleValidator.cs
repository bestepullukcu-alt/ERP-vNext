using FluentValidation;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class TransitionInitiativeLifecycleValidator : AbstractValidator<TransitionInitiativeLifecycleCommand>
{
    public TransitionInitiativeLifecycleValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.TargetState).IsInEnum(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
