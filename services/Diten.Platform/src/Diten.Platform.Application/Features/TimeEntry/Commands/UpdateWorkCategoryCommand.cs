using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D10 — edit a category's label, description, counts-as-work and order (never its code).</summary>
public sealed record UpdateWorkCategoryCommand(Guid Id, UpdateWorkCategoryRequest Request, string CorrelationId)
    : IRequest<Response<WorkCategoryDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Update, "WorkCategory",
        EntityId: Id, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["code"] = Request?.Code });
}
