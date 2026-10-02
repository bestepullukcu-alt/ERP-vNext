using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class AssignPermissionCommandHandler : IRequestHandler<AssignPermissionCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IRoleAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;

    public AssignPermissionCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRoleAuditRecorder rbacAudit,
        ICurrentUserAccessor currentUser)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(AssignPermissionCommand request, CancellationToken ct)
    {
        // BL-412 — this is the person path (POST api/roles/{id}/permissions), so the grant row names the person: the
        // same actor id the RBAC audit row carries (ICurrentUserAccessor, read once here). Without an actor nothing is
        // written — "System" is reserved for seed/provisioning/sync, which never reach this handler.
        if (_currentUser.UserId is not { } actorId)
            return RoleErrorCodes.Refuse<NoContent>(RoleErrorCodes.ActorRequired, "An authenticated user is required to grant a permission.", 401);

        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role == null) return RoleErrorCodes.Refuse<NoContent>(RoleErrorCodes.NotFound, "Role not found.", 404);

        // Permissions are global, so we use ID directly.
        var permission = await _permissionRepository.GetByIdAsync(request.PermissionId, ct);

        // FEAT-ROLEPERMS-TENANT-SCOPE — manual assignment must honor the same platform-escalation boundary
        // that DefaultRolePermissionTemplate enforces during default provisioning. In a TENANT context
        // (not platform-admin), a tenant role may only receive tenant-assignable permissions; a platform-admin
        // permission (or an unknown/missing one we cannot vet) is rejected. Platform-admin context is exempt,
        // and the REVOKE path is intentionally NOT guarded so a mis-granted permission can always be removed.
        if (!_tenantContext.IsPlatformContext &&
            (permission is null || !DefaultRolePermissionTemplate.IsTenantAssignable(permission)))
        {
            return RoleErrorCodes.Refuse<NoContent>(RoleErrorCodes.PermissionNotTenantAssignable, "This permission cannot be assigned to a tenant role.", 403);
        }

        await _rolePermissionRepository.AssignAsync(
            RolePermission.ManualGrant(request.RoleId, request.PermissionId, _tenantContext.TenantId, actorId.ToString()), ct);

        // FU13 — bump the tenant role-assignment version so every holder's cached snapshot is invalidated at once.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a permission was granted to a role (permissionKey resolved best-effort for readability).
        // WP-ROLES-CLOSE-01 — the same local row, now also forwarded to Platform's central audit log.
        await _rbacAudit.RecordAsync(RoleAuditEvents.PermissionGranted, _tenantContext.TenantId, request.RoleId,
            new Dictionary<string, object?> { ["roleName"] = role.Name, ["permissionId"] = request.PermissionId, ["permissionKey"] = permission?.Key }, ct);

        return Response<NoContent>.Success(204);
    }
}
