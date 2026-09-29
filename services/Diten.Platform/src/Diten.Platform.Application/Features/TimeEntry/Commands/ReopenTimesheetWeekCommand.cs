using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 A9 / F4 — a time admin reopens one person's week older than the edit window, with a reason;
/// creates the week's Draft revision when it has none.</summary>
public sealed record ReopenTimesheetWeekCommand(ReopenTimesheetWeekRequest Request, string CorrelationId)
    : IRequest<Response<TimesheetWeekMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Reactivate, "TimesheetWeek",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?>
        {
            ["userId"] = Request?.UserId, ["weekKey"] = Request?.WeekKey, ["reason"] = Request?.Reason
        });
}
