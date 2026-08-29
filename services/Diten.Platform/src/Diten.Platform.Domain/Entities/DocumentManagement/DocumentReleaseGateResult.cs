using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU10 — the result of ONE of the six non-waivable gates within a <see cref="DocumentReleaseGateEvaluation"/>
/// (GMG-QMS-SOP-0001 §19, §19.1). Every gate carries a Yes/No result plus, when met, an evidence reference, a verifier
/// and a verification date. <see cref="ExceptionPermitted"/> is permanently false and is never client-editable; there
/// is no manual override. A "Yes" without evidence/verifier/date is downgraded to "No" by the evaluator.
/// </summary>
public sealed class DocumentReleaseGateResult : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public required Guid EvaluationId { get; set; }

    public ReleaseGateKey GateKey { get; set; }
    public int GateNumber { get; set; }
    public required string GateName { get; set; }

    public ReleaseGateResultValue GateResult { get; set; } = ReleaseGateResultValue.No;

    /// <summary>All six gates are non-waivable (SOP §19). Always true; not editable.</summary>
    public bool IsNonWaivable { get; set; } = true;

    /// <summary>Permanently false — the exception field for a non-waivable gate is not editable (SOP §19.1).</summary>
    public bool ExceptionPermitted { get; set; }

    public string? EvidenceReference { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerifiedByRole { get; set; }
    public DateTimeOffset? VerificationDate { get; set; }

    public ReleaseGateEvidenceSource Source { get; set; } = ReleaseGateEvidenceSource.Computed;

    public string? BlockingReason { get; set; }
    public string? WarningReason { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
