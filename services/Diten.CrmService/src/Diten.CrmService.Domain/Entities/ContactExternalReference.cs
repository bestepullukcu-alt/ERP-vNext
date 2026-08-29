namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// External/legacy identity for a Contact (mirrors AccountExternalReference). Same ExternalId may come from different
/// SourceSystems. Unique per (TenantId, SourceSystem, ExternalId). Compatibility data only — no runtime dependency.
/// </summary>
public sealed class ContactExternalReference : EntityBase
{
    public Guid ContactId { get; set; }

    /// <summary>Normalized source system code (e.g. "OldCRM").</summary>
    public string SourceSystem { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Legacy entity kind the external id came from (e.g. "Doctor", "Person").</summary>
    public string? SourceEntity { get; set; }

    public string? DisplayName { get; set; }
    public string? Notes { get; set; }
}
