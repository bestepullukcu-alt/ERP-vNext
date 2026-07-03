using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

/// <summary>
/// Internal S2S endpoint that lets the Platform module-catalog declare permissions into the AuthService
/// permission catalogue. The catalog page/page-action is the source of truth; this endpoint is the
/// consumer. Protected by the same <c>X-Internal-Api-Key</c> shared secret used by other internal routes.
///
/// Phase 1 is additive-only: it upserts (never deletes). Hand-seeded permissions (e.g. goldenslim.*)
/// are untouched. Deletion/orphan reconciliation is deferred to Phase 1.5.
/// </summary>
[ApiController]
[Route("internal/permissions")]
public sealed class InternalPermissionsController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IFullCatalogPermissionGrantService _fullCatalogGrantService;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ILogger<InternalPermissionsController> _logger;

    public InternalPermissionsController(
        IInternalEventAuthService internalEventAuthService,
        IPermissionRepository permissionRepository,
        IFullCatalogPermissionGrantService fullCatalogGrantService,
        IRolePermissionRepository rolePermissionRepository,
        IRbacAuditRecorder rbacAudit,
        ILogger<InternalPermissionsController> logger)
    {
        _internalEventAuthService = internalEventAuthService;
        _permissionRepository = permissionRepository;
        _fullCatalogGrantService = fullCatalogGrantService;
        _rolePermissionRepository = rolePermissionRepository;
        _rbacAudit = rbacAudit;
        _logger = logger;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncPermissionRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PermissionKey))
        {
            return BadRequest(new { message = "permissionKey is required" });
        }

        if (!PermissionKeyParser.TryParse(request.PermissionKey, out var module, out var resource, out var action))
        {
            _logger.LogWarning(
                "Rejected catalog permission sync with malformed key. PermissionKey={PermissionKey}",
                request.PermissionKey);
            return BadRequest(new { message = "permissionKey must be a lowercase module.resource.action key (>= 3 segments)" });
        }

        var normalizedKey = $"{module}.{resource}.{action}";
        // FIX-CATALOG-PERM-RESYNC-DUPKEY — look up INCLUDING soft-deleted rows: the unique key index still owns a
        // soft-deleted doc, so a CREATE (InsertOne) with the same key would hit E11000 → 500. Reactivate instead.
        var existing = await _permissionRepository.GetByKeyIncludingDeletedAsync(normalizedKey, ct);
        if (existing is null)
        {
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? normalizedKey
                : request.DisplayName.Trim();
            var permission = new Permission(module, resource, action, displayName, NormalizeOptional(request.Description));
            // FEAT-CATALOG-PERM-DELETE-SYNC — a catalog-CREATED permission is operator/catalog-owned, NOT a seeded
            // system permission. Mark it user-defined (IsSystem=false) so the DELETE-sync can later remove it when the
            // owning descriptor is deleted. Hand-seeded system permissions (auth.* etc.) keep IsSystem=true (protected).
            permission.MarkAsUserDefined();
            await _permissionRepository.CreateAsync(permission, ct);

            // A1 — a first-time permission must land on the full-catalog role (default-tenant SuperAdmin) so it
            // becomes usable on re-login without a hand-edited seed. Idempotent + best-effort (never blocks sync).
            await _fullCatalogGrantService.GrantToFullCatalogRolesAsync(permission.Id, ct);

            _logger.LogInformation(
                "Catalog permission synced (created). Key={Key} Module={Module}",
                permission.Key,
                module);

            return Ok(new SyncPermissionResponse(permission.Key, "created"));
        }

        // Refresh display metadata only; Module/Resource/Action/Key are immutable (the key is the identity).
        var newDisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? existing.DisplayName : request.DisplayName.Trim();
        var newDescription = request.Description is null ? existing.Description : NormalizeOptional(request.Description);

        if (existing.IsDeleted)
        {
            // FIX-CATALOG-PERM-RESYNC-DUPKEY / FIX-CATALOG-PERM-REACTIVATE-PERSIST — REACTIVATE a previously deleted
            // (catalog-owned) permission: revive the SAME doc (no duplicate), refresh metadata, keep it user-defined,
            // and re-grant it to the full-catalog role (like first creation). ReactivateAsync uses an Id-only filter —
            // the normal filtered UpdateAsync/ReplaceOne would match zero rows on a soft-deleted doc and never persist.
            await _permissionRepository.ReactivateAsync(existing.Id, newDisplayName, newDescription, ct);
            await _fullCatalogGrantService.GrantToFullCatalogRolesAsync(existing.Id, ct);

            _logger.LogInformation(
                "Catalog permission synced (reactivated). Key={Key} Module={Module}",
                existing.Key,
                module);

            return Ok(new SyncPermissionResponse(existing.Key, "reactivated"));
        }

        // Idempotent live update: same key never duplicates.
        existing.Update(newDisplayName, newDescription);
        await _permissionRepository.UpdateAsync(existing, ct);

        _logger.LogInformation(
            "Catalog permission synced (updated). Key={Key} Module={Module}",
            existing.Key,
            module);

        return Ok(new SyncPermissionResponse(existing.Key, "updated"));
    }

    /// <summary>
    /// FEAT-CATALOG-PERM-DELETE-SYNC — removes a CATALOG-SOURCED permission when its owning descriptor is deleted
    /// (Phase 1.5, the counterpart of <see cref="Sync"/>). Best-effort from the caller's side. Rules: seeded system
    /// permissions (IsSystem=true) are NEVER deleted (409); an unknown key is idempotent (204); a user-defined key
    /// clears all its grant rows, deletes the permission, and writes an RBAC audit event.
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (!PermissionKeyParser.TryParse(key, out var module, out var resource, out var action))
        {
            _logger.LogWarning("Rejected catalog permission delete with malformed key. PermissionKey={PermissionKey}", key);
            return BadRequest(new { message = "permissionKey must be a lowercase module.resource.action key (>= 3 segments)" });
        }

        var normalizedKey = $"{module}.{resource}.{action}";
        var existing = await _permissionRepository.GetByKeyAsync(normalizedKey, ct);
        if (existing is null)
        {
            // Idempotent: already gone.
            return NoContent();
        }

        if (existing.IsSystem)
        {
            // Seeded/system permission — the catalog does not own it and may not delete it.
            _logger.LogWarning(
                "Refused catalog permission delete of a system permission. Key={Key}", existing.Key);
            return Conflict(new { message = "system permissions cannot be removed via catalog sync" });
        }

        // Clear every grant of this (global) permission first, so no orphan rolePermissions survive the delete.
        var removedGrants = await _rolePermissionRepository.RemoveByPermissionIdAsync(existing.Id, ct);
        await _permissionRepository.DeleteAsync(existing.Id, ct);

        // Audit — global scope (Guid.Empty tenant); IDs + key only, no PII.
        await _rbacAudit.RecordAsync("permission_catalog_removed", Guid.Empty,
            new { permissionId = existing.Id, permissionKey = existing.Key, removedGrants }, ct);

        _logger.LogInformation(
            "Catalog permission removed. Key={Key} RemovedGrants={RemovedGrants}", existing.Key, removedGrants);

        return NoContent();
    }

    /// <summary>
    /// Read-only S2S list of the DISTINCT <c>Module</c> values present in the permission catalogue, with each
    /// module's live permission count. The Module string is returned verbatim (no case change), so a catalog
    /// ModuleCode picked from this list maps 1:1 to <c>Permission.Module</c> and the entitlement bridge matches.
    /// </summary>
    [HttpGet("modules")]
    public async Task<IActionResult> GetModules(CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        var permissions = await _permissionRepository.GetAllAsync(ct);
        var modules = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission.Module))
            .GroupBy(permission => permission.Module, StringComparer.Ordinal) // exact case preserved
            .Select(group => new PermissionModuleSummary(group.Key, group.Count()))
            .OrderBy(summary => summary.Module, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(modules);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record SyncPermissionRequest(string PermissionKey, string? DisplayName, string? Description);

    public sealed record SyncPermissionResponse(string Key, string Status);

    public sealed record PermissionModuleSummary(string Module, int PermissionCount);
}
