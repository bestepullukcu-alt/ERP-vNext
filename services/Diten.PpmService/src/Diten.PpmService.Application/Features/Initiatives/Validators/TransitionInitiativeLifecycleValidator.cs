using FluentValidation;
using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class TransitionInitiativeLifecycleValidator : AbstractValidator<TransitionInitiativeLifecycleCommand>
{
    public TransitionInitiativeLifecycleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetState).IsInEnum();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Closure!.CompletionSummary).NotEmpty().MaximumLength(4000).When(x => x.Closure is not null);
        RuleFor(x => x.Closure!.OutcomeCode).Must(x => InitiativeVocabularies.CompletionOutcomes.Contains(x, StringComparer.Ordinal)).When(x => x.Closure is not null);
        RuleFor(x => x.Closure!.ClosureReasonCode).Must(x => InitiativeVocabularies.ClosureReasons.Contains(x, StringComparer.Ordinal)).When(x => x.Closure is not null);
        RuleFor(x => x.Closure!.BenefitDisposition).Must(x => InitiativeVocabularies.BenefitDispositions.Contains(x, StringComparer.Ordinal)).When(x => x.Closure is not null);
    }
}
