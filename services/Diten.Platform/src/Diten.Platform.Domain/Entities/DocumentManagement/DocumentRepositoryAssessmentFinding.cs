using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU16 — a single finding raised by evaluating a <see cref="DocumentRepositoryAssessment"/> (GMG-QMS-SOP-0001
/// §11.1). Findings are idempotent per <see cref="FindingKey"/> for a given assessment (re-evaluation upserts an OPEN
/// finding rather than duplicating it). Critical open findings prevent the repository from supporting the release gate.
/// Never hard-deleted; resolving changes status only.
/// </summary>
public sealed class DocumentRepositoryAssessmentFinding : TenantScopedEntity
{
    public required Guid RepositoryAssessmentId { get; set; }

    /// <summary>Deterministic dedupe key, e.g. <c>MissingRestoreTest</c>.</summary>
    public required string FindingKey { get; set; }

    public RepositoryFindingType FindingType { get; set; }
    public RepositoryFindingSeverity Severity { get; set; } = RepositoryFindingSeverity.Warning;
    public RepositoryFindingStatus Status { get; set; } = RepositoryFindingStatus.Open;

    public required string Description { get; set; }
    public string? EvidenceReference { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
