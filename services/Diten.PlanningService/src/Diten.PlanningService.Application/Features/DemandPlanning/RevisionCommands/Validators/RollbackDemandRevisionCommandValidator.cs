using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class RollbackDemandRevisionCommandValidator
    : AbstractValidator<RollbackDemandRevisionCommand>
{
    public RollbackDemandRevisionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.SourceRevisionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2_000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpectedSourceStateVersion).GreaterThan(0);
    }
}
