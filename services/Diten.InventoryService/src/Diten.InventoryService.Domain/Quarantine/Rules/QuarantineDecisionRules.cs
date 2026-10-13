using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Rules;

/// <summary>A refused rule: the contract error code (pack §12), its HTTP status and an English message.</summary>
public sealed record QuarantineViolation(string Code, int Status, string Message);

/// <summary>A requested hold-decision line (before the target status is derived).</summary>
public sealed record RequestedLine(int LineNo, LineDecision Decision, decimal Quantity);

/// <summary>The requested disposition of blocked stock.</summary>
public sealed record RequestedDisposition(
    DispositionKind Kind,
    decimal Quantity,
    Guid? SupplierId,
    string? ReturnDocumentNo,
    string? DestructionMethod,
    DateOnly? DestructionDate);

/// <summary>
/// MOD-0175 pack §12 V1–V14 as pure rules, in the order the pack lists them. The handler gathers the facts (target record,
/// lot status, current bucket balance, active block reasons, sample location) and asks; the first violation is the answer.
/// Codes are the QUARANTINE-BUNDLE v1 codes (contract §4.24); <c>REASON_REQUIRED</c> / <c>LOT_DATES_INVALID</c> are the
/// TRACE codes reused on purpose.
/// </summary>
public static class QuarantineDecisionRules
{
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 1000;
    public const int MaxDestructionBackdateDays = 30;

    // ── codes (contract §4.24) ────────────────────────────────────────────────────────────────────────────
    public const string DecisionInvalid = "QUARANTINE_DECISION_INVALID";
    public const string PartialSumMismatch = "QUARANTINE_PARTIAL_SUM_MISMATCH";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string CoaNotConfirmed = "QUARANTINE_COA_NOT_CONFIRMED";
    public const string BlockReasonRequired = "QUARANTINE_BLOCK_REASON_REQUIRED";
    public const string DecisionAlreadyPending = "QUARANTINE_DECISION_ALREADY_PENDING";
    public const string HoldClosed = "QUARANTINE_HOLD_CLOSED";
    public const string BlockedClosed = "QUARANTINE_BLOCKED_CLOSED";
    public const string StockChanged = "QUARANTINE_STOCK_CHANGED";
    public const string LotHeld = "QUARANTINE_LOT_HELD";
    public const string RetestDateRequired = "QUARANTINE_RETEST_DATE_REQUIRED";
    public const string LotDatesInvalid = "LOT_DATES_INVALID";
    public const string QuantityInvalid = "QUARANTINE_QUANTITY_INVALID";
    public const string SupplierRequired = "QUARANTINE_SUPPLIER_REQUIRED";
    public const string DestructionMethodRequired = "QUARANTINE_DESTRUCTION_METHOD_REQUIRED";
    public const string SampleLocationNotSet = "QUARANTINE_SAMPLE_LOCATION_NOT_SET";
    public const string SampleLocationInvalid = "QUARANTINE_SAMPLE_LOCATION_INVALID";
    public const string ReviewNotCompleted = "QUARANTINE_REVIEW_NOT_COMPLETED";

    /// <summary>V3: trimmed reason 10–1000.</summary>
    public static QuarantineViolation? Reason(string? reason)
    {
        var length = reason?.Trim().Length ?? 0;
        return length is >= MinReasonLength and <= MaxReasonLength
            ? null
            : new(ReasonRequired, 400, $"reason is required: {MinReasonLength}-{MaxReasonLength} characters.");
    }

    /// <summary>V1 / V2 (KQ-A): one line for the whole quantity, or two different decisions, each &gt; 0, summing to it.</summary>
    public static QuarantineViolation? Lines(IReadOnlyList<RequestedLine> lines, decimal holdQuantity)
    {
        if (lines.Count is < 1 or > 2 || lines.Select(l => l.LineNo).Distinct().Count() != lines.Count
            || lines.Any(l => l.LineNo is < 1 or > 2))
        {
            return new(DecisionInvalid, 400, "A hold decision has one line, or two lines (lineNo 1 and 2) when it is partial.");
        }

        if (lines.Count == 1)
        {
            return lines[0].Quantity == holdQuantity
                ? null
                : new(DecisionInvalid, 400, "A single-line decision decides the whole hold quantity.");
        }

        if (lines[0].Decision == lines[1].Decision)
        {
            return new(DecisionInvalid, 400, "The two lines of a partial decision need different decisions.");
        }

        if (lines.Any(l => l.Quantity <= 0m))
        {
            return new(PartialSumMismatch, 400, "Every line of a partial decision must be greater than 0.");
        }

        return lines.Sum(l => l.Quantity) == holdQuantity
            ? null
            : new(PartialSumMismatch, 400, $"The lines total {lines.Sum(l => l.Quantity)} but the hold quantity is {holdQuantity}.");
    }

    /// <summary>V4: a Release line needs the CoA / analysis result confirmed.</summary>
    public static QuarantineViolation? Coa(IReadOnlyList<RequestedLine> lines, bool coaChecked)
        => lines.Any(l => l.Decision == LineDecision.Release) && !coaChecked
            ? new(CoaNotConfirmed, 400, "Releasing needs the CoA / analysis result confirmed (coaChecked).")
            : null;

    /// <summary>V5: a Reject / Block line needs an ACTIVE block-reason code.</summary>
    public static QuarantineViolation? BlockReason(bool needsReason, string? code, IReadOnlySet<string> activeCodes)
        => needsReason && (string.IsNullOrWhiteSpace(code) || !activeCodes.Contains(code))
            ? new(BlockReasonRequired, 400, "Rejecting or blocking needs an active block-reason code.")
            : null;

    /// <summary>V6: the hold is open and no decision is waiting for approval.</summary>
    public static QuarantineViolation? HoldState(HoldStatus status, Guid? activeDecisionId)
    {
        if (status == HoldStatus.Decided)
        {
            return new(HoldClosed, 409, "The hold is already decided.");
        }

        return activeDecisionId is not null || status == HoldStatus.DecisionPending
            ? new(DecisionAlreadyPending, 409, "Another decision of this hold is waiting for approval.")
            : null;
    }

    /// <summary>V6 for a blocked record.</summary>
    public static QuarantineViolation? BlockedState(BlockedStatus status, Guid? activeDecisionId)
    {
        if (status != BlockedStatus.Open)
        {
            return new(BlockedClosed, 409, "The blocked-stock record is closed.");
        }

        return activeDecisionId is not null
            ? new(DecisionAlreadyPending, 409, "Another decision of this blocked record is waiting for approval.")
            : null;
    }

    /// <summary>V7: the bucket still holds the quantity (re-checked by the ledger at apply).</summary>
    public static QuarantineViolation? Bucket(decimal bucketOnHand, decimal quantity)
        => bucketOnHand >= quantity
            ? null
            : new(StockChanged, 409, $"The stock row holds {bucketOnHand}, less than the decided {quantity}; it changed since the record was made.");

    /// <summary>V8 (CT-D4): on a Blocked / Expired lot only Destroy is possible.</summary>
    public static QuarantineViolation? LotHold(bool lotHeld, DecisionKind kind, DispositionKind? disposition)
        => lotHeld && !(kind == DecisionKind.Disposition && disposition == DispositionKind.Destroy)
            ? new(LotHeld, 409, "The lot is Blocked or Expired: only destruction is possible (CT-D4).")
            : null;

    /// <summary>V9 (KQ-H): releasing a Retest hold needs a new retest date after today and not after expiry.</summary>
    public static QuarantineViolation? RetestDate(bool retestRelease, DateOnly? newRetestDate, DateOnly today, DateOnly? expiry)
    {
        if (!retestRelease)
        {
            return null;
        }

        if (newRetestDate is null)
        {
            return new(RetestDateRequired, 400, "Releasing a retest hold needs newRetestDate.");
        }

        return newRetestDate <= today || (expiry is not null && newRetestDate > expiry)
            ? new(LotDatesInvalid, 400, "newRetestDate must be after today and not after the lot's expiry date.")
            : null;
    }

    /// <summary>V10–V12: the disposition's own fields.</summary>
    public static QuarantineViolation? Disposition(RequestedDisposition disposition, decimal blockedQuantity, DateOnly today)
    {
        if (disposition.Quantity <= 0m || disposition.Quantity > blockedQuantity)
        {
            return new(QuantityInvalid, 400, $"The disposition quantity must be greater than 0 and at most {blockedQuantity}.");
        }

        if (disposition.Kind == DispositionKind.ReturnToSupplier
            && (disposition.SupplierId is null || string.IsNullOrWhiteSpace(disposition.ReturnDocumentNo)))
        {
            return new(SupplierRequired, 400, "A return to the supplier needs supplierId and returnDocumentNo.");
        }

        if (disposition.Kind == DispositionKind.Destroy
            && (string.IsNullOrWhiteSpace(disposition.DestructionMethod) || disposition.DestructionDate is null
                || disposition.DestructionDate < today.AddDays(-MaxDestructionBackdateDays)))
        {
            return new(DestructionMethodRequired, 400,
                $"Destruction needs destructionMethod and a destructionDate not earlier than {MaxDestructionBackdateDays} days ago.");
        }

        return null;
    }

    /// <summary>
    /// V13 (KQ-E): the warehouse has a configured sample location (else 409 NOT_SET) that is an Active stockable location
    /// of the same warehouse, other than the stock's current one (else 400 INVALID).
    /// </summary>
    public static QuarantineViolation? SampleLocation(string? configuredLocationId, bool configuredIsValid, string currentLocationId)
    {
        if (configuredLocationId is null)
        {
            return new(SampleLocationNotSet, 409, "No sample location is configured for this warehouse (Quarantine settings).");
        }

        return configuredIsValid && !string.Equals(configuredLocationId, currentLocationId, StringComparison.Ordinal)
            ? null
            : new(SampleLocationInvalid, 400, "The configured sample location is not an Active stockable location of this warehouse (or it is the stock's own location).");
    }

    /// <summary>
    /// FU-0175-Q1-SER: a serial-tracked record is decided in whole serials — every part an integer quantity, and the record's
    /// serial count equals its quantity (else the record no longer matches the stock: 409 STOCK_CHANGED).
    /// </summary>
    public static QuarantineViolation? Serials(IReadOnlyList<string>? serials, decimal recordQuantity, IEnumerable<decimal> parts)
    {
        if (serials is null || serials.Count == 0)
        {
            return null;
        }

        if (serials.Count != recordQuantity)
        {
            return new(StockChanged, 409, $"The record holds {serials.Count} serials for a quantity of {recordQuantity}.");
        }

        return parts.All(q => q == decimal.Truncate(q))
            ? null
            : new(DecisionInvalid, 400, "A serial-tracked record is decided in whole units (one serial each).");
    }

    /// <summary>V14: a blocked record is unblocked only after its review is complete.</summary>
    public static QuarantineViolation? Unblock(bool reviewed)
        => reviewed ? null : new(ReviewNotCompleted, 409, "The review of this blocked record is not completed.");
}
