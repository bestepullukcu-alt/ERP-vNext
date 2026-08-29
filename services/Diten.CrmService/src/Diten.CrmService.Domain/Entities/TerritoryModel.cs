namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 Territory Management — versioned territory plan container (aggregate root, FU01 scope).
/// Tenant-owned (TenantId server-resolved, never from client). Lifecycle status is a MOD-0048 published
/// value (<c>territory-model-status</c>); FU01 only creates/edits <c>draft</c> models — activation, approval,
/// supersede and change-request logic are later FUs and are NOT implemented here. The business plan version is
/// <see cref="VersionNumber"/>; the inherited <see cref="EntityBase.Version"/> stays the technical concurrency token.
/// </summary>
public sealed class TerritoryModel : EntityBase
{
    /// <summary>Tenant-scoped human-readable identifier. Unique per (TenantId, ModelCode). Trimmed/normalized.</summary>
    public string ModelCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional MOD-0048 <c>country</c> reference (scope of the plan). Not persisted as geo.</summary>
    public string? CountryScope { get; set; }

    /// <summary>Optional upper breakdown code (division scope). Legacy — superseded by <see cref="BusinessScopes"/>.</summary>
    public string? DivisionScope { get; set; }

    /// <summary>MOD-0151 FU02A crossing business scopes (business-unit only for now). Empty when none selected.</summary>
    public List<TerritoryBusinessScope> BusinessScopes { get; set; } = [];

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>MOD-0048 published <c>territory-model-status</c> value code. FU01 keeps this at <c>draft</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Business plan version (starts at 1; increments when a new version is cloned). NOT the concurrency token.</summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>Source model of a clone / new version (optional).</summary>
    public Guid? BasedOnModelId { get; set; }

    /// <summary>Required on new-version / lifecycle changes (FU01 stores it when supplied).</summary>
    public string? ChangeReason { get; set; }

    /// <summary>End-to-end correlation id (optional).</summary>
    public string? CorrelationId { get; set; }
}
