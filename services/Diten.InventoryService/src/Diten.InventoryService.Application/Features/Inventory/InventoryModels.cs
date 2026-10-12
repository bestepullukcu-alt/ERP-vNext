using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.Inventory;

/*
 * Wire models — the UNWRAPPED INVENTORY-BUNDLE v1 schemas (pack §8 exception to Response<T>, limited to
 * /api/inventory). Property names serialise camelCase; null members are OMITTED rather than written as null
 * (ASSUMPTION-S1-06) because OpenAPI 3.1 has no `nullable` keyword and several contract properties are plain
 * `$ref: StockStatus` — an explicit null would not validate. Decimals are strings (contract `Decimal`, never float);
 * timestamps are UTC ISO-8601 strings. No property here exists without a counterpart in the frozen contract.
 * (Pack §10 names this file InventoryModels.cs; its wire records are grouped here by that convention.)
 */

/// <summary>Contract <c>MovementResponse</c>.</summary>
public sealed class MovementResponseDto
{
    public string TransactionId { get; init; } = string.Empty;
    public string TransactionNumber { get; init; } = string.Empty;
    public string PostedAt { get; init; } = string.Empty;
    public bool IdempotentReplay { get; init; }
    public BalanceViewDto? BalanceSnapshot { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;

    /// <summary>INVENTORY 1.3.0 (MOD-0176): what the expiry hooks decided; absent when no hook applied.</summary>
    public ExpiryChecksDto? ExpiryChecks { get; init; }
}

/// <summary>INVENTORY 1.3.0 <c>ExpiryChecks</c> (MOD-0176 F2).</summary>
public sealed class ExpiryChecksDto
{
    /// <summary>Compliant | Bypassed — present on an issue under the FEFO hook.</summary>
    public string? Fefo { get; init; }

    /// <summary>Ok | NotDefined | Warned | NotApplicable — present on a goods receipt under the minimum-remaining hook.</summary>
    public string? MinRemaining { get; init; }

    public IReadOnlyList<string> ExceptionIds { get; init; } = [];
}

/// <summary>Contract <c>BalanceView</c>.</summary>
public sealed class BalanceViewDto
{
    public string ItemId { get; init; } = string.Empty;
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public string LegalEntityId { get; init; } = string.Empty;
    public string? WarehouseId { get; init; }
    public string? LocationId { get; init; }
    public string? LotId { get; init; }
    public string StockStatus { get; init; } = string.Empty;
    public string OnHand { get; init; } = string.Empty;
    public string Reserved { get; init; } = string.Empty;
    public string Available { get; init; } = string.Empty;
    public string InTransit { get; init; } = string.Empty;
    public string Blocked { get; init; } = string.Empty;
    public string QualityInspection { get; init; } = string.Empty;
    public string BaseUomId { get; init; } = string.Empty;

    /// <summary>Only on a movement's <c>balanceSnapshot</c>: the posted transaction's base quantity.</summary>
    public string? BaseQuantity { get; init; }

    public string? LastMovementId { get; init; }
    public string? LastPostingDate { get; init; }
}

/// <summary>Contract <c>AvailabilityView</c>.</summary>
public sealed class AvailabilityViewDto
{
    public string? ItemId { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string LegalEntityId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Scope { get; init; } = new Dictionary<string, string>();
    public string OnHand { get; init; } = string.Empty;
    public string Reserved { get; init; } = string.Empty;
    public string Available { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> ByStatus { get; init; } = new Dictionary<string, string>();
    public string? BaseUomId { get; init; }
    public string AsOf { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>Contract <c>getBalance</c> 200 body.</summary>
public sealed class BalancePageDto
{
    public IReadOnlyList<BalanceViewDto> Rows { get; init; } = [];
    public long Total { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>Contract <c>TransactionItem</c>.</summary>
public sealed class TransactionItemDto
{
    public string TransactionId { get; init; } = string.Empty;
    public string TransactionNumber { get; init; } = string.Empty;
    public string MovementType { get; init; } = string.Empty;
    public string? MovementReason { get; init; }
    public string ItemId { get; init; } = string.Empty;
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public string Quantity { get; init; } = string.Empty;
    public string UomId { get; init; } = string.Empty;
    public string BaseQuantity { get; init; } = string.Empty;
    public string BaseUomId { get; init; } = string.Empty;
    public string? ToWarehouseId { get; init; }
    public string? ToLocationId { get; init; }
    public string? LotId { get; init; }
    public string? FromStockStatus { get; init; }
    public string? ToStockStatus { get; init; }
    public string SourceModule { get; init; } = string.Empty;
    public string? SourceDocumentId { get; init; }
    public string? SourceLineId { get; init; }
    public string TransactionDate { get; init; } = string.Empty;
    public string PostingDate { get; init; } = string.Empty;
}

/// <summary>
/// Contract <c>ReservationResponse</c>. createReservation fills every member; releaseReservation answers the contract
/// example shape (<c>reservationId, status, availableAfter, contractVersion</c>) — the other members are null and omitted.
/// </summary>
public sealed class ReservationResponseDto
{
    public string ReservationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ReservedQuantity { get; init; }
    public string AvailableAfter { get; init; } = string.Empty;
    public string? Etag { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>Contract <c>getTransactions</c> 200 body.</summary>
public sealed class TransactionPageDto
{
    public IReadOnlyList<TransactionItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}
