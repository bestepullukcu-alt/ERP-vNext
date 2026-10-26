using System.Globalization;
using System.Text.Json.Serialization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Application.Features.Traceability;

/*
 * Wire models of TRACE-BUNDLE v1 (frozen) and v1.1 (additive, trace.openapi.yaml 1.1.0). Unwrapped like the
 * INVENTORY-BUNDLE / LOCATION wire; camelCase; null members are omitted. No member exists without a counterpart in
 * trace.openapi.yaml (the contract-validating test handler closes every schema). contractVersion stays "v1".
 */

/// <summary>Contract <c>LotRef</c> (v1 members + v1.1 optional members).</summary>
public class LotRefDto
{
    public string LotId { get; init; } = string.Empty;
    public string LotNumber { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string? ManufacturerLot { get; init; }
    public string? SupplierLot { get; init; }
    public string? ManufacturingDate { get; init; }
    public string? ExpiryDate { get; init; }
    public string? RetestDate { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CoaReference { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;

    // v1.1
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public int Version { get; init; }
    public string? LegalEntityId { get; init; }
    public LotStockSummaryDto? StockSummary { get; init; }
    public string ExpiryState { get; init; } = string.Empty;
    public bool RetestDue { get; init; }
    public string TrackingMode { get; init; } = string.Empty;

    // TRACE 1.4.0 (MOD-0176 KF-D) — present only when the lot has them.
    public string? SuggestedExpiryDate { get; init; }
    public string? SuggestedRetestDate { get; init; }
    public LotExpiryOverrideDto? ExpiryOverride { get; init; }

    /// <summary>Not on the wire body: the controller turns it into the <c>ETag: W/"n"</c> header.</summary>
    [JsonIgnore]
    public int? ETagVersion { get; init; }
}

/// <summary>TRACE 1.4.0 <c>LotExpiryOverride</c> (MOD-0176 KF-D): the override the lot dates were set with — a signature REFERENCE only.</summary>
public sealed class LotExpiryOverrideDto
{
    public string OverrideId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? SignatureId { get; init; }
    public string? SuggestedExpiry { get; init; }
    public string? SuggestedRetest { get; init; }
    public string By { get; init; } = string.Empty;
    public string AtUtc { get; init; } = string.Empty;
}

/// <summary>Contract <c>LotDetail</c> (v1.1) = LotRef members + source + serialCount.</summary>
public sealed class LotDetailDto : LotRefDto
{
    public LotSourceDto? Source { get; init; }
    public long SerialCount { get; init; }
}

public sealed class LotSourceDto
{
    public string Kind { get; init; } = string.Empty;
    public string? DocumentNumber { get; init; }
    public string? PartyName { get; init; }
    public string? Date { get; init; }
}

/// <summary>Contract <c>LotStockSummary</c> (v1.1, K-A) — filled from the ledger per-lot projection (slice P2).</summary>
public sealed class LotStockSummaryDto
{
    public string OnHand { get; init; } = "0";
    public string BaseUomId { get; init; } = string.Empty;
    public string LegalEntityId { get; init; } = string.Empty;
    public string AsOf { get; init; } = string.Empty;
}

/// <summary>listLots 200 (v1 shape; <c>page</c> / <c>pageSize</c> only on a paged v1.1 call).</summary>
public sealed class LotListDto
{
    public IReadOnlyList<LotRefDto> Items { get; init; } = [];
    public long Total { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

/// <summary>Contract <c>LotChangeEntry</c> (v1.1).</summary>
public sealed class LotChangeDto
{
    public string EntryId { get; init; } = string.Empty;
    public string LotId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Reason { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public string? ActorDisplay { get; init; }
    public string OccurredAt { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

/// <summary>Contract <c>LotHistoryPage</c> (v1.1), newest first.</summary>
public sealed class LotHistoryPageDto
{
    public IReadOnlyList<LotChangeDto> Items { get; init; } = [];
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>Contract <c>SerialRef</c> (v1 members + v1.1 optional members).</summary>
public sealed class SerialRefDto
{
    public string SerialId { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string? LotId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = InventoryContract.Version;

    // v1.1
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public string? LotNumber { get; init; }
    public string? WarehouseId { get; init; }
    public string? LocationId { get; init; }
    public string? LastTransactionId { get; init; }
    public string? LastMovementAt { get; init; }
}

/// <summary>Contract <c>SerialPage</c> (v1.1).</summary>
public sealed class SerialPageDto
{
    public IReadOnlyList<SerialRefDto> Items { get; init; } = [];
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>getGenealogy 200 (v1 shape + v1.1 item members).</summary>
public sealed class GenealogyDto
{
    public string LotId { get; init; } = string.Empty;
    public IReadOnlyList<GenealogyItemDto> Parents { get; init; } = [];
    public IReadOnlyList<GenealogyItemDto> Children { get; init; } = [];
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class GenealogyItemDto
{
    public string ParentLotId { get; init; } = string.Empty;
    public string ChildLotId { get; init; } = string.Empty;
    public string TransformationReference { get; init; } = string.Empty;
    public string LinkId { get; init; } = string.Empty;
    public string SourceModule { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>linkGenealogy 201 / 200 (v1.1).</summary>
public sealed class GenealogyLinkDto
{
    public string LinkId { get; init; } = string.Empty;
    public string ParentLotId { get; init; } = string.Empty;
    public string ChildLotId { get; init; } = string.Empty;
    public string TransformationReference { get; init; } = string.Empty;
    public string SourceModule { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>validateTrace 200 (v1 shape, v1.1 typed results).</summary>
public sealed class ValidateResponseDto
{
    public IReadOnlyList<ValidateResultDto> Results { get; init; } = [];
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class ValidateResultDto
{
    public string? LotId { get; init; }
    public string? SerialId { get; init; }
    public bool Exists { get; init; }
    public string? Status { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public bool? Usable { get; init; }
}

/// <summary>checkGs1Code 200 (v1.1, K-B).</summary>
public sealed class Gs1CheckResultDto
{
    public string? SerialNumber { get; init; }
    public string? Gtin { get; init; }
    public string? LotNumber { get; init; }
    public string? Expiry { get; init; }
    public IReadOnlyList<Gs1CheckDto> Checks { get; init; } = [];
    public bool Accepted { get; init; }
    public string? GtinSkippedReason { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class Gs1CheckDto
{
    public string Field { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string? Expected { get; init; }
    public string? Actual { get; init; }
}

public static class TraceMapping
{
    public static LotRefDto ToRef(Lot lot, DateOnly today, bool withETag = false, LotPosition? stock = null) => new()
    {
        StockSummary = stock is null ? null : ToStockSummary(stock),
        LotId = Id(lot.Id),
        LotNumber = lot.LotNumber,
        ItemId = Id(lot.ItemId),
        ManufacturerLot = lot.ManufacturerLot,
        SupplierLot = lot.SupplierLot,
        ManufacturingDate = Wire(lot.ManufacturingDate),
        ExpiryDate = Wire(lot.ExpiryDate),
        RetestDate = Wire(lot.RetestDate),
        CountryOfOrigin = lot.CountryOfOrigin,
        Status = lot.Status.ToString(),
        CoaReference = lot.CoaReference,
        SkuId = Id(lot.SkuId),
        SkuLevel = WireNames.ToWire(lot.SkuLevel),
        Version = lot.Version,
        LegalEntityId = lot.LegalEntityId is { } le ? Id(le) : null,
        ExpiryState = LotDateRules.ExpiryState(Day(lot.ExpiryDate), today),
        RetestDue = LotDateRules.RetestDue(Day(lot.RetestDate), today),
        TrackingMode = lot.TrackingMode.ToString(),
        ETagVersion = withETag ? lot.Version : null,
        SuggestedExpiryDate = Wire(lot.SuggestedExpiryDate),
        SuggestedRetestDate = Wire(lot.SuggestedRetestDate),
        ExpiryOverride = lot.ExpiryOverride is { } o
            ? new LotExpiryOverrideDto
            {
                OverrideId = Id(o.OverrideId), Reason = o.Reason, SignatureId = o.SignatureId, SuggestedExpiry = Wire(o.SuggestedExpiry),
                SuggestedRetest = Wire(o.SuggestedRetest), By = Id(o.By), AtUtc = Wire(o.AtUtc)!
            }
            : null
    };

    public static LotDetailDto ToDetail(Lot lot, DateOnly today, LotSourceDto? source, long serialCount, LotPosition? stock = null)
    {
        var r = ToRef(lot, today, withETag: true, stock);
        return new LotDetailDto
        {
            StockSummary = r.StockSummary,
            LotId = r.LotId, LotNumber = r.LotNumber, ItemId = r.ItemId, ManufacturerLot = r.ManufacturerLot,
            SupplierLot = r.SupplierLot, ManufacturingDate = r.ManufacturingDate, ExpiryDate = r.ExpiryDate,
            RetestDate = r.RetestDate, CountryOfOrigin = r.CountryOfOrigin, Status = r.Status, CoaReference = r.CoaReference,
            SkuId = r.SkuId, SkuLevel = r.SkuLevel, Version = r.Version, LegalEntityId = r.LegalEntityId,
            ExpiryState = r.ExpiryState, RetestDue = r.RetestDue, TrackingMode = r.TrackingMode, ETagVersion = r.ETagVersion,
            SuggestedExpiryDate = r.SuggestedExpiryDate, SuggestedRetestDate = r.SuggestedRetestDate, ExpiryOverride = r.ExpiryOverride,
            Source = source,
            SerialCount = serialCount
        };
    }

    /// <summary>K-A (WP-0174-P2): the ledger per-lot position of the caller's company — read, never summed per request.</summary>
    public static LotStockSummaryDto ToStockSummary(LotPosition position) => new()
    {
        OnHand = InventoryQuantity.ToWire(position.OnHand),
        BaseUomId = position.BaseUomId,
        LegalEntityId = position.LegalEntityId is { } le ? Id(le) : string.Empty,
        AsOf = Wire(position.UpdatedAt ?? position.CreatedAt)!
    };

    public static LotChangeDto ToChange(LotChangeEntry e) => new()
    {
        EntryId = Id(e.Id),
        LotId = Id(e.LotId),
        Action = e.Action.ToString(),
        Field = e.Field,
        OldValue = e.OldValue,
        NewValue = e.NewValue,
        Reason = e.Reason,
        ActorUserId = Id(e.ActorUserId),
        OccurredAt = Wire(e.OccurredAt)!,
        CorrelationId = Id(e.CorrelationId),
        Source = e.Source
    };

    /// <summary>Stored Active / Consumed; Blocked is derived from the lot (Blocked or Expired lot → Blocked serial).</summary>
    public static SerialRefDto ToSerial(SerialNumber s, Lot? lot) => new()
    {
        SerialId = Id(s.Id),
        SerialNumber = s.Number,
        ItemId = Id(s.ItemId),
        LotId = s.LotId is { } lotId ? Id(lotId) : null,
        Status = WireSerialStatus(s.Status, lot?.Status),
        SkuId = Id(s.SkuId),
        SkuLevel = WireNames.ToWire(s.SkuLevel),
        LotNumber = lot?.LotNumber,
        WarehouseId = s.WarehouseId,
        LocationId = s.LocationId,
        LastTransactionId = s.LastTransactionId,
        LastMovementAt = Wire(s.LastMovementAt)
    };

    public static string WireSerialStatus(SerialStatus stored, LotStatus? lotStatus)
        => stored == SerialStatus.Consumed ? "Consumed"
            : lotStatus is { } ls && !LotStatusRules.IsUsable(ls) ? "Blocked"
            : "Active";

    public static GenealogyItemDto ToItem(LotGenealogyLink link) => new()
    {
        ParentLotId = Id(link.ParentLotId),
        ChildLotId = Id(link.ChildLotId),
        TransformationReference = link.TransformationReference,
        LinkId = Id(link.Id),
        SourceModule = link.SourceModule,
        CreatedAt = Wire(link.CreatedAt)!
    };

    public static GenealogyLinkDto ToLink(LotGenealogyLink link) => new()
    {
        LinkId = Id(link.Id),
        ParentLotId = Id(link.ParentLotId),
        ChildLotId = Id(link.ChildLotId),
        TransformationReference = link.TransformationReference,
        SourceModule = link.SourceModule,
        CreatedAt = Wire(link.CreatedAt)!
    };

    public static string ETag(int version) => $"W/\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static string Id(Guid id) => id.ToString("D");

    public static string? Wire(DateTimeOffset? value)
        => value?.UtcDateTime.ToString(TraceContract.DateTimeFormat, CultureInfo.InvariantCulture);

    public static DateOnly? Day(DateTimeOffset? value) => value is { } v ? DateOnly.FromDateTime(v.UtcDateTime) : null;

    public static DateTimeOffset Midnight(DateOnly day) => new(day.Year, day.Month, day.Day, 0, 0, 0, TimeSpan.Zero);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    public static string Wire(SkuLevel level) => WireNames.ToWire(level);
}
