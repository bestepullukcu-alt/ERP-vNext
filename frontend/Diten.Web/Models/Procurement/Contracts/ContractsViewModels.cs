using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.Contracts;

// MOD-0144 Contracting & Clause Library — frontend view models.
// Fields bind ONLY the CONTRACTING contract (docs/analysis/contracts/contracting.openapi.yaml):
// ContractUpsert / Contract (contract master + embedded ClauseRef), ClauseUpsert / Clause (clause library).
// Supplier identity (MOD-0140) and award/rfx identity (MOD-0145) are CONSUMED, never created (fail-closed server-side).
public sealed class ContractEditViewModel
{
    // Server-assigned public code (Contract.contractId); empty on create.
    public string? ContractId { get; set; }

    // MOD-0140 consume; fail-closed server-side (unknown supplier → 404 UNKNOWN_REFERENCE).
    [Required]
    public string SupplierId { get; set; } = string.Empty;

    // MOD-0145 award consume (optional); fail-closed server-side when provided.
    public string? RfxId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    // Required effective start (nullable CLR type so the value binder maps an empty POST cleanly).
    [Required]
    public DateTime? EffectiveFrom { get; set; }

    // Optional effective end (≥ EffectiveFrom); nullable → no generated data-val-required.
    public DateTime? EffectiveTo { get; set; }

    // LE base currency (nullable; server default when empty — ASSUMPTION-0144-04).
    public string? Currency { get; set; }

    // ContractStatus enum (read-only surface; server-driven). Draft on create.
    public string? Status { get; set; } = "Draft";

    // ClauseRef[] — {clauseId (clause library reference), deviation?, deviationText?}.
    public List<ContractClauseInput> Clauses { get; set; } = [new ContractClauseInput()];

    // evidenceRef → MOD-0029/0031 Evidence; binary NOT stored here (string references only).
    public List<string> EvidenceRefs { get; set; } = [];

    // Server-driven approval instance (MOD-0023) set on activate; read-only display.
    public string? WorkflowInstanceId { get; set; }
}

// ClauseRef request/display row (contract ClauseRef).
public sealed class ContractClauseInput
{
    public string? ClauseId { get; set; }
    public bool Deviation { get; set; }
    public string? DeviationText { get; set; }
}

// ── Deserialization of the CONTRACTING contract Contract (inside the house Response<T>.Data) ──
public sealed class ContractApiModel
{
    public string ContractId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? RfxId { get; set; }
    public string Title { get; set; } = string.Empty;
    // ContractStatus enum serializes numerically (no JsonStringEnumConverter on the service) → bound as int? and
    // mapped to canonical names in the controller. EffectiveFrom/To are contract date strings ("yyyy-MM-dd").
    public int? Status { get; set; }
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public string? Currency { get; set; }
    public List<ClauseRefApiModel> Clauses { get; set; } = [];
    public List<string> EvidenceRefs { get; set; } = [];
    public string? WorkflowInstanceId { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class ClauseRefApiModel
{
    public string? ClauseId { get; set; }
    public bool? Deviation { get; set; }
    public string? DeviationText { get; set; }
}

// CONTRACTING listContracts result — contract {items, nextCursor, contractVersion}.
public sealed class ContractListData
{
    public List<ContractApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

// POST /api/contracts payload — ContractUpsert shape (server resolves TenantId/LegalEntityId).
public sealed class ContractUpsertPayload
{
    public string SupplierId { get; set; } = string.Empty;
    public string? RfxId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public string? Currency { get; set; }
    public List<ClauseRefPayload> Clauses { get; set; } = [];
    public List<string>? EvidenceRefs { get; set; }
}

public sealed class ClauseRefPayload
{
    public string ClauseId { get; set; } = string.Empty;
    public bool Deviation { get; set; }
    public string? DeviationText { get; set; }
}

// ── Clause library (CONTRACTING /clauses) ──
public sealed class ClauseApiModel
{
    public string ClauseId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ContractVersion { get; set; }
}

public sealed class ClauseListData
{
    public List<ClauseApiModel> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class ClauseUpsertPayload
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
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
