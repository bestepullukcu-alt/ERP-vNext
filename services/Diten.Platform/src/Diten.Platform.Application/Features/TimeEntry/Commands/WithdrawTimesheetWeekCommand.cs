using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 A6 — the person takes a submitted week back before the approver decides.</summary>
public sealed record WithdrawTimesheetWeekCommand(string WeekKey, WithdrawTimesheetWeekRequest Request, string CorrelationId)
    : IRequest<Response<TimesheetWeekMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimesheetWeek",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["weekKey"] = WeekKey, ["transition"] = "withdraw" });
}
