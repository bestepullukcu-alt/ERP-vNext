using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Manufacturing.Boms;

// MOD-0193 BOM & Routings — form + read models for the same-origin adapter (ManufacturingBomsController).
// Shapes follow the bom contract (MOD-0193, v1.1.0). TenantId never appears (the service reads it from the token);
// the legal entity is the one the user chose on the page, sent as X-Legal-Entity-Id and proven by MDM.

public sealed class BomEditViewModel
{
    public string? BomVersionId { get; set; }

    /// <summary>The legal entity the BOM belongs to — chosen on create from MDM's lookup, fixed afterwards.</summary>
    [Required]
    [RegularExpression("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    public string? LegalEntityId { get; set; }

    [Required]
    [RegularExpression("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    public string? ItemId { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public int RowVersion { get; set; }

    public int Version { get; set; }

    public string Status { get; set; } = "Draft";

    public List<BomComponentInput> Components { get; set; } = [];

    public List<RoutingStepInput> Steps { get; set; } = [];
}

public sealed class BomComponentInput
{
    public string? ComponentItemId { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public int? Position { get; set; }

    /// <summary>Comma-separated item ids (the form's single text field).</summary>
    public string? Alternates { get; set; }
}

public sealed class RoutingStepInput
{
    public int? StepNo { get; set; }
    public string? Operation { get; set; }
    public string? WorkCenter { get; set; }
}

public sealed class BomDetailsViewModel
{
    public required BomApiView Bom { get; init; }
    public IReadOnlyList<BomHistoryApiEntry> History { get; init; } = [];
    public bool HistoryLoaded { get; init; }
    public string LegalEntityName { get; init; } = string.Empty;
}

// ── Wire models (service → adapter) ──

public sealed class BomApiView
{
    public string BomVersionId { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<BomApiComponent> Components { get; set; } = [];
    public BomApiRouting? Routing { get; set; }
    public string? Description { get; set; }
    public string? ChangeControlRef { get; set; }
    public int RowVersion { get; set; }
    public Guid LegalEntityId { get; set; }
}

public sealed class BomApiComponent
{
    public Guid ComponentItemId { get; set; }
    public string Quantity { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
    public int Position { get; set; }
    public List<string>? Alternates { get; set; }
}

public sealed class BomApiRouting
{
    public string RoutingId { get; set; } = string.Empty;
    public List<BomApiStep> Steps { get; set; } = [];
}

public sealed class BomApiStep
{
    public int StepNo { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? WorkCenter { get; set; }
}

public sealed class BomHistoryApiResponse
{
    public List<BomHistoryApiEntry> Entries { get; set; } = [];
}

public sealed class BomHistoryApiEntry
{
    public string Operation { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public List<string> ChangedFields { get; set; } = [];
    public string? ChangeControlRef { get; set; }
    public Guid ActorId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class BomApiError
{
    public BomApiErrorBody? Error { get; set; }
}

public sealed class BomApiErrorBody
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class BomReleaseInput
{
    public string? ChangeControlRef { get; set; }
    public int RowVersion { get; set; }
}

public sealed class BomExplodeInput
{
    public string? Quantity { get; set; }
}

public sealed class LegalEntityLookupEnvelope
{
    public List<LegalEntityLookupItem>? Data { get; set; }
}

public sealed class LegalEntityLookupItem
{
    public Guid LegalEntityId { get; set; }
    public string? Code { get; set; }
    public string? LegalName { get; set; }
    public string? DisplayName { get; set; }
    public string? LifecycleState { get; set; }
    public bool Referenceable { get; set; }
}
