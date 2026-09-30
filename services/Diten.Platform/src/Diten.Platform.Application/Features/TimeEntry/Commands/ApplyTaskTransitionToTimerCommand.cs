using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D2 (system) — what a MOD-0024 transition means for the timers: Start/Resume by the holder
/// starts the holder's timer on the task; a write that leaves InProgress or moves the holder stops it. Sent by the
/// transition observer only when there is something to do, so every audit entry is an effective change.</summary>
public sealed record ApplyTaskTransitionToTimerCommand(TaskTransitionObservation Observation, string CorrelationId)
    : IRequest<Response<string>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
        EntityId: Observation?.TaskItemId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?>
        {
            ["transition"] = "task-transition",
            ["taskTransitionId"] = Observation?.TransitionId,
            ["kind"] = Observation?.Kind.ToString()
        });
}
