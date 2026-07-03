namespace Diten.Platform.Application.Features.Navigation;

/// <summary>
/// FEAT-NAVPREFS-DOMAINS — a tenant's preference for a single DOMAIN group: reorder (<see cref="SortOrder"/>
/// override, null = catalog order) and rename (<see cref="DisplayNameOverride"/>, null/blank = resolved name).
/// Used both as a GET response row and a PUT input row.
/// </summary>
public sealed record TenantNavDomainPreferenceDto(
    string DomainCode,
    int? SortOrder,
    string? DisplayNameOverride);

/// <summary>
/// FEAT-NAVPREFS-DOMAINS — the full sidebar preference set: per-module + per-domain. Returned by GET preferences
/// and accepted by PUT preferences (which also accepts the legacy module-only array for backward compatibility).
/// </summary>
public sealed record TenantNavPreferencesDto(
    IReadOnlyList<TenantNavPreferenceDto> Modules,
    IReadOnlyList<TenantNavDomainPreferenceDto> Domains);
