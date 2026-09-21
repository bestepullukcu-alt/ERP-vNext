using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// SCMM-12-UI (CAND-CAP-0011) Claim authoring console view models — mirrors the MOD-0162-FU03 Concept UI shapes.
// TenantId is never part of any model (server-resolved). The console is the Compact primary surface (route-based
// Create/Edit pages); the Index list rows are fetched client-side through the same-origin proxy. The component picker
// options (KnowledgeContent by-id) are populated server-side by the proxy controller.

/// <summary>A picker option (component / status). Mirrors ConceptOptionViewModel. IsInactive marks an EnsureSelected
/// fallback option (an archived / unresolved reference kept so the stored value survives the round-trip).</summary>
public sealed class ClaimOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>Claim read model (Edit source). Mirrors the CrmService ClaimDto view/form fields (SCMM-12).</summary>
public sealed class ClaimDetailViewModel
{
    public Guid ClaimId { get; set; }
    public string ClaimCode { get; set; } = string.Empty;
    public string ClaimName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ClaimText { get; set; } = string.Empty;
    public List<string> Qualifiers { get; set; } = [];
    public List<string> ProductRefs { get; set; } = [];
    public List<string> MarketRefs { get; set; } = [];
    public List<string> AudienceRefs { get; set; } = [];
    public Guid? EligibilityPolicyId { get; set; }
    public List<string> EvidenceRefs { get; set; } = [];
    public List<Guid> ComponentRefs { get; set; } = [];
    public string ClaimVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>Claim create/edit form model (Compact). Required fields mirror the backend validator exactly. The
/// applicability object is flattened to sibling list fields; the controller re-nests them into the request payload.</summary>
public sealed class ClaimEditViewModel
{
    public Guid ClaimId { get; set; }

    // ClaimCode is the stable business key — set on create, immutable afterwards (read-only on edit).
    [Required, StringLength(120)] public string ClaimCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ClaimName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Required] public string ClaimText { get; set; } = string.Empty;

    // Governed body multi-value lists + applicability (posted from tag / multi-select inputs).
    public List<string> Qualifiers { get; set; } = [];
    public List<string> ProductRefs { get; set; } = [];
    public List<string> MarketRefs { get; set; } = [];
    public List<string> AudienceRefs { get; set; } = [];
    public Guid? EligibilityPolicyId { get; set; }
    public List<string> EvidenceRefs { get; set; } = [];
    // By-id KnowledgeContent references (D02c reuse) — the component picker.
    public List<Guid> ComponentRefs { get; set; } = [];

    [StringLength(60)] public string? ClaimVersion { get; set; }
    // Create / update carry only draft | inactive (approve / archive are dedicated actions). Kept null on an approved
    // edit so the backend preserves the approved status.
    public string? Status { get; set; }
    [Required] public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    // True once the claim is approved: the governed body is frozen (backend 409s a body change), so the form renders
    // those fields read-only and does not post a status.
    public bool IsApproved { get; set; }
    public bool IsArchived { get; set; }

    // Populated server-side.
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public List<ClaimOptionViewModel> ComponentOptions { get; set; } = [];
}

public sealed class ClaimGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
