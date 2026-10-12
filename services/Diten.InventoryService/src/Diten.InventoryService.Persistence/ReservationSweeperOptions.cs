namespace Diten.InventoryService.Persistence;

/// <summary>Configuration section <c>Inventory:Reservations</c>.</summary>
public sealed class ReservationSweeperOptions
{
    public const string SectionName = "Inventory:Reservations";

    /// <summary>The periodic expiry sweeper (pack ASSUMPTION-0173-16). On by default.</summary>
    public bool SweeperEnabled { get; set; } = true;

    public int SweepIntervalSeconds { get; set; } = 60;
}
