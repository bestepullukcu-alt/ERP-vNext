using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 §13 (system) — close the person's running segment when the read finds it orphaned: its task
/// is no longer InProgress, no longer theirs or gone (<c>Reconcile</c>, at the invalidating transition), or their legal
/// entity has the timer switched off (<c>SwitchedOff</c>).</summary>
public sealed record ReconcileOrphanTimerCommand(Guid UserId, string CorrelationId)
    : IRequest<Response<int>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["transition"] = "reconcile", ["userId"] = UserId });
}
