using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D5/D6 — submit the caller's open draft for line-manager approval through MOD-0023.</summary>
public sealed record SubmitTimesheetWeekCommand(string WeekKey, SubmitTimesheetWeekRequest Request, string CorrelationId)
    : IRequest<Response<TimesheetWeekMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimesheetWeek",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["weekKey"] = WeekKey, ["transition"] = "submit" });
}
