using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175-FU01 DL-3 — the ledger guard (pack §8.8, Q-06 = A): MOD-0175's <see cref="IReversalPrecondition"/>. Two rules, nothing else:
/// <list type="number">
/// <item>the original is a MOD-0175 movement (<c>SourceModule = MOD-0175</c>: "Karantinaya Al", "Stoğu bloke et", a decision line, Rework,
/// the sample move …) → refused, <c>cause OwnMovementReversed</c> — its REVERSAL would skip the quality decision (DEC-INV-08); the record that
/// has it as its source movement is named when there is one;</item>
/// <item>the original is the source movement of an INTAKE quarantine / blocked record and the §8.3 table — the SAME
/// <see cref="QuarantineSourceReversal.PreviewAsync"/> facts and <see cref="SourceReversalRules.Evaluate"/> DL-1 closes with — answers
/// Conflict (R6, R7, R9–R11), a decision being applied (R5, R5a → <c>DecisionInProgress</c>) or nothing it knows (R13 → <c>Unmatched</c>,
/// fail-closed) → refused.</item>
/// </list>
/// Every other answer (R2–R4, R8, no record) is Allow — DL-1 then closes the record. A record of MOD-0175 origin (R1) is rule 1's: its
/// source movement is a MOD-0175 movement; rule 2 does not answer for it (pack §8.8 — rule 2 names intake records only). Reads are the posting's own tenant + company (the
/// scoped repositories); a read that fails throws, and the posting answers 503 (never Allow). A refusal leaves one
/// <see cref="QuarantineReversalRefusalAuditRecord"/> in <c>inventory_audit</c>, written by <see cref="FlushRefusalAuditAsync"/>.
/// </summary>
public sealed class QuarantineReversalPrecondition : IReversalPrecondition
{
    public const string OwnModuleRule = "OwnModule";

    private readonly QuarantineSourceReversal _reversal;
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineSupport _support;
    private readonly ILogger<QuarantineReversalPrecondition> _logger;
    private QuarantineReversalRefusalAuditRecord? _pending;

    public QuarantineReversalPrecondition(
        QuarantineSourceReversal reversal, IQuarantineRepository repository, QuarantineSupport support, ILogger<QuarantineReversalPrecondition> logger)
    {
        _reversal = reversal;
        _repository = repository;
        _support = support;
        _logger = logger;
    }

    public async Task<ReversalPreconditionResult> CheckAsync(InventoryTransaction original, CancellationToken cancellationToken)
    {
        _pending = null;
        if (string.Equals(original.SourceModule, QuarantineContract.ModuleId, StringComparison.Ordinal))
        {
            var (hold, blocked) = await _repository.FindSourceRecordAsync(original.Id.ToString("D"), cancellationToken);
            var (type, id, number) = hold is not null ? (HoldType, (Guid?)hold.Id, hold.HoldNumber)
                : blocked is not null ? (BlockedType, (Guid?)blocked.Id, blocked.BlockedNumber)
                : ((string?)null, (Guid?)null, (string?)null);
            return Refuse(original, OwnMovementReversed, OwnModuleRule, type, id, number,
                "The original is a MOD-0175 quality movement: its REVERSAL would skip the quality decision. Correct it with a new quarantine decision.");
        }

        var preview = await _reversal.PreviewAsync(original, cancellationToken);
        if (preview is null)
        {
            return ReversalPreconditionResult.Allow;
        }

        var cause = CauseOf(preview.Outcome);
        if (cause is null)
        {
            return ReversalPreconditionResult.Allow;
        }

        return Refuse(original, cause, preview.Outcome.Rule, preview.Subject == HistorySubject.Hold ? HoldType : BlockedType, preview.RecordId,
            preview.RecordNumber,
            $"The original is the source movement of quarantine record {preview.RecordNumber}, whose quality decision locks it ({cause}). Correct it with a new quarantine decision.");
    }

    public async Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default)
    {
        if (_pending is { } row)
        {
            _pending = null;
            await _repository.InsertReversalRefusalAuditAsync(row, cancellationToken);
        }
    }

    // ── the closed cause list of INVENTORY 1.8.0 (pack §8.8) ──────────────────────────────────────────────────

    public const string HoldType = "QuarantineHold";
    public const string BlockedType = "BlockedStock";
    public const string OwnMovementReversed = "OwnMovementReversed";
    public const string Decided = "Decided";
    public const string LinesPosted = "LinesPosted";
    public const string QuantityChanged = "QuantityChanged";
    public const string DecisionInProgress = "DecisionInProgress";
    public const string Unmatched = "Unmatched";

    /// <summary>
    /// Rule 2: the §8.3 answer → the refusal cause; null = Allow (R2–R4, R8 close the record; R1 is rule 1's — a MOD-0175 record; R0 / R0a / R12
    /// cannot meet a new REVERSAL).
    /// </summary>
    public static string? CauseOf(SourceReversalOutcome outcome) => outcome.Verdict switch
    {
        SourceReversalVerdict.Conflict => outcome.Cause switch
        {
            SourceReversalCause.OwnMovementReversed => null,
            SourceReversalCause.Decided => Decided,
            SourceReversalCause.LinesPosted => LinesPosted,
            SourceReversalCause.QuantityChanged => QuantityChanged,
            _ => Unmatched // a Conflict without a cause the list knows: fail-closed
        },
        SourceReversalVerdict.Voided when outcome.Rule is "R5" or "R5a" => DecisionInProgress,
        SourceReversalVerdict.Voided => null,
        SourceReversalVerdict.NoChange when outcome.NoChange == SourceReversalNoChange.Unmatched => Unmatched,
        SourceReversalVerdict.NoChange => null,
        _ => Unmatched
    };

    private ReversalPreconditionResult Refuse(
        InventoryTransaction original, string cause, string rule, string? recordType, Guid? recordId, string? recordNumber, string message)
    {
        _logger.LogWarning("quarantine.reversal_precondition.refused Original={Original} Cause={Cause} Rule={Rule} Record={Record}",
            original.TransactionNumber, cause, rule, recordNumber ?? "-");
        var now = QuarantineSupport.Now();
        _pending = new QuarantineReversalRefusalAuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            ErrorCode = InventoryErrorCodes.ReversalQualityRecordLocked,
            Cause = cause,
            Rule = rule,
            OriginalTransactionId = original.Id,
            OriginalTransactionNumber = original.TransactionNumber,
            OriginalMovementType = WireNames.ToWire(original.MovementType),
            OriginalSourceModule = original.SourceModule,
            RecordType = recordType,
            RecordId = recordId,
            RecordNumber = recordNumber,
            UserId = _support.UserId,
            CorrelationId = _support.CorrelationId,
            OccurredAt = now
        };

        var details = new Dictionary<string, object?> { ["cause"] = cause };
        if (recordNumber is not null)
        {
            details["recordNumber"] = recordNumber;
            details["recordType"] = recordType;
        }

        return ReversalPreconditionResult.Refuse(InventoryErrorCodes.ReversalQualityRecordLocked, message, details);
    }
}
