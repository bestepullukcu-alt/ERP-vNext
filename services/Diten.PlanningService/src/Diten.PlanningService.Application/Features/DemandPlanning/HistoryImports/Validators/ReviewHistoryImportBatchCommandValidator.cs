using FluentValidation;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class ReviewHistoryImportBatchCommandValidator : AbstractValidator<ReviewHistoryImportBatchCommand>
{
    public ReviewHistoryImportBatchCommandValidator()
    {
        // Business-invalid attempts must reach the handler so their failure is audited.
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
    }
}
