using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D3 (system) — close the person's running segment at its local midnight, if that has passed.
/// Sent by the midnight job (<paramref name="Notify"/>: one morning notification per closed person-day) and by the first
/// read after midnight (no notification — the banner replaces it). The run may end EARLIER than midnight (its task left the
/// person, the switch went off — v2 F2); <see cref="ClosedReason"/> puts the reason it actually closed with on the audit
/// entry (v3 G7).</summary>
public sealed record CloseTimersAtLocalMidnightCommand(Guid UserId, bool Notify, string CorrelationId)
    : IRequest<Response<int>>, IAuditableCommand, IAuditMetadataProvider
{
    /// <summary>
    /// The audit metadata, held by the command so the handler can add the close reason once it is known. The audit
    /// behaviour takes this dictionary before the handler runs and merges it into the entry only AFTER — so what the
    /// handler writes here is on the entry (<c>TimerCorrectionV3HttpMongoTests</c> pins that).
    /// </summary>
    private readonly Dictionary<string, object?> _metadata = new() { ["transition"] = "close-at-local-midnight" };

    /// <summary>LocalMidnight, Reconcile or SwitchedOff — set by the handler when it closed a segment; absent otherwise.</summary>
    public string? ClosedReason
    {
        get => _metadata.TryGetValue("closedReason", out var value) ? value as string : null;
        set => _metadata["closedReason"] = value;
    }

    public AuditRequestMetadata GetAuditMetadata()
    {
        _metadata["userId"] = UserId;
        return new AuditRequestMetadata(
            TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimerSegment",
            EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
            CorrelationId: TimeEntryModule.Correlation(CorrelationId),
            Metadata: _metadata);
    }
}
