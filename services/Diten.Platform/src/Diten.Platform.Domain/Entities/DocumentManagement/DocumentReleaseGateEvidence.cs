using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU10 — IMMUTABLE manual release-gate evidence (GMG-QMS-SOP-0001 §19.1). For gates that cannot be computed
/// automatically (approved repository / execution materials / training / withdrawal method) a user records evidence
/// with a mandatory reference, verifier and date. The evaluator reads the LATEST evidence per gate. Append-only;
/// never hard-deleted; a GDocP correction trail is FU21. There is NO exception/waiver — the gates are non-waivable.
/// </summary>
public sealed class DocumentReleaseGateEvidence : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public ReleaseGateKey GateKey { get; set; }

    /// <summary>Mandatory — a gate marked met without an evidence reference is not met.</summary>
    public required string EvidenceReference { get; set; }

    public Guid VerifiedByUserId { get; set; }
    public string? VerifiedByRole { get; set; }
    public DateTimeOffset VerificationDate { get; set; } = DateTimeOffset.UtcNow;

    public string? Comment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
