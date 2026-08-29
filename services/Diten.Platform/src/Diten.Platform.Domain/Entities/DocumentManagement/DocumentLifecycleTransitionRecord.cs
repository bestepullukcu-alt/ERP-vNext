using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU08 — an immutable lifecycle transition record for a Document Master Register entry (GMG-QMS-SOP-0001
/// §6.2). One row per transition, forming a permanent lifecycle history / evidence trail (this is lifecycle history,
/// NOT the GDocP correction trail, which is FU21, and it is additive to the central audit behaviour). Never
/// hard-deleted.
/// </summary>
public sealed class DocumentLifecycleTransitionRecord : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    public ControlledDocumentLifecycleStatus FromStatus { get; set; }
    public ControlledDocumentLifecycleStatus ToStatus { get; set; }

    public string? TransitionReason { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Comment { get; set; }

    /// <summary>Set only on the MarkEffective transition.</summary>
    public DateTimeOffset? EffectiveDate { get; set; }

    /// <summary>The replacement entry involved in a supersession, when applicable.</summary>
    public Guid? RelatedReplacementRegisterEntryId { get; set; }

    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PerformedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
