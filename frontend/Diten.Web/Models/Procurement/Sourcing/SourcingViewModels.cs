using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.Sourcing;

// MOD-0145 Sourcing (RFQ/RFP) — frontend view models.
// Fields bind ONLY the SOURCING contract (docs/analysis/contracts/sourcing.openapi.yaml):
// RfxUpsert / RfxEvent (RFx master + embedded RfxLine), BidUpsert / Bid (embedded BidLine), AwardDecision.
// Supplier identity (MOD-0140) and item/UoM identity (MOD-0290) are CONSUMED, never created.
public sealed class SourcingEditViewModel
{
    // Server-assigned public code (RfxEvent.rfxId); empty on create.
    public string? RfxId { get; set; }

    // RfxType enum: RFQ | RFP | RFI.
    [Required]
    public string Type { get; set; } = "RFQ";

    [Required]
    public string Title { get; set; } = string.Empty;

    // Optional close date (nullable → no generated data-val-required on an optional date field).
    public DateTime? ClosesAt { get; set; }

    // RfxStatus enum (read-only surface; server-driven). Draft on create.
    public string? Status { get; set; } = "Draft";

    // Invited supplier public codes (MOD-0140 consume; fail-closed server-side).
    public List<string> InvitedSupplierIds { get; set; } = [];

    // RfxLine[] — {itemId (MOD-0290 uuid consume), quantity (Decimal string), uomId (MOD-0290 consume)}.
    public List<SourcingLineInput> Lines { get; set; } = [new SourcingLineInput()];

    // DEC-INV-19 external feed (nullable).
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

// RfxLine request/display row (contract RfxLine). quantity is Decimal string (float YASAK).
public sealed class SourcingLineInput
{
    public string? ItemId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
}

// ── Deserialization of the SOURCING contract RfxEvent (inside the house Response<T>.Data) ──
// Enums (type/status) are serialized numerically by the service (no JsonStringEnumConverter observed);
// bound as int? and mapped to canonical contract names in the controller. See ASSUMPTION-SRC-FE-02.
public sealed class RfxEventApiModel
{
    public string RfxId { get; set; } = string.Empty;
    public int? Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Status { get; set; }
    public DateTimeOffset? ClosesAt { get; set; }
    public List<string> InvitedSupplierIds { get; set; } = [];
    public List<RfxLineApiModel> Lines { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class RfxLineApiModel
{
    public string? ItemId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
}

// SOURCING listRfxEvents result — contract {items, nextCursor, contractVersion}.
public sealed class RfxEventListData
{
    public List<RfxEventApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/sourcing/events payload — RfxUpsert shape (server resolves TenantId/LegalEntityId).
public sealed class RfxUpsertPayload
{
    public string Type { get; set; } = "RFQ";
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? ClosesAt { get; set; }
    public List<string>? InvitedSupplierIds { get; set; }
    public List<RfxLinePayload> Lines { get; set; } = [];
}

public sealed class RfxLinePayload
{
    public string ItemId { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
}

// ── Suppliers consume surface (MOD-0140) — invited-supplier lookup only ──
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
