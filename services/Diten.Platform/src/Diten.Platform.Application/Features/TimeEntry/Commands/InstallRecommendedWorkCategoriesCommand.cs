using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 R6 — install the recommended categories (labels by resource key, 7 languages in T2).
/// Idempotent: a code already present is left exactly as it is.</summary>
public sealed record InstallRecommendedWorkCategoriesCommand(string CorrelationId)
    : IRequest<Response<InstallRecommendedWorkCategoriesResultDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Create, "WorkCategory",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId));
}
