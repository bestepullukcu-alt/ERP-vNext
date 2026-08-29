using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU10 — a single release-gate EVALUATION run for a register entry (GMG-QMS-SOP-0001 §19). Each evaluation
/// is immutable and produces six <see cref="DocumentReleaseGateResult"/> rows; re-evaluation creates a NEW evaluation
/// so history is preserved (never hard-deleted). The most recent evaluation is authoritative.
/// </summary>
public sealed class DocumentReleaseGateEvaluation : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    public ReleaseGateEvaluationStatus EvaluationStatus { get; set; } = ReleaseGateEvaluationStatus.NotEvaluated;

    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? EvaluatedBy { get; set; }

    public int GateCount { get; set; }
    public int CompletedGateCount { get; set; }
    public int BlockingCount { get; set; }
    public int WarningCount { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
