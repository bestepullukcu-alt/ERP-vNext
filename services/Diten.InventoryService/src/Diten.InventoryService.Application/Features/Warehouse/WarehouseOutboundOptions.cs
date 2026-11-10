using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// <c>Warehouse:Outbound</c> — the S2 allocation settings (pack §8.17 step 5a / 6): the recovery lock an <c>Allocating</c> outbound keeps before
/// the maintenance service may complete it (proposal 120 s), and the maintenance tick. Checked at start: an inconsistent setting stops the host.
/// </summary>
public sealed class WarehouseOutboundOptions
{
    public const string SectionName = "Warehouse:Outbound";

    /// <summary>§8.17 step 5a: <c>AllocationLeaseUntilUtc = now + AllocationLeaseSeconds</c>.</summary>
    public int AllocationLeaseSeconds { get; set; } = 120;

    /// <summary>The maintenance hosted service runs at all (tests switch it off and call one tick themselves).</summary>
    public bool MaintenanceEnabled { get; set; } = true;

    public int MaintenanceIntervalSeconds { get; set; } = 60;

    /// <summary>Outbounds one tick looks at, at most.</summary>
    public int MaintenanceBatchSize { get; set; } = 200;

    /// <summary>
    /// DL-7 (pack §8.23): a <c>ReadyToShip</c> outbound whose <c>ReadyAtUtc</c> is older than this is the S3 <c>DispatchNotSeen</c> exception
    /// (proposal 24 h).
    /// </summary>
    public int DispatchNotSeenHours { get; set; } = 24;
}

public sealed class WarehouseOutboundOptionsValidator : IValidateOptions<WarehouseOutboundOptions>
{
    public ValidateOptionsResult Validate(string? name, WarehouseOutboundOptions options)
    {
        var failures = new List<string>();
        if (options.AllocationLeaseSeconds is < 10 or > 3600) failures.Add("Warehouse:Outbound:AllocationLeaseSeconds must be 10-3600.");
        if (options.MaintenanceIntervalSeconds is < 5 or > 3600) failures.Add("Warehouse:Outbound:MaintenanceIntervalSeconds must be 5-3600.");
        if (options.MaintenanceBatchSize is < 1 or > 1000) failures.Add("Warehouse:Outbound:MaintenanceBatchSize must be 1-1000.");
        if (options.DispatchNotSeenHours is < 1 or > 720) failures.Add("Warehouse:Outbound:DispatchNotSeenHours must be 1-720.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
