namespace Diten.Platform.Infrastructure.Settings;

/// <summary>
/// WP-DM-1b — configuration for the tenant-AGNOSTIC Document Master Register auto-seed. Mirrors the
/// <see cref="AuditRetentionSeedOptions"/> settings shape. There is NO hardcoded tenant (the legacy
/// PositionAssignmentSeed's 97c5 hardcode is deliberately NOT followed): the target tenant comes from
/// <see cref="TenantId"/>. When <see cref="Enabled"/> is false, <see cref="TenantId"/> is absent/blank, or the section
/// is missing (production), the seed is skipped — no leakage. Local dev sets these from its own appsettings.
/// </summary>
public sealed class DocumentRegisterSeedOptions
{
    public const string SectionName = "DocumentRegisterSeed";

    /// <summary>Master switch. Default false: absent config ⇒ no seed.</summary>
    public bool Enabled { get; set; }

    /// <summary>Target tenant (GUID string). Null/blank/non-GUID ⇒ skip (never guessed or hardcoded).</summary>
    public string? TenantId { get; set; }

    /// <summary>Path to the GMG controlled-document reference CSV. Missing/nonexistent ⇒ skip.</summary>
    public string? CsvPath { get; set; }

    /// <summary>True only when a real, non-empty target tenant GUID is configured.</summary>
    public bool TryGetTenantId(out Guid tenantId) =>
        Guid.TryParse(TenantId, out tenantId) && tenantId != Guid.Empty;
}
