using Diten.Platform.Application.Features.DocumentManagementLifecycle.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle.Validators;

// MOD-0029-FU08 — input-shape validators. Transition legality and per-status reason rules stay in the service.

public sealed class TransitionDocumentLifecycleValidator : AbstractValidator<TransitionDocumentLifecycleCommand>
{
    public TransitionDocumentLifecycleValidator()
    {
        RuleFor(x => x.RegisterEntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.TargetStatus).NotEmpty().When(x => x.Input is not null);
    }
}
