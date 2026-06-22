namespace Diten.Platform.Application.Contracts;

/// <summary>
/// Pushes a catalog-declared permission key into the AuthService permission catalogue (S2S).
/// Best-effort by contract: a failure to reach or persist in AuthService must NEVER block the
/// catalog save — the implementation logs and returns a non-success status instead of throwing.
/// Phase 1 is additive-only (upsert; no delete).
/// </summary>
public interface ICatalogPermissionSyncService
{
    Task<CatalogPermissionSyncStatus> SyncPermissionAsync(string? permissionKey, string? displayName, CancellationToken ct);
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
    Failed
}
