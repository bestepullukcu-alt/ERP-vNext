namespace Diten.Platform.Application.Features.Navigation;

/// <summary>
/// FEAT-TENANT-NAV-PREFS — a tenant's preference for a single module: reorder (<see cref="SortOrder"/> override,
/// null = catalog order), show/hide (<see cref="IsHidden"/>), rename (<see cref="DisplayNameOverride"/>, null/blank
/// = real name). Used both as the GET response row and the PUT input row.
/// </summary>
public sealed record TenantNavPreferenceDto(
    string ModuleCode,
    int? SortOrder,
    bool IsHidden,
    string? DisplayNameOverride);
