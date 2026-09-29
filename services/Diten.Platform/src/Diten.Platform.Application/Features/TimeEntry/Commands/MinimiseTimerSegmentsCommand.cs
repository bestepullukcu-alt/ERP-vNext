using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D4 (system) — clear the start/stop instants of an approved week's segments (an audited update,
/// never a delete). Sent by the approval finalizer only when there is something to clear.</summary>
/// <para><paramref name="SegmentId"/> (v2 F3): a segment that closed onto a week already submitted or approved is minimised on
/// its own, at once — the week's approval will not come back for it.</para>
public sealed record MinimiseTimerSegmentsCommand(Guid WeekId, Guid UserId, string WeekKey, string CorrelationId, Guid? SegmentId = null)
    : IRequest<Response<long>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Redact, "TimerSegment",
        EntityId: SegmentId ?? WeekId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["transition"] = "minimise", ["weekKey"] = WeekKey, ["segmentId"] = SegmentId });
}
