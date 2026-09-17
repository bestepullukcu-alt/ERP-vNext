using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.Grn;

// MOD-0142 Receiving (GRN) — frontend view models.
// Fields bind ONLY the GRN-EVENT contract (docs/analysis/contracts/grn-event.openapi.yaml):
// GrnRequest / GrnResponse (GRN header + embedded GrnLine). Item/SKU/UoM identity (MOD-0290),
// location identity (LOCATION) and PO reference (MOD-0141) are CONSUMED, never created.
// Quantities are Decimal strings (float YASAK). No update endpoint (create + reverse only).
public sealed class GrnEditViewModel
{
    // Server-assigned public code (GrnResponse.grnId); empty on create.
    public string? GrnId { get; set; }

    // Upstream PO reference (MOD-0141), nullable.
    public string? PoId { get; set; }

    // Consumed LOCATION warehouse (required).
    [Required]
    public string WarehouseId { get; set; } = string.Empty;

    // Consumed LOCATION bin/location (nullable).
    public string? LocationId { get; set; }

    // GrnStatus enum (read-only surface; server-driven). Draft on create.
    public string? Status { get; set; } = "Draft";

    // Server-assigned receipt timestamp (display-only on Details).
    public DateTime? ReceivedAt { get; set; }

    // GrnLine[] — receipt lines. quantity is Decimal string (float YASAK).
    public List<GrnLineInput> Lines { get; set; } = [new GrnLineInput()];

    // DEC-INV-19 external feed (nullable).
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

// GrnLine request/display row (contract GrnLine + GrnResponse.lines[]).
// Create binds itemId/skuId/skuLevel/lotNumber/serialIds/quantity/uomId/toStockStatus/poLineId;
// Details binds the server-computed poLineId/inventoryTransactionId/lotId/quantity read references.
public sealed class GrnLineInput
{
    public string? PoLineId { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? LotNumber { get; set; }

    // Comma-separated serial ids on the wire; split to string[] server-side (contract serialIds).
    public string? SerialIds { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? ToStockStatus { get; set; }

    // GrnResponse.lines[] read-only references (INVENTORY POST /movements results — balance DEĞİL).
    public string? InventoryTransactionId { get; set; }
    public string? LotId { get; set; }
}

// ── Deserialization of the GRN contract GrnResponse (inside the house Response<T>.Data) ──
// GrnStatus is serialized numerically by the service (no JsonStringEnumConverter observed;
// Draft=0, Posted=1, Reversed=2); bound as int? and mapped to canonical contract names in the controller.
public sealed class GrnResponseApiModel
{
    public string GrnId { get; set; } = string.Empty;
    public string? PoId { get; set; }
    public int? Status { get; set; }
    public List<GrnLineResultApiModel> Lines { get; set; } = [];
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class GrnLineResultApiModel
{
    public string? PoLineId { get; set; }
    public string? InventoryTransactionId { get; set; }
    public string? LotId { get; set; }
    public string? Quantity { get; set; }
}

// GetGrnList result — backend GrnListResultDto {items, nextCursor, contractVersion}.
public sealed class GrnListData
{
    public List<GrnResponseApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/grn payload — GrnRequest shape (server resolves TenantId/LegalEntityId; Idempotency-Key is a header).
public sealed class GrnRequestPayload
{
    public string? PoId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string? LocationId { get; set; }
    public List<GrnLinePayload> Lines { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class GrnLinePayload
{
    public string? PoLineId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string SkuLevel { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public List<string>? SerialIds { get; set; }
    public string Quantity { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
    public string ToStockStatus { get; set; } = string.Empty;
}

// House response envelope (Response<T>): { data, isSuccessful, statusCode, errors }.
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
