using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D3 (system) — close the person's running segment at its local midnight, if that has passed.
/// Sent by the midnight job (<paramref name="Notify"/>: one morning notification per closed person-day) and by the first
/// read after midnight (no notification — the banner replaces it).</summary>
public sealed record CloseTimersAtLocalMidnightCommand(Guid UserId, bool Notify, string CorrelationId)
    : IRequest<Response<int>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["transition"] = "close-at-local-midnight", ["userId"] = UserId });
}
