namespace Diten.InventoryService.Domain.Enums;

/// <summary>Direction class of a movement type — rows of the pack §12 direction matrix.</summary>
public enum MovementClass
{
    Inbound,
    Outbound,
    WarehouseTransfer,
    LocationTransfer,
    StatusTransfer,
    Reversal
}
