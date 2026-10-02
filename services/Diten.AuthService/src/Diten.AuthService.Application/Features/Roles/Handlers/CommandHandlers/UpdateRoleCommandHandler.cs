using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Response<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IRoleAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;

    public UpdateRoleCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRoleAuditRecorder rbacAudit,
        ICurrentUserAccessor currentUser)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _currentUser = currentUser;
    }

    public async Task<Response<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        // BL-412 — PUT api/roles/{id} is a person path: UpdatedBy names the person (same actor id as role_updated).
        if (_currentUser.UserId is not { } actorId)
            return RoleErrorCodes.Refuse<RoleDto>(RoleErrorCodes.ActorRequired, "An authenticated user is required to update a role.", 401);

        var role = await _roleRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (role == null) return RoleErrorCodes.Refuse<RoleDto>(RoleErrorCodes.NotFound, "Role not found.", 404);

        // FEAT-AUDIT-RBAC — capture the before-state for the audit delta before mutating.
        var beforeDisplayName = role.DisplayName;
        var beforeDescription = role.Description;

        role.Update(request.DisplayName, request.Description);
        role.UpdatedBy = actorId.ToString();
        var updated = await _roleRepository.UpdateAsync(role, ct);

        // FU13 — bump the tenant role-assignment version so cached authorization snapshots refresh.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — role metadata updated; record the before/after delta.
        // WP-ROLES-CLOSE-01 — the same local row, now also forwarded to Platform's central audit log.
        await _rbacAudit.RecordAsync(RoleAuditEvents.Updated, _tenantContext.TenantId, updated.Id, new Dictionary<string, object?>
        {
            ["roleName"] = updated.Name,
            ["before"] = new Dictionary<string, object?> { ["displayName"] = beforeDisplayName, ["description"] = beforeDescription },
            ["after"] = new Dictionary<string, object?> { ["displayName"] = updated.DisplayName, ["description"] = updated.Description }
        }, ct);

        var permissions = await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, _tenantContext.TenantId, ct);

        return Response<RoleDto>.Success(new RoleDto(updated.Id, updated.Name, updated.DisplayName, updated.Description, updated.IsSystem, permissions.Count()));
    }
}
