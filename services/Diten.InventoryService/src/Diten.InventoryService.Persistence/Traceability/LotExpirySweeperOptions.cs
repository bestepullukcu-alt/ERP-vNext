namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>Configuration section <c>Inventory:Trace</c> of the MOD-0174 expiry sweeper (B-3).</summary>
public sealed class LotExpirySweeperOptions
{
    public const string SectionName = "Inventory:Trace";

    public bool SweeperEnabled { get; set; } = true;

    public int SweepIntervalSeconds { get; set; } = 900;
}
