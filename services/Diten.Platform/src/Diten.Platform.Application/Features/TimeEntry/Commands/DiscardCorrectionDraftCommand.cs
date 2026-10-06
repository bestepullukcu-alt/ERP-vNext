using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 §3.2 — drop an unsubmitted correction draft (soft delete; the approved revision is untouched).</summary>
public sealed record DiscardCorrectionDraftCommand(string WeekKey, string CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Delete, "TimesheetWeekRevision",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["weekKey"] = WeekKey });
}
