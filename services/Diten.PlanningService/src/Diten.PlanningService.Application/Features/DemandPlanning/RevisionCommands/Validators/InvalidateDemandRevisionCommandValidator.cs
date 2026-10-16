using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class InvalidateDemandRevisionCommandValidator
    : AbstractValidator<InvalidateDemandRevisionCommand>
{
    public InvalidateDemandRevisionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.RevisionId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedStateVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImpactCode).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(2000);
    }
}
