using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D2 — start the caller's timer on a task they hold InProgress, or on an active category. A
/// running timer on another target is switched (and the switch can be undone for 60 s).</summary>
public sealed record StartTimerCommand(StartTimerRequest Request, string CorrelationId)
    : IRequest<Response<TimerMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?>
        {
            ["transition"] = "start", ["taskItemId"] = Request?.TaskItemId, ["categoryCode"] = Request?.CategoryCode
        });
}
