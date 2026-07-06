namespace Diten.Platform.Application.Contracts;

/// <summary>
/// Pushes a catalog-declared permission key into the AuthService permission catalogue (S2S).
/// Best-effort by contract: a failure to reach or persist in AuthService must NEVER block the
/// catalog save — the implementation logs and returns a non-success status instead of throwing.
/// Phase 1 is additive-only (upsert; no delete).
/// </summary>
public interface ICatalogPermissionSyncService
{
    /// <summary>
    /// İŞ3-FAZ1b — carries the owning module's <paramref name="moduleCode"/> (becomes Permission.Module in AuthService,
    /// replacing the key-prefix derivation) and the route-derived <paramref name="scope"/> ("Tenant"/"PlatformAdmin").
    /// Both are optional; an AuthService that predates the fields ignores them (backward compatible).
    /// </summary>
    Task<CatalogPermissionSyncStatus> SyncPermissionAsync(
        string? permissionKey, string? displayName, string? moduleCode, string? scope, CancellationToken ct);

    /// <summary>
    /// FEAT-CATALOG-PERM-DELETE-SYNC — Phase 1.5, the counterpart of <see cref="SyncPermissionAsync"/>: requests
    /// AuthService to remove a catalog-sourced permission (called only when the LAST catalog descriptor referencing
    /// it is being deleted). Same best-effort contract — never throws; a failure is logged and the catalog delete is
    /// unaffected. AuthService protects seeded/system permissions (they return 409 → <see cref="CatalogPermissionSyncStatus.Failed"/>).
    /// </summary>
    Task<CatalogPermissionSyncStatus> RemovePermissionAsync(string? permissionKey, CancellationToken ct);
}

public enum CatalogPermissionSyncStatus
{
    /// <summary>Nothing to sync — the descriptor had no permission key.</summary>
    SkippedEmpty,

    /// <summary>The key is not a canonical module.resource.action grammar; not sent.</summary>
    InvalidFormat,

    /// <summary>AuthService accepted the upsert.</summary>
    Synced,

    /// <summary>AuthService was unreachable or rejected the upsert; logged, save not blocked.</summary>
    Failed,

    /// <summary>AuthService removed the permission (204/404 — idempotent).</summary>
    Removed,

    /// <summary>Removal not attempted — another live catalog descriptor still references this permission key.</summary>
    SkippedStillReferenced
}
