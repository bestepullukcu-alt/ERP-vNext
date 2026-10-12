using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D5 — open a correction revision of an approved week (reason required); the approved
/// revision stays in force until the correction itself is approved.</summary>
public sealed record RequestTimesheetCorrectionCommand(string WeekKey, RequestTimesheetCorrectionRequest Request, string CorrelationId)
    : IRequest<Response<TimesheetWeekMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Create, "TimesheetWeekRevision",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["weekKey"] = WeekKey, ["reason"] = Request?.Reason });
}
