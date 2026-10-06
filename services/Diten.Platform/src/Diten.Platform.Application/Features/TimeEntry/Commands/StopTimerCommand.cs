using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D2 — stop the caller's running timer. Never touches the task: stopping the timer on an
/// InProgress task leaves it InProgress and writes no task transition.</summary>
/// <param name="TaskItemId">Set by the Task Center card (T2b): stop only if the running timer is on THIS task. The card
/// can be stale — the timer may have moved to another task from the chip or another tab — and a Stop pressed on task A
/// must never stop task B. Null (the chip, My Timesheet) stops whatever runs.</param>
public sealed record StopTimerCommand(string CorrelationId, Guid? TaskItemId = null)
    : IRequest<Response<TimerMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["transition"] = "stop" });
}
