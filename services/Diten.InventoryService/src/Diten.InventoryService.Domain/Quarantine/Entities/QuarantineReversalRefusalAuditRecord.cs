using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Quarantine.Entities;

/// <summary>
/// MOD-0175-FU01 DL-3 (pack §8.8, AC-31): the refusal row of the REVERSAL precondition — a REVERSAL the ledger refused with 422
/// <c>REVERSAL_QUALITY_RECORD_LOCKED</c> because its original is a MOD-0175 movement or the source of an intake quarantine / blocked record
/// whose quality decision is under way or done. Written to <c>inventory_audit</c> AFTER the refusal (nothing else was written), insert only:
/// who asked, for which original, which record locked it, why (the closed <c>cause</c> list of INVENTORY 1.8.0), the §8.3 rule, correlation.
/// </summary>
public sealed class QuarantineReversalRefusalAuditRecord : EntityBase
{
    /// <summary>The row's action — named like the ledger's own posting rows (<c>InventoryMovementPosted</c>).</summary>
    public const string RefusedAction = "QuarantineReversalRefused";

    public const string FailedOutcome = "Failed";

    public string Action { get; set; } = RefusedAction;

    public string Outcome { get; set; } = FailedOutcome;

    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>OwnMovementReversed | Decided | LinesPosted | QuantityChanged | DecisionInProgress | Unmatched.</summary>
    public string Cause { get; set; } = string.Empty;

    /// <summary>The §8.3 row behind a rule-2 refusal (R5, R5a, R6, R7, R9–R11, R13); <c>OwnModule</c> for rule 1.</summary>
    public string Rule { get; set; } = string.Empty;

    public Guid OriginalTransactionId { get; set; }

    public string OriginalTransactionNumber { get; set; } = string.Empty;

    public string OriginalMovementType { get; set; } = string.Empty;

    public string OriginalSourceModule { get; set; } = string.Empty;

    /// <summary><c>QuarantineHold</c> | <c>BlockedStock</c>; null when the original is a MOD-0175 movement that is no record's source.</summary>
    public string? RecordType { get; set; }

    public Guid? RecordId { get; set; }

    public string? RecordNumber { get; set; }

    public Guid UserId { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
