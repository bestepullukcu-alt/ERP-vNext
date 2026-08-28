namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// External/legacy identity for an Account (§10.1b). Separate from AccountCode (CRM's own id).
/// Same ExternalId may come from different SourceSystems. Unique per (TenantId, SourceSystem, ExternalId).
/// No runtime OldSystem dependency — this is compatibility data only.
/// </summary>
public sealed class AccountExternalReference : EntityBase
{
    public Guid AccountId { get; set; }

    /// <summary>Normalized source system code (e.g. "OldCRM", "OldSystem").</summary>
    public string SourceSystem { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Legacy entity kind the external id came from (e.g. "WorkPlace", "Client", "Account").</summary>
    public string? SourceEntity { get; set; }

    public string? DisplayName { get; set; }
    public string? Notes { get; set; }
}
