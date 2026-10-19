using System.Globalization;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory;

/// <summary>Entity → contract wire mapping. Only fields the frozen contract declares are emitted.</summary>
public static class InventoryMapping
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string Utc(DateTimeOffset value)
        => value.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture);

    public static MovementResponseDto ToMovementResponse(InventoryTransaction transaction, bool idempotentReplay, Guid legalEntityId)
        => new()
        {
            TransactionId = transaction.Id.ToString(),
            TransactionNumber = transaction.TransactionNumber,
            PostedAt = Utc(transaction.CreatedAt),
            IdempotentReplay = idempotentReplay,
            BalanceSnapshot = ToBalanceView(transaction.ResultSnapshot, legalEntityId, transaction.BaseQuantity),
            ExpiryChecks = transaction.ExpiryChecks is { } checks
                ? new ExpiryChecksDto { Fefo = checks.Fefo, MinRemaining = checks.MinRemaining, ExceptionIds = [.. checks.ExceptionIds] }
                : null
        };

    public static BalanceViewDto ToBalanceView(BalanceSnapshot row, Guid legalEntityId, decimal? baseQuantity = null)
        => new()
        {
            ItemId = row.ItemId.ToString(),
            SkuId = row.SkuId.ToString(),
            SkuLevel = WireNames.ToWire(row.SkuLevel),
            LegalEntityId = legalEntityId.ToString(),
            WarehouseId = row.WarehouseId,
            LocationId = row.LocationId,
            LotId = row.LotId,
            StockStatus = WireNames.ToWire(row.StockStatus),
            OnHand = InventoryQuantity.ToWire(row.OnHand),
            Reserved = InventoryQuantity.ToWire(row.Reserved),
            Available = InventoryQuantity.ToWire(row.Available),
            InTransit = InventoryQuantity.ToWire(row.InTransit),
            Blocked = InventoryQuantity.ToWire(row.Blocked),
            QualityInspection = InventoryQuantity.ToWire(row.QualityInspection),
            BaseUomId = row.BaseUomId,
            BaseQuantity = baseQuantity.HasValue ? InventoryQuantity.ToWire(baseQuantity.Value) : null,
            LastMovementId = row.LastMovementId?.ToString(),
            LastPostingDate = row.LastPostingDate.HasValue ? Utc(row.LastPostingDate.Value) : null
        };

    public static BalanceViewDto ToBalanceView(InventoryBalance row, Guid legalEntityId)
        => ToBalanceView(row.ToSnapshot(), legalEntityId);

    /// <summary>ETag of a reservation version (pack ASSUMPTION-0173-17).</summary>
    public static string ETag(int version) => $"W/\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>The ORIGINAL create result — what the first call returned, replayed verbatim on an idempotent retry.</summary>
    public static ReservationResponseDto ToCreatedReservation(InventoryReservation reservation)
        => new()
        {
            ReservationId = reservation.Id.ToString(),
            Status = WireNames.ToWire(ReservationStatus.Active),
            ReservedQuantity = InventoryQuantity.ToWire(reservation.Quantity),
            AvailableAfter = InventoryQuantity.ToWire(reservation.CreatedAvailableAfter),
            Etag = ETag(1)
        };

    public static ReservationResponseDto ToReleasedReservation(InventoryReservation reservation, decimal availableAfter)
        => new()
        {
            ReservationId = reservation.Id.ToString(),
            Status = WireNames.ToWire(reservation.Status),
            AvailableAfter = InventoryQuantity.ToWire(availableAfter)
        };

    public static TransactionItemDto ToTransactionItem(InventoryTransaction transaction)
        => ToTransactionItem(transaction, null);

    /// <summary>
    /// INVENTORY 1.5.0 (WP-MVP1-NAMES1): the row with its <c>createdBy</c> — <c>userId</c> = the posting user, <c>name</c> = the entry of
    /// <paramref name="names"/> for (posting correlation, posting user), else omitted. A posting without a user carries no <c>createdBy</c>.
    /// </summary>
    public static TransactionItemDto ToTransactionItem(InventoryTransaction transaction, IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>? names)
        => new()
        {
            CreatedBy = names is null || transaction.CreatedBy == Guid.Empty
                ? null
                : new TransactionCreatedByDto(transaction.CreatedBy.ToString("D"),
                    names.TryGetValue((transaction.CorrelationId, transaction.CreatedBy), out var name) ? name : null),
            TransactionId = transaction.Id.ToString(),
            TransactionNumber = transaction.TransactionNumber,
            MovementType = WireNames.ToWire(transaction.MovementType),
            MovementReason = transaction.MovementReason,
            ItemId = transaction.ItemId.ToString(),
            SkuId = transaction.SkuId.ToString(),
            SkuLevel = WireNames.ToWire(transaction.SkuLevel),
            Quantity = InventoryQuantity.ToWire(transaction.Quantity),
            UomId = transaction.UomId,
            BaseQuantity = InventoryQuantity.ToWire(transaction.BaseQuantity),
            BaseUomId = transaction.BaseUomId,
            ToWarehouseId = transaction.ToWarehouseId,
            ToLocationId = transaction.ToLocationId,
            LotId = transaction.LotId,
            FromStockStatus = transaction.FromStockStatus.HasValue ? WireNames.ToWire(transaction.FromStockStatus.Value) : null,
            ToStockStatus = transaction.ToStockStatus.HasValue ? WireNames.ToWire(transaction.ToStockStatus.Value) : null,
            SourceModule = transaction.SourceModule,
            SourceDocumentId = transaction.SourceDocumentId,
            SourceLineId = transaction.SourceLineId,
            TransactionDate = Utc(transaction.TransactionDate),
            PostingDate = Utc(transaction.PostingDate)
        };
}
