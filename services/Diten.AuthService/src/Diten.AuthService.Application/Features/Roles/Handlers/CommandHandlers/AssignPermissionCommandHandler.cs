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
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;

    public AssignPermissionCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRbacAuditRecorder rbacAudit,
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
        var actorId = _currentUser.UserId;
        if (actorId is null || actorId == Guid.Empty)
        {
            return Response<NoContent>.Fail("Authentication required.", 401);
        }

        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        // Permissions are global, so we use ID directly.
        var permission = await _permissionRepository.GetByIdAsync(request.PermissionId, ct);

        // Manual assignment to a tenant role always honors the tenant-assignable boundary, including when
        // initiated by a platform operator. The REVOKE path remains unguarded so a mis-grant can be removed.
        if (permission is null || !DefaultRolePermissionTemplate.IsTenantAssignable(permission))
        {
            return Response<NoContent>.Fail("This permission cannot be assigned to a tenant role.", 403);
        }

        var inserted = await _rolePermissionRepository.TryAssignAsync(
            RolePermission.ManualGrant(request.RoleId, request.PermissionId, _tenantContext.TenantId, actorId.Value.ToString("D")),
            ct);
        if (!inserted)
        {
            return Response<NoContent>.Success(204);
        }

        // FU13 — bump the tenant role-assignment version so every holder's cached snapshot is invalidated at once.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a permission was granted to a role (permissionKey resolved best-effort for readability).
        await _rbacAudit.RecordAsync("role_permission_granted", _tenantContext.TenantId,
            new { roleId = request.RoleId, roleName = role.Name, permissionId = request.PermissionId, permissionKey = permission?.Key }, ct);

        return Response<NoContent>.Success(204);
    }
}
