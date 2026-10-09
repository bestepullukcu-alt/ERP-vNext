using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.StockPresence;

/// <summary>
/// INVENTORY-BUNDLE 1.1.0 <c>getStockPresence</c> (WP-0173-PM2, MOD-0290 R-12): service-to-service, on InventoryService
/// directly, NEVER through the Gateway. Caller = a service key (<c>X-Internal-Api-Key</c>), never a user token; tenant =
/// <c>X-Tenant-Id</c>; answer = presence only, no quantity.
/// </summary>
public static class StockPresenceContract
{
    public const string BasePath = "/api/internal/inventory";

    public const string Path = BasePath + "/stock-presence";

    /// <summary>The operation name a key must list in its configured endpoints to open this path.</summary>
    public const string OperationId = "getStockPresence";

    /// <summary><c>&lt;identifier&gt;:&lt;secret&gt;</c> — one key per calling service (ASSUMPTION-0173-PM2-01).</summary>
    public const string KeyHeader = "X-Internal-Api-Key";

    public const string CoverageComplete = "COMPLETE";

    /// <summary>
    /// The 1.1.0 <c>SkuLevel</c> spellings this operation accepts: the 1.0.2 three (from <see cref="WireNames"/>) plus
    /// <c>Item</c>, which only this operation knows until WP-0173-PM1.
    /// </summary>
    public static bool TryParseLevel(string? wire, out SkuLevel level)
    {
        if (WireNames.TryParseSkuLevel(wire, out level))
        {
            return true;
        }

        if (string.Equals(wire, "Item", StringComparison.Ordinal))
        {
            level = SkuLevel.Item;
            return true;
        }

        level = default;
        return false;
    }
}
