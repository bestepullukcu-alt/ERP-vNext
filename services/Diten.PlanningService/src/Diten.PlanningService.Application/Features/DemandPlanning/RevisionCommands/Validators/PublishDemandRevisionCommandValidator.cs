using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed class PublishDemandRevisionCommandValidator
    : AbstractValidator<PublishDemandRevisionCommand>
{
    public PublishDemandRevisionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.RevisionId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedStateVersion).GreaterThanOrEqualTo(0);
    }
}
