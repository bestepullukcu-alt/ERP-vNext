namespace Diten.AuthService.Application.DTOs;

/// <summary>The assigned permissions of a role (read model for the FE-C role-permission screen).</summary>
public sealed record RolePermissionsDto(
    Guid RoleId,
    IReadOnlyList<RolePermissionEntryDto> Permissions);

/// <summary>
/// One assigned permission. <see cref="GrantSource"/> (System | Module | Manual) and
/// <see cref="SourceModuleCode"/> let the UI distinguish baseline / entitlement-driven / manual
/// grants (e.g. only manual grants are operator-removable). Bounded, stable fields — not raw
/// internal metadata.
/// </summary>
public sealed record RolePermissionEntryDto(
    Guid PermissionId,
    string Key,
    string GrantSource,
    string? SourceModuleCode);
