using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// MOD-0162-FU03 Concept Graph UI view models. TenantId is never part of any model — it is server-resolved. The Compact
// primary surface is ConceptNode; Type / Relationship / ChainTemplate are Slim tabs driven by AJAX. Reference pickers
// are populated server-side by the proxy controller.

/// <summary>A picker option (Subject / ConceptType / Global Product). Group carries the parent key for cascades
/// (a ConceptType option's group is its SubjectId, enabling the Subject→Type cascade).</summary>
public sealed class ConceptOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>ConceptNode read model (Details / Edit source). The ExternalRef pair is provenance only — no master is copied.</summary>
public sealed class ConceptNodeDetailViewModel
{
    public Guid ConceptNodeId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ConceptTypeId { get; set; }
    // Resolved display labels for the classification ids (fail-soft; null when unresolved → view falls back to the id).
    public string? SubjectName { get; set; }
    public string? ConceptTypeName { get; set; }
    public string ConceptNodeCode { get; set; } = string.Empty;
    public string ConceptNodeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ExternalRefType { get; set; }
    public string? ExternalRefId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>ConceptNode create/edit form model (Compact). Required fields mirror the backend validator exactly.</summary>
public sealed class ConceptNodeEditViewModel
{
    public Guid ConceptNodeId { get; set; }

    [Required] public Guid SubjectId { get; set; }
    [Required] public Guid ConceptTypeId { get; set; }
    [Required, StringLength(120)] public string ConceptNodeCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ConceptNodeName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public string? Status { get; set; }
    [Required] public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ExternalRefType { get; set; }
    [StringLength(240)] public string? ExternalRefId { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsArchived { get; set; }

    // Populated server-side.
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public IReadOnlyList<string> ExternalRefTypes { get; set; } = [];
    public List<ConceptOptionViewModel> SubjectOptions { get; set; } = [];
    public List<ConceptOptionViewModel> TypeOptions { get; set; } = [];

    // Non-null when the Global Product picker cannot be used (endpoint 404 / permission 403 / unavailable). The view
    // then renders the picker disabled with this reason instead of a silent empty list.
    public string? GlobalProductPickerDisabledReason { get; set; }

    // EnsureSelected for the Global Product picker: when ExternalRefType = global-product, the stored ExternalRefId is
    // pre-resolved to "canonicalCode — globalProductName" so an Edit form opens on a product, never on a bare GUID.
    // Null when there is nothing to resolve; on a failed resolve the view falls back to the raw id (never blank —
    // the value must survive the round-trip).
    public string? GlobalProductSelectedLabel { get; set; }
    public string? ContractError { get; set; }
}

public sealed class ConceptNodePageViewModel
{
    public ConceptNodeDetailViewModel Node { get; set; } = new();
    public bool CanManage { get; set; }
}

public sealed class ConceptContractViewModel
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public ConceptVocabularyViewModel Vocabularies { get; set; } = new();
    public List<string> Permissions { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class ConceptVocabularyViewModel
{
    public List<string> ConceptStatuses { get; set; } = [];
    public List<string> ChainStatuses { get; set; } = [];
    public List<string> RelationshipTypes { get; set; } = [];
    public List<string> Directions { get; set; } = [];
    public List<string> ExternalRefTypes { get; set; } = [];
    public List<string> LinkRoles { get; set; } = [];
}

public sealed class ConceptGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
