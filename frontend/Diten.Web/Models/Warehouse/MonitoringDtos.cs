using System.Text.Json;

namespace Diten.Web.Models.Warehouse;

// MOD-0178 DL-7 — the S3 monitoring wire (WMS draft §9, pack §8.23) as the Web proxies read it from Gateway 5000 /api/warehouse. Read-only;
// no S3 answer carries a ship-to (Q-10a), so nothing here has one.

/// <summary>WMS draft §9 <c>WarehouseException</c> (+ <c>warehouseId</c>); <see cref="Details"/> is the kind's own object (pack §8.23).</summary>
public sealed class WmsExceptionDto
{
    public string? ExceptionId { get; set; }
    public string? Kind { get; set; }
    public string? TaskId { get; set; }
    public string? OutboundId { get; set; }
    public string? WarehouseId { get; set; }
    public string? Code { get; set; }
    public JsonElement Details { get; set; }
    public string? OpenedAtUtc { get; set; }
    public string? Status { get; set; }
}

public sealed class WmsExceptionPageDto
{
    public List<WmsExceptionDto>? Items { get; set; }
    public long? Total { get; set; }
}

/// <summary>One Activity Trace row (task timeline, outbound timeline or a <c>sourceModule MOD-0178</c> movement).</summary>
public sealed class WmsActivityDto
{
    public string? Source { get; set; }
    public string? Kind { get; set; }
    public string? AtUtc { get; set; }
    public string? RecordId { get; set; }
    public string? RecordNumber { get; set; }
    public int? Seq { get; set; }
    public string? WarehouseId { get; set; }
    public WmsPersonDto? Actor { get; set; }
    public bool System { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}

public sealed class WmsActivityPageDto
{
    public List<WmsActivityDto>? Items { get; set; }
    public long? Total { get; set; }
}

public sealed class WmsEventQueueDto
{
    public long Pending { get; set; }
    public long Delivered { get; set; }
    public long? OldestPendingAgeSeconds { get; set; }
    public string? OldestPendingAtUtc { get; set; }
}

public sealed class WmsAwaitingDispatchDto
{
    public long Count { get; set; }
    public string? OldestReadyAtUtc { get; set; }
}

public sealed class WmsLastSyncDto
{
    public string? LastShippedAtUtc { get; set; }
}

public sealed class WmsPendingConfirmationsDto
{
    public long Putaway { get; set; }
    public long Pick { get; set; }
    public long Total { get; set; }
}

/// <summary>§8.23 Integration Monitor — the four cards' values (r24, son bakış 02 F-9).</summary>
public sealed class WmsIntegrationStatusDto
{
    public WmsEventQueueDto? EventQueue { get; set; }
    public WmsAwaitingDispatchDto? AwaitingDispatch { get; set; }
    public WmsLastSyncDto? LastSync { get; set; }
    public WmsPendingConfirmationsDto? PendingConfirmations { get; set; }
    public string? AsOfUtc { get; set; }
}

/// <summary>
/// What the Integration Monitor page draws: the status (null when it could not be read) or the localized refusal. The cards are drawn by the
/// SERVER from this model — no loading title, no client fetch (a failed read shows the reason and nothing else).
/// </summary>
public sealed class WarehouseIntegrationViewModel
{
    public WmsIntegrationStatusDto? Status { get; init; }

    /// <summary>The localized reason the status is not shown (never the code, never "403").</summary>
    public string? Error { get; init; }

    /// <summary>The Gateway refused because no company is selected yet — the page shows only the company selector.</summary>
    public bool CompanyNotSelected { get; init; }
}
