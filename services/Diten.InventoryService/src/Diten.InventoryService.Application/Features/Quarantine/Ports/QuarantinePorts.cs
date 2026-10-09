using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;

namespace Diten.InventoryService.Application.Features.Quarantine.Ports;

/// <summary>Result of asking the approval side to start an approval for a submitted decision.</summary>
public sealed record ApprovalStart(bool Started, string? InstanceId, string? ErrorCode, int Status, string? Message)
{
    public static ApprovalStart Ok(string instanceId) => new(true, instanceId, null, 202, null);

    public static ApprovalStart Failed(string code, int status, string message) => new(false, null, code, status, message);
}

/// <summary>
/// Where a submitted decision goes for approval (pack §1 Q1: "driven by an internal approval port"). Production has ONE
/// implementation since Q5 (O-Q2-5): <c>WorkflowApprovalPort</c> — MOD-0023 (DEC-INV-08, KQ-F: no release without workflow).
/// The Q1 test port lives in the test suite only.
/// The outcome comes back through <c>QuarantineOutcomeApplier</c>.
/// </summary>
public interface IQuarantineApprovalPort
{
    Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default);
}

/// <summary>An active reservation of a SKU (CT-Q6 refusal details).</summary>
public sealed record ReservationLine(Guid ReservationId, string SourceModule, string? SourceDocumentId, decimal RemainingQuantity);

/// <summary>
/// Read-only ledger facts MOD-0175 needs that the ledger repository does not answer (pack §8 / ASSUMPTION-0175-05): the
/// transactions of a 0175 document, the SKU position and the active reservations of a SKU. Implemented in
/// <c>Persistence/Quarantine</c> with reads only — 0175 never writes a ledger collection (architecture-tested); every
/// posting goes through the ledger's internal posting service.
/// </summary>
public interface IQuarantineLedgerReader
{
    Task<IReadOnlyList<InventoryTransaction>> TransactionsAsync(
        IReadOnlyCollection<string> sourceDocumentIds, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);

    Task<decimal?> SkuUnreservedAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationLine>> ActiveReservationsAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);

    /// <summary>Q4 (KQ-H): this company's balance rows (OnHand &gt; 0) of the given lots in one stock status — the lot's buckets.</summary>
    Task<IReadOnlyList<InventoryBalance>> LotBalancesAsync(IReadOnlyCollection<string> lotIds, StockStatus status, CancellationToken cancellationToken = default);

    /// <summary>Q4 (KQ-H): the tenant's lots whose retest date is before <paramref name="before"/> (retest queue candidates).</summary>
    Task<IReadOnlyList<Diten.InventoryService.Domain.Traceability.Entities.Lot>> RetestLotsAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    /// <summary>
    /// Q4 (O-Q2-6, ASSUMPTION-0175-Q4-02): the Active registered serials of a SKU / lot at a location, in intake order (received first);
    /// the caller removes the serials already named on an open 0175 record of that bucket.
    /// </summary>
    Task<IReadOnlyList<string>> SerialsAtAsync(Guid itemId, Guid skuId, SkuLevel skuLevel, string? lotId, string warehouseId, string locationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Q4 (KQ-H, CT-Q16): the stored retest hold of a lot. <see cref="RefreshAsync"/> sets it (retest date before the UTC day of
/// <c>now</c>) or clears it (date extended) together with the SKU positions in ONE transaction — the retest release
/// calls it right after the approved new retest date is on the lot, so the released stock is available at once.
/// </summary>
public interface ILotRetestHold
{
    Task<bool> RefreshAsync(Guid lotId, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>A ledger transaction the intake sweep found (cross-tenant scan, read-only).</summary>
public sealed record IntakeCandidate(InventoryTransaction Transaction, Guid TenantId, Guid LegalEntityId);

/// <summary>Cross-tenant, read-only feed of recent inbound transactions into QUARANTINE / QUALITY_INSPECTION / BLOCKED (intake sweep).</summary>
public interface IQuarantineIntakeFeed
{
    Task<IReadOnlyList<IntakeCandidate>> RecentIntakesAsync(DateTimeOffset since, DateTimeOffset until, int limit, CancellationToken cancellationToken = default);
}
