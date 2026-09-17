using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.Requisitions;

// MOD-0141 Requisition — frontend view models.
// Fields bind ONLY the REQUISITION-PO contract (docs/analysis/contracts/requisition-po.openapi.yaml):
// RequisitionUpsert / Requisition (requisition master + embedded RequisitionLine).
// Item/SKU/UoM identity (MOD-0290/MOD-0048) is CONSUMED, never created (opaque references; fail-closed server-side).
public sealed class RequisitionsEditViewModel
{
    // Server-assigned public code (Requisition.requisitionId); empty on create.
    public string? RequisitionId { get; set; }

    // RequisitionStatus enum (read-only surface; server-driven). Draft on create.
    public string? Status { get; set; } = "Draft";

    // RequisitionLine[] — {itemId (MOD-0290 uuid consume), skuId? (uuid), quantity (Decimal string), uomId (MOD-0048 consume), needBy? (date)}.
    public List<RequisitionLineInput> Lines { get; set; } = [new RequisitionLineInput()];

    // Free-text justification (nullable).
    public string? Justification { get; set; }
}

// RequisitionLine request/display row (contract RequisitionLine). quantity is Decimal string (float YASAK).
public sealed class RequisitionLineInput
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? NeedBy { get; set; }
}

// ── Deserialization of the REQUISITION-PO contract Requisition (inside the house Response<T>.Data) ──
// Enums (status) are serialized numerically by the service (no JsonStringEnumConverter observed);
// bound as int? and mapped to canonical contract names in the controller. See ASSUMPTION-P2P-FE-01.
public sealed class RequisitionApiModel
{
    public string RequisitionId { get; set; } = string.Empty;
    public int? Status { get; set; }
    public List<RequisitionLineApiModel> Lines { get; set; } = [];
    public string? Justification { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public int Version { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class RequisitionLineApiModel
{
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? NeedBy { get; set; }
}

// listRequisitions result — contract {items, nextCursor, contractVersion}.
public sealed class RequisitionListData
{
    public List<RequisitionApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/requisitions payload — RequisitionUpsert shape (server resolves TenantId/LegalEntityId).
public sealed class RequisitionUpsertPayload
{
    public List<RequisitionLinePayload> Lines { get; set; } = [];
    public string? Justification { get; set; }
}

public sealed class RequisitionLinePayload
{
    public string ItemId { get; set; } = string.Empty;
    public string? SkuId { get; set; }
    public string Quantity { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
    public string? NeedBy { get; set; }
}

// House response envelope (Response<T>): { data, isSuccessful, statusCode, errors }.
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
