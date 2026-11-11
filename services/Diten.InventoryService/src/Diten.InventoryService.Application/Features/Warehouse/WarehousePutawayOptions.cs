using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// <c>Warehouse:Putaway</c> (pack §4.2, §8.3): the flight lock, the posting time limit, the "long Pending" badge and the reconciler. Checked at
/// start — an inconsistent setting stops the host (fail-closed, <see cref="WarehousePutawayOptionsValidator"/>).
/// </summary>
public sealed class WarehousePutawayOptions
{
    public const string SectionName = "Warehouse:Putaway";

    /// <summary>The flight lock of an attempt (proposal 120 s); must be ≥ <see cref="PostingTimeoutSeconds"/> + 30.</summary>
    public int InFlightLeaseSeconds { get; set; } = 120;

    /// <summary>The upper bound of one posting call (proposal 60 s).</summary>
    public int PostingTimeoutSeconds { get; set; } = 60;

    /// <summary>A Pending older than this is "Uzun süredir bekliyor" (proposal 30); must be &gt; 0 and its seconds &gt; the lease.</summary>
    public int LongPendingMinutes { get; set; } = 30;

    /// <summary>The reconciler's tick (Q-09: 60 s) and the age of a Pending it looks at (2 minutes).</summary>
    public int ReconcileIntervalSeconds { get; set; } = 60;

    public int ReconcileAgeSeconds { get; set; } = 120;

    /// <summary>Records per branch per tick (§8.3: ≤ 200, two budgets).</summary>
    public int ReconcileBatchSize { get; set; } = 200;

    public bool ReconcilerEnabled { get; set; } = true;
}

/// <summary>
/// The start-up check of pack §4.2: <c>InFlightLeaseSeconds ≥ PostingTimeoutSeconds + 30</c>, <c>LongPendingMinutes &gt; 0</c> and
/// <c>LongPendingMinutes × 60 &gt; InFlightLeaseSeconds</c> (the badge never shows before the first attempt's lock ran out — independent
/// review 14 F-3). Any violation → the host does not start.
/// </summary>
public sealed class WarehousePutawayOptionsValidator : IValidateOptions<WarehousePutawayOptions>
{
    public ValidateOptionsResult Validate(string? name, WarehousePutawayOptions options)
    {
        var failures = new List<string>();
        if (options.PostingTimeoutSeconds <= 0) failures.Add("Warehouse:Putaway:PostingTimeoutSeconds must be > 0.");
        if (options.InFlightLeaseSeconds < options.PostingTimeoutSeconds + 30)
            failures.Add("Warehouse:Putaway:InFlightLeaseSeconds must be ≥ PostingTimeoutSeconds + 30.");
        if (options.LongPendingMinutes <= 0) failures.Add("Warehouse:Putaway:LongPendingMinutes must be > 0.");
        else if ((long)options.LongPendingMinutes * 60 <= options.InFlightLeaseSeconds)
            failures.Add("Warehouse:Putaway:LongPendingMinutes × 60 must be > InFlightLeaseSeconds.");
        if (options.ReconcileIntervalSeconds <= 0 || options.ReconcileAgeSeconds < 0) failures.Add("Warehouse:Putaway reconciler timings are invalid.");
        if (options.ReconcileBatchSize is < 1 or > 200) failures.Add("Warehouse:Putaway:ReconcileBatchSize must be 1-200.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
