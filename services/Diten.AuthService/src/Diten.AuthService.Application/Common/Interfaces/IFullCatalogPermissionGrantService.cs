namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// A1 — when a permission is created for the FIRST time (e.g. auto-registered from a Platform
/// controller's <c>[HasPermission]</c>), grant it to the full-catalog role(s) (default-tenant SuperAdmin)
/// so it materialises in that role's token on the next re-login. Idempotent and best-effort: an already
/// granted permission is a no-op and any failure is logged without throwing (the sync must still succeed).
/// Only the full-catalog role is touched — Admin/Viewer and tenant roles are intentionally left alone.
/// </summary>
public interface IFullCatalogPermissionGrantService
{
    Task GrantToFullCatalogRolesAsync(Guid permissionId, CancellationToken ct);
}
