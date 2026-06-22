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
    private readonly ILogger<InternalPermissionsController> _logger;

    public InternalPermissionsController(
        IInternalEventAuthService internalEventAuthService,
        IPermissionRepository permissionRepository,
        ILogger<InternalPermissionsController> logger)
    {
        _internalEventAuthService = internalEventAuthService;
        _permissionRepository = permissionRepository;
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

        var existing = await _permissionRepository.GetByKeyAsync($"{module}.{resource}.{action}", ct);
        if (existing is null)
        {
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? $"{module}.{resource}.{action}"
                : request.DisplayName.Trim();
            var permission = new Permission(module, resource, action, displayName, NormalizeOptional(request.Description));
            await _permissionRepository.CreateAsync(permission, ct);

            _logger.LogInformation(
                "Catalog permission synced (created). Key={Key} Module={Module}",
                permission.Key,
                module);

            return Ok(new SyncPermissionResponse(permission.Key, "created"));
        }

        // Idempotent: same key never duplicates. Refresh display metadata only; Module/Resource/Action/Key
        // are immutable (the key is the identity).
        var newDisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? existing.DisplayName : request.DisplayName.Trim();
        var newDescription = request.Description is null ? existing.Description : NormalizeOptional(request.Description);
        existing.Update(newDisplayName, newDescription);
        await _permissionRepository.UpdateAsync(existing, ct);

        _logger.LogInformation(
            "Catalog permission synced (updated). Key={Key} Module={Module}",
            existing.Key,
            module);

        return Ok(new SyncPermissionResponse(existing.Key, "updated"));
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
