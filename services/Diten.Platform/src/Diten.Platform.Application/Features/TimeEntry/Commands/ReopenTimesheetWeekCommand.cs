using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 A9 / F4 — a time admin reopens one person's week older than the edit window, with a reason;
/// creates the week's Draft revision when it has none.
/// <para><paramref name="WeekId"/> is resolved by the SERVER before the command runs (the week's open revision, or the
/// id the new revision will get), so the audit entry names the week it changed (F15) — the audit metadata is read
/// before the handler runs.</para></summary>
public sealed record ReopenTimesheetWeekCommand(ReopenTimesheetWeekRequest Request, Guid WeekId, string CorrelationId)
    : IRequest<Response<TimesheetWeekMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Reactivate, "TimesheetWeek",
        EntityId: WeekId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?>
        {
            ["userId"] = Request?.UserId, ["weekKey"] = Request?.WeekKey, ["reason"] = Request?.Reason
        });
}
