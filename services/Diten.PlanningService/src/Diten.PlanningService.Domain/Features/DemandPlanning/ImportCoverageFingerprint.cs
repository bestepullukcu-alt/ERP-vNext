using System.Security.Cryptography;
using System.Text.Json;

namespace Diten.PlanningService.Domain.Features.DemandPlanning;

public static class ImportCoverageFingerprint
{
    public static string Compute(IEnumerable<ImportWeekCoverage> weeks)
    {
        var canonical = weeks.OrderBy(x => x.SkuId).ThenBy(x => x.SkuLevel, StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal)
            .ThenBy(x => x.WeekStart)
            .Select(x => new
            {
                skuId = x.SkuId.ToString("D"), x.SkuLevel, x.WarehouseId, x.UomId,
                weekStart = x.WeekStart.ToString("yyyy-MM-dd"),
                stockState = x.StockState?.ToString()
            });
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
    }
}
