using Diten.InventoryService.Domain.CycleCounting.Enums;

namespace Diten.InventoryService.Domain.CycleCounting.Rules;

/// <summary>One ledger movement's net effect on ONE line key (bin, SKU, level, lot, status), as the submit's snapshot read saw it.</summary>
public sealed record CycleCountKeyMovement(Guid TransactionId, DateTimeOffset CreatedAt, decimal Delta);

/// <summary>What the submit writes on a counted line (pack §4.2, §8.4).</summary>
public sealed record CycleCountVariance(decimal MovementsSinceSnapshot, decimal ExpectedAtCount, decimal VarianceQuantity, bool MovementDuringCount,
    bool SnapshotDrift, CycleCountLineStatus Status);

/// <summary>
/// Pack §8.4 — the variance of one counted line, pure. Inputs come from ONE <c>ReadConcern.Snapshot</c> read (the window's movements on the key and
/// the key's balance today):
/// <list type="bullet">
/// <item>after the snapshot = the window's movements (<c>CreatedAt ≥ SnapshotAt − W</c>) NOT in <c>SnapshotTransactionIds</c> — those are already in
/// <c>ExpectedQuantity</c>;</item>
/// <item><c>MovementsSinceSnapshot</c> = the net of those with <c>CreatedAt ≤ CountedAt</c>; <c>ExpectedAtCount = Expected + MovementsSinceSnapshot</c>;
/// <c>VarianceQuantity = Counted − ExpectedAtCount</c>;</item>
/// <item><c>MovementDuringCount</c> (Q-6 = B) = at least one of them touched the key up to the entry;</item>
/// <item><c>SnapshotDrift</c> (F-5) = <c>Expected + Σ(every movement after the snapshot) ≠ balance today</c> — a late commit outside the window,
/// a re-keyed projection or a clock skew;</item>
/// <item>status: <c>NoVariance</c> only when the variance is 0 AND neither flag is set; otherwise <c>AwaitingApproval</c> (r4, 02/F-1: a drift line
/// never closes without a decision, at variance 0 too). No tolerance here — DL-3.</item>
/// </list>
/// </summary>
public static class CycleCountVarianceRules
{
    public static CycleCountVariance Compute(decimal expected, decimal counted, DateTimeOffset countedAt, IReadOnlyCollection<Guid> snapshotTransactionIds,
        IReadOnlyList<CycleCountKeyMovement> windowMovements, decimal balanceToday)
    {
        var afterSnapshot = windowMovements.Where(m => !snapshotTransactionIds.Contains(m.TransactionId)).ToList();
        var untilEntry = afterSnapshot.Where(m => m.CreatedAt <= countedAt).ToList();

        var movementsSinceSnapshot = untilEntry.Sum(m => m.Delta);
        var expectedAtCount = expected + movementsSinceSnapshot;
        var variance = counted - expectedAtCount;
        var movementDuringCount = untilEntry.Count > 0;
        var snapshotDrift = expected + afterSnapshot.Sum(m => m.Delta) != balanceToday;

        var status = variance == 0m && !movementDuringCount && !snapshotDrift ? CycleCountLineStatus.NoVariance : CycleCountLineStatus.AwaitingApproval;
        return new CycleCountVariance(movementsSinceSnapshot, expectedAtCount, variance, movementDuringCount, snapshotDrift, status);
    }
}
