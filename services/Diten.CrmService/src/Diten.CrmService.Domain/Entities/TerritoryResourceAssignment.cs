namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU04/FU04A resource assignment (aggregate, model-scoped, pack §7.5 / §10 / §22.3). Answers "who is responsible for
/// this territory node / business scope?" — e.g. Beylikdüzü Zone + Alpha → Medical Representative Ayşe.
///
/// <para><b>Boundaries.</b> Employee / User / Person / Position master is NOT owned here (MOD-0288 / MOD-0018);
/// this aggregate only holds a <see cref="TerritoryResourceRef"/> seam. It also assigns no ACCOUNTS: customer↔territory
/// coverage is <c>AccountTerritoryAssignment</c>, which is FU05 and does not exist yet.</para>
///
/// <para><b>Ending is not deleting</b> (pack §10): terminate with <c>Status=ended</c> + <see cref="ValidTo"/>.
/// Only a not-yet-effective (<c>proposed</c>) assignment may be soft-deleted; there is no hard delete.</para>
/// </summary>
public sealed class TerritoryResourceAssignment : EntityBase
{
    public Guid ModelId { get; set; }

    /// <summary>Target node. NULL for scopes that are not territory-bound (business-unit / product-portfolio /
    /// business-scope / model-wide / all-business-scopes) — driven by the coverage scope's MOD-0048 metadata.</summary>
    public Guid? TerritoryId { get; set; }

    /// <summary>The person/user being made responsible. A reference, never a copy of an HR record.</summary>
    public TerritoryResourceRef Resource { get; set; } = new();

    /// <summary>Canonical position identity for FU04A writes and operational queries.</summary>
    public TerritoryPositionRef Position { get; set; } = new();

    /// <summary>Legacy flat Position snapshot retained only to deserialize FU04 records created before FU04A.
    /// New writes use <see cref="Position"/>.</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Legacy compatibility field. Operational matching resolves <see cref="Position"/> first.</summary>
    public string PositionCode { get; set; } = string.Empty;

    /// <summary>Legacy compatibility field. New DTOs expose PositionTitle from <see cref="Position"/>.</summary>
    public string PositionName { get; set; } = string.Empty;

    /// <summary>MOD-0048 published <c>territory-coverage-scope</c> value code.</summary>
    public string CoverageScope { get; set; } = string.Empty;

    /// <summary>Business-unit scopes this responsibility covers. Must stay inside the model's own scope set.</summary>
    public List<TerritoryBusinessScope> BusinessScopes { get; set; } = [];

    /// <summary>MOD-0048 published <c>territory-assignment-status</c> value code (proposed / active / ended / rejected).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>MOD-0048 published <c>territory-assignment-source</c> value code (manual / override / rule / import).</summary>
    public string AssignmentSource { get; set; } = string.Empty;

    /// <summary>Primary responsibility. Only roles whose metadata says <c>canBePrimary</c> may set this; a
    /// non-primary (backup / shared) assignment is exempt from the exclusivity guards (pack §10).</summary>
    public bool IsPrimary { get; set; }

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    /// <summary>Required when the assignment source metadata says <c>requiresReason</c> (manual / override).</summary>
    public string? ChangeReason { get; set; }

    public string? CorrelationId { get; set; }

    public Guid? ReplacedAssignmentId { get; set; }
    public Guid? ReplacementAssignmentId { get; set; }
    public string? ReplacementReason { get; set; }
    public string? PreviousPositionCode { get; set; }
    public string? NewPositionCode { get; set; }

    public Guid? TransferFromAssignmentId { get; set; }
    public Guid? TransferToAssignmentId { get; set; }
    public string? TransferReason { get; set; }

    public string EffectivePositionCode
        => !string.IsNullOrWhiteSpace(Position.PositionCode) ? Position.PositionCode : PositionCode;

    public string EffectivePositionTitle
        => !string.IsNullOrWhiteSpace(Position.PositionTitle) ? Position.PositionTitle : PositionName;
}

/// <summary>
/// Consume-only snapshot of the canonical Organization Position. It is not a Position master copy.
/// The snapshot keeps current responsibility deterministic when the directory is temporarily unavailable.
/// </summary>
public sealed class TerritoryPositionRef
{
    public Guid? PositionId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string ValidationMode { get; set; } = "snapshot";
    public string? PolicySource { get; set; }
}

/// <summary>
/// Reference to the assigned person (pack §10 seam). MOD-0151 stores the id plus a display snapshot so a list can be
/// rendered without a cross-service call; the snapshot is display-only and is never the system of record. No employee
/// master, no seeded person list.
/// </summary>
public sealed class TerritoryResourceRef
{
    /// <summary>Id in the owning master (MOD-0288 Person / MOD-0018 User / HCM Employee).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Which master the id belongs to — kept so a later HCM/Person integration can resolve it correctly.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Display snapshot for lists. Display-only, never a query/match key (same rule as
    /// <c>AccountCodeSnapshot</c> in pack §7.4).</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }
}
