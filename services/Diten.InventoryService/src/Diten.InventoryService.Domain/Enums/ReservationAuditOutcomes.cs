namespace Diten.InventoryService.Domain.Enums;

/// <summary>
/// AUD-001 §3 outcome vocabulary of <c>InventoryReservationAuditRecord.Outcome</c> (MOD-0173-FU03 §4.4): every refusal of the pack is
/// <see cref="Failed"/>; <c>Denied</c> (a 403) never reaches a handler and is not written here. Rows written before FU03 carry no value
/// and read as <see cref="Succeeded"/>.
/// </summary>
public static class ReservationAuditOutcomes
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
