using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.PurchaseOrders;

// MOD-0141 Purchase Order — frontend view models.
// Fields bind ONLY the REQUISITION-PO contract (docs/analysis/contracts/requisition-po.openapi.yaml):
// PurchaseOrderUpsert / PurchaseOrder (PO master + embedded PoLine).
// Supplier identity (MOD-0140), item/SKU/UoM identity (MOD-0290/MOD-0048) are CONSUMED, never created
// (opaque references; fail-closed server-side). lineAmount/totalAmount are server-computed (not user input).
public sealed class PurchaseOrdersEditViewModel
{
    // Server-assigned public code (PurchaseOrder.poId); empty on create.
    public string? PoId { get; set; }

    // MOD-0140 supplier public code (consume; fail-closed 404 server-side).
    [Required]
    public string SupplierId { get; set; } = string.Empty;

    // Optional approved-requisition reference.
    public string? RequisitionId { get; set; }

    // LE base currency (required).
    [Required]
    public string Currency { get; set; } = string.Empty;

    // PoStatus enum (read-only surface; server-driven). Draft on create.
    public string? Status { get; set; } = "Draft";

    // PoLine[] — {itemId (MOD-0290 uuid consume), skuId? (uuid), quantity (Decimal string), uomId (MOD-0048 consume), unitPrice (Decimal string)}.
    public List<PurchaseOrderLineInput> Lines { get; set; } = [new PurchaseOrderLineInput()];

    // DEC-INV-19 external feed (nullable).
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // Server-computed display-only (Σ lineAmount). Never sent on create; shown Decimal-string (float YASAK).
    public string? TotalAmount { get; set; }
}

// PoLine request/display row (contract PoLine). quantity/unitPrice are Decimal strings (float YASAK).
public sealed class PurchaseOrderLineInput
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? UnitPrice { get; set; }

    // Server-computed display-only (quantity × unitPrice). Never sent on create; shown Decimal-string.
    public string? LineAmount { get; set; }
}

// ── Deserialization of the REQUISITION-PO contract PurchaseOrder (inside the house Response<T>.Data) ──
// Enums (status) are serialized numerically by the service (no JsonStringEnumConverter observed);
// bound as int? and mapped to canonical contract names in the controller. See ASSUMPTION-P2P-FE-01.
public sealed class PurchaseOrderApiModel
{
    public string PoId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? RequisitionId { get; set; }
    public int? Status { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<PurchaseOrderLineApiModel> Lines { get; set; } = [];
    public string? TotalAmount { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public int Version { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class PurchaseOrderLineApiModel
{
    public string? PoLineId { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? UnitPrice { get; set; }
    public string? LineAmount { get; set; }
}

// listPurchaseOrders result — contract {items, nextCursor, contractVersion}.
public sealed class PurchaseOrderListData
{
    public List<PurchaseOrderApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/purchase-orders payload — PurchaseOrderUpsert shape (server resolves TenantId/LegalEntityId).
public sealed class PurchaseOrderUpsertPayload
{
    public string SupplierId { get; set; } = string.Empty;
    public string? RequisitionId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<PurchaseOrderLinePayload> Lines { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class PurchaseOrderLinePayload
{
    public string ItemId { get; set; } = string.Empty;
    public string? SkuId { get; set; }
    public string Quantity { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
    public string UnitPrice { get; set; } = string.Empty;
}

// ── Suppliers consume surface (MOD-0140) — supplier lookup only ──
public sealed class SupplierLookupData
{
    public List<SupplierLookupItem> Items { get; set; } = [];
}

public sealed class SupplierLookupItem
{
    public string SupplierId { get; set; } = string.Empty;
    public string? Name { get; set; }
}

// House response envelope (Response<T>): { data, isSuccessful, statusCode, errors }.
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
