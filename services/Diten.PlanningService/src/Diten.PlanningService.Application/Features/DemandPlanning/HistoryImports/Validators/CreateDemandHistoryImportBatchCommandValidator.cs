using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class CreateDemandHistoryImportBatchCommandValidator
    : AbstractValidator<CreateDemandHistoryImportBatchCommand>
{
    public CreateDemandHistoryImportBatchCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.SelectedLegalEntityHint).NotEmpty();
        RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SourceFileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ScopeFrom).NotEqual(default(DateOnly));
        RuleFor(x => x.ScopeThrough).GreaterThanOrEqualTo(x => x.ScopeFrom);
        RuleFor(x => x.WarehouseScope).NotEmpty();
        RuleForEach(x => x.WarehouseScope).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FileBytes).NotEmpty().Must(x => x.Length <= 1_048_576)
            .WithMessage("File must be no larger than 1 MiB.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
