using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU12 — an escalation raised by the periodic-review engine (GMG-QMS-SOP-0001 §9.15, §15). There is NO
/// tolerance band for an overdue Critical review: it escalates to the GQD, who determines on a documented impact
/// assessment whether the document may remain Effective or shall be Suspended. This FU RAISES the escalation and holds
/// the determination requirement; performing a suspension is FU13. Never hard-deleted.
/// </summary>
public sealed class DocumentPeriodicReviewEscalation : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public required Guid PeriodicReviewId { get; set; }

    public ReviewEscalationType EscalationType { get; set; }
    public ReviewEscalationSeverity Severity { get; set; } = ReviewEscalationSeverity.Warning;
    public ReviewEscalationStatus Status { get; set; } = ReviewEscalationStatus.Open;
    public ReviewEscalationRole RequiredRole { get; set; } = ReviewEscalationRole.QADocumentation;

    public required string Description { get; set; }
    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionComment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
