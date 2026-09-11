using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Authorization;
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
        // İŞ3-FAZ1b — Module = the manifest ModuleCode (not the key's first segment); Scope = the route-derived value
        // the sender computed. Both are optional so an OLD sender (no fields) falls back to the pre-1b behaviour
        // (Module = key prefix, Scope derived from Module by the ctor).
        // FIX-PERM-MODULE-CASE-CONSISTENCY — store Module LOWERCASE: the Platform sender upper-cases the catalog
        // ModuleCode (ModuleCatalogCodeNormalizer), but the seed moduleOverride + manifest slugs are lowercase, so
        // persist a single casing here to keep the RoleAssignments "Module" grouping from splitting per case.
        var moduleOverride = string.IsNullOrWhiteSpace(request.ModuleCode) ? null : request.ModuleCode.Trim().ToLowerInvariant();
        var incomingScope = ParseScope(request.Scope);

        // FIX-CATALOG-PERM-RESYNC-DUPKEY — look up INCLUDING soft-deleted rows: the unique key index still owns a
        // soft-deleted doc, so a CREATE (InsertOne) with the same key would hit E11000 → 500. Reactivate instead.
        var existing = await _permissionRepository.GetByKeyIncludingDeletedAsync(normalizedKey, ct);
        if (existing is null)
        {
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? normalizedKey
                : request.DisplayName.Trim();
            var permission = new Permission(module, resource, action, displayName, NormalizeOptional(request.Description),
                moduleOverride: moduleOverride, scope: incomingScope);
            // FEAT-CATALOG-PERM-DELETE-SYNC — a catalog-CREATED permission is operator/catalog-owned, NOT a seeded
            // system permission. Mark it user-defined (IsSystem=false) so the DELETE-sync can later remove it when the
            // owning descriptor is deleted. Hand-seeded system permissions (auth.* etc.) keep IsSystem=true (protected).
            permission.MarkAsUserDefined();
            await _permissionRepository.CreateAsync(permission, ct);

            // A1 — a first-time permission must land on the full-catalog role (default-tenant SuperAdmin) so it
            // becomes usable on re-login without a hand-edited seed. Idempotent + best-effort (never blocks sync).
            // BL-359 — the grant service itself refuses an explicit-grant-only key (e.g. assign-owner).
            await _fullCatalogGrantService.GrantToFullCatalogRolesAsync(permission.Id, permission.Key, ct);

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
            await _fullCatalogGrantService.GrantToFullCatalogRolesAsync(existing.Id, existing.Key, ct);

            _logger.LogInformation(
                "Catalog permission synced (reactivated). Key={Key} Module={Module}",
                existing.Key,
                module);

            return Ok(new SyncPermissionResponse(existing.Key, "reactivated"));
        }

        // Idempotent live update: same key never duplicates.
        existing.Update(newDisplayName, newDescription);

        // İŞ3-FAZ1b — refresh Module (= manifest ModuleCode) and Scope (= route-derived) on the existing row so the
        // catalog migration lands without a DB wipe (Key stays immutable). An old sender that sends neither field
        // leaves both untouched.
        //
        // FIX-RBAC-PERM-MODULE-ATTRIBUTION — the seeded-system-on-Module=="platform" lock that used to sit here is
        // gone. It existed because Module WAS the escalation boundary, so moving a seeded platform key to its owning
        // module would have flipped that key to tenant scope. Module and Scope are now separate signals: Scope is
        // carried explicitly and the tie-break below never downgrades it, so re-attributing the Module is exactly the
        // migration this fix wants and no longer touches the boundary. The lock had also become unreachable — after
        // the derivation no seeded permission carries "platform" as its Module at all.
        if (moduleOverride is not null)
        {
            existing.SetModule(moduleOverride);
        }
        if (incomingScope.HasValue)
        {
            // Tie-break (most restrictive wins): the same key can be synced from several pages with different
            // routes. If ANY of them is platform-scoped the key stays PlatformAdmin — never downgrade to Tenant.
            // This is now the ONLY thing standing between a manifest and the escalation boundary — do not relax it.
            var effectiveScope = existing.Scope == PermissionScope.PlatformAdmin || incomingScope.Value == PermissionScope.PlatformAdmin
                ? PermissionScope.PlatformAdmin
                : PermissionScope.Tenant;
            existing.SetScope(effectiveScope);
        }

        await _permissionRepository.UpdateAsync(existing, ct);

        _logger.LogInformation(
            "Catalog permission synced (updated). Key={Key} Module={Module}",
            existing.Key,
            existing.Module);

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
    /// module's live permission count. FIX-PERM-MODULE-CASE-CONSISTENCY — <c>Permission.Module</c> is now persisted
    /// lowercase, so this returns consistent lowercase module names. A catalog ModuleCode maps to it
    /// CASE-INSENSITIVELY (the entitlement bridge already matches OrdinalIgnoreCase), so an upper-cased catalog code
    /// still resolves 1:1.
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

    // İŞ3-FAZ1b — parse the wire Scope ("Tenant"/"PlatformAdmin", case-insensitive). Absent/unrecognised → null
    // (fallback: the ctor derives Scope from Module, or an update leaves the existing Scope untouched).
    private static PermissionScope? ParseScope(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && Enum.TryParse<PermissionScope>(scope.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;

    // İŞ3-FAZ1b — ModuleCode + Scope are OPTIONAL (nullable) so an older Platform sender still binds (fields default
    // to null → pre-1b behaviour). ModuleCode becomes Permission.Module; Scope is the route-derived authz scope.
    public sealed record SyncPermissionRequest(
        string PermissionKey,
        string? DisplayName,
        string? Description,
        string? ModuleCode = null,
        string? Scope = null);

    public sealed record SyncPermissionResponse(string Key, string Status);

    public sealed record PermissionModuleSummary(string Module, int PermissionCount);
}
