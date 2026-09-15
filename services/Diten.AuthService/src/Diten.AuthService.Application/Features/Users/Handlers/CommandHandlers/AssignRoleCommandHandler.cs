using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly IRbacAuditRecorder _rbacAudit;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<AssignRoleCommandHandler> _logger;

    public AssignRoleCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        IRbacAuditRecorder rbacAudit,
        ICurrentUserAccessor currentUser,
        ILogger<AssignRoleCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _rbacAudit = rbacAudit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        // BL-412 F1 — the only dispatcher is POST api/users/{id}/roles ([Authorize] + [HasPermission]); seed, tenant-admin
        // invitation, platform-admin provisioning and self-registration write UserRole directly and never reach this
        // handler. So the assignment names the person — the same actor id the user_role_assigned audit row carries.
        if (_currentUser.UserId is not { } actorId)
            return Response<NoContent>.Fail("An authenticated user is required to assign a role.", 401);

        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user == null) return Response<NoContent>.Fail("User not found.", 404);

        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        if (await _userRoleRepository.ExistsAsync(request.UserId, request.RoleId, _tenantContext.TenantId, ct))
            return Response<NoContent>.Success(204);

        await _userRoleRepository.AssignAsync(new UserRole(request.UserId, request.RoleId, _tenantContext.TenantId, actorId.ToString()), ct);

        // FU13 — invalidate the tenant's cached authorization snapshots by bumping the role-assignment version.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // FEAT-AUDIT-RBAC — a role was newly assigned to a user (idempotent no-op above is not audited).
        await _rbacAudit.RecordAsync("user_role_assigned", _tenantContext.TenantId,
            new { targetUserId = request.UserId, roleId = request.RoleId, roleName = role.Name }, ct);

        return Response<NoContent>.Success(204);
    }
}
