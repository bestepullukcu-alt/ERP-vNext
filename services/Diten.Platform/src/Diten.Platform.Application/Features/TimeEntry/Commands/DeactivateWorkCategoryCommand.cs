using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D10 — retire a category. Never a delete: old entries still resolve its label.</summary>
public sealed record DeactivateWorkCategoryCommand(Guid Id, WorkCategoryStateRequest Request, string CorrelationId)
    : IRequest<Response<WorkCategoryDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Deactivate, "WorkCategory",
        EntityId: Id, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId));
}
