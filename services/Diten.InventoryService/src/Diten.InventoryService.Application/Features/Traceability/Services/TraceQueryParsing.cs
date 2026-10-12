using System.Globalization;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>Literal query parameters of the TRACE read operations (400 <c>INVALID_REQUEST</c> when malformed).</summary>
public static class TraceQueryParsing
{
    /// <summary>page ≥ 1 and 1 ≤ pageSize ≤ 200 (defaults 1 / 50). False = malformed.</summary>
    public static bool TryPaging(string? page, string? pageSize, out int pageValue, out int pageSizeValue)
    {
        pageValue = 1;
        pageSizeValue = InventoryContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageValue) || pageValue < 1))
            return false;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out pageSizeValue)
                                     || pageSizeValue < 1 || pageSizeValue > InventoryContract.MaxPageSize))
            return false;
        return true;
    }

    /// <summary>"true" / "false" only; absent = false.</summary>
    public static bool TryFlag(string? raw, out bool value)
    {
        value = false;
        if (raw is null) return true;
        if (raw is "true") { value = true; return true; }
        return raw is "false";
    }
}
