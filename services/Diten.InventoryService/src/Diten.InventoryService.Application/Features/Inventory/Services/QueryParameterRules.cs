using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Shared GET parameter rules (pack §12): UUID filters must be UUIDs, enum filters must be contract values,
/// <c>page ≥ 1</c>, <c>1 ≤ pageSize ≤ 200</c> (defaults 1 / 50 per the contract).
/// </summary>
public static class QueryParameterRules
{
    public static bool IsOptionalUuid(string? raw) => raw is null || WireParsing.TryParseUuid(raw, out _);

    public static bool IsOptionalSkuLevel(string? raw) => raw is null || WireNames.TryParseSkuLevel(raw, out _);

    public static bool IsOptionalStockStatus(string? raw) => raw is null || WireNames.TryParseStockStatus(raw, out _);

    public static bool IsOptionalDate(string? raw) => raw is null || WireParsing.TryParseUtcDate(raw, out _);

    public static bool IsValidPage(string? raw) => raw is null || WireParsing.TryParsePositiveInt(raw, out _);

    public static bool IsValidPageSize(string? raw)
        => raw is null || (WireParsing.TryParsePositiveInt(raw, out var size) && size <= InventoryContract.MaxPageSize);

    public static Guid? Uuid(string? raw) => WireParsing.TryParseUuid(raw, out var value) ? value : null;

    public static SkuLevel? Level(string? raw) => WireNames.TryParseSkuLevel(raw, out var value) ? value : null;

    public static StockStatus? Status(string? raw) => WireNames.TryParseStockStatus(raw, out var value) ? value : null;

    public static int Page(string? raw) => WireParsing.TryParsePositiveInt(raw, out var value) ? value : 1;

    public static int PageSize(string? raw)
        => WireParsing.TryParsePositiveInt(raw, out var value) ? value : InventoryContract.DefaultPageSize;

    public static string? Text(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw;
}
