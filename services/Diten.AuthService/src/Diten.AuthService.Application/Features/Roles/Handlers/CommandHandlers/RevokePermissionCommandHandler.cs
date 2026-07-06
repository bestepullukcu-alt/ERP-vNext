using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class RevokePermissionCommandHandler : IRequestHandler<RevokePermissionCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRbacAuditRecorder _rbacAudit;

    public RevokePermissionCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IUserRoleRepository userRoleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IPermissionRepository permissionRepository,
        IRbacAuditRecorder rbacAudit)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRoleRepository = userRoleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _permissionRepository = permissionRepository;
        _rbacAudit = rbacAudit;
    }

    public async Task<Response<NoContent>> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        // Role lookup is kept for the audit roleName below. FIX-ROLEPERM-REVOKE-SYSTEMROLE — the blanket
        // "cannot remove from a system role" 403 was removed: it was redundant with (and broader than) the S-GUARD
        // below, wrongly blocking removal of MANUAL grants on a system role (assign was allowed but revoke was not —
        // the same asymmetry fixed for user↔role in FIX-USERROLE-REVOKE-SYSTEMROLE). The S-GUARD is the authoritative
        // protection: System/Module baseline grants stay locked (409) on ANY role, Manual grants are removable.
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, tenantId, ct);

        // S-GUARD (bridge integrity): only Manual grants may be removed through the API. System
        // (baseline) and Module (entitlement-sourced) grants are provisioning-managed — a manual
        // delete here would corrupt the entitlement→permission bridge (a removed System grant is lost
        // until re-provisioning; a Module grant must only be removed by the entitlement consumer when
        // its module is disabled). This mirrors the S2 revoke spec on the AuthService side.
        var grants = await _rolePermissionRepository.GetByRoleAsync(request.RoleId, tenantId, ct);
        var target = grants.FirstOrDefault(g => g.PermissionId == request.PermissionId);
        if (target is null)
            return Response<NoContent>.Success(204); // idempotent: nothing to remove
        if (target.GrantSource != GrantSource.Manual)
            return Response<NoContent>.Fail("System/Module grants are provisioning-managed and cannot be manually removed.", 409);

        await _rolePermissionRepository.RevokeAsync(request.RoleId, request.PermissionId, tenantId, ct);

        // FU13 — bump the tenant role-assignment version so every holder's cached snapshot is invalidated at once.
        await _versionService.IncrementAsync(tenantId, ct);

        // AG-STEP-010 / MOD-0018-FU13 Group C (OD-FU13-01, B-Option 1): removing a permission from a role narrows the
        // effective permissions of EVERY user holding that role, so close the refresh path for each holder — a still-
        // valid refresh token must not re-mint an access token carrying the removed grant. Tenant-scoped holder lookup,
        // distinct, sequential fail-fast: a failure here is NOT swallowed and the permission removal above stands (no
        // rollback); residual exposure is bounded by the access-token TTL (≤15 min). No deny-list / event / retry/outbox.
        var holders = await _userRoleRepository.GetUserIdsByRoleAsync(request.RoleId, tenantId, ct);
        foreach (var userId in holders.Distinct())
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(userId, tenantId, ct);
        }

        // FEAT-AUDIT-RBAC — a permission was revoked from a role (permissionKey resolved best-effort for readability).
        var permission = await _permissionRepository.GetByIdAsync(request.PermissionId, ct);
        await _rbacAudit.RecordAsync("role_permission_revoked", tenantId,
            new { roleId = request.RoleId, roleName = role?.Name, permissionId = request.PermissionId, permissionKey = permission?.Key }, ct);

        return Response<NoContent>.Success(204);
    }
}
