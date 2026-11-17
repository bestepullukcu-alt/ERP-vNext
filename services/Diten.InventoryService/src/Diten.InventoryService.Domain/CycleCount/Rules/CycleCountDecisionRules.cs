using System.Globalization;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.CycleCounting.Rules;

/// <summary>The company count settings a rule reads (pack §4.4) — the stored record or the defaults.</summary>
public sealed record CycleCountPolicy(decimal ToleranceAbsolute, decimal TolerancePercent, int MaxRecounts, int MaxLinesPerCount)
{
    /// <summary>Pack §4.4: no record → 0 / 0 / 2 / 2000 (every variance needs a decision).</summary>
    public static readonly CycleCountPolicy Default = new(0m, 0m, 2, CycleCountRules.DefaultMaxLinesPerCount);

    public static CycleCountPolicy Of(CycleCountSettings? settings)
        => settings is null ? Default : new(settings.ToleranceAbsolute, settings.TolerancePercent, settings.MaxRecounts, settings.MaxLinesPerCount);
}

/// <summary>What a ledger movement looks like to the §2.4 classification (the movement's own declared fields).</summary>
public sealed record CycleCountMovementFacts(Guid TransactionId, string IdempotencyKey, string SourceModule, string? SourceDocumentId, string? SourceLineId,
    DateTimeOffset CreatedAt);

/// <summary>
/// MOD-0181 DL-3 pure rules (pack §8.3, §8.5, §8.6, §8.9, §2.4, §12): the tolerance, the four eyes, which decision a line takes in which state, the
/// ONE ledger correction of an approved variance, the settings bounds and the "count or manual" classification of a <c>COUNT_*</c> movement.
/// No I/O.
/// </summary>
public static class CycleCountDecisionRules
{
    /// <summary>Pack §8.6: <c>sourceModule</c> of every 0181 movement.</summary>
    public const string SourceModule = "MOD-0181";

    /// <summary>Pack §8.6 (name fixed in the contract draft): <c>sourceType</c>.</summary>
    public const string SourceType = "CYCLE_COUNT";

    /// <summary>Q-17 = A: the fixed opaque movement reason.</summary>
    public const string MovementReason = "CYCLE_COUNT_VARIANCE";

    public const string KeyPrefix = "cycle-count:";

    /// <summary>Settings bounds (pack §12; the upper bounds are the contract draft's): percent 0..100, recounts 0..10, lines 1..5000.</summary>
    public const decimal MaxTolerancePercent = 100m;

    public const int MaxRecountsLimit = 10;

    public const int MaxLinesPerCountLimit = 5000;

    // ── §8.5 tolerance and four eyes ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pack §8.5 (Q-3 = A): <c>|v| ≤ ToleranceAbsolute</c> AND <c>|v| / ExpectedAtCount × 100 ≤ TolerancePercent</c> (an expected 0 never meets the
    /// percent), and no <c>MovementDuringCount</c>, no <c>SnapshotDrift</c>, not serial-controlled (Q-10 = A). The default 0 / 0 takes nothing.
    /// </summary>
    public static bool WithinTolerance(CycleCountLine line, CycleCountPolicy policy)
    {
        if (line.VarianceQuantity is not { } variance || line.ExpectedAtCount is not { } atCount) return false;
        if (line.MovementDuringCount || line.SnapshotDrift || line.SerialControlled) return false;
        if (atCount <= 0m) return false;
        var size = Math.Abs(variance);
        return size <= policy.ToleranceAbsolute && size * 100m / atCount <= policy.TolerancePercent;
    }

    /// <summary>Pack §8.5 (Q-5 = A): the line's counter and the count's submitter may not approve — no setting turns this off.</summary>
    public static bool IsSelfApproval(CycleCount count, CycleCountLine line, Guid approver)
        => approver == line.CountedBy || approver == count.SubmittedBy;

    // ── §8.3 which line takes which decision ─────────────────────────────────────────────────────────────

    /// <summary>approve: only an AwaitingApproval line.</summary>
    public static bool CanApprove(CycleCountLine line) => line.Status == CycleCountLineStatus.AwaitingApproval;

    private static bool IsRefused(CycleCountLine line)
        => line.Status == CycleCountLineStatus.Approved && line.Adjustment?.Status == CycleCountAdjustmentStatus.Refused;

    /// <summary>reject: an AwaitingApproval line, or an approved line whose correction the ledger refused (pack §8.3).</summary>
    public static bool CanReject(CycleCountLine line) => line.Status == CycleCountLineStatus.AwaitingApproval || IsRefused(line);

    /// <summary>recount: AwaitingApproval or (Approved + Refused) — pack §8.5 "Koşullar".</summary>
    public static bool CanRecount(CycleCountLine line) => line.Status == CycleCountLineStatus.AwaitingApproval || IsRefused(line);

    /// <summary>
    /// retry: an approved line whose correction is Pending (outcome unknown), or Refused on a reservation block (<c>INSUFFICIENT_STOCK</c>, §8.7) —
    /// another refusal takes only reject / recount (pack §8.3). An unapproved line never posts (AC-21).
    /// </summary>
    public static bool CanRetry(CycleCountLine line)
        => line.Status == CycleCountLineStatus.Approved && line.Adjustment is { } a
           && (a.Status == CycleCountAdjustmentStatus.Pending
               || (a.Status == CycleCountAdjustmentStatus.Refused && a.RefusalCode == InsufficientStock));

    /// <summary>resolve-integrity: only an IntegrityError correction (F-12).</summary>
    public static bool CanResolve(CycleCountLine line)
        => line.Status == CycleCountLineStatus.Approved && line.Adjustment?.Status == CycleCountAdjustmentStatus.IntegrityError;

    public const string InsufficientStock = "INSUFFICIENT_STOCK";

    // ── §8.6 the one correction ──────────────────────────────────────────────────────────────────────────

    public static string IdempotencyKey(Guid lineId) => KeyPrefix + lineId.ToString("D");

    /// <summary>A stored time to the second — the value sent IS the value stored (the ledger fingerprint covers the dates).</summary>
    public static DateTimeOffset Second(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    /// <summary>Q-19 = A: the posting date is the first attempt's UTC day.</summary>
    public static DateTimeOffset Day(DateTimeOffset value) => new(value.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>
    /// Pack §8.6: variance &gt; 0 → <c>COUNT_GAIN</c>, &lt; 0 → <c>COUNT_LOSS</c>, quantity |variance|; <c>transactionDate</c> = the line's
    /// <c>CountedAt</c>, <c>postingDate</c> = the first attempt's UTC day; the key <c>cycle-count:{lineId:D}</c>. A variance of 0 (a flagged line)
    /// has nothing to post — null. Only the two count types ever come out of here (AC-29).
    /// </summary>
    public static CycleCountAdjustment? FirstAdjustment(CycleCountLine line, DateTimeOffset now)
    {
        if (line.VarianceQuantity is not { } variance || variance == 0m) return null;
        return new CycleCountAdjustment
        {
            MovementType = variance > 0m ? MovementType.CountGain : MovementType.CountLoss,
            Quantity = Math.Abs(variance),
            IdempotencyKey = IdempotencyKey(line.Id),
            Status = CycleCountAdjustmentStatus.Pending,
            TransactionDate = Second(line.CountedAt ?? now),
            PostingDate = Day(now),
            Attempts = 1,
            FirstAttemptAt = now,
            LastAttemptAt = now
        };
    }

    /// <summary>AC-29: 0181 writes COUNT_GAIN / COUNT_LOSS only — any other type is a programming error, refused before the ledger is called.</summary>
    public static bool IsCountType(MovementType type) => type is MovementType.CountGain or MovementType.CountLoss;

    // ── §8.3 the last state ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pack §8.3: a line in its last state — NoVariance, Rejected, Recounted, or Approved with its correction Posted (an approved flagged line of
    /// variance 0 has no correction and is final at once). The count closes when every line is.
    /// </summary>
    public static bool IsFinal(CycleCountLine line) => line.Status switch
    {
        CycleCountLineStatus.NoVariance or CycleCountLineStatus.Rejected or CycleCountLineStatus.Recounted => true,
        CycleCountLineStatus.Approved => line.Adjustment is null ? line.VarianceQuantity == 0m : line.Adjustment.Status == CycleCountAdjustmentStatus.Posted,
        _ => false
    };

    // ── §12 settings ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Pack §12 (AC-49): absolute ≥ 0 (≤ 6 fraction digits), percent 0..100, recounts 0..10, lines 1..5000 — else the field's name.</summary>
    public static string? SettingsError(decimal absolute, decimal percent, int maxRecounts, int maxLines)
    {
        if (absolute < 0m) return "toleranceAbsolute";
        if (percent < 0m || percent > MaxTolerancePercent) return "tolerancePercent";
        if (maxRecounts < 0 || maxRecounts > MaxRecountsLimit) return "maxRecounts";
        return maxLines is < 1 or > MaxLinesPerCountLimit ? "maxLinesPerCount" : null;
    }

    // ── §2.4 count or manual ─────────────────────────────────────────────────────────────────────────────

    /// <summary>The line a <c>cycle-count:</c> key names, or null.</summary>
    public static Guid? LineOfKey(string? key)
        => key is not null && key.StartsWith(KeyPrefix, StringComparison.Ordinal) && Guid.TryParseExact(key[KeyPrefix.Length..], "D", out var id) ? id : null;

    /// <summary>
    /// Pack §2.4 (Q-21 = A, r4 02/F-2) — a <c>COUNT_*</c> movement H is "Sayım" ⇔ for the line L its key names, BOTH directions hold:
    /// (1) movement → line: <c>H.IdempotencyKey = L.Adjustment.IdempotencyKey</c>, <c>H.SourceModule = MOD-0181</c>, <c>H.SourceLineId = L.Id</c>,
    /// <c>H.SourceDocumentId = L.CountId</c>; (2) line → movement: <c>Posted</c> → <c>L.Adjustment.TransactionId = H.Id</c>; <c>Pending</c> →
    /// <c>H.CreatedAt ≥ L.Adjustment.FirstAttemptAt</c>; IntegrityError / Refused / no correction → no match. Anything else is "Elle" — a
    /// <c>MOD-0181</c> declaration alone never makes a movement a count's (the caller declares it).
    /// </summary>
    public static bool IsCountMovement(CycleCountMovementFacts h, CycleCountLine? l)
    {
        if (l?.Adjustment is not { } a) return false;
        var declared = h.IdempotencyKey == a.IdempotencyKey && h.SourceModule == SourceModule
                       && string.Equals(h.SourceLineId, l.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)
                       && string.Equals(h.SourceDocumentId, l.CountId.ToString("D"), StringComparison.OrdinalIgnoreCase);
        if (!declared) return false;
        return a.Status switch
        {
            CycleCountAdjustmentStatus.Posted => a.TransactionId == h.TransactionId,
            CycleCountAdjustmentStatus.Pending => h.CreatedAt >= a.FirstAttemptAt,
            _ => false
        };
    }

    /// <summary>A "Elle" movement that declares MOD-0181 is shown with "kaynak beyanı uyuşmuyor" (pack §2.4).</summary>
    public static bool DeclaresCount(CycleCountMovementFacts h) => h.SourceModule == SourceModule;

    public static string Wire(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
