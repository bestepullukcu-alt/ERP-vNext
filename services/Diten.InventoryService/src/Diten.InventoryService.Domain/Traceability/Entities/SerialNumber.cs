using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// One serial number (collection <c>trace_serials</c>, pack §4). Created by an inbound ledger posting (slice P2); P1
/// owns the record and its reads. Identity is tenant-wide: Tenant + SKU + serial number (CT-D7), key built by
/// <see cref="Rules.LotIdentityPolicy"/>. <see cref="EntityBase.LegalEntityId"/> = the receiving company. Never deleted.
/// </summary>
public sealed class SerialNumber : EntityBase
{
    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public Guid? LotId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string IdentityKey { get; set; } = string.Empty;

    public SerialStatus Status { get; set; } = SerialStatus.Active;

    public string? WarehouseId { get; set; }

    public string? LocationId { get; set; }

    public string? ReceivedTransactionId { get; set; }

    public string? LastTransactionId { get; set; }

    public DateTimeOffset? LastMovementAt { get; set; }
}
