using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — the membership record proving that a specific regulated record was placed under a specific
/// legal hold, and when (GMG-QMS-SOP-0001 §22). This is the evidence that the hold actually reached the record.
///
/// The subject list of an active hold may change over time, but HISTORY IS PRESERVED: removing a record from a
/// hold sets <see cref="LegalHoldSubjectStatus.Released"/> with a timestamp — the membership row is never deleted
/// and never rewritten to look as though the hold had not applied.
/// </summary>
public sealed class DocumentLegalHoldSubject : TenantScopedEntity
{
    public required Guid LegalHoldId { get; set; }
    public RetentionSubjectType SubjectType { get; set; } = RetentionSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }

    public DateTimeOffset HoldAppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? HoldReleasedAt { get; set; }
    public LegalHoldSubjectStatus Status { get; set; } = LegalHoldSubjectStatus.Active;

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
